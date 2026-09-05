using System.Security.Claims;
using BullEvents.Api.Models;

namespace BullEvents.Api.Infrastructure;

/// <summary>
/// The ambient tenant/user for the current request, resolved once from the JWT
/// and injected wherever it's needed — most importantly into the DbContext,
/// which uses it to scope every query.
///
/// This is the piece that stands in for Postgres row-level security: instead of
/// trusting each controller to remember `.Where(x =&gt; x.CompanyId == …)`, the
/// filter is applied by the model itself and cannot be forgotten.
/// </summary>
public class TenantContext
{
    public int CompanyId { get; private set; }
    public int UserId { get; private set; }
    public string UserName { get; private set; } = "System";
    public string Role { get; private set; } = string.Empty;
    public IReadOnlyList<int> BranchIds { get; private set; } = [];
    public string? IpAddress { get; private set; }

    /// <summary>
    /// The <c>jti</c> of the token this request arrived on, and therefore the
    /// session it belongs to. Null for background work and for the anonymous
    /// endpoints.
    /// </summary>
    public Guid? TokenId { get; private set; }

    /// <summary>
    /// True once a real principal has been resolved. Background work (seeding,
    /// model training) runs without one, and skips the tenant filter.
    /// </summary>
    public bool IsResolved { get; private set; }

    public bool IsPlatformAdmin => Role == Roles.SuperAdmin;

    /// <summary>Roles that can see every branch in the company, not just their own.</summary>
    public bool SeesAllBranches =>
        Role is Roles.SuperAdmin or Roles.CompanyAdmin or Roles.AccountsFinance;

    public void Resolve(ClaimsPrincipal? principal, string? ipAddress)
    {
        IpAddress = ipAddress;

        if (principal?.Identity?.IsAuthenticated != true) return;

        CompanyId = ReadInt(principal, "company_id");
        UserId = ReadInt(principal, ClaimTypes.NameIdentifier, "sub", "user_id");
        UserName = principal.FindFirst("name")?.Value
            ?? principal.FindFirst(ClaimTypes.Name)?.Value
            ?? "Unknown";
        Role = principal.FindFirst("role")?.Value
            ?? principal.FindFirst(ClaimTypes.Role)?.Value
            ?? string.Empty;

        BranchIds = principal.FindAll("branch_id")
            .Select(c => int.TryParse(c.Value, out var id) ? id : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        TokenId = Guid.TryParse(principal.FindFirst("jti")?.Value, out var jti) ? jti : null;

        IsResolved = CompanyId > 0;
    }

    /// <summary>Runs a unit of work as a specific tenant — used by seeding and training.</summary>
    public void ResolveSystem(int companyId)
    {
        CompanyId = companyId;
        UserId = 0;
        UserName = "System";
        Role = Roles.SuperAdmin;
        IsResolved = companyId > 0;
    }

    private static int ReadInt(ClaimsPrincipal principal, params string[] types)
    {
        foreach (var type in types)
        {
            var raw = principal.FindFirst(type)?.Value;
            if (int.TryParse(raw, out var value)) return value;
        }

        return 0;
    }
}

/// <summary>Populates <see cref="TenantContext"/> at the start of every request.</summary>
public class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, TenantContext tenant)
    {
        tenant.Resolve(context.User, context.Connection.RemoteIpAddress?.ToString());
        await next(context);
    }
}
