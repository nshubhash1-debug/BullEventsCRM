using System.Net;
using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Enforces the sign-in window and address restrictions an administrator set on
/// a profile.
///
/// Checked at the door rather than on every request: a policy says who may
/// <em>start</em> a session, and re-testing the clock on every call would sign
/// somebody out mid-sentence at seven o'clock. The one exception is the idle
/// timeout, which is a property of the session and belongs with the session
/// guard.
///
/// Platform operators are exempt throughout. A time-of-day rule that locks the
/// person who has to lift it out of the system is a rule that cannot be undone,
/// and this codebase has already been bitten once by exactly that with tenant
/// suspension.
/// </summary>
public class LoginPolicyGuard(AppDbContext db, ILogger<LoginPolicyGuard> logger)
{
    /// <summary>Why a sign-in was refused, or null when it is allowed.</summary>
    public async Task<string?> RefuseAsync(
        User user, IPAddress? address, CancellationToken ct = default)
    {
        if (user.Role == Roles.SuperAdmin) return null;

        var policy = await db.LoginPolicies
            .Include(p => p.PermissionSet)
            .Where(p => p.IsActive && p.PermissionSet != null
                && p.PermissionSet.RoleKey == user.Role)
            .FirstOrDefaultAsync(ct);

        if (policy is null) return null;

        /* ---------------- when ---------------- */

        // Local time, not UTC: "ten to seven" means ten to seven where the person
        // is sitting, and a policy written in one timezone and enforced in
        // another is a policy nobody can reason about.
        var now = DateTime.Now;

        if ((policy.AllowedDays & (1 << (int)now.DayOfWeek)) == 0)
        {
            return $"Sign-in is not allowed on a {now.DayOfWeek} for this role.";
        }

        var minute = (now.Hour * 60) + now.Minute;

        if (policy.LoginFromMinute is int from && policy.LoginToMinute is int to)
        {
            // A window that wraps past midnight is read as two halves, so a night
            // shift of 22:00–06:00 does not collapse into "never".
            var inside = from <= to
                ? minute >= from && minute <= to
                : minute >= from || minute <= to;

            if (!inside)
            {
                return $"Sign-in for this role is allowed between {Clock(from)} and {Clock(to)}.";
            }
        }

        /* ---------------- where ---------------- */

        if (!string.IsNullOrWhiteSpace(policy.AllowedIpRanges))
        {
            if (address is null) return "Sign-in is restricted to specific addresses.";

            var allowed = policy.AllowedIpRanges
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (!allowed.Any(range => Matches(address, range)))
            {
                logger.LogWarning(
                    "Refused sign-in for {Email} from {Address}: outside the allowed ranges.",
                    user.Email, address);

                return "Sign-in is not allowed from this network.";
            }
        }

        return null;
    }

    /// <summary>The idle limit for a role, or null when the token's own life governs.</summary>
    public async Task<int?> IdleTimeoutAsync(string role, CancellationToken ct = default)
    {
        if (role == Roles.SuperAdmin) return null;

        return await db.LoginPolicies
            .Include(p => p.PermissionSet)
            .Where(p => p.IsActive && p.PermissionSet != null && p.PermissionSet.RoleKey == role)
            .Select(p => p.IdleTimeoutMinutes)
            .FirstOrDefaultAsync(ct);
    }

    private static string Clock(int minutes) =>
        $"{minutes / 60:D2}:{minutes % 60:D2}";

    /// <summary>
    /// Whether an address falls inside a CIDR range, or equals a bare address.
    ///
    /// Both forms are accepted because administrators write both, and refusing
    /// "203.0.113.4" for want of a "/32" is the kind of pedantry that gets the
    /// whole feature switched off.
    /// </summary>
    private static bool Matches(IPAddress address, string range)
    {
        try
        {
            if (!range.Contains('/'))
            {
                return IPAddress.TryParse(range, out var single)
                    && single.Equals(address);
            }

            var parts = range.Split('/', 2);
            if (!IPAddress.TryParse(parts[0], out var network)) return false;
            if (!int.TryParse(parts[1], out var prefix)) return false;

            var networkBytes = network.GetAddressBytes();
            var addressBytes = address.GetAddressBytes();

            // Different families never match — comparing an IPv4 address against
            // an IPv6 range byte by byte would otherwise read whatever happened
            // to be there.
            if (networkBytes.Length != addressBytes.Length) return false;
            if (prefix < 0 || prefix > networkBytes.Length * 8) return false;

            var whole = prefix / 8;
            var remainder = prefix % 8;

            for (var i = 0; i < whole; i++)
            {
                if (networkBytes[i] != addressBytes[i]) return false;
            }

            if (remainder == 0) return true;

            var mask = (byte)(0xFF << (8 - remainder));
            return (networkBytes[whole] & mask) == (addressBytes[whole] & mask);
        }
        catch
        {
            // A malformed range must not let everybody in, nor take sign-in down.
            return false;
        }
    }
}
