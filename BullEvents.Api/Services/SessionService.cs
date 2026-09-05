using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BullEvents.Api.Services;

/// <summary>
/// Opens, checks and withdraws sessions.
///
/// The check runs on every authenticated request, so it is answered from memory
/// for a short window rather than from the database. That window is the only
/// real cost of the design: a revoke takes up to <see cref="LiveWindow"/> to
/// bite on a node that has already cached the answer. Kept short deliberately —
/// an administrator who has just deactivated somebody should not have to
/// explain a delay measured in minutes.
/// </summary>
public class SessionService(AppDbContext db, IMemoryCache cache)
{
    /// <summary>How long a "this session is live" answer is trusted without re-reading.</summary>
    public static readonly TimeSpan LiveWindow = TimeSpan.FromSeconds(30);

    /// <summary>How stale a <c>LastSeenAt</c> may get before it is worth a write.</summary>
    private static readonly TimeSpan SeenWriteInterval = TimeSpan.FromMinutes(1);

    private static string Key(Guid tokenId) => $"session:{tokenId}";

    /// <summary>
    /// Records a newly issued token. The caller has already minted it — this
    /// stores the row the <c>jti</c> points at.
    /// </summary>
    public async Task OpenAsync(
        Guid tokenId,
        User user,
        int companyId,
        DateTime expiresAt,
        string? device,
        string? ipAddress,
        CancellationToken ct = default)
    {
        db.UserSessions.Add(new UserSession
        {
            TokenId = tokenId,
            UserId = user.Id,
            CompanyId = companyId,
            ExpiresAt = expiresAt,
            Device = Trim(device, 200),
            IpAddress = Trim(ipAddress, 64),
        });

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Whether this token still stands, and a nudge to <c>LastSeenAt</c> when it
    /// has gone stale.
    ///
    /// Filters are ignored on purpose: this runs while the request is deciding
    /// which tenant it belongs to, so a tenant-scoped read here would be
    /// circular.
    /// </summary>
    public async Task<bool> IsLiveAsync(Guid tokenId, CancellationToken ct = default)
    {
        if (cache.TryGetValue<bool>(Key(tokenId), out var cached)) return cached;

        var now = DateTime.UtcNow;

        var session = await db.UserSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TokenId == tokenId, ct);

        // A token whose row is missing predates the session table, or points at
        // a row somebody deleted. Refusing it is the safe answer and the
        // migration below closes the gap by revoking nothing — old tokens simply
        // stop working, which is the same thing a key rotation does.
        var live = session is not null && session.IsLive(now);

        // The profile's idle limit, if one is set. Enforced here rather than at
        // sign-in because it is a property of the session, not of the door: the
        // question is how long this token has been sitting untouched, and the
        // only place that is known is the moment it is used again.
        //
        // The row is revoked rather than merely refused, so the session shows as
        // ended on the login-history screen with a reason somebody can read,
        // instead of silently failing every request from then on.
        if (live && await IdleLimitAsync(session!, ct) is int limit
            && now - session!.LastSeenAt > TimeSpan.FromMinutes(limit))
        {
            session.RevokedAt = now;
            session.RevokedReason = RevokeReasons.IdleTooLong;
            await db.SaveChangesAsync(ct);

            cache.Set(Key(tokenId), false, LiveWindow);
            return false;
        }

        if (live && now - session!.LastSeenAt > SeenWriteInterval)
        {
            session.LastSeenAt = now;
            await db.SaveChangesAsync(ct);
        }

        cache.Set(Key(tokenId), live, LiveWindow);
        return live;
    }

    /// <summary>
    /// The idle limit that applies to a session, from its holder's profile.
    ///
    /// Resolved through the user rather than stored on the session, so changing
    /// a profile's policy takes effect on sessions that are already open — which
    /// is the whole point of setting one.
    /// </summary>
    private async Task<int?> IdleLimitAsync(UserSession session, CancellationToken ct)
    {
        var role = await db.Users
            .IgnoreQueryFilters()
            .Where(u => u.Id == session.UserId)
            .Select(u => u.Role)
            .FirstOrDefaultAsync(ct);

        if (role is null || role == Roles.SuperAdmin) return null;

        return await db.LoginPolicies
            .IgnoreQueryFilters()
            .Include(p => p.PermissionSet)
            .Where(p => p.IsActive && p.PermissionSet != null && p.PermissionSet.RoleKey == role)
            .Select(p => p.IdleTimeoutMinutes)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>Ends one session.</summary>
    public async Task<bool> RevokeAsync(Guid tokenId, string reason, CancellationToken ct = default)
    {
        var session = await db.UserSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TokenId == tokenId && s.RevokedAt == null, ct);

        if (session is null) return false;

        session.RevokedAt = DateTime.UtcNow;
        session.RevokedReason = reason;

        await db.SaveChangesAsync(ct);
        Forget(tokenId);

        return true;
    }

