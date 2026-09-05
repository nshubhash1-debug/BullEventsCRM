using System.Text.Json;
using BullEvents.Api.Data;
using BullEvents.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BullEvents.Api.Infrastructure;

/// <summary>
/// The two checks a signed token cannot make on its own: is this session still
/// wanted, and is the tenant behind it still allowed to trade.
///
/// Both used to be answered only at sign-in, which meant deactivating somebody
/// or suspending a company did nothing until their token expired — up to twelve
/// hours of access that everybody involved believed had already been taken
/// away. Neither check touches the database on most requests; both are answered
/// from a short-lived cache.
/// </summary>
public class SessionGuardMiddleware(RequestDelegate next)
{
    /// <summary>
    /// How long a company's trading status is trusted without re-reading. Longer
    /// than the session window because a suspension is a deliberate, rare act
    /// and the revoke that accompanies it does the immediate work.
    /// </summary>
    private static readonly TimeSpan StatusWindow = TimeSpan.FromMinutes(2);

    public async Task InvokeAsync(
        HttpContext context,
        TenantContext tenant,
        SessionService sessions,
        AppDbContext db,
        IMemoryCache cache)
    {
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context);
            return;
        }

        // An API key is not a session. It carries its own lifecycle — scopes,
        // an expiry, a revoke — checked when the key was resolved, and it has no
        // jti to look up here.
        if (context.User.FindFirst(ApiKeyMiddleware.ApiKeyClaim) is not null)
        {
            await next(context);
            return;
        }

        if (tenant.TokenId is not Guid tokenId)
        {
            // A token minted before sessions existed. It verifies, but there is
            // no row saying it is still wanted, and treating "no record" as
            // "still valid" would leave exactly the tokens this exists to stop.
            await Refuse(context, StatusCodes.Status401Unauthorized,
                "Your session has ended. Please sign in again.");
            return;
        }

        if (!await sessions.IsLiveAsync(tokenId, context.RequestAborted))
        {
            await Refuse(context, StatusCodes.Status401Unauthorized,
                "This session was signed out. Please sign in again.");
            return;
        }

        // The operator who suspended a tenant still has to be able to open it,
        // or a suspension is a one-way door for the platform as well as the
        // customer.
        if (tenant.IsResolved
            && !tenant.IsPlatformAdmin
            && !await IsTradingAsync(db, cache, tenant.CompanyId, context.RequestAborted))
        {
            // 403 rather than 401: the credentials are fine and signing in again
            // will not help, which is exactly what the two codes are for.
            await Refuse(context, StatusCodes.Status403Forbidden,
                "This workspace is suspended. Contact your administrator.");
            return;
        }

        await next(context);
    }

    private static async Task<bool> IsTradingAsync(
        AppDbContext db, IMemoryCache cache, int companyId, CancellationToken ct)
    {
        var key = $"company-status:{companyId}";
        if (cache.TryGetValue<bool>(key, out var cached)) return cached;

        var status = await db.Companies
            .IgnoreQueryFilters()
            .Where(c => c.Id == companyId)
            .Select(c => c.Status)
            .FirstOrDefaultAsync(ct);

        // Trial is a paying-in-future state, not a stopped one — only an explicit
        // suspension closes the door. A company row that has gone missing is not
        // trading either.
        var trading = status is not null
            && !string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase);

        cache.Set(key, trading, StatusWindow);
        return trading;
    }

    /// <summary>Drops the cached verdict so a status change bites immediately.</summary>
    public static void ForgetCompany(IMemoryCache cache, int companyId) =>
        cache.Remove($"company-status:{companyId}");

    private static async Task Refuse(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { message }), context.RequestAborted);
    }
}
