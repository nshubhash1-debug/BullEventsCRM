using System.Text.Json;
using System.Text.Json.Nodes;
using BullEvents.Api.Data;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Where the outside world delivers.
///
/// Anonymous by necessity — a portal dashboard offers one URL field and no way
/// to set a header, so the token in the path is the credential. That makes this
/// the most exposed surface in the product, and it is written accordingly:
/// nothing here trusts the body, every delivery is recorded whatever happens to
/// it, and the response never says more than the sender needs.
///
/// One endpoint serves every provider. What differs between 99acres and a
/// website form is the shape of the JSON, and <see cref="InboundLeadMapper"/>
/// absorbs that — so adding a portal is a row in the catalogue rather than
/// another controller.
/// </summary>
[ApiController]
[Route("api/connect")]
[EnableRateLimiting("auth")]
public class ConnectInboundController(
    AppDbContext db,
    EntitlementService entitlements,
    WebhookDispatcher webhooks,
    ILogger<ConnectInboundController> logger) : ControllerBase
{
    /// <summary>
    /// Meta and WhatsApp verify a webhook by calling it with a challenge before
    /// they will send anything. Answering it is the whole handshake.
    /// </summary>
    [HttpGet("{provider}/{token:guid}")]
    public async Task<IActionResult> Verify(
        string provider,
        Guid token,
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.challenge")] string? challenge,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        CancellationToken ct)
    {
        var connector = await Resolve(provider, token, ct);
        if (connector is null) return NotFound(new { message = "Unknown endpoint." });

        if (mode != "subscribe" || challenge is null)
        {
            // Not a handshake. A plain GET is somebody checking the URL is alive,
            // and confirming that much is harmless.
            return Ok(new { status = "ready", provider = connector.Provider });
        }

        var expected = Credential(connector, "verifyToken");

        if (!string.IsNullOrWhiteSpace(expected) && expected != verifyToken)
        {
            await Record(connector, "test", ConnectorOutcomes.Rejected,
                "Subscription refused: the verify token did not match.", null, "{}", ct);

            return Forbid();
        }

        await Record(connector, "test", ConnectorOutcomes.Created,
            "Provider subscribed to this endpoint.", null, "{}", ct);

        // Echoed as plain text, which is what Meta's handshake expects.
        return Content(challenge, "text/plain");
    }

    /// <summary>
    /// A delivery. Whatever it turns out to be, it is recorded before this
    /// returns.
    /// </summary>
    [HttpPost("{provider}/{token:guid}")]
    public async Task<IActionResult> Receive(string provider, Guid token, CancellationToken ct)
    {
        var connector = await Resolve(provider, token, ct);

        // Deliberately the same answer as a wrong token: an endpoint that says
        // "right token, wrong provider" is an endpoint that can be probed.
        if (connector is null) return NotFound(new { message = "Unknown endpoint." });

        var body = await ReadBodyAsync(ct);

        if (!connector.IsActive)
        {
            await Record(connector, "lead", ConnectorOutcomes.Rejected,
                "This connector is switched off.", null, body, ct);

            // 200 on purpose. A portal that gets an error retries for hours and
            // then disables the feed; the delivery is logged either way and the
            // administrator can see exactly what was refused.
            return Ok(new { status = "ignored" });
        }

        var secret = Credential(connector, "sharedSecret");

        if (!string.IsNullOrWhiteSpace(secret) && !SecretMatches(secret))
        {
            await Record(connector, "lead", ConnectorOutcomes.Rejected,
                "The shared secret was missing or wrong.", null, body, ct);

            return Unauthorized(new { message = "Rejected." });
        }

        try
        {
            return await CaptureAsync(connector, body, ct);
        }
        catch (Exception error)
        {
            // The payload is kept so the delivery can be replayed once whatever
            // broke is fixed. A failure here must never look like a rejection.
            logger.LogError(error, "Inbound delivery failed for connector {Connector}.", connector.Id);

            await Record(connector, "lead", ConnectorOutcomes.Failed,
                Trim(error.Message), null, body, ct);

            await MarkError(connector, Trim(error.Message), ct);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { message = "Could not process that delivery." });
        }
    }

    /* ------------------------------------------------------------------ *
     * capture
     * ------------------------------------------------------------------ */

    private async Task<IActionResult> CaptureAsync(
        Connector connector, string body, CancellationToken ct)
    {
        var mapped = InboundLeadMapper.Map(InboundLeadMapper.Parse(body));

        if (string.IsNullOrWhiteSpace(mapped.Phone) && string.IsNullOrWhiteSpace(mapped.Email))
        {
            await Record(connector, "lead", ConnectorOutcomes.Rejected,
                "Nothing in the delivery could be read as a phone number or an email.",
                null, body, ct);

            return BadRequest(new
            {
                message = "An enquiry needs a phone number or an email address.",
            });
        }

        // A retried delivery must not become a second lead. The provider's own
        // id is the reliable signal; where they do not send one, a repeat from
        // the same number within the hour is the honest fallback.
        var existing = await FindExistingAsync(connector, mapped, ct);

        if (existing is not null)
        {
            await Record(connector, "lead", ConnectorOutcomes.Duplicate,
                $"Matched the existing lead #{existing.Id}.", existing.Id, body, ct);

            return Ok(new { status = "duplicate", leadId = existing.Id });
        }

        var entitled = await entitlements.ResolveAsync(connector.CompanyId, ct);

        if (!entitled.HasRoomFor(Limits.Leads))
        {
            await Record(connector, "lead", ConnectorOutcomes.Rejected,
                $"The {entitled.Plan.Name} plan's lead limit is full.", null, body, ct);

            await MarkError(connector, "The plan's lead limit is full.", ct);

            return StatusCode(StatusCodes.Status402PaymentRequired,
                new { message = "This workspace has reached its lead limit." });
        }

        var branchId = connector.DefaultBranchId
            ?? await db.Branches.IgnoreQueryFilters()
                .Where(b => b.CompanyId == connector.CompanyId)
                .OrderBy(b => b.Id)
                .Select(b => b.Id)
                .FirstOrDefaultAsync(ct);

        if (branchId == 0)
        {
            await Record(connector, "lead", ConnectorOutcomes.Rejected,
                "This workspace has no branch to file a lead under.", null, body, ct);

            return BadRequest(new { message = "No branch is configured." });
        }

        var notes = new[] { mapped.Message, InboundLeadMapper.ExtrasNote(mapped.Extras) }
            .Where(part => !string.IsNullOrWhiteSpace(part));

        var lead = new Lead
        {
            CompanyId = connector.CompanyId,
            BranchId = branchId,
            OwnerId = connector.DefaultOwnerId,
            Name = string.IsNullOrWhiteSpace(mapped.Name) ? "Unnamed enquiry" : mapped.Name,
            Phone = mapped.Phone,
            Email = mapped.Email,
            City = mapped.City,
            PreferredLocality = mapped.Locality,
            EventType = mapped.Requirement,
            EventCategory = mapped.Requirement is null
                ? null
                : EventTypes.CategoryOf(mapped.Requirement),
            EventDate = mapped.EventDate,
            GuestCount = mapped.GuestCount,
            BudgetMin = mapped.BudgetMin,
            BudgetMax = mapped.BudgetMax,
            Campaign = mapped.Campaign,
            Notes = string.Join("\n\n", notes) is { Length: > 0 } text ? text : null,
            Source = connector.SourceLabel ?? ConnectorCatalog.For(connector.Provider)?.Name ?? "Portal",
            Stage = LeadStages.New,

            // A portal enquiry is somebody who went looking, and it arrives with
            // nobody watching it. An hour is the window in which it is still warm.
            Priority = LeadPriorities.High,
            SlaDueAt = DateTime.UtcNow.AddHours(1),
            ExternalId = Qualify(connector, mapped.ExternalId),
        };

        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        await Record(connector, "lead", ConnectorOutcomes.Created,
            $"Created lead #{lead.Id}.", lead.Id, body, ct);

        await MarkHealthy(connector, ct);

        await webhooks.RaiseAsync(WebhookEvents.LeadCreated, new
        {
            id = lead.Id,
            name = lead.Name,
            phone = lead.Phone,
            email = lead.Email,
            source = lead.Source,
            stage = lead.Stage,
            connector = connector.Name,
        }, ct);

        return Ok(new { status = "created", leadId = lead.Id });
    }

    /// <summary>
    /// The lead this delivery is a repeat of, if it is one.
    ///
    /// Scoped to the connector's own external ids so two providers cannot
    /// collide on a bare "1234", and falling back to a same-number enquiry
    /// within the hour — which is what a portal retry looks like when it carries
    /// no id at all.
    /// </summary>
    private async Task<Lead?> FindExistingAsync(
        Connector connector, MappedLead mapped, CancellationToken ct)
    {
        var qualified = Qualify(connector, mapped.ExternalId);

        if (qualified is not null)
        {
            return await db.Leads.IgnoreQueryFilters()
                .FirstOrDefaultAsync(
                    l => l.CompanyId == connector.CompanyId && l.ExternalId == qualified, ct);
        }

        if (string.IsNullOrWhiteSpace(mapped.Phone)) return null;

        var since = DateTime.UtcNow.AddHours(-1);

        return await db.Leads.IgnoreQueryFilters()
            .Where(l => l.CompanyId == connector.CompanyId
                        && l.Phone == mapped.Phone
                        && l.CreatedAt >= since)
            .OrderByDescending(l => l.Id)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// Namespaces a provider's id by connector, so "1234" from 99acres and
    /// "1234" from MagicBricks are two enquiries rather than one.
    /// </summary>
    private static string? Qualify(Connector connector, string? externalId) =>
        string.IsNullOrWhiteSpace(externalId)
            ? null
            : $"{connector.Provider}:{connector.Id}:{externalId.Trim()}";

    /* ------------------------------------------------------------------ *
     * plumbing
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Unfiltered: this endpoint is anonymous, so there is no ambient tenant.
    /// The token is what decides which company the delivery belongs to.
    /// </summary>
    private Task<Connector?> Resolve(string provider, Guid token, CancellationToken ct) =>
        db.Connectors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(
                c => c.InboundToken == token && c.Provider == provider, ct);

    private async Task<string> ReadBodyAsync(CancellationToken ct)
    {
        // Form posts are what a plain HTML form sends, and a website connector
        // is exactly that case. Normalised to JSON so one mapper handles both.
        if (Request.HasFormContentType)
        {
            var form = await Request.ReadFormAsync(ct);
            var obj = new JsonObject();

            foreach (var field in form)
            {
                obj[field.Key] = JsonValue.Create(field.Value.ToString());
            }

            return obj.ToJsonString();
        }

        using var reader = new StreamReader(Request.Body);
        var raw = await reader.ReadToEndAsync(ct);

        return raw.Length <= 64_000 ? raw : raw[..64_000];
    }

    /// <summary>
    /// The shared secret, accepted from a header or the query string.
    ///
    /// Both, because portal dashboards vary in what they let you set and a
    /// secret nobody can send is a secret nobody uses.
    /// </summary>
    private bool SecretMatches(string expected)
    {
        var presented = Request.Headers["X-Connector-Secret"].ToString();

        if (string.IsNullOrWhiteSpace(presented))
        {
            presented = Request.Query["secret"].ToString();
        }

        return !string.IsNullOrWhiteSpace(presented)
            && System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(presented),
                System.Text.Encoding.UTF8.GetBytes(expected));
    }

    private static string? Credential(Connector connector, string key)
    {
        if (string.IsNullOrWhiteSpace(connector.CredentialsJson)) return null;

        try
        {
            return JsonNode.Parse(connector.CredentialsJson) is JsonObject obj
                ? obj[key]?.ToString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task Record(
        Connector connector,
        string kind,
        string outcome,
        string detail,
        int? leadId,
        string payload,
        CancellationToken ct)
    {
        db.ConnectorEvents.Add(new ConnectorEvent
        {
            CompanyId = connector.CompanyId,
            ConnectorId = connector.Id,
            Kind = kind,
            Outcome = outcome,
            Detail = Trim(detail),
            LeadId = leadId,
            Payload = string.IsNullOrWhiteSpace(payload) ? "{}" : payload,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        });

        connector.LastEventAt = DateTime.UtcNow;
        connector.EventCount++;

        await db.SaveChangesAsync(ct);
    }

    private async Task MarkHealthy(Connector connector, CancellationToken ct)
    {
        if (connector.Status == ConnectorStatuses.Connected && connector.LastError is null) return;

        connector.Status = ConnectorStatuses.Connected;
        connector.LastError = null;

        await db.SaveChangesAsync(ct);
    }

    private async Task MarkError(Connector connector, string reason, CancellationToken ct)
    {
        connector.Status = ConnectorStatuses.Error;
        connector.LastError = Trim(reason);

        await db.SaveChangesAsync(ct);
    }

    private static string Trim(string value) =>
        value.Length <= 300 ? value : value[..300];
}
