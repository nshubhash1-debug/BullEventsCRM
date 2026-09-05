using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/* ------------------------------------------------------------------ *
 * DTOs
 * ------------------------------------------------------------------ */

public record RoleSummaryDto(
    string Key,
    string Name,
    string Description,
    string Scope,
    string ScopeLabel,
    string[] Modules,
    bool ReadOnly,
    bool WonBusinessOnly,
    bool PeopleData,
    int UserCount,
    int? ProfileId);

public record ObjectPermissionDto(string Object, string[] Actions);

public record ProfileDetailDto(
    int Id,
    string Name,
    string? Description,
    string? RoleKey,
    bool IsSystem,
    string[] Modules,
    IReadOnlyList<ObjectPermissionDto> Objects);

public record UpdateProfileRequest(
    string[]? Modules,
    IReadOnlyList<ObjectPermissionDto>? Objects);

/* ---------------- sharing rules ---------------- */

/// <summary>A value with the label a screen should show for it.</summary>
public record NamedOptionDto(string Value, string Label);

public record SharingRuleDto(
    int Id,
    string Name,
    string? Description,
    string Object,
    string? OwnerRoleKey,
    string? OwnerRoleName,
    string? CriteriaField,
    string? CriteriaOperator,
    string? CriteriaValue,
    string Target,
    string? TargetRoleKey,
    string? TargetRoleName,
    int? TargetBranchId,
    string? TargetBranchName,
    int? TargetUserId,
    string? TargetUserName,
    bool GrantEdit,
    bool IsActive);

/// <summary>
/// The rules plus everything the editor needs to describe one, in a single
/// payload — a screen that cannot resolve "TeleSales" into a name renders half
/// a rule, and three round trips is three chances for that.
/// </summary>
public record SharingRulesDto(
    IReadOnlyList<SharingRuleDto> Rules,
    string[] Objects,
    IReadOnlyList<NamedOptionDto> Roles,
    IReadOnlyList<NamedOptionDto> Branches,
    IReadOnlyList<NamedOptionDto> Users,
    IReadOnlyDictionary<string, string[]> ShareableFields);

public record PermissionGrantDto(
    int Id,
    int UserId,
    string UserName,
    DateTime? ExpiresAt,
    string? Reason,
    /// <summary>Past its expiry. Kept for the history; the resolver already ignores it.</summary>
    bool HasLapsed);

public record PermissionSetDto(
    int Id,
    string Name,
    string? Description,
    bool IsSystem,
    IReadOnlyList<ObjectPermissionDto> Objects,
    IReadOnlyList<PermissionGrantDto> Grants);

public record SavePermissionSetRequest(
    string Name,
    string? Description,
    IReadOnlyList<ObjectPermissionDto>? Objects);

public record GrantPermissionSetRequest(int UserId, DateTime? ExpiresAt, string? Reason);

public record SaveSharingRuleRequest(
    string Name,
    string? Description,
    string Object,
    string? OwnerRoleKey,
    string? CriteriaField,
    string? CriteriaOperator,
    string? CriteriaValue,
    string Target,
    string? TargetRoleKey,
    int? TargetBranchId,
    int? TargetUserId,
    bool GrantEdit,
    bool IsActive);

/* ---------------- login policies ---------------- */

public record LoginPolicyDto(
    int ProfileId,
    string ProfileName,
    string? RoleKey,
    int UserCount,
    string? AllowedIpRanges,
    int? LoginFromMinute,
    int? LoginToMinute,
    /// <summary>Bitmask from Sunday. 0b1111111 is every day.</summary>
    int AllowedDays,
    int? IdleTimeoutMinutes,
    bool IsActive);

public record SaveLoginPolicyRequest(
    string? AllowedIpRanges,
    int? LoginFromMinute,
    int? LoginToMinute,
    int AllowedDays,
    int? IdleTimeoutMinutes,
    bool IsActive);

public record ObjectVisibilityDto(string Object, string Visibility, bool GrantAccessUsingHierarchy);

/* ---------------- login history ---------------- */

/// <summary>One sign-in, and what became of it.</summary>
public record LoginSessionDto(
    int Id,
    int UserId,
    string UserName,
    string UserEmail,
    string Role,
    DateTime IssuedAt,
    DateTime ExpiresAt,
    DateTime LastSeenAt,
    DateTime? RevokedAt,
    string? RevokedReason,
    string? Device,
    string? IpAddress,
    /// <summary>Neither revoked nor expired — somebody is holding this right now.</summary>
    bool IsLive);

public record LoginHistoryDto(
    IReadOnlyList<LoginSessionDto> Sessions,
    int Live,
    int Revoked,
    int Expired,
    /// <summary>Distinct accounts that signed in over the window.</summary>
    int DistinctUsers,
    int WindowDays);

/* ---------------- security health ---------------- */

/// <summary>
/// One thing worth knowing about how this tenant is secured.
///
/// A finding, not a rule: it names what was measured and what it found, so the
/// screen can say "3 of 14 accounts have never signed in" rather than a bare
/// red dot nobody can act on.
/// </summary>
public record SecurityFindingDto(
    string Key,
    string Title,
    /// <summary>Ok, Warning or Risk.</summary>
    string Severity,
    string Finding,
    string Why,
    string? Fix,
    string? FixHref,
    int Count);

public record SecurityHealthDto(
    /// <summary>0-100. Weighted so a Risk costs more than a Warning.</summary>
    int Score,
    string Grade,
    int Risks,
    int Warnings,
    int Passed,
    DateTime CheckedAt,
    IReadOnlyList<SecurityFindingDto> Findings);

/* ---------------- field permissions ---------------- */

/// <summary>One securable field, and what this profile currently does with it.</summary>
public record FieldPermissionDto(
    string Object,
    string Field,
    string Label,
    string Why,
    bool Sensitive,
    bool CanRead,
    bool CanEdit);

/// <summary>
/// The rules to store. Only the exceptions travel — a field the profile leaves
/// alone has no row, and sending one for every field would turn a handful of
/// deliberate restrictions into a wall of noise nobody could read.
/// </summary>
public record SaveFieldPermissionsRequest(IReadOnlyList<FieldPermissionDto> Fields);

public record HierarchyNodeDto(
    int Id,
    string Name,
    string Email,
    string Role,
    string RoleName,
    string Scope,
    bool IsActive,
    int? ManagerId,
    /// <summary>Everyone under this person, at any depth — what a team scope sees.</summary>
    int TeamSize,
    IReadOnlyList<HierarchyNodeDto> Reports);

public record SetManagerRequest(int? ManagerId);

public record UserModuleDto(string Module, bool Effective, bool FromRole, bool? Override);

/* ---------------- teams ---------------- */

public record TeamMemberDto(
    int Id,
    string Name,
    string Email,
    string Role,
    string RoleName,
    bool IsActive,
    /// <summary>How far below the manager this person sits: 1 is a direct report.</summary>
    int Depth,
    /// <summary>How many people sit under them in turn — a sub-team inside the team.</summary>
    int TeamSize,
    IReadOnlyList<string> Branches);

