using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/* ------------------------------------------------------------------ *
 * DTOs
 * ------------------------------------------------------------------ */

public record ApiKeyDto(
    int Id,
    string Name,
    /// <summary>Enough of the key to recognise it, never enough to use it.</summary>
    string Prefix,
    string[] Scopes,
    string? CreatedByName,
    DateTime CreatedAt,
    DateTime? ExpiresAt,
    DateTime? LastUsedAt,
    string? LastUsedIp,
    long CallCount,
    DateTime? RevokedAt,
    bool IsLive);

/// <summary>
/// The one response that carries the secret.
///
/// Returned exactly once, at creation. Nothing else in the API can produce it
/// again, because the server only kept a hash.
/// </summary>
public record MintedApiKeyDto(ApiKeyDto Key, string Secret);

public record CreateApiKeyRequest(
    [Required, MinLength(2)] string Name,
    [Required] string[] Scopes,
    DateTime? ExpiresAt);

public record ScopeDto(string Scope, string Label, string Description);

public record WebhookEndpointDto(
    int Id,
    string Name,
    string Url,
    string[] Events,
    bool IsActive,
    DateTime CreatedAt,
    int ConsecutiveFailures,
    DateTime? DisabledAt,
    string? DisabledReason,
    /// <summary>Shown once on creation so the receiver can be configured.</summary>
    string? Secret);

public record SaveWebhookRequest(
    [Required, MinLength(2)] string Name,
    [Required, Url] string Url,
    [Required] string[] Events,
    bool IsActive);

public record WebhookEventDto(string Event, string Description);

public record DeliveryDto(
    int Id,
    int EndpointId,
    string EndpointName,
    string Event,
    string Status,
    int Attempts,
    DateTime CreatedAt,
    DateTime? LastAttemptAt,
    DateTime? NextAttemptAt,
    int? ResponseCode,
    string? Error);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The integration console: the keys other systems authenticate with, the