    /// <summary>
    /// Ends every live session for a person — what deactivation, a role change
    /// and a password change all need.
    /// </summary>
    public async Task<int> RevokeAllForUserAsync(
        int userId, string reason, Guid? except = null, CancellationToken ct = default)
    {
        var sessions = await db.UserSessions
            .IgnoreQueryFilters()
            .Where(s => s.UserId == userId && s.RevokedAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var ended = 0;

        foreach (var session in sessions)
        {
            if (except is Guid keep && session.TokenId == keep) continue;

            session.RevokedAt = now;
            session.RevokedReason = reason;
            Forget(session.TokenId);
            ended++;
        }

        if (ended > 0) await db.SaveChangesAsync(ct);
        return ended;
    }

    /// <summary>
    /// Ends every live session inside one tenant — what suspending a company
    /// means.
    ///
    /// <paramref name="except"/> spares the session doing the suspending: it is
    /// opened against this same tenant, and ending it would leave nobody able to
    /// lift the suspension again.
    /// </summary>
    public async Task<int> RevokeAllForCompanyAsync(
        int companyId, string reason, Guid? except = null, CancellationToken ct = default)
    {
        var sessions = await db.UserSessions
            .IgnoreQueryFilters()
            .Where(s => s.CompanyId == companyId && s.RevokedAt == null)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var ended = 0;

        foreach (var session in sessions)
        {
            if (except is Guid keep && session.TokenId == keep) continue;

            session.RevokedAt = now;
            session.RevokedReason = reason;
            Forget(session.TokenId);
            ended++;
        }

        if (ended > 0) await db.SaveChangesAsync(ct);
        return ended;
    }

    /// <summary>
    /// Drops the cached verdict so a revoke bites on the next request rather
    /// than at the end of the window — on this node, at least.
    /// </summary>
    private void Forget(Guid tokenId) => cache.Remove(Key(tokenId));

    private static string? Trim(string? value, int max) =>
        string.IsNullOrWhiteSpace(value)
            ? null
            : value.Length <= max ? value : value[..max];

    /// <summary>
    /// Turns a user-agent string into something a person recognises in a list of
    /// their own devices. Deliberately crude — the alternative is a parsing
    /// library maintained against an arms race of spoofed agent strings, for a
    /// label.
    /// </summary>
    public static string Describe(string? userAgent)
    {
        if (string.IsNullOrWhiteSpace(userAgent)) return "Unknown device";

        var browser =
            userAgent.Contains("Edg/", StringComparison.OrdinalIgnoreCase) ? "Edge"
            : userAgent.Contains("OPR/", StringComparison.OrdinalIgnoreCase) ? "Opera"
            : userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase) ? "Chrome"
            : userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase) ? "Firefox"
            : userAgent.Contains("Safari/", StringComparison.OrdinalIgnoreCase) ? "Safari"
            : "Browser";

        var platform =
            userAgent.Contains("Android", StringComparison.OrdinalIgnoreCase) ? "Android"
            : userAgent.Contains("iPhone", StringComparison.OrdinalIgnoreCase) ? "iPhone"
            : userAgent.Contains("iPad", StringComparison.OrdinalIgnoreCase) ? "iPad"
            : userAgent.Contains("Mac OS", StringComparison.OrdinalIgnoreCase) ? "macOS"
            : userAgent.Contains("Windows", StringComparison.OrdinalIgnoreCase) ? "Windows"
            : userAgent.Contains("Linux", StringComparison.OrdinalIgnoreCase) ? "Linux"
            : "Unknown";

        return $"{browser} on {platform}";
    }
}