/// <summary>
/// One manager and everyone beneath them.
///
/// A team here is not a stored object — it is the reporting line, read from the
/// side. Storing teams separately would give the company two hierarchies that
/// drift apart, and only one of them decides what a team-scoped role can see.
/// </summary>
public record TeamSummaryDto(
    int ManagerId,
    string ManagerName,
    string ManagerEmail,
    string ManagerRole,
    string ManagerRoleName,
    string Scope,
    string ScopeLabel,
    bool ManagerIsActive,
    int DirectReports,
    /// <summary>Everyone beneath the manager at any depth.</summary>
    int TotalMembers,
    int InactiveMembers,
    /// <summary>How many levels deep the team runs below the manager.</summary>
    int Depth,
    /// <summary>
    /// Whether the manager's role actually reaches their reports' records. False
    /// is the quiet failure this screen exists to surface: somebody has been
    /// given a team on an own-records seat, so the reporting line under them
    /// grants nothing.
    /// </summary>
    bool ScopeCoversTeam,
    IReadOnlyList<TeamMemberDto> Members);

public record TeamsOverviewDto(
    IReadOnlyList<TeamSummaryDto> Teams,
    /// <summary>People who report to nobody and carry nobody.</summary>
    IReadOnlyList<TeamMemberDto> Unassigned);

/// <summary>Moves several people under one manager, or off the tree entirely.</summary>
public record ReassignTeamRequest(
    IReadOnlyList<int> UserIds,
    int? ManagerId);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The Admin Console's access screens: what each role means, who reports to
/// whom, and which modules a person opens.
///
/// Separate from <see cref="UsersController"/>, which is CRUD over the user
/// row. This one is about the access model around it, and is gated harder —
/// everything here changes what somebody else can see.
/// </summary>
[ApiController]
[Route("api/admin/access")]
[Authorize]
// Administering somebody else's access is the "act on other people's users"
// power, so it is gated on exactly that rather than on a list of role names —
// which is the same swap this controller's own screens exist to make possible.
[RequirePermission(SecuredObjects.User, ObjectAction.ModifyAll)]
public class AccessAdminController(AppDbContext db, SessionService sessions) : ControllerBase
{
    /* ---------------- login history ---------------- */

    /// <summary>
    /// Who signed in, from where, and whether they still hold the session.
    ///
    /// Reads the session table the request guard already writes, so this is a
    /// window onto enforcement rather than a second record of it — if a row
    /// says live, that token really does still open the CRM.
    /// </summary>
    [HttpGet("login-history")]
    public async Task<ActionResult<LoginHistoryDto>> LoginHistory(
        [FromQuery] int days = 30,
        [FromQuery] int? userId = null,
        [FromQuery] bool liveOnly = false,
        CancellationToken ct = default)
    {
        var window = Math.Clamp(days, 1, 365);
        var since = DateTime.UtcNow.AddDays(-window);
        var now = DateTime.UtcNow;

        var query = db.UserSessions
            .Include(x => x.User)
            .Where(x => x.IssuedAt >= since);

        if (userId is int id) query = query.Where(x => x.UserId == id);
        if (liveOnly) query = query.Where(x => x.RevokedAt == null && x.ExpiresAt > now);

        var rows = await query
            .OrderByDescending(x => x.IssuedAt)
            .Take(1000)
            .ToListAsync(ct);

        var sessions = rows.Select(x => new LoginSessionDto(
            x.Id, x.UserId,
            x.User?.Name ?? "—",
            x.User?.Email ?? "—",
            x.User?.Role ?? "—",
            x.IssuedAt, x.ExpiresAt, x.LastSeenAt,
            x.RevokedAt, x.RevokedReason,
            x.Device, x.IpAddress,
            x.IsLive(now))).ToList();

        return Ok(new LoginHistoryDto(
            sessions,
            sessions.Count(x => x.IsLive),
            sessions.Count(x => x.RevokedAt is not null),

            // Expired rather than revoked: nobody ended it, it simply ran out.
            sessions.Count(x => x.RevokedAt is null && x.ExpiresAt <= now),
            sessions.Select(x => x.UserId).Distinct().Count(),
            window));
    }

    /// <summary>Ends one live session. The holder is signed out within seconds.</summary>
    [HttpPost("login-history/{id:int}/revoke")]
    public async Task<IActionResult> RevokeSession(
        int id, [FromServices] SessionService sessions, CancellationToken ct)
    {
        var session = await db.UserSessions.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (session is null) return NotFound(new { message = "That session no longer exists." });

        await sessions.RevokeAsync(session.TokenId, RevokeReasons.RevokedByAdmin, ct);
        return NoContent();
    }

    /* ---------------- security health ---------------- */