/// endpoints this company wants told about things, and whether those deliveries
/// are actually landing.
/// </summary>
[ApiController]
[Route("api/admin/integrations")]
[Authorize]
[RequirePermission(SecuredObjects.User, ObjectAction.ModifyAll)]
public class IntegrationsController(
    AppDbContext db,
    ApiKeyService keys,
    EntitlementService entitlements,
    IHostEnvironment environment,
    TenantContext tenant) : ControllerBase
{
    /* ---------------- keys ---------------- */

    [HttpGet("scopes")]
    public ActionResult<IReadOnlyList<ScopeDto>> Scopes() =>
        Ok(ApiScopes.All.Select(s => new ScopeDto(s.Scope, s.Label, s.Description)).ToList());

    [HttpGet("keys")]
    public async Task<ActionResult<IReadOnlyList<ApiKeyDto>>> Keys(CancellationToken ct)
    {
        var rows = await db.ApiKeys.OrderByDescending(k => k.CreatedAt).ToListAsync(ct);
        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpPost("keys")]
    public async Task<ActionResult<MintedApiKeyDto>> CreateKey(
        CreateApiKeyRequest request, CancellationToken ct)
    {
        var plan = (await entitlements.ResolveAsync(ct: ct)).Plan;

        if (!plan.ApiAccess)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = $"API access is not part of the {plan.Name} plan.",
            });
        }

        var scopes = request.Scopes.Where(ApiScopes.Exists).ToList();

        if (scopes.Count == 0)
        {
            return BadRequest(new
            {
                message = "A key with no scopes can do nothing. Grant at least one.",
            });
        }

        var minted = await keys.CreateAsync(
            tenant.CompanyId, request.Name, scopes, request.ExpiresAt,
            tenant.UserId, tenant.UserName, ct);

        return Ok(new MintedApiKeyDto(ToDto(minted.Key), minted.Secret));
    }

    /// <summary>
    /// Withdraws a key. Not a delete: the row is what lets somebody answer
    /// "what was this key doing before we turned it off".
    /// </summary>
    [HttpPost("keys/{id:int}/revoke")]
    public async Task<ActionResult<ApiKeyDto>> RevokeKey(int id, CancellationToken ct)
    {
        var key = await db.ApiKeys.FirstOrDefaultAsync(k => k.Id == id, ct);
        if (key is null) return NotFound(new { message = "That key no longer exists." });

        key.RevokedAt ??= DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(key));
    }

    /* ---------------- webhooks ---------------- */

    [HttpGet("events")]
    public ActionResult<IReadOnlyList<WebhookEventDto>> Events() =>
        Ok(WebhookEvents.All.Select(e => new WebhookEventDto(e.Event, e.Description)).ToList());

    [HttpGet("webhooks")]
    public async Task<ActionResult<IReadOnlyList<WebhookEndpointDto>>> Webhooks(CancellationToken ct)
    {
        var rows = await db.WebhookEndpoints.OrderBy(e => e.Id).ToListAsync(ct);

        // The secret is withheld on the list. It is shown once, at creation,
        // and a screen that reprints it turns every shoulder into a leak.
        return Ok(rows.Select(e => ToDto(e, secret: null)).ToList());
    }

    [HttpPost("webhooks")]
    public async Task<ActionResult<WebhookEndpointDto>> CreateWebhook(
        SaveWebhookRequest request, CancellationToken ct)
    {
        var plan = (await entitlements.ResolveAsync(ct: ct)).Plan;

        if (!plan.Webhooks)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = $"Webhooks are not part of the {plan.Name} plan.",
            });
        }

        var refused = CheckUrl(request.Url, environment);
        if (refused is not null) return refused;

        var events = request.Events.Where(WebhookEvents.Exists).ToList();

        if (events.Count == 0)
        {
            return BadRequest(new { message = "Choose at least one event to send." });
        }

        var secret = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();

        var endpoint = new WebhookEndpoint
        {
            CompanyId = tenant.CompanyId,
            Name = request.Name.Trim(),
            Url = request.Url.Trim(),
            Secret = secret,
            EventsCsv = string.Join(',', events),
            IsActive = request.IsActive,
        };

        db.WebhookEndpoints.Add(endpoint);
        await db.SaveChangesAsync(ct);

        return Ok(ToDto(endpoint, secret));
    }

    [HttpPut("webhooks/{id:int}")]
    public async Task<ActionResult<WebhookEndpointDto>> UpdateWebhook(
        int id, SaveWebhookRequest request, CancellationToken ct)
    {
        var endpoint = await db.WebhookEndpoints.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (endpoint is null) return NotFound(new { message = "That endpoint no longer exists." });

        var refused = CheckUrl(request.Url, environment);
        if (refused is not null) return refused;

        endpoint.Name = request.Name.Trim();
        endpoint.Url = request.Url.Trim();
        endpoint.EventsCsv = string.Join(',', request.Events.Where(WebhookEvents.Exists));

        // Switching an endpoint back on clears the failure count, so the
        // auto-disable threshold measures the run since somebody last looked
        // rather than since the beginning of time.
        if (request.IsActive && !endpoint.IsActive)
        {
            endpoint.ConsecutiveFailures = 0;
            endpoint.DisabledAt = null;
            endpoint.DisabledReason = null;
        }

        endpoint.IsActive = request.IsActive;

        await db.SaveChangesAsync(ct);

        return Ok(ToDto(endpoint, secret: null));
    }

    [HttpDelete("webhooks/{id:int}")]
    public async Task<IActionResult> DeleteWebhook(int id, CancellationToken ct)
    {
        var endpoint = await db.WebhookEndpoints.FirstOrDefaultAsync(e => e.Id == id, ct);
        if (endpoint is null) return NotFound(new { message = "That endpoint no longer exists." });

        db.WebhookEndpoints.Remove(endpoint);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>The delivery log — what was sent, what came back, and what is still queued.</summary>
    [HttpGet("deliveries")]
    public async Task<ActionResult<IReadOnlyList<DeliveryDto>>> Deliveries(
        [FromQuery] int? endpointId = null,
        [FromQuery] string? status = null,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var query = db.WebhookDeliveries.Include(d => d.Endpoint).AsQueryable();

        if (endpointId is not null) query = query.Where(d => d.EndpointId == endpointId);

        if (Enum.TryParse<DeliveryStatus>(status, true, out var parsed))
        {
            query = query.Where(d => d.Status == parsed);
        }

        var rows = await query
            .OrderByDescending(d => d.CreatedAt)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

        return Ok(rows
            .Select(d => new DeliveryDto(
                d.Id, d.EndpointId, d.Endpoint?.Name ?? "Removed", d.Event,
                d.Status.ToString(), d.Attempts, d.CreatedAt, d.LastAttemptAt,
                d.NextAttemptAt, d.ResponseCode, d.Error))
            .ToList());
    }

    /// <summary>
    /// Puts a failed delivery back in the queue.
    ///
    /// The payload is the one that was captured, not a rebuilt copy: replaying
    /// what the receiver should have got is the point, and regenerating it would
    /// send today's version of a record that has since changed.
    /// </summary>
    [HttpPost("deliveries/{id:int}/replay")]
    public async Task<ActionResult<DeliveryDto>> Replay(int id, CancellationToken ct)
    {
        var delivery = await db.WebhookDeliveries
            .Include(d => d.Endpoint)
            .FirstOrDefaultAsync(d => d.Id == id, ct);

        if (delivery is null) return NotFound(new { message = "That delivery no longer exists." });

        delivery.Status = DeliveryStatus.Pending;
        delivery.Attempts = 0;
        delivery.NextAttemptAt = DateTime.UtcNow;
        delivery.Error = null;
        delivery.ResponseCode = null;

        await db.SaveChangesAsync(ct);

        return Ok(new DeliveryDto(
            delivery.Id, delivery.EndpointId, delivery.Endpoint?.Name ?? "Removed",
            delivery.Event, delivery.Status.ToString(), delivery.Attempts,
            delivery.CreatedAt, delivery.LastAttemptAt, delivery.NextAttemptAt,
            delivery.ResponseCode, delivery.Error));
    }

    /* ---------------- helpers ---------------- */

    private static ApiKeyDto ToDto(ApiKey k) => new(
        k.Id, k.Name, k.Prefix, k.Scopes, k.CreatedByName, k.CreatedAt,
        k.ExpiresAt, k.LastUsedAt, k.LastUsedIp, k.CallCount, k.RevokedAt,
        k.IsLive(DateTime.UtcNow));

    private static WebhookEndpointDto ToDto(WebhookEndpoint e, string? secret) => new(
        e.Id, e.Name, e.Url, e.Events, e.IsActive, e.CreatedAt,
        e.ConsecutiveFailures, e.DisabledAt, e.DisabledReason, secret);

    /// <summary>
    /// Refuses an endpoint the server should not be made to call.
    ///
    /// A webhook URL is somewhere this process will connect to on a schedule,
    /// with whatever network position it holds. Left unchecked that is a
    /// server-side request forgery with a retry ladder attached — loopback and
    /// the private ranges are exactly where a cloud metadata service lives.
    /// </summary>
    private static ActionResult? CheckUrl(string value, IHostEnvironment environment)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            return new BadRequestObjectResult(new { message = "That is not a valid URL." });
        }

        // Nobody can build a webhook integration against an endpoint they are
        // not allowed to point at their own machine, so development lets both
        // rules go. Production keeps them: this is a request the server makes on
        // a schedule from wherever it sits, which is a forgery with a retry
        // ladder attached if the address is not checked.
        if (environment.IsDevelopment() && (uri.IsLoopback || IsPrivate(uri.DnsSafeHost)))
        {
            return null;
        }

        if (uri.Scheme != Uri.UriSchemeHttps)
        {
            return new BadRequestObjectResult(new
            {
                message = "A webhook URL must be https. The payload carries customer data.",
            });
        }

        var host = uri.DnsSafeHost;

        if (uri.IsLoopback
            || host.Equals("metadata.google.internal", StringComparison.OrdinalIgnoreCase)
            || IsPrivate(host))
        {
            return new BadRequestObjectResult(new
            {
                message = "That address is inside a private network and cannot be called from here.",
            });
        }

        return null;
    }

    private static bool IsPrivate(string host)
    {
        if (!System.Net.IPAddress.TryParse(host, out var ip)) return false;

        var bytes = ip.GetAddressBytes();

        return bytes.Length == 4
            && (bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || (bytes[0] == 169 && bytes[1] == 254));
    }
}
