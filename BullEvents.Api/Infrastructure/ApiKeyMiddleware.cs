using System.Security.Claims;
using System.Text.Json;
using BullEvents.Api.Models;
using BullEvents.Api.Services;

namespace BullEvents.Api.Infrastructure;

/// <summary>
/// Turns an <c>X-Api-Key</c> header into a signed-in principal for the public
/// API.
///
/// A middleware rather than an authentication scheme because the two
/// credentials serve different surfaces and must not be interchangeable: a
/// bearer token opens the whole CRM and a key opens <c>/api/v1</c> only. Doing
/// it here keeps that boundary in one readable place rather than spread across
/// a scheme, a policy and a set of attributes.
/// </summary>
public class ApiKeyMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Api-Key";

    /// <summary>The only path prefix a key can reach.</summary>
    private const string PublicApi = "/api/v1";

    /// <summary>Marks a principal as an integration rather than a person.</summary>
    public const string ApiKeyClaim = "api_key_id";

    public async Task InvokeAsync(HttpContext context, ApiKeyService keys)
    {
        var presented = context.Request.Headers[HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(presented))
        {
            // No key. If this was a public-API call it will be refused below by
            // the endpoint's own [Authorize]; anything else carries on as normal.
            await next(context);
            return;
        }

        if (!context.Request.Path.StartsWithSegments(PublicApi, StringComparison.OrdinalIgnoreCase))
        {
            await Refuse(context, StatusCodes.Status403Forbidden,
                $"An API key reaches {PublicApi} only. Use a signed-in session for the rest.");
            return;
        }

        var key = await keys.VerifyAsync(
            presented, context.Connection.RemoteIpAddress?.ToString(), context.RequestAborted);

        if (key is null)
        {
            await Refuse(context, StatusCodes.Status401Unauthorized,
                "That API key is not valid, has expired, or was revoked.");
            return;
        }

        var claims = new List<Claim>
        {
            new(ApiKeyClaim, key.Id.ToString()),
            new("sub", "0"),
            new("name", key.Name),
            new("company_id", key.CompanyId.ToString()),

            // Deliberately not a role from the catalogue. An integration is not
            // a seat, and giving it one would hand it every default that seat
            // carries rather than the scopes it was granted.
            new("role", "ApiKey"),
        };

        claims.AddRange(key.Scopes.Select(scope => new Claim("scope", scope)));

        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(claims, authenticationType: "ApiKey", nameType: "name", roleType: "role"));

        await next(context);
    }

    private static async Task Refuse(HttpContext context, int status, string message)
    {
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json";

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(new { message }), context.RequestAborted);
    }
}

/// <summary>
/// Gates a public-API action on one of the key's scopes.
///
/// The CRM's own permission matrix does not apply here: a key belongs to no
/// user, so there is no profile to resolve. What it holds is exactly what it
/// was granted, which is the point of issuing one.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireScopeAttribute(string scope)
    : Attribute, Microsoft.AspNetCore.Mvc.Filters.IAuthorizationFilter
{
    public string Scope { get; } = scope;

    public void OnAuthorization(Microsoft.AspNetCore.Mvc.Filters.AuthorizationFilterContext context)
    {
        if (context.Result is not null) return;

        var user = context.HttpContext.User;

        if (user.FindFirst(ApiKeyMiddleware.ApiKeyClaim) is null)
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult(new
            {
                message = $"Send an {ApiKeyMiddleware.HeaderName} header.",
            });
            return;
        }

        var held = user.FindAll("scope").Select(c => c.Value);

        if (held.Contains(Scope, StringComparer.OrdinalIgnoreCase)) return;

        context.Result = new Microsoft.AspNetCore.Mvc.ObjectResult(new
        {
            message = $"This key does not hold the {Scope} scope.",
        })
        {
            StatusCode = StatusCodes.Status403Forbidden,
        };
    }
}