    /// <summary>
    /// What is currently weak about this tenant's security, measured rather
    /// than asserted.
    ///
    /// Every finding is computed from live rows — accounts, sessions, keys,
    /// permission profiles — so the screen cannot drift from the thing it is
    /// describing. Each one carries the count it found and where to go and fix
    /// it, because a health check that only says "warning" makes work instead
    /// of removing it.
    /// </summary>
    [HttpGet("security-health")]
    public async Task<ActionResult<SecurityHealthDto>> SecurityHealth(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var findings = new List<SecurityFindingDto>();

        /* ---------------- accounts ---------------- */

        var users = await db.Users
            .Select(u => new
            {
                u.Id, u.Name, u.Role, u.IsActive,
                u.MustChangePassword, u.LastLoginAt, u.PasswordChangedAt,
            })
            .ToListAsync(ct);

        var active = users.Where(u => u.IsActive).ToList();

        var neverSignedIn = active.Count(u => u.LastLoginAt is null);
        findings.Add(new SecurityFindingDto(
            "never-signed-in", "Accounts that have never been used",
            neverSignedIn == 0 ? "Ok" : "Warning",
            neverSignedIn == 0
                ? "Every active account has signed in at least once."
                : $"{neverSignedIn} of {active.Count} active accounts have never signed in.",
            "An account nobody has ever used is a credential in circulation with no one watching it. "
                + "Invites that were never taken up are the usual cause.",
            neverSignedIn == 0 ? null : "Deactivate the ones that are not needed",
            "/dashboard/users", neverSignedIn));

        var dormant = active.Count(u => u.LastLoginAt is not null && u.LastLoginAt < now.AddDays(-60));
        findings.Add(new SecurityFindingDto(
            "dormant", "Dormant accounts",
            dormant == 0 ? "Ok" : dormant > 3 ? "Risk" : "Warning",
            dormant == 0
                ? "No active account has been idle for more than 60 days."
                : $"{dormant} accounts have not signed in for over 60 days.",
            "People leave and nobody remembers to close the account. A dormant login is the one "
                + "most likely to be shared, reused elsewhere, or simply forgotten.",
            dormant == 0 ? null : "Review and deactivate",
            "/dashboard/users", dormant));

        var starterPasswords = active.Count(u => u.MustChangePassword);
        findings.Add(new SecurityFindingDto(
            "starter-password", "Accounts still on their starting password",
            starterPasswords == 0 ? "Ok" : "Risk",
            starterPasswords == 0
                ? "Every account is on a password its owner set."
                : $"{starterPasswords} accounts still carry the password somebody else set for them.",
            "A password an administrator chose is a password at least two people know.",
            starterPasswords == 0 ? null : "Ask them to sign in and change it",
            "/dashboard/users", starterPasswords));

        /* ---------------- administrators ---------------- */

        var admins = active.Count(u => u.Role == Roles.CompanyAdmin || u.Role == Roles.SuperAdmin);
        findings.Add(new SecurityFindingDto(
            "admin-count", "Administrator accounts",
            admins == 0 ? "Risk" : admins <= 3 ? "Ok" : "Warning",
            admins == 0
                ? "There is no active administrator."
                : $"{admins} of {active.Count} accounts can administer this workspace.",
            "Every administrator can read the whole book and change who else can. The count should be "
                + "small enough to name out loud.",
            admins <= 3 ? null : "Move people to a narrower role",
            "/dashboard/users/roles", admins));

        /* ---------------- sessions ---------------- */

        var liveSessions = await db.UserSessions
            .CountAsync(x => x.RevokedAt == null && x.ExpiresAt > now, ct);

        var staleSessions = await db.UserSessions
            .CountAsync(x => x.RevokedAt == null && x.ExpiresAt > now
                && x.LastSeenAt < now.AddDays(-7), ct);

        findings.Add(new SecurityFindingDto(
            "stale-sessions", "Sessions left open",
            staleSessions == 0 ? "Ok" : "Warning",
            staleSessions == 0
                ? $"{liveSessions} live sessions, all in use within the last week."
                : $"{staleSessions} of {liveSessions} live sessions have not been used for over a week.",
            "A token nobody is using is a token nobody would notice being used.",
            staleSessions == 0 ? null : "Revoke the ones that are not needed",
            "/dashboard/users/login-history", staleSessions));

        /* ---------------- keys and integrations ---------------- */

        var liveKeys = await db.ApiKeys.CountAsync(k => k.RevokedAt == null, ct);
        var oldKeys = await db.ApiKeys
            .CountAsync(k => k.RevokedAt == null && k.CreatedAt < now.AddDays(-180), ct);

        findings.Add(new SecurityFindingDto(
            "api-key-age", "API keys",
            liveKeys == 0 || oldKeys == 0 ? "Ok" : "Warning",
            liveKeys == 0
                ? "No API key is live."
                : oldKeys == 0
                    ? $"{liveKeys} live keys, none older than six months."
                    : $"{oldKeys} of {liveKeys} live keys are over six months old.",
            "A key that is never rotated is a password that never changes, held by a system that "
                + "cannot be asked whether it still needs it.",
            oldKeys == 0 ? null : "Rotate them",
            "/dashboard/companies/integrations", oldKeys));

        /* ---------------- field-level protection ---------------- */

        var restrictedFields = await db.FieldPermissions.CountAsync(ct);
        findings.Add(new SecurityFindingDto(
            "field-security", "Field-level restrictions",
            restrictedFields > 0 ? "Ok" : "Warning",
            restrictedFields > 0
                ? $"{restrictedFields} field rules are in force."
                : "No field is restricted on any profile.",
            "Customer phone numbers and pricing are the two things a departing salesperson takes with "
                + "them. Object permissions alone do not stop that; field rules do.",
            restrictedFields > 0 ? null : "Restrict the sensitive fields",
            "/dashboard/users/roles", restrictedFields));

        /* ---------------- the sign-in door ---------------- */

        // Read from the running configuration rather than from a stored setting,
        // so this reports what is actually true of this process right now.
        var secondFactorOff = HttpContext.RequestServices
            .GetService<SignInPolicy>()?.SkipSecondFactor ?? false;

        findings.Add(new SecurityFindingDto(
            "second-factor", "Sign-in code",
            secondFactorOff ? "Risk" : "Ok",
            secondFactorOff
                ? "The sign-in code is switched off — a correct password signs in outright."
                : "Every sign-in is confirmed with a code.",
            "A password alone is one leak, one reused credential or one shoulder-surf away from "
                + "somebody else holding the whole CRM.",
            secondFactorOff ? "Host the API outside Development to turn it back on" : null,
            null, secondFactorOff ? 1 : 0));

        /* ---------------- score ---------------- */

        var risks = findings.Count(f => f.Severity == "Risk");
        var warnings = findings.Count(f => f.Severity == "Warning");
        var passed = findings.Count(f => f.Severity == "Ok");

        // A risk costs three times a warning: the point of the number is to make
        // one genuine hole outrank a handful of tidy-ups.
        var penalty = (risks * 15) + (warnings * 5);
        var score = Math.Clamp(100 - penalty, 0, 100);

        var grade = score >= 90 ? "Strong"
            : score >= 75 ? "Reasonable"
            : score >= 50 ? "Weak"
            : "Poor";

        return Ok(new SecurityHealthDto(
            score, grade, risks, warnings, passed, now,
            findings.OrderBy(f => f.Severity switch
            {
                "Risk" => 0,
                "Warning" => 1,
                _ => 2,
            }).ThenBy(f => f.Title).ToList()));
    }

    /* ---------------- sharing rules ---------------- */

    /// <summary>
    /// The standing rules that widen who can see a slice of records, with the
    /// reference data the editor needs to describe one.
    ///
    /// Roles, branches and people travel with the rules rather than being
    /// fetched separately: the screen cannot render a rule without resolving
    /// "TeleSales" and "branch 3" into names, and three round trips to draw one
    /// list is three chances for it to render half-labelled.
    /// </summary>
    [HttpGet("sharing-rules")]
    public async Task<ActionResult<SharingRulesDto>> SharingRules(CancellationToken ct)
    {
        var rules = await db.SharingRules
            .OrderBy(r => r.Object).ThenBy(r => r.Name)
            .ToListAsync(ct);

        var branches = await db.Branches
            .Select(b => new NamedOptionDto(b.Id.ToString(), b.Name))
            .ToListAsync(ct);

        var users = await db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new NamedOptionDto(u.Id.ToString(), u.Name + " · " + u.Role))
            .ToListAsync(ct);

        var branchNames = branches.ToDictionary(b => b.Value, b => b.Label);
        var userNames = users.ToDictionary(u => u.Value, u => u.Label);

        var roles = RoleCatalog.All
            .Select(r => new NamedOptionDto(r.Key, r.Name))
            .ToList();

        var roleNames = roles.ToDictionary(r => r.Value, r => r.Label, StringComparer.OrdinalIgnoreCase);

