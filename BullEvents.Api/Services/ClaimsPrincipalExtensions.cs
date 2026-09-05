using System.Security.Claims;

namespace BullEvents.Api.Services;

public static class ClaimsPrincipalExtensions
{
    public static int GetCompanyId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst("company_id")?.Value
            ?? throw new InvalidOperationException("company_id claim is missing.");
        return int.Parse(value);
    }

    public static int GetUserId(this ClaimsPrincipal user)
    {
        var value = user.FindFirst("sub")?.Value
            ?? throw new InvalidOperationException("sub claim is missing.");
        return int.Parse(value);
    }
}
