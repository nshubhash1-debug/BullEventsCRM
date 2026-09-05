namespace BullEvents.Api.Models;

/// <summary>
/// One issued access token, recorded so it can be taken back.
///
/// A JWT is self-contained by design: the API can verify it without asking
/// anybody, which is what makes it fast and what makes it impossible to
/// withdraw. Every token now carries a <c>jti</c> that points at a row here, and
/// the request pipeline checks that row. The cost is one cached lookup per
/// request; what it buys is that deactivating somebody, suspending their
/// company or changing their role stops the session they already have, instead
/// of waiting out the twelve-hour expiry.
/// </summary>
public class UserSession : ITenantScoped
{
    public int Id { get; set; }

    /// <summary>
    /// The tenant this session was opened against — not necessarily the user's
    /// home company, because a platform operator switching tenants opens a new
    /// session against the one they are visiting.
    /// </summary>
    public int CompanyId { get; set; }

    public int UserId { get; set; }

    /// <summary>The token's <c>jti</c>. Unique, and the only thing the token carries about this row.</summary>
    public Guid TokenId { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Refreshed at most once a minute rather than on every request — this is a
    /// "when was this device last used" answer, and writing it per request would
    /// turn every read into a write.
    /// </summary>
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public DateTime? RevokedAt { get; set; }

    /// <summary>Why it ended, so the sessions screen can say more than "revoked".</summary>
    public string? RevokedReason { get; set; }

    /// <summary>Trimmed to something a person can recognise — "Chrome on Windows".</summary>
    public string? Device { get; set; }

    public string? IpAddress { get; set; }

    public User? User { get; set; }

    public bool IsLive(DateTime now) => RevokedAt is null && ExpiresAt > now;
}

/// <summary>The reasons a session ends other than by expiring.</summary>
public static class RevokeReasons
{
    public const string SignedOut = "Signed out";
    public const string SignedOutEverywhere = "Signed out of every device";
    public const string RevokedByAdmin = "Revoked by an administrator";
    public const string RoleChanged = "Role or access changed";
    public const string Deactivated = "Account deactivated";

    /// <summary>Left untouched for longer than the profile's idle limit.</summary>
    public const string IdleTooLong = "Idle for too long";
    public const string PasswordChanged = "Password changed";
    public const string CompanySuspended = "Company suspended";
    public const string SwitchedCompany = "Switched to another company";
}
