using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// CRUD over the user row itself — who exists, what they are called, and which
/// role, branches and manager they carry.
///
/// The access <em>model</em> around that row lives in <see cref="AccessAdminController"/>:
/// what a role means, the permission matrix, and per-person module exceptions.
/// The split is deliberate — this controller is opened by anyone who can list
/// their colleagues, that one only by administrators.
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
[SecuredBy(SecuredObjects.User)]
public class UsersController(
    AppDbContext db,
    SessionService sessions,
    EntitlementService entitlements) : ControllerBase
{

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> GetUsers(CancellationToken ct)
    {
        var companyId = User.GetCompanyId();

        var rows = await db.Users
            .Where(u => u.CompanyId == companyId)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.IsActive,
                u.ManagerId,
                ManagerName = u.Manager != null ? u.Manager.Name : null,
                u.MustChangePassword,
                u.LastLoginAt,
                u.CreatedAt,
                BranchIds = u.UserBranches.Select(ub => ub.BranchId).ToList(),
                BranchNames = u.UserBranches.Select(ub => ub.Branch!.Name).ToList(),
                ModuleOverrides = u.ModuleGrants.Count,
            })
            .ToListAsync(ct);

        // Team size is the transitive count, not the direct one: a team-scoped
        // role reaches everyone beneath it at any depth, and a number that only
        // counted direct reports would understate what an AGM actually sees.
        var directReports = rows
            .Where(r => r.ManagerId is not null)
            .GroupBy(r => r.ManagerId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(r => r.Id).ToList());

        int TeamSize(int id, HashSet<int> seen)
        {
            if (!directReports.TryGetValue(id, out var children)) return 0;

            var total = 0;
            foreach (var child in children)
            {
                // Two independent edits can make a pair each other's manager;
                // without the seen set the walk never returns.
                if (!seen.Add(child)) continue;
                total += 1 + TeamSize(child, seen);
            }

            return total;
        }

        return Ok(rows
            .OrderBy(r => r.Name)
            .Select(r =>
            {
                var role = RoleCatalog.For(r.Role);

                return new UserListItemDto(
                    r.Id, r.Name, r.Email, r.Role, role.Name, role.Scope.ToString(),
                    r.IsActive, r.BranchIds, r.BranchNames,
                    r.ManagerId, r.ManagerName, TeamSize(r.Id, [r.Id]),
                    r.MustChangePassword, r.LastLoginAt, r.CreatedAt, r.ModuleOverrides);
            })
            .ToList());
    }

    [HttpPost]
    public async Task<ActionResult<UserListItemDto>> CreateUser(
        CreateUserRequest request, CancellationToken ct)
    {
        var companyId = User.GetCompanyId();

        var roleError = CheckRole(request.Role);
        if (roleError is not null) return roleError;

        // Checked before the row is written rather than after: an invite that
        // succeeds and then cannot sign in is worse than one that is refused with
        // a reason.
        await entitlements.EnsureRoomAsync(Limits.Seats, ct: ct);

        var email = request.Email.Trim().ToLowerInvariant();

        // Unfiltered, because the uniqueness of an email is a platform-wide
        // fact: a tenant-scoped check would report the address free and then
        // fail on the index.
        if (await db.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email, ct))
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        if (!await ManagerIsInCompanyAsync(request.ManagerId, companyId, ct))
        {
            return NotFound(new { message = "That manager is not in this company." });
        }

        var user = new User
        {
            CompanyId = companyId,
            Name = request.Name.Trim(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            IsActive = true,
            ManagerId = request.ManagerId,
            MustChangePassword = request.MustChangePassword ?? true,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        await AssignBranchesAsync(user, request.BranchIds, companyId, ct);
        await db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetUsers), await ProjectAsync(user.Id, companyId, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<UserListItemDto>> UpdateUser(
        int id, UpdateUserRequest request, CancellationToken ct)
    {
        var companyId = User.GetCompanyId();

        var user = await db.Users
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId, ct);

        if (user is null) return NotFound(new { message = "That user no longer exists." });

        var roleError = CheckRole(request.Role);
        if (roleError is not null) return roleError;

        if (request.ManagerId == id)
        {
            return BadRequest(new { message = "Somebody cannot report to themselves." });
        }

        var lockout = await GuardAgainstLockoutAsync(user, request.Role, request.IsActive, companyId, ct);
        if (lockout is not null) return lockout;

        if (!user.IsActive && request.IsActive)
        {
            await entitlements.EnsureRoomAsync(Limits.Seats, ct: ct);
        }

        if (!await ManagerIsInCompanyAsync(request.ManagerId, companyId, ct))
        {
            return NotFound(new { message = "That manager is not in this company." });
        }

        if (request.ManagerId is int managerId && await WouldLoopAsync(id, managerId, ct))
        {
            return BadRequest(new { message = "That would create a loop in the reporting line." });
        }

        // Captured before the assignment, because what matters is whether this
        // edit changed them — a save that touched only the name should not sign
        // anybody out.
        var roleChanged = user.Role != request.Role;
        var deactivated = user.IsActive && !request.IsActive;

        user.Role = request.Role;
        user.IsActive = request.IsActive;
        user.ManagerId = request.ManagerId;

        db.UserBranches.RemoveRange(user.UserBranches);
        await AssignBranchesAsync(user, request.BranchIds, companyId, ct);

        await db.SaveChangesAsync(ct);

        // The token carries the old role and the old branches, so a session that
        // survives this edit keeps the access it was just moved off. Ending it
        // is what makes the change take effect now rather than at expiry.
        if (roleChanged || deactivated)
        {
            await sessions.RevokeAllForUserAsync(
                user.Id,
                deactivated ? RevokeReasons.Deactivated : RevokeReasons.RoleChanged,
                ct: ct);
        }

        return Ok(await ProjectAsync(user.Id, companyId, ct));
    }

    /// <summary>
    /// Sets a password on somebody else's account and blocks the account until
    /// they replace it.
    ///
    /// The block is not optional. Whatever is typed here is known to two people
    /// the moment it is saved, and an account left sitting on it is a shared
    /// credential rather than a personal one.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(
        int id, ResetUserPasswordRequest request, CancellationToken ct)
    {
        var companyId = User.GetCompanyId();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id && u.CompanyId == companyId, ct);
        if (user is null) return NotFound(new { message = "That user no longer exists." });

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.PasswordChangedAt = DateTime.UtcNow;
        user.MustChangePassword = true;

        await db.SaveChangesAsync(ct);

        // A reset is usually somebody locked out, or somebody whose account is
        // suspected. Both mean whatever is currently signed in should stop.
        await sessions.RevokeAllForUserAsync(user.Id, RevokeReasons.PasswordChanged, ct: ct);

        return NoContent();
    }

    /// <summary>
    /// Activates or deactivates several accounts at once — what gets run on a
    /// whole branch when a team is stood down, or on a batch of invites that
    /// were never taken up.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("bulk-status")]
    public async Task<ActionResult<IReadOnlyList<UserListItemDto>>> BulkStatus(
        BulkUserStatusRequest request, CancellationToken ct)
    {
        var companyId = User.GetCompanyId();
        var ids = request.UserIds.Distinct().ToList();

        if (!request.IsActive && ids.Contains(User.GetUserId()))
        {
            return BadRequest(new { message = "You cannot deactivate your own account." });
        }

        var users = await db.Users
            .Where(u => u.CompanyId == companyId && ids.Contains(u.Id))
            .ToListAsync(ct);

        if (users.Count == 0) return Ok(Array.Empty<UserListItemDto>());

        if (!request.IsActive)
        {
            var survivors = await db.Users.CountAsync(
                u => u.CompanyId == companyId
                     && u.IsActive
                     && (u.Role == Roles.CompanyAdmin || u.Role == Roles.SuperAdmin)
                     && !ids.Contains(u.Id), ct);

            if (survivors == 0)
            {
                return BadRequest(new
                {
                    message = "That would leave the company with no active administrator.",
                });
            }
        }

        if (request.IsActive)
        {
            var reactivating = users.Count(u => !u.IsActive);
            if (reactivating > 0)
            {
                await entitlements.EnsureRoomAsync(Limits.Seats, reactivating, ct);
            }
        }

        var switchedOff = request.IsActive
            ? []
            : users.Where(u => u.IsActive).Select(u => u.Id).ToList();

        foreach (var user in users) user.IsActive = request.IsActive;
        await db.SaveChangesAsync(ct);

        foreach (var id in switchedOff)
        {
            await sessions.RevokeAllForUserAsync(id, RevokeReasons.Deactivated, ct: ct);
        }

        var updated = new List<UserListItemDto>(users.Count);
        foreach (var user in users) updated.Add(await ProjectAsync(user.Id, companyId, ct));

        return Ok(updated);
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Rejects a role the catalogue does not know, and stops a company admin
    /// minting a platform operator.
    ///
    /// The first check matters more than it looks: <c>RoleCatalog.For</c> falls
    /// back to the narrowest seat, so without this an unrecognised role name
    /// creates the account with an access level nobody asked for and no error to
    /// explain it.
    /// </summary>
    private ActionResult? CheckRole(string role)
    {
        if (!RoleCatalog.Exists(role))
        {
            return BadRequest(new { message = $"'{role}' is not a role on this platform." });
        }

        return role == Roles.SuperAdmin && !User.IsInRole(Roles.SuperAdmin)
            ? Forbid()
            : null;
    }

    /// <summary>
    /// Refuses the two edits that lock a company out of its own console: the
    /// last administrator demoting themselves, and the last one switching their
    /// own account off. Both are recoverable only by a platform operator.
    /// </summary>
    private async Task<ActionResult?> GuardAgainstLockoutAsync(
        User user, string newRole, bool newActive, int companyId, CancellationToken ct)
    {
        var wasAdmin = user.IsActive && user.Role is Roles.CompanyAdmin or Roles.SuperAdmin;
        var staysAdmin = newActive && newRole is Roles.CompanyAdmin or Roles.SuperAdmin;

        if (!wasAdmin || staysAdmin) return null;

        var others = await db.Users.CountAsync(
            u => u.CompanyId == companyId
                 && u.Id != user.Id
                 && u.IsActive
                 && (u.Role == Roles.CompanyAdmin || u.Role == Roles.SuperAdmin), ct);

        return others > 0
            ? null
            : BadRequest(new
            {
                message = "This is the company's last active administrator. "
                          + "Promote somebody else first.",
            });
    }

    private async Task<bool> ManagerIsInCompanyAsync(int? managerId, int companyId, CancellationToken ct) =>
        managerId is null
        || await db.Users.AnyAsync(u => u.Id == managerId && u.CompanyId == companyId, ct);

    /// <summary>
    /// Whether pointing <paramref name="userId"/> at <paramref name="managerId"/>
    /// closes the reporting line into a ring. A ring hands everyone in it access
    /// to everyone else's records and hangs the walk that computes team scope.
    /// </summary>
    private async Task<bool> WouldLoopAsync(int userId, int managerId, CancellationToken ct)
    {
        var edges = await db.Users
            .Where(u => u.ManagerId != null)
            .Select(u => new { u.Id, ManagerId = u.ManagerId!.Value })
            .ToDictionaryAsync(u => u.Id, u => u.ManagerId, ct);

        var cursor = managerId;

        for (var guard = 0; guard < 200; guard++)
        {
            if (cursor == userId) return true;
            if (!edges.TryGetValue(cursor, out var next)) return false;
            cursor = next;
        }

        // Two hundred hops without reaching a root means the stored tree is
        // already a ring; treat it as one rather than adding an edge to it.
        return true;
    }

    /// <summary>
    /// Assigns the branches this company actually owns, silently dropping any
    /// id that belongs to another tenant — the request is untrusted input, and a
    /// stray id is a bad request rather than grounds to hand out cross-tenant
    /// visibility.
    /// </summary>
    private async Task AssignBranchesAsync(
        User user, IReadOnlyList<int>? branchIds, int companyId, CancellationToken ct)
    {
        var wanted = (branchIds ?? []).Distinct().ToList();
        if (wanted.Count == 0) return;

        var valid = await db.Branches
            .Where(b => b.CompanyId == companyId && wanted.Contains(b.Id))
            .Select(b => b.Id)
            .ToListAsync(ct);

        db.UserBranches.AddRange(
            valid.Select(id => new UserBranch { UserId = user.Id, BranchId = id }));
    }

    /// <summary>One row, rebuilt through the same shape the list returns.</summary>
    private async Task<UserListItemDto> ProjectAsync(int id, int companyId, CancellationToken ct)
    {
        var row = await db.Users
            .Where(u => u.Id == id && u.CompanyId == companyId)
            .Select(u => new
            {
                u.Id,
                u.Name,
                u.Email,
                u.Role,
                u.IsActive,
                u.ManagerId,
                ManagerName = u.Manager != null ? u.Manager.Name : null,
                u.MustChangePassword,
                u.LastLoginAt,
                u.CreatedAt,
                BranchIds = u.UserBranches.Select(ub => ub.BranchId).ToList(),
                BranchNames = u.UserBranches.Select(ub => ub.Branch!.Name).ToList(),
                ModuleOverrides = u.ModuleGrants.Count,
                // Direct reports only. The transitive count needs the whole
                // table, and one row's response is not worth loading it.
                TeamSize = db.Users.Count(r => r.ManagerId == u.Id),
            })
            .FirstAsync(ct);

        var role = RoleCatalog.For(row.Role);

        return new UserListItemDto(
            row.Id, row.Name, row.Email, row.Role, role.Name, role.Scope.ToString(),
            row.IsActive, row.BranchIds, row.BranchNames,
            row.ManagerId, row.ManagerName, row.TeamSize,
            row.MustChangePassword, row.LastLoginAt, row.CreatedAt, row.ModuleOverrides);
    }
}
