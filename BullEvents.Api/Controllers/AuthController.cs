using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(
    AppDbContext db,
    TokenService tokenService,
    OtpService otp,
    SessionService sessions,
    TenantContext tenant,
    SignInPolicy signIn,
    LoginPolicyGuard loginPolicy,
    IEmailSender email) : ControllerBase
{

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request)
    {
        var user = await db.Users
            .FirstOrDefaultAsync(u => u.Email == request.Email && u.IsActive);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        // Checked before the code is sent rather than after it is typed: mailing
        // somebody a second factor for a door that is closed wastes their time
        // and teaches them the code is unreliable.
        if (user.Role != Roles.SuperAdmin && await IsSuspendedAsync(user.CompanyId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "This workspace is suspended. Contact your administrator.",
            });
        }

        // The profile's own sign-in window and address restrictions, checked
        // after the password so a refusal never doubles as a way to discover
        // which addresses are allowed for an account that does not exist.
        if (await loginPolicy.RefuseAsync(user, HttpContext.Connection.RemoteIpAddress) is string refusal)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = refusal });
        }

        var masked = MaskEmail(user.Email);

        // With the second factor off there is nothing to verify, so the session
        // is issued here and the client is handed it directly rather than being
        // sent to a code screen that would accept anything. No challenge is
        // created and no mail is attempted.
        if (signIn.SkipSecondFactor)
        {
            var session = await IssueSessionAsync(user);

            return session is null
                ? Unauthorized(new { message = "Account is no longer active." })
                : Ok(new LoginResponse(string.Empty, masked, Delivered: false, Session: session));
        }

        var pendingToken = tokenService.CreatePendingOtpToken(user.Id);

        await otp.IssueAsync(user);

        return Ok(new LoginResponse(pendingToken, masked, email.Delivers));
    }

    /// <summary>
    /// Second factor. The token issued here is bound to the user's home company
    /// so the session is always usable; a platform admin is additionally told to
    /// pick a tenant, which re-issues the token through <see cref="SelectCompany"/>.
    /// </summary>
    [HttpPost("verify-otp")]
    public async Task<ActionResult<AuthResponse>> VerifyOtp(VerifyOtpRequest request)
    {
        var userId = tokenService.ValidatePendingOtpToken(request.PendingToken);
        if (userId is null)
        {
            return Unauthorized(new { message = "Verification session expired. Please sign in again." });
        }

        var check = await otp.VerifyAsync(userId.Value, request.Code);

        if (check != OtpService.Result.Ok)
        {
            // Each case is named, because "incorrect code" for an expired one
            // sends the user hunting for a typo in a code that was never going
            // to work — and they retype it until the attempt limit kills it.
            return Unauthorized(new
            {
                message = check switch
                {
                    OtpService.Result.Expired =>
                        "That code has expired. Sign in again to get a new one.",
                    OtpService.Result.TooManyAttempts =>
                        "Too many incorrect attempts. Sign in again to get a new code.",
                    OtpService.Result.NoChallenge =>
                        "No code is outstanding. Sign in again.",
                    _ => "Incorrect verification code.",
                },
            });
        }

        var verified = await db.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);

        if (verified is null)
        {
            return Unauthorized(new { message = "Account is no longer active." });
        }

        if (verified.Role != Roles.SuperAdmin && await IsSuspendedAsync(verified.CompanyId))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "This workspace is suspended. Contact your administrator.",
            });
        }

        var session = await IssueSessionAsync(verified);

        return session is null
            ? Unauthorized(new { message = "Account is no longer active." })
            : Ok(session);
    }

    /// <summary>
    /// Mints the access token, opens the session and builds the profile the
    /// client signs in with.
    ///
    /// Shared by the two ways in — passing the code, and the development path
    /// where there is no code — so that neither can drift into issuing a session
    /// the other would not. Returns null when the account no longer resolves to
    /// a live company.
    /// </summary>
    private async Task<AuthResponse?> IssueSessionAsync(User account)
    {
        var user = await db.Users
            .Include(u => u.Company)
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == account.Id && u.IsActive);

        if (user?.Company is null) return null;

        var branchIds = user.UserBranches.Select(ub => ub.BranchId).ToList();
        var (accessToken, expiresAt, tokenId) =
            tokenService.CreateAccessToken(user, user.Company, branchIds);

        // Stamped at the point a session is actually issued: an account whose
        // password was guessed but whose second factor was never passed has not
        // been signed into, and reading it as a login would hide exactly the
        // dormant accounts an administrator is looking for.
        user.LastLoginAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        await OpenSessionAsync(tokenId, user, user.CompanyId, expiresAt);

        var profile = new UserProfileDto(
            user.Id, user.Name, user.Email, user.Role,
            user.Company.Id, user.Company.Name, branchIds
        );

        var isPlatformAdmin = user.Role == Roles.SuperAdmin;
        var companies = await LoadCompanyOptionsAsync(isPlatformAdmin, user.CompanyId);

        return new AuthResponse(
            accessToken, expiresAt, profile,
            RequiresCompanySelection: isPlatformAdmin && companies.Count > 0,
            Companies: companies,
            MustChangePassword: user.MustChangePassword
        );
    }

    /// <summary>
    /// Replaces the password on the signed-in account and clears the block.
    ///
    /// The current password is required even though the caller already holds a
    /// valid token: a token can be lifted from an unlocked machine, and without
    /// this check that is enough to lock the real owner out of their own account.
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized(new { message = "Account is no longer active." });

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return Unauthorized(new { message = "That is not your current password." });
        }

        if (BCrypt.Net.BCrypt.Verify(request.NewPassword, user.PasswordHash))
        {
            return BadRequest(new { message = "The new password must be different from the old one." });
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.PasswordChangedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        // Everywhere except here. Somebody changing their password because they
        // think it leaked has just told us to end whatever the other party has
        // open; keeping their own session alive is the difference between that
        // and signing themselves out too.
        var ended = await sessions.RevokeAllForUserAsync(
            user.Id, RevokeReasons.PasswordChanged, except: tenant.TokenId);

        return Ok(new
        {
            message = ended > 0
                ? $"Password changed. {ended} other {(ended == 1 ? "session was" : "sessions were")} signed out."
                : "Password changed.",
        });
    }

    /// <summary>The tenants this session may open — drives the company picker and switcher.</summary>
    [HttpGet("companies")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<CompanyOptionDto>>> GetSelectableCompanies()
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized(new { message = "Account is no longer active." });

        return Ok(await LoadCompanyOptionsAsync(user.Role == Roles.SuperAdmin, user.CompanyId));
    }

    /// <summary>
    /// Re-issues the access token against the chosen tenant.
    ///
    /// Every query filter in the DbContext reads <c>company_id</c> off the JWT,
    /// so swapping tenants is a token swap and nothing else — there is no
    /// "current company" stored server-side that could drift from the claims.
    /// </summary>
    [HttpPost("select-company")]
    [Authorize]
    public async Task<ActionResult<AuthResponse>> SelectCompany(SelectCompanyRequest request)
    {
        var user = await CurrentUserAsync();
        if (user is null) return Unauthorized(new { message = "Account is no longer active." });

        var isPlatformAdmin = user.Role == Roles.SuperAdmin;
        if (!isPlatformAdmin && request.CompanyId != user.CompanyId)
        {
            return Forbid();
        }

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == request.CompanyId);
        if (company is null) return NotFound(new { message = "That company no longer exists." });

        // A trial is open for business; a suspension is not — except to the
        // operator whose job is lifting it. Locking them out of the tenant they
        // suspended is how a platform ends up with no way back in.
        if (!isPlatformAdmin
            && !string.Equals(company.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new
            {
                message = $"{company.Name} is {company.Status.ToLowerInvariant()} and cannot be opened.",
            });
        }

        // A platform admin has no branch memberships inside a tenant it is only
        // visiting, so it gets every branch of that tenant; a company user keeps
        // the branches actually assigned to them.
        var branchIds = isPlatformAdmin
            ? await db.Branches.Where(b => b.CompanyId == company.Id).Select(b => b.Id).ToListAsync()
            : user.UserBranches.Select(ub => ub.BranchId).ToList();

        var (accessToken, expiresAt, tokenId) =
            tokenService.CreateAccessToken(user, company, branchIds);

        await OpenSessionAsync(tokenId, user, company.Id, expiresAt);

        // The token being replaced is bound to the previous tenant and would
        // still open it. Retiring it here is what makes switching a move rather
        // than an accumulation of open doors.
        if (tenant.TokenId is Guid previous)
        {
            await sessions.RevokeAsync(previous, RevokeReasons.SwitchedCompany);
        }

        var profile = new UserProfileDto(
            user.Id, user.Name, user.Email, user.Role,
            company.Id, company.Name, branchIds
        );

        return Ok(new AuthResponse(
            accessToken, expiresAt, profile,
            RequiresCompanySelection: false,
            Companies: await LoadCompanyOptionsAsync(isPlatformAdmin, user.CompanyId),
            MustChangePassword: user.MustChangePassword
        ));
    }

    /* ------------------------------------------------------------------ *
     * Sessions
     * ------------------------------------------------------------------ */

    /// <summary>Every device this account is currently signed in on.</summary>
    [HttpGet("sessions")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<SessionDto>>> Sessions(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var now = DateTime.UtcNow;

        var rows = await db.UserSessions
            .IgnoreQueryFilters()
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastSeenAt)
            .ToListAsync(ct);

        return Ok(rows
            .Select(s => new SessionDto(
                s.Id,
                s.Device ?? "Unknown device",
                s.IpAddress,
                s.IssuedAt,
                s.LastSeenAt,
                s.ExpiresAt,
                s.TokenId == tenant.TokenId))
            .ToList());
    }

    /// <summary>Ends the session this request arrived on.</summary>
    [HttpPost("sign-out")]
    [Authorize]
    public async Task<IActionResult> EndSession(CancellationToken ct)
    {
        if (tenant.TokenId is Guid tokenId)
        {
            await sessions.RevokeAsync(tokenId, RevokeReasons.SignedOut, ct);
        }

        return NoContent();
    }

    /// <summary>Ends one other session — the "that is not my laptop" button.</summary>
    [HttpPost("sessions/{id:int}/revoke")]
    [Authorize]
    public async Task<IActionResult> RevokeSession(int id, CancellationToken ct)
    {
        var userId = User.GetUserId();

        var session = await db.UserSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == id && s.UserId == userId, ct);

        if (session is null) return NotFound(new { message = "That session no longer exists." });

        await sessions.RevokeAsync(session.TokenId, RevokeReasons.SignedOut, ct);
        return NoContent();
    }

    /// <summary>Ends every session except this one.</summary>
    [HttpPost("sessions/revoke-others")]
    [Authorize]
    public async Task<ActionResult> RevokeOtherSessions(CancellationToken ct)
    {
        var ended = await sessions.RevokeAllForUserAsync(
            User.GetUserId(), RevokeReasons.SignedOutEverywhere, tenant.TokenId, ct);

        return Ok(new { ended });
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    private Task OpenSessionAsync(Guid tokenId, User user, int companyId, DateTime expiresAt) =>
        sessions.OpenAsync(
            tokenId, user, companyId, expiresAt,
            SessionService.Describe(Request.Headers.UserAgent.ToString()),
            HttpContext.Connection.RemoteIpAddress?.ToString());

    /// <summary>
    /// Unfiltered on purpose: this runs while the request is still anonymous, so
    /// the tenant filter has no tenant to apply.
    /// </summary>
    private async Task<bool> IsSuspendedAsync(int companyId)
    {
        var status = await db.Companies
            .IgnoreQueryFilters()
            .Where(c => c.Id == companyId)
            .Select(c => c.Status)
            .FirstOrDefaultAsync();

        return string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase);
    }

    private Task<User?> CurrentUserAsync()
    {
        var userId = User.GetUserId();
        return db.Users
            .Include(u => u.UserBranches)
            .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
    }

    private async Task<IReadOnlyList<CompanyOptionDto>> LoadCompanyOptionsAsync(
        bool isPlatformAdmin, int homeCompanyId)
    {
        var query = db.Companies.AsQueryable();
        if (!isPlatformAdmin) query = query.Where(c => c.Id == homeCompanyId);

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new CompanyOptionDto(
                c.Id,
                c.Name,
                c.Slug,
                c.PlanTier,
                c.Status,
                db.Branches.Count(b => b.CompanyId == c.Id),
                db.Users.Count(u => u.CompanyId == c.Id),
                c.Id == homeCompanyId
            ))
            .ToListAsync();
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2) return email;
        var local = parts[0];
        var masked = local.Length > 2
            ? local[..2] + new string('*', Math.Max(local.Length - 2, 2))
            : local[..1] + "***";
        return $"{masked}@{parts[1]}";
    }
}