        return Ok(new SharingRulesDto(
            rules.Select(r => new SharingRuleDto(
                r.Id, r.Name, r.Description, r.Object,
                r.OwnerRoleKey,
                r.OwnerRoleKey is null ? null : roleNames.GetValueOrDefault(r.OwnerRoleKey, r.OwnerRoleKey),
                r.CriteriaField, r.CriteriaOperator, r.CriteriaValue,
                r.Target.ToString(),
                r.TargetRoleKey,
                r.TargetRoleKey is null ? null : roleNames.GetValueOrDefault(r.TargetRoleKey, r.TargetRoleKey),
                r.TargetBranchId,
                r.TargetBranchId is int b ? branchNames.GetValueOrDefault(b.ToString()) : null,
                r.TargetUserId,
                r.TargetUserId is int u ? userNames.GetValueOrDefault(u.ToString()) : null,
                r.GrantEdit, r.IsActive)).ToList(),
            SecuredObjects.All,
            roles,
            branches,
            users,
            ShareableFields));
    }

    /// <summary>
    /// The fields a criteria rule may test, per object.
    ///
    /// A deliberately short list. Sharing on a free-text field produces a rule
    /// nobody can predict the reach of, and sharing on a field that is edited
    /// daily produces access that appears and disappears under people — so this
    /// offers the stable, low-cardinality columns a territory is actually drawn
    /// on.
    /// </summary>
    private static readonly Dictionary<string, string[]> ShareableFields = new()
    {
        [SecuredObjects.Lead] = ["City", "State", "Source", "Stage", "Priority", "EventType", "EventCategory", "Zone"],
        [SecuredObjects.Contact] = ["City", "State", "Type", "LifecycleStage"],
        [SecuredObjects.Opportunity] = ["Stage", "Source"],
        [SecuredObjects.Quotation] = ["Status", "ProjectName"],
        [SecuredObjects.Booking] = ["Status", "ProjectName", "TowerName"],
        [SecuredObjects.FollowUp] = ["Channel", "Status"],
    };

    [HttpPost("sharing-rules")]
    public async Task<ActionResult<SharingRuleDto>> CreateSharingRule(
        SaveSharingRuleRequest input, CancellationToken ct)
    {
        var rule = new SharingRule { CompanyId = User.GetCompanyId() };

        var error = ApplySharingRule(rule, input);
        if (error is not null) return BadRequest(new { message = error });

        db.SharingRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return Ok(await OneSharingRuleAsync(rule.Id, ct));
    }

    [HttpPut("sharing-rules/{id:int}")]
    public async Task<ActionResult<SharingRuleDto>> UpdateSharingRule(
        int id, SaveSharingRuleRequest input, CancellationToken ct)
    {
        var rule = await db.SharingRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NotFound(new { message = "That rule no longer exists." });

        var error = ApplySharingRule(rule, input);
        if (error is not null) return BadRequest(new { message = error });

        await db.SaveChangesAsync(ct);

        return Ok(await OneSharingRuleAsync(rule.Id, ct));
    }

    [HttpDelete("sharing-rules/{id:int}")]
    public async Task<IActionResult> DeleteSharingRule(int id, CancellationToken ct)
    {
        var rule = await db.SharingRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NoContent();

        db.SharingRules.Remove(rule);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// Validates and copies a rule request onto the row.
    ///
    /// Returns the reason it was refused, or null. The checks are the ones that
    /// stop a rule that reads sensibly but can never fire: a target with no
    /// subject, or a rule that names neither an owner role nor a criterion and
    /// would therefore share nothing at all.
    /// </summary>
    private static string? ApplySharingRule(SharingRule rule, SaveSharingRuleRequest input)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return "The rule needs a name.";

        if (!SecuredObjects.All.Contains(input.Object, StringComparer.OrdinalIgnoreCase))
        {
            return $"'{input.Object}' is not an object that can be shared.";
        }

        if (!Enum.TryParse<ShareTarget>(input.Target, ignoreCase: true, out var target))
        {
            return $"'{input.Target}' is not a sharing target.";
        }

        var ownerBased = !string.IsNullOrWhiteSpace(input.OwnerRoleKey);
        var criteriaBased = !string.IsNullOrWhiteSpace(input.CriteriaField)
            && !string.IsNullOrWhiteSpace(input.CriteriaValue);

        if (!ownerBased && !criteriaBased)
        {
            return "A rule has to say which records it shares — either an owner role or a criterion.";
        }

        if (ownerBased && criteriaBased)
        {
            return "A rule shares either what a role owns or what matches a criterion, not both.";
        }

        var targetError = target switch
        {
            ShareTarget.Role or ShareTarget.RoleAndSubordinates
                when string.IsNullOrWhiteSpace(input.TargetRoleKey) => "Choose the role to share with.",
            ShareTarget.Branch when input.TargetBranchId is null => "Choose the branch to share with.",
            ShareTarget.User when input.TargetUserId is null => "Choose the person to share with.",
            _ => null,
        };

        if (targetError is not null) return targetError;

        rule.Name = input.Name.Trim();
        rule.Description = input.Description?.Trim();
        rule.Object = input.Object;

        rule.OwnerRoleKey = ownerBased ? input.OwnerRoleKey : null;
        rule.CriteriaField = criteriaBased ? input.CriteriaField : null;
        rule.CriteriaOperator = criteriaBased ? (input.CriteriaOperator ?? "equals") : null;
        rule.CriteriaValue = criteriaBased ? input.CriteriaValue : null;

        rule.Target = target;
        rule.TargetRoleKey = target is ShareTarget.Role or ShareTarget.RoleAndSubordinates
            ? input.TargetRoleKey : null;
        rule.TargetBranchId = target == ShareTarget.Branch ? input.TargetBranchId : null;
        rule.TargetUserId = target == ShareTarget.User ? input.TargetUserId : null;

        rule.GrantEdit = input.GrantEdit;
        rule.IsActive = input.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;

        return null;
    }

    private async Task<SharingRuleDto> OneSharingRuleAsync(int id, CancellationToken ct)
    {
        var all = await SharingRules(ct);
        var payload = (all.Result as OkObjectResult)?.Value as SharingRulesDto;

        return payload!.Rules.First(r => r.Id == id);
    }

    /* ---------------- login policies ---------------- */

    /// <summary>
    /// When and from where each profile may sign in.
    ///
    /// One policy per profile rather than per person, so "Tele Sales works ten
    /// to seven from the office" is a single rule instead of forty copies that
    /// drift apart the first time somebody is hired.
    /// </summary>
    [HttpGet("login-policies")]
    public async Task<ActionResult<IReadOnlyList<LoginPolicyDto>>> LoginPolicies(CancellationToken ct)
    {
        var profiles = await db.PermissionSets
            .OrderBy(p => p.Name)
            .Select(p => new { p.Id, p.Name, p.RoleKey })
            .ToListAsync(ct);

        var policies = await db.LoginPolicies.ToListAsync(ct);

        var userCounts = await db.Users
            .Where(u => u.IsActive)
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Role, x => x.Count, ct);

        return Ok(profiles.Select(profile =>
        {
            var policy = policies.FirstOrDefault(p => p.PermissionSetId == profile.Id);

            return new LoginPolicyDto(
                profile.Id,
                profile.Name,
                profile.RoleKey,
                profile.RoleKey is null ? 0 : userCounts.GetValueOrDefault(profile.RoleKey),
                policy?.AllowedIpRanges,
                policy?.LoginFromMinute,
                policy?.LoginToMinute,
                policy?.AllowedDays ?? 0b1111111,
                policy?.IdleTimeoutMinutes,
                policy?.IsActive ?? false);
        }).ToList());
    }

    [HttpPut("login-policies/{profileId:int}")]
    public async Task<ActionResult<LoginPolicyDto>> SaveLoginPolicy(
        int profileId, SaveLoginPolicyRequest input, CancellationToken ct)
    {
        var profile = await db.PermissionSets.FirstOrDefaultAsync(p => p.Id == profileId, ct);
        if (profile is null) return NotFound(new { message = "That profile no longer exists." });

        if (input.LoginFromMinute is int from && (from < 0 || from > 1440))
        {
            return BadRequest(new { message = "The start of the window has to be a time of day." });
        }

        if (input.LoginToMinute is int to && (to < 0 || to > 1440))
        {
            return BadRequest(new { message = "The end of the window has to be a time of day." });
        }

        // A window with no days is a policy that locks the profile out entirely,
        // which is almost always a slip rather than an intention.
        if (input.IsActive && (input.AllowedDays & 0b1111111) == 0)
        {
            return BadRequest(new { message = "Allow at least one day, or the profile can never sign in." });
        }

        var policy = await db.LoginPolicies.FirstOrDefaultAsync(p => p.PermissionSetId == profileId, ct);

        if (policy is null)
        {
            policy = new LoginPolicy { CompanyId = User.GetCompanyId(), PermissionSetId = profileId };
            db.LoginPolicies.Add(policy);
        }

        policy.AllowedIpRanges = string.IsNullOrWhiteSpace(input.AllowedIpRanges)
            ? null : input.AllowedIpRanges.Trim();
        policy.LoginFromMinute = input.LoginFromMinute;
        policy.LoginToMinute = input.LoginToMinute;
        policy.AllowedDays = input.AllowedDays;
        policy.IdleTimeoutMinutes = input.IdleTimeoutMinutes;
        policy.IsActive = input.IsActive;

        await db.SaveChangesAsync(ct);

        var all = await LoginPolicies(ct);
        var rows = (all.Result as OkObjectResult)?.Value as IReadOnlyList<LoginPolicyDto>;

        return Ok(rows!.First(r => r.ProfileId == profileId));
    }

    /* ---------------- permission sets ---------------- */

    /// <summary>
    /// The add-on bundles, and who currently holds each.
    ///
    /// Separate from profiles on purpose. A profile is the one baseline a seat
    /// carries; a permission set is an extra grant laid on top of it, and the
    /// distinction is what lets somebody cover a colleague's leave for a
    /// fortnight without their job title changing.
    /// </summary>
    [HttpGet("permission-sets")]
    public async Task<ActionResult<IReadOnlyList<PermissionSetDto>>> PermissionSetList(
        CancellationToken ct)
        => Ok(await PermissionSetsAsync(ct));

    private async Task<List<PermissionSetDto>> PermissionSetsAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var sets = await db.PermissionSets
            .Include(p => p.ObjectPermissions)
            .Include(p => p.Assignments)
            .Where(p => !p.IsProfile)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var names = await db.Users
            .Where(u => u.IsActive)
            .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        return sets.Select(set => new PermissionSetDto(
            set.Id, set.Name, set.Description, set.IsSystem,
            set.ObjectPermissions
                .Select(o => new ObjectPermissionDto(o.Object, Names(o.Actions)))
                .OrderBy(o => o.Object)
                .ToList(),
            set.Assignments
                .Where(a => names.ContainsKey(a.UserId))
                .Select(a => new PermissionGrantDto(
                    a.Id, a.UserId, names[a.UserId], a.ExpiresAt, a.Reason,

                    // Expired grants stay on the row so the history is readable,
                    // but they are marked rather than silently counted — the
                    // resolver has already stopped honouring them.
                    a.ExpiresAt != null && a.ExpiresAt <= now))
                .OrderBy(a => a.UserName)
                .ToList()))
            .ToList();
    }

    [HttpPost("permission-sets")]
    public async Task<ActionResult<PermissionSetDto>> CreatePermissionSet(
        SavePermissionSetRequest input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            return BadRequest(new { message = "The set needs a name." });
        }

        var set = new PermissionSet
        {
            CompanyId = User.GetCompanyId(),
            Name = input.Name.Trim(),
            Description = input.Description?.Trim(),
            IsProfile = false,
        };

        ApplyGrants(set, input.Objects);

        db.PermissionSets.Add(set);
        await db.SaveChangesAsync(ct);

        return Ok((await PermissionSetsAsync(ct)).First(p => p.Id == set.Id));
    }

    [HttpPut("permission-sets/{id:int}")]
    public async Task<ActionResult<PermissionSetDto>> UpdatePermissionSet(
        int id, SavePermissionSetRequest input, CancellationToken ct)
    {
        var set = await db.PermissionSets
            .Include(p => p.ObjectPermissions)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsProfile, ct);

        if (set is null) return NotFound(new { message = "That permission set no longer exists." });

        set.Name = input.Name.Trim();
        set.Description = input.Description?.Trim();

        db.ObjectPermissions.RemoveRange(set.ObjectPermissions);
        set.ObjectPermissions.Clear();

        ApplyGrants(set, input.Objects);
        await db.SaveChangesAsync(ct);

        return Ok((await PermissionSetsAsync(ct)).First(p => p.Id == id));
    }

    [HttpDelete("permission-sets/{id:int}")]
    public async Task<IActionResult> DeletePermissionSet(int id, CancellationToken ct)
    {
        var set = await db.PermissionSets.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (set is null) return NoContent();

        if (set.IsSystem)
        {
            return BadRequest(new { message = "A set that ships with the product cannot be deleted." });
        }

        db.PermissionSets.Remove(set);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Grants a set to somebody, optionally until a date.</summary>
    [HttpPost("permission-sets/{id:int}/grant")]
    public async Task<ActionResult<PermissionSetDto>> GrantPermissionSet(
        int id, GrantPermissionSetRequest input, CancellationToken ct)
    {
        var set = await db.PermissionSets.FirstOrDefaultAsync(p => p.Id == id && !p.IsProfile, ct);
        if (set is null) return NotFound(new { message = "That permission set no longer exists." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == input.UserId && u.IsActive, ct);
        if (user is null) return BadRequest(new { message = "That account is not active." });

        var already = await db.UserPermissionSets
            .FirstOrDefaultAsync(a => a.PermissionSetId == id && a.UserId == input.UserId, ct);

        if (already is not null)
        {
            // Re-granting extends rather than duplicating. Two rows for the same
            // pair would make "when does this lapse" ambiguous.
            already.ExpiresAt = input.ExpiresAt;
            already.Reason = input.Reason?.Trim();
            already.GrantedById = User.GetUserId();
        }
        else
        {
            db.UserPermissionSets.Add(new UserPermissionSet
            {
                UserId = input.UserId,
                PermissionSetId = id,
                ExpiresAt = input.ExpiresAt,
                Reason = input.Reason?.Trim(),
                GrantedById = User.GetUserId(),
            });
        }

        await db.SaveChangesAsync(ct);

        // Their live sessions carry the old permission snapshot, so they are
        // ended — the same rule the rest of this console follows when access
        // changes underneath somebody.
        await sessions.RevokeAllForUserAsync(input.UserId, RevokeReasons.RoleChanged, ct: ct);

        return Ok((await PermissionSetsAsync(ct)).First(p => p.Id == id));
    }

    [HttpDelete("permission-sets/grants/{grantId:int}")]
    public async Task<IActionResult> RevokePermissionSet(int grantId, CancellationToken ct)
    {
        var grant = await db.UserPermissionSets.FirstOrDefaultAsync(a => a.Id == grantId, ct);
        if (grant is null) return NoContent();

        var userId = grant.UserId;

        db.UserPermissionSets.Remove(grant);
        await db.SaveChangesAsync(ct);

        await sessions.RevokeAllForUserAsync(userId, RevokeReasons.RoleChanged, ct: ct);

        return NoContent();
    }

    private static void ApplyGrants(PermissionSet set, IReadOnlyList<ObjectPermissionDto>? objects)
    {
        foreach (var grant in objects ?? [])
        {
            if (!SecuredObjects.All.Contains(grant.Object, StringComparer.OrdinalIgnoreCase)) continue;

            var actions = Parse(grant.Actions);
            if (actions == ObjectAction.None) continue;

            set.ObjectPermissions.Add(new ObjectPermission
            {
                Object = grant.Object,
                Actions = actions,
            });
        }
    }

    /* ---------------- roles ---------------- */




    /// <summary>The role catalogue, with how many people hold each.</summary>
    [HttpGet("roles")]
    public async Task<ActionResult<IReadOnlyList<RoleSummaryDto>>> RoleList(CancellationToken ct)
    {
        var counts = await db.Users
            .GroupBy(u => u.Role)
            .Select(g => new { Role = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Role, x => x.Count, ct);

        var profiles = await db.PermissionSets
            .Where(p => p.IsProfile && p.RoleKey != null)
            .Select(p => new { p.Id, p.RoleKey })
            .ToDictionaryAsync(p => p.RoleKey!, p => p.Id, ct);

        return Ok(RoleCatalog.All.Select(role => new RoleSummaryDto(
            role.Key,
            role.Name,
            role.Description,
            role.Scope.ToString(),
            ScopeLabel(role.Scope),
            role.Modules,
            role.ReadOnly,
            role.WonBusinessOnly,
            role.PeopleData,
            counts.GetValueOrDefault(role.Key),
            profiles.TryGetValue(role.Key, out var id) ? id : null))
            .ToList());
    }

    private static string ScopeLabel(DataScope scope) => scope switch
    {
        DataScope.Own => "Own records only",
        DataScope.Team => "Own records and everyone reporting to them",
        DataScope.Company => "Every record in the company",
        _ => "Every record in every company",
    };

    /* ---------------- profiles ---------------- */

    [HttpGet("profiles/{id:int}")]
    public async Task<ActionResult<ProfileDetailDto>> Profile(int id, CancellationToken ct)
    {
        var profile = await db.PermissionSets
            .Include(p => p.ObjectPermissions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (profile is null) return NotFound(new { message = "That profile no longer exists." });

        return Ok(ToDetail(profile));
    }

    [HttpPut("profiles/{id:int}")]
    public async Task<ActionResult<ProfileDetailDto>> UpdateProfile(
        int id, UpdateProfileRequest request, CancellationToken ct)
    {
        var profile = await db.PermissionSets
            .Include(p => p.ObjectPermissions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (profile is null) return NotFound(new { message = "That profile no longer exists." });

        if (request.Modules is not null)
        {
            profile.ModulesCsv = string.Join(',', request.Modules.Where(Modules.All.Contains));
        }

        if (request.Objects is not null)
        {
            // Replaced wholesale rather than merged: the screen sends the full
            // matrix it is showing, and merging would make an unticked box mean
            // "leave it alone" instead of "take it away".
            db.ObjectPermissions.RemoveRange(profile.ObjectPermissions);
            profile.ObjectPermissions.Clear();

            foreach (var row in request.Objects)
            {
                if (!SecuredObjects.All.Contains(row.Object)) continue;

                profile.ObjectPermissions.Add(new ObjectPermission
                {
                    Object = row.Object,
                    Actions = Parse(row.Actions),
                });
            }
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(ToDetail(profile));
    }

    private static ProfileDetailDto ToDetail(PermissionSet profile) => new(
        profile.Id,
        profile.Name,
        profile.Description,
        profile.RoleKey,
        profile.IsSystem,
        profile.ModulesCsv?.Split(',', StringSplitOptions.RemoveEmptyEntries) ?? [],
        profile.ObjectPermissions
            .OrderBy(o => o.Object)
            .Select(o => new ObjectPermissionDto(o.Object, Names(o.Actions)))
            .ToList());

    /// <summary>
    /// The single-bit actions and the name each goes over the wire as.
    ///
    /// Both halves are spelled out rather than derived, because the aliases in
    /// <see cref="ObjectAction"/> defeat both shortcuts. Filtering the
    /// composites out of <c>Enum.GetValues</c> drops View along with Read —
    /// they are one value — and <c>ToString()</c> on that value answers "Read",
    /// which is not what the permission screen ticks. Between them the screen
    /// showed View unticked on every object, and saving what it showed stripped
    /// View from the profile.
    /// </summary>
    private static readonly (ObjectAction Flag, string Name)[] Atoms =
    [
        (ObjectAction.View, nameof(ObjectAction.View)),
        (ObjectAction.Create, nameof(ObjectAction.Create)),
        (ObjectAction.Edit, nameof(ObjectAction.Edit)),
        (ObjectAction.Delete, nameof(ObjectAction.Delete)),
        (ObjectAction.ViewAll, nameof(ObjectAction.ViewAll)),
        (ObjectAction.ModifyAll, nameof(ObjectAction.ModifyAll)),
    ];

    /// <summary>
    /// Folds the named actions into the stored flags.
    ///
    /// Only the atoms are accepted, so a request naming a composite — "Full",
    /// "Everything" — grants nothing rather than everything. The screen never
    /// sends those; the check is here because this endpoint decides what other
    /// people can see, and the body is untrusted input.
    /// </summary>
    private static ObjectAction Parse(IEnumerable<string> names) =>
        names.Aggregate(
            ObjectAction.None,
            (all, name) => all | Atoms
                .FirstOrDefault(a => string.Equals(a.Name, name, StringComparison.OrdinalIgnoreCase))
                .Flag);

    private static string[] Names(ObjectAction actions) =>
        Atoms
            .Where(a => (actions & a.Flag) == a.Flag)
            .Select(a => a.Name)
            .ToArray();

    /* ---------------- field permissions ---------------- */

    /// <summary>
    /// Every field this profile could restrict, with what it currently does.
    ///
    /// The full catalogue comes back rather than only the stored exceptions,
    /// because the screen is a list of decisions and a field with no row is a
    /// decision too — "follows the object". Rendering that from an empty list
    /// would need the client to hold its own copy of the catalogue.
    /// </summary>
    [HttpGet("profiles/{id:int}/fields")]
    public async Task<ActionResult<IReadOnlyList<FieldPermissionDto>>> ProfileFields(
        int id, CancellationToken ct)
    {
        var profile = await db.PermissionSets
            .Include(p => p.FieldPermissions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (profile is null) return NotFound(new { message = "That profile no longer exists." });

        var stored = profile.FieldPermissions.ToDictionary(
            f => $"{f.Object}.{f.Field}", StringComparer.OrdinalIgnoreCase);

        return Ok(SecurableFields.All
            .Select(field =>
            {
                var rule = stored.GetValueOrDefault($"{field.Object}.{field.Name}");

                return new FieldPermissionDto(
                    field.Object, field.Name, field.Label, field.Why, field.Sensitive,
                    // No row means the field follows its object, which is full
                    // access — the table stores restrictions, not grants.
                    rule?.CanRead ?? true,
                    rule?.CanEdit ?? true);
            })
            .ToList());
    }

    [HttpPut("profiles/{id:int}/fields")]
    public async Task<ActionResult<IReadOnlyList<FieldPermissionDto>>> SaveProfileFields(
        int id, SaveFieldPermissionsRequest request, CancellationToken ct)
    {
        var profile = await db.PermissionSets
            .Include(p => p.FieldPermissions)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (profile is null) return NotFound(new { message = "That profile no longer exists." });

        // Replaced wholesale, like the object matrix: the screen sends every
        // field it is showing, and merging would make an unticked box mean
        // "leave it alone" instead of "take it away".
        db.FieldPermissions.RemoveRange(profile.FieldPermissions);
        profile.FieldPermissions.Clear();

        foreach (var row in request.Fields)
        {
            if (!SecurableFields.Exists(row.Object, row.Field)) continue;

            // Only the restrictions are worth a row. A field with both verbs
            // intact is the default, and storing it would grow the table by the
            // size of the catalogue for every profile in every company.
            if (row.CanRead && row.CanEdit) continue;

            profile.FieldPermissions.Add(new FieldPermission
            {
                Object = row.Object,
                Field = row.Field,
                CanRead = row.CanRead,
                // A field nobody may read is not one anybody may write, whatever
                // the request says. Letting those disagree would leave a seat
                // able to overwrite a number it cannot see.
                CanEdit = row.CanRead && row.CanEdit,
            });
        }

        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return await ProfileFields(id, ct);
    }

    /* ---------------- object visibility ---------------- */

    [HttpGet("visibility")]
    public async Task<ActionResult<IReadOnlyList<ObjectVisibilityDto>>> Visibility(CancellationToken ct)
    {
        var rules = await db.ObjectVisibilityRules.OrderBy(v => v.Object).ToListAsync(ct);

        return Ok(rules
            .Select(v => new ObjectVisibilityDto(
                v.Object, v.Visibility.ToString(), v.GrantAccessUsingHierarchy))
            .ToList());
    }

    [HttpPut("visibility/{securedObject}")]
    public async Task<IActionResult> SetVisibility(
        string securedObject, ObjectVisibilityDto request, CancellationToken ct)
    {
        var rule = await db.ObjectVisibilityRules
            .FirstOrDefaultAsync(v => v.Object == securedObject, ct);

        if (rule is null) return NotFound(new { message = "No such object." });

        if (Enum.TryParse<ObjectVisibility>(request.Visibility, true, out var visibility))
        {
            rule.Visibility = visibility;
        }

        rule.GrantAccessUsingHierarchy = request.GrantAccessUsingHierarchy;
        rule.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- reporting lines ---------------- */

    /// <summary>
    /// The reporting tree, plus the people who are not on it yet.
    ///
    /// Returned as a tree rather than a flat list because that is the shape the
    /// question has: an AGM's team is everyone beneath them, and a client that
    /// had to assemble that from parent pointers would end up reimplementing
    /// the walk the server already does for access control.
    /// </summary>
    [HttpGet("hierarchy")]
    public async Task<ActionResult<IReadOnlyList<HierarchyNodeDto>>> Hierarchy(CancellationToken ct)
    {
        var users = await db.Users
            .OrderBy(u => u.Name)
            .Select(u => new { u.Id, u.Name, u.Email, u.Role, u.IsActive, u.ManagerId })
            .ToListAsync(ct);

        var childrenOf = users
            .Where(u => u.ManagerId is not null)
            .GroupBy(u => u.ManagerId!.Value)
            .ToDictionary(g => g.Key, g => g.Select(u => u.Id).ToList());

        var byId = users.ToDictionary(u => u.Id);

        HierarchyNodeDto Build(int id, HashSet<int> seen)
        {
            var user = byId[id];
            var role = RoleCatalog.For(user.Role);

            var reports = new List<HierarchyNodeDto>();

            // The seen set breaks a cycle. Someone can be made their own
            // manager's manager through two separate edits, and without this
            // the walk never returns.
            if (childrenOf.TryGetValue(id, out var children))
            {
                foreach (var child in children)
                {
                    if (!seen.Add(child)) continue;
                    reports.Add(Build(child, seen));
                }
            }

            return new HierarchyNodeDto(
                user.Id, user.Name, user.Email, user.Role, role.Name,
                role.Scope.ToString(), user.IsActive, user.ManagerId,
                reports.Sum(r => r.TeamSize + 1),
                reports);
        }

        var roots = users
            .Where(u => u.ManagerId is null || !byId.ContainsKey(u.ManagerId.Value))
            .Select(u => Build(u.Id, [u.Id]))
            .ToList();

        return Ok(roots);
    }

    [HttpPut("users/{id:int}/manager")]
    public async Task<IActionResult> SetManager(
        int id, SetManagerRequest request, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound(new { message = "That user no longer exists." });

        if (request.ManagerId == id)
        {
            return BadRequest(new { message = "Somebody cannot report to themselves." });
        }

        if (request.ManagerId is int managerId)
        {
            var manager = await db.Users.FirstOrDefaultAsync(u => u.Id == managerId, ct);
            if (manager is null) return NotFound(new { message = "That manager no longer exists." });

            // Walking up from the proposed manager must not arrive back here.
            // A cycle would give everyone in it access to everyone else's
            // records and hang the team walk that computes it.
            var edges = await db.Users
                .Where(u => u.ManagerId != null)
                .Select(u => new { u.Id, ManagerId = u.ManagerId!.Value })
                .ToDictionaryAsync(u => u.Id, u => u.ManagerId, ct);

            var cursor = managerId;
            var guard = 0;

            while (guard++ < 100)
            {
                if (cursor == id)
                {
                    return BadRequest(new
                    {
                        message = "That would create a loop in the reporting line.",
                    });
                }

                if (!edges.TryGetValue(cursor, out var next)) break;
                cursor = next;
            }
        }

        user.ManagerId = request.ManagerId;
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /* ---------------- per-user modules ---------------- */

    [HttpGet("users/{id:int}/modules")]
    public async Task<ActionResult<IReadOnlyList<UserModuleDto>>> UserModules(int id, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound(new { message = "That user no longer exists." });

        var role = RoleCatalog.For(user.Role);
        var fromRole = role.Modules.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var overrides = await db.UserModuleGrants
            .Where(g => g.UserId == id)
            .ToDictionaryAsync(g => g.Module, g => g.Granted, StringComparer.OrdinalIgnoreCase, ct);

        return Ok(Modules.All.Select(module =>
        {
            var grantedByRole = fromRole.Contains(module);
            var exception = overrides.TryGetValue(module, out var granted) ? granted : (bool?)null;

            return new UserModuleDto(module, exception ?? grantedByRole, grantedByRole, exception);
        }).ToList());
    }

    [HttpPut("users/{id:int}/modules/{module}")]
    public async Task<IActionResult> SetUserModule(
        int id, string module, [FromBody] bool? granted, CancellationToken ct)
    {
        if (!Modules.All.Contains(module)) return NotFound(new { message = "No such module." });

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user is null) return NotFound(new { message = "That user no longer exists." });

        var existing = await db.UserModuleGrants
            .FirstOrDefaultAsync(g => g.UserId == id && g.Module == module, ct);

        // Null clears the exception and hands the module back to the role,
        // which is different from denying it — and is what the screen's
        // "follow the role" option means.
        if (granted is null)
        {
            if (existing is not null) db.UserModuleGrants.Remove(existing);
        }
        else if (existing is not null)
        {
            existing.Granted = granted.Value;
        }
        else
        {
            db.UserModuleGrants.Add(new UserModuleGrant
            {
                UserId = id,
                Module = module,
                Granted = granted.Value,
            });
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- teams ---------------- */

    /// <summary>
    /// Every manager with their team, plus everyone who is on nobody's.
    ///
    /// Derived from the reporting line rather than from a teams table, so what
    /// this screen shows and what the access layer enforces cannot disagree.
    /// </summary>
    [HttpGet("teams")]
    public async Task<ActionResult<TeamsOverviewDto>> Teams(CancellationToken ct)
    {
        var people = await LoadPeopleAsync(ct);

        var childrenOf = people.Values
            .Where(p => p.ManagerId is not null && people.ContainsKey(p.ManagerId.Value))
            .GroupBy(p => p.ManagerId!.Value)
            .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Name).ToList());

        // Walks a manager's whole subtree, stamping how deep each person sits.
        List<TeamMemberDto> Members(int managerId, int depth, HashSet<int> seen)
        {
            if (!childrenOf.TryGetValue(managerId, out var children)) return [];

            var rows = new List<TeamMemberDto>();

            foreach (var child in children)
            {
                // A ring in the stored tree would otherwise walk forever.
                if (!seen.Add(child.Id)) continue;

                var below = Members(child.Id, depth + 1, seen);

                rows.Add(new TeamMemberDto(
                    child.Id, child.Name, child.Email, child.Role,
                    RoleCatalog.For(child.Role).Name, child.IsActive,
                    depth, below.Count, child.Branches));

                rows.AddRange(below);
            }

            return rows;
        }

        var teams = new List<TeamSummaryDto>();

        foreach (var manager in people.Values.Where(p => childrenOf.ContainsKey(p.Id)).OrderBy(p => p.Name))
        {
            var members = Members(manager.Id, 1, [manager.Id]);
            var role = RoleCatalog.For(manager.Role);

            teams.Add(new TeamSummaryDto(
                manager.Id, manager.Name, manager.Email, manager.Role, role.Name,
                role.Scope.ToString(), ScopeLabel(role.Scope), manager.IsActive,
                childrenOf[manager.Id].Count,
                members.Count,
                members.Count(m => !m.IsActive),
                members.Count == 0 ? 0 : members.Max(m => m.Depth),
                role.Scope >= DataScope.Team,
                members));
        }

        var unassigned = people.Values
            .Where(p => p.ManagerId is null && !childrenOf.ContainsKey(p.Id))
            .OrderBy(p => p.Name)
            .Select(p => new TeamMemberDto(
                p.Id, p.Name, p.Email, p.Role, RoleCatalog.For(p.Role).Name,
                p.IsActive, 0, 0, p.Branches))
            .ToList();

        return Ok(new TeamsOverviewDto(teams, unassigned));
    }

    /// <summary>
    /// Moves a batch of people under one manager, or off the tree.
    ///
    /// Batched because that is the shape the real task has — a manager leaves
    /// and their eleven reports move together. Doing it one call at a time left
    /// half a team parented to somebody who had already gone whenever one of
    /// the calls failed.
    /// </summary>
    [HttpPost("teams/reassign")]
    public async Task<ActionResult<TeamsOverviewDto>> Reassign(
        ReassignTeamRequest request, CancellationToken ct)
    {
        var ids = request.UserIds.Distinct().ToList();
        if (ids.Count == 0) return await Teams(ct);

        if (request.ManagerId is int target && ids.Contains(target))
        {
            return BadRequest(new { message = "Somebody cannot report to themselves." });
        }

        var users = await db.Users.Where(u => ids.Contains(u.Id)).ToListAsync(ct);

        if (users.Count != ids.Count)
        {
            return NotFound(new { message = "Some of those people are no longer in this company." });
        }

        if (request.ManagerId is int managerId)
        {
            var manager = await db.Users.FirstOrDefaultAsync(u => u.Id == managerId, ct);
            if (manager is null)
            {
                return NotFound(new { message = "That manager is no longer in this company." });
            }

            // Checked against the tree as it will be, not as it is: moving two
            // people in one call can close a ring that neither move closes on
            // its own.
            var edges = await db.Users
                .Where(u => u.ManagerId != null)
                .Select(u => new { u.Id, ManagerId = u.ManagerId!.Value })
                .ToDictionaryAsync(u => u.Id, u => u.ManagerId, ct);

            foreach (var id in ids) edges[id] = managerId;

            foreach (var id in ids)
            {
                if (!WalkReachesSelf(edges, id)) continue;

                var name = users.First(u => u.Id == id).Name;
                return BadRequest(new
                {
                    message = $"Moving {name} there would create a loop in the reporting line.",
                });
            }
        }

        foreach (var user in users) user.ManagerId = request.ManagerId;
        await db.SaveChangesAsync(ct);

        return await Teams(ct);
    }

    /// <summary>Whether walking up from <paramref name="start"/> arrives back at it.</summary>
    private static bool WalkReachesSelf(Dictionary<int, int> edges, int start)
    {
        if (!edges.TryGetValue(start, out var cursor)) return false;

        for (var guard = 0; guard < 500; guard++)
        {
            if (cursor == start) return true;
            if (!edges.TryGetValue(cursor, out var next)) return false;
            cursor = next;
        }

        return true;
    }

    private sealed record Person(
        int Id, string Name, string Email, string Role, bool IsActive,
        int? ManagerId, IReadOnlyList<string> Branches);

    private async Task<Dictionary<int, Person>> LoadPeopleAsync(CancellationToken ct)
    {
        var rows = await db.Users
            .Select(u => new Person(
                u.Id, u.Name, u.Email, u.Role, u.IsActive, u.ManagerId,
                u.UserBranches.Select(ub => ub.Branch!.Name).ToList()))
            .ToListAsync(ct);

        return rows.ToDictionary(p => p.Id);
    }
}
