using System.Text.Json;
using System.Text.Json.Nodes;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace BullEvents.Api.Infrastructure;

/// <summary>
/// Removes fields the caller may not read from whatever the action was about to
/// return.
///
/// It works on the serialised shape rather than on the DTO type, which is the
/// only approach that survives this codebase: a lead comes back as a bare
/// object from one endpoint, inside <c>items</c> from the grid, and nested under
/// a quotation from a third. Walking the JSON reaches all three without every
/// DTO having to know it is being filtered.
///
/// Registered globally but costs nothing on the overwhelming majority of
/// requests: a caller with no field rules — which is everybody until somebody
/// writes one — is detected in a dictionary lookup and the response is passed
/// through untouched.
/// </summary>
public class FieldSecurityFilter : IAsyncResultFilter
{
    public async Task OnResultExecutionAsync(
        ResultExecutingContext context, ResultExecutionDelegate next)
    {
        if (context.Result is not ObjectResult result || result.Value is null)
        {
            await next();
            return;
        }

        var securedObject = SecuredObjectFor(context);
        if (securedObject is null)
        {
            await next();
            return;
        }

        var services = context.HttpContext.RequestServices;
        var tenant = services.GetRequiredService<TenantContext>();

        if (!tenant.IsResolved)
        {
            await next();
            return;
        }

        var permissions = await services.GetRequiredService<PermissionResolver>()
            .ResolveAsync(context.HttpContext.RequestAborted);

        var hidden = SecurableFields.For(securedObject)
            .Where(f => !permissions.CanReadField(securedObject, f.Name))
            .Select(f => f.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (hidden.Count == 0)
        {
            await next();
            return;
        }

        var json = JsonSerializer.SerializeToNode(
            result.Value,
            result.Value.GetType(),
            JsonOptions(context));

        if (json is not null)
        {
            Strip(json, hidden);
            result.Value = json;
            // The value is a JsonNode now, so the type hint the action declared
            // would send the serialiser looking for properties that are no
            // longer there.
            result.DeclaredType = typeof(JsonNode);
        }

        await next();
    }

    /// <summary>
    /// Which object this response is about, taken from the controller's own
    /// <see cref="SecuredByAttribute"/>. A controller that does not declare one
    /// has no field rules to apply.
    /// </summary>
    private static string? SecuredObjectFor(FilterContext context) =>
        context.ActionDescriptor is ControllerActionDescriptor descriptor
            ? descriptor.ControllerTypeInfo
                .GetCustomAttributes(typeof(SecuredByAttribute), inherit: true)
                .Cast<SecuredByAttribute>()
                .FirstOrDefault()?.SecuredObject
            : null;

    /// <summary>
    /// Removes the named properties wherever they appear, at any depth.
    ///
    /// Depth matters: the same lead is returned bare, inside a paged
    /// <c>items</c> array, and nested under a quotation. Stripping only the top
    /// level would leave the phone number visible on two of the three.
    /// </summary>
    private static void Strip(JsonNode node, HashSet<string> hidden)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var name in obj.Select(p => p.Key).Where(hidden.Contains).ToList())
                {
                    obj.Remove(name);
                }

                foreach (var child in obj.Select(p => p.Value).OfType<JsonNode>().ToList())
                {
                    Strip(child, hidden);
                }

                break;

            case JsonArray array:
                foreach (var child in array.OfType<JsonNode>().ToList())
                {
                    Strip(child, hidden);
                }

                break;
        }
    }

    /// <summary>
    /// The application's own serialiser options, so the intermediate JSON is
    /// camel-cased and UTC-stamped exactly as the response would have been.
    /// Serialising with the defaults instead would rename every property on the
    /// way through.
    /// </summary>
    private static JsonSerializerOptions JsonOptions(FilterContext context) =>
        context.HttpContext.RequestServices
            .GetRequiredService<Microsoft.Extensions.Options.IOptions<JsonOptions>>()
            .Value.JsonSerializerOptions;
}
