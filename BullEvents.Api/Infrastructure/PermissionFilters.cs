using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BullEvents.Api.Infrastructure;

/* ------------------------------------------------------------------ *
 * Why these exist
 *
 * Authorisation used to be a hand-maintained list of role names in an
 * [Authorize] attribute on each action. Two things were wrong with that. The
 * permission matrix a company edits in the admin console changed nothing, so
 * the screen described an intention rather than a rule. And the lists drifted:
 * several still named roles that had been renamed, which authorised nobody.
 *
 * These filters put the stored matrix in the request path. What a company ticks
 * is now what the API enforces.
 * ------------------------------------------------------------------ */

/// <summary>
/// Declares the object a controller works, and gates every action on it.
///
/// The verb is inferred from the HTTP method — GET reads, POST creates, PUT and
/// PATCH edit, DELETE deletes — because that is what the methods mean, and
/// spelling it out on two hundred actions would be two hundred chances to spell
/// it wrong. Where an action does not follow the convention, and a great many
/// do not (<c>POST /query</c> is a read; <c>POST /{id}/approve</c> is an edit),
/// <see cref="PermissionActionAttribute"/> on the method says so and wins.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SecuredByAttribute(string securedObject) : Attribute, IAsyncAuthorizationFilter
{
    public string SecuredObject { get; } = securedObject;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.Result is not null) return;

        var action = ActionFor(context);

        // None is the explicit opt-out — an action inside a secured controller
        // that carries its own gate, or none at all.
        if (action == ObjectAction.None) return;

        await PermissionCheck.EnforceAsync(context, SecuredObject, action);
    }

    private ObjectAction ActionFor(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is ControllerActionDescriptor descriptor)
        {
            var declared = descriptor.MethodInfo
                .GetCustomAttributes(typeof(PermissionActionAttribute), inherit: false)
                .Cast<PermissionActionAttribute>()
                .FirstOrDefault();

            if (declared is not null) return declared.Action;
        }

        return context.HttpContext.Request.Method.ToUpperInvariant() switch
        {
            "GET" or "HEAD" or "OPTIONS" => ObjectAction.View,
            "POST" => ObjectAction.Create,
            "PUT" or "PATCH" => ObjectAction.Edit,
            "DELETE" => ObjectAction.Delete,
            _ => ObjectAction.View,
        };
    }
}

/// <summary>
/// Overrides the verb <see cref="SecuredByAttribute"/> would have inferred for
/// one action. <see cref="ObjectAction.None"/> opts the action out of the
/// controller's gate entirely.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = true)]
public sealed class PermissionActionAttribute(ObjectAction action) : Attribute
{
    public ObjectAction Action { get; } = action;
}

/// <summary>
/// Gates one action on a permission that is not the controller's own object —
/// the auto-assign endpoint on the intelligence controller writes leads, not
/// reports.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequirePermissionAttribute(string securedObject, ObjectAction action)
    : Attribute, IAsyncAuthorizationFilter
{
    public string SecuredObject { get; } = securedObject;
    public ObjectAction Action { get; } = action;

    public Task OnAuthorizationAsync(AuthorizationFilterContext context) =>
        context.Result is not null
            ? Task.CompletedTask
            : PermissionCheck.EnforceAsync(context, SecuredObject, Action);
}

/// <summary>
/// Gates an action on whether the caller's seat opens a module at all.
///
/// A different question from the object permissions: not "may they edit a lead"
/// but "is this part of the product theirs to open". The calendar and
/// scheduling endpoints have no secured object of their own, and this is what
/// they gate on.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public sealed class RequireModuleAttribute(string module) : Attribute, IAsyncAuthorizationFilter
{
    public string Module { get; } = module;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.Result is not null) return;

        var permissions = await PermissionCheck.ResolveAsync(context);
        if (permissions is null || permissions.HasModule(Module)) return;

        context.Result = PermissionCheck.Forbid(
            $"The {Module} module is not switched on for your account. "
            + "An administrator can turn it on from the Users screen.");
    }
}

/// <summary>
/// Restricts an action to platform operators.
///
/// Kept out of the matrix on purpose: a platform seat is not a permission
/// question, and resolving it through the same table would mean a misconfigured
/// row could lock out the person whose job is fixing misconfigured rows.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public sealed class RequirePlatformAdminAttribute : Attribute, IAsyncAuthorizationFilter
{
    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.Result is not null) return Task.CompletedTask;

        var tenant = context.HttpContext.RequestServices.GetRequiredService<TenantContext>();

        if (!tenant.IsResolved)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Sign in to continue." });
        }
        else if (!tenant.IsPlatformAdmin)
        {
            context.Result = PermissionCheck.Forbid("This is a platform operator action.");
        }

        return Task.CompletedTask;
    }
}

/* ------------------------------------------------------------------ *
 * Shared machinery
 * ------------------------------------------------------------------ */

internal static class PermissionCheck
{
    internal static async Task<EffectivePermissions?> ResolveAsync(AuthorizationFilterContext context)
    {
        var services = context.HttpContext.RequestServices;
        var tenant = services.GetRequiredService<TenantContext>();

        if (!tenant.IsResolved)
        {
            context.Result = new UnauthorizedObjectResult(new { message = "Sign in to continue." });
            return null;
        }

        // Cached on the resolver for the life of the request, so a controller
        // carrying a class filter and a method filter reads the model once.
        var resolver = services.GetRequiredService<PermissionResolver>();
        return await resolver.ResolveAsync(context.HttpContext.RequestAborted);
    }

    internal static async Task EnforceAsync(
        AuthorizationFilterContext context, string securedObject, ObjectAction action)
    {
        var permissions = await ResolveAsync(context);
        if (permissions is null || permissions.Can(securedObject, action)) return;

        context.Result = Forbid(Explain(securedObject, action));
    }

    internal static ObjectResult Forbid(string message) =>
        new(new { message }) { StatusCode = StatusCodes.Status403Forbidden };

    /// <summary>
    /// Says which permission was missing.
    ///
    /// A bare 403 sends the user to their administrator, who then has to guess
    /// which of sixteen objects and six verbs it was. Naming it costs nothing —
    /// the caller already knows what they tried to do.
    /// </summary>
    private static string Explain(string securedObject, ObjectAction action)
    {
        var verb = action switch
        {
            ObjectAction.View => "view",
            ObjectAction.Create => "create",
            ObjectAction.Edit => "edit",
            ObjectAction.Delete => "delete",
            ObjectAction.ViewAll => "view every one of the",
            ObjectAction.ModifyAll => "act on other people's",
            _ => "act on",
        };

        return $"Your role cannot {verb} {Plural(securedObject)}. "
             + "An administrator can change this under Roles & access.";
    }

    private static string Plural(string securedObject) => securedObject switch
    {
        SecuredObjects.Opportunity => "opportunities",
        SecuredObjects.Company => "companies",
        SecuredObjects.SiteVisit => "site visits",
        SecuredObjects.ObmVisit => "OBM visits",
        SecuredObjects.FollowUp => "follow-ups",
        _ => $"{securedObject.ToLowerInvariant()}s",
    };
}
