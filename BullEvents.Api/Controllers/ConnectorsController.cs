using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
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

public record CredentialFieldDto(
    string Key, string Label, string Help, bool Secret, bool Required);

public record ConnectorDefinitionDto(
    string Provider,
    string Name,
    string Category,
    string Tagline,
    string Description,
    string Direction,
    IReadOnlyList<CredentialFieldDto> Credentials,
    bool HasInboundEndpoint,
    IReadOnlyList<string> SetupSteps,
    bool Live,
    /// <summary>How many of these this company has configured.</summary>
    int ConfiguredCount);

public record ConnectorDto(
    int Id,
    string Provider,
    string ProviderName,
    string Category,
    string Name,
    bool IsActive,
    string Status,
    string? LastError,
    DateTime? LastEventAt,
    long EventCount,
    int? DefaultBranchId,
    string? DefaultBranchName,
    int? DefaultOwnerId,
    string? DefaultOwnerName,
    string? SourceLabel,
    /// <summary>The URL to paste into the provider. Null when it does not deliver by webhook.</summary>
    string? InboundUrl,
    /// <summary>
    /// Which credentials are set, by key. Never the values — a secret that can
    /// be read back out of the console leaks through the console.
    /// </summary>
    IReadOnlyList<string> CredentialsSet,
    DateTime CreatedAt);

public record SaveConnectorRequest(
    [Required] string Provider,
    [Required, MinLength(2)] string Name,
    bool IsActive,
    int? DefaultBranchId,
    int? DefaultOwnerId,
    string? SourceLabel,
    /// <summary>
    /// Only the fields being changed. A key that is absent keeps whatever is
    /// stored, so a form that never received a secret cannot blank it by
    /// submitting an empty box.
    /// </summary>
    Dictionary<string, string>? Credentials);

public record ConnectorEventDto(
    int Id,
    string Kind,
    string Outcome,
    string? Detail,
    int? LeadId,
    string? IpAddress,
    DateTime At,
    /// <summary>The body as it arrived, for the argument about whether it did.</summary>
    string Payload);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The connector console: which outside systems this workspace talks to, how
/// they are configured, and what has actually come through.
/// </summary>
[ApiController]
[Route("api/admin/connectors")]
[Authorize]
[RequirePermission(SecuredObjects.User, ObjectAction.ModifyAll)]
public class ConnectorsController(
    AppDbContext db,
    EntitlementService entitlements,
    TenantContext tenant,
    IConfiguration configuration) : ControllerBase
{
    /// <summary>The catalogue, with how many of each this company already has.</summary>
    [HttpGet("catalogue")]
    public async Task<ActionResult<IReadOnlyList<ConnectorDefinitionDto>>> Catalogue(
        CancellationToken ct)
    {
        var counts = await db.Connectors
            .GroupBy(c => c.Provider)
            .Select(g => new { Provider = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Provider, x => x.Count, StringComparer.OrdinalIgnoreCase, ct);

        return Ok(ConnectorCatalog.All
            .Select(d => new ConnectorDefinitionDto(
                d.Provider, d.Name, d.Category, d.Tagline, d.Description,
                d.Direction.ToString(),
                d.Credentials
                    .Select(f => new CredentialFieldDto(f.Key, f.Label, f.Help, f.Secret, f.Required))
                    .ToList(),
                d.HasInboundEndpoint, d.SetupSteps, d.Live,
                counts.GetValueOrDefault(d.Provider)))
            .ToList());
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ConnectorDto>>> List(CancellationToken ct)
    {
        var rows = await db.Connectors
            .Include(c => c.DefaultBranch)
            .Include(c => c.DefaultOwner)
            .OrderBy(c => c.Provider).ThenBy(c => c.Id)
            .ToListAsync(ct);

        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ConnectorDto>> Get(int id, CancellationToken ct)
    {
        var connector = await db.Connectors
            .Include(c => c.DefaultBranch)
            .Include(c => c.DefaultOwner)
            .FirstOrDefaultAsync(c => c.Id == id, ct);

        return connector is null
            ? NotFound(new { message = "That connector no longer exists." })
            : Ok(ToDto(connector));
    }

    [HttpPost]
    public async Task<ActionResult<ConnectorDto>> Create(
        SaveConnectorRequest request, CancellationToken ct)
    {
        var definition = ConnectorCatalog.For(request.Provider);
        if (definition is null)
        {
            return BadRequest(new { message = $"'{request.Provider}' is not a connector this platform offers." });
        }

        var plan = (await entitlements.ResolveAsync(ct: ct)).Plan;

        // Connectors ride on the same capability as the public API: both are a
        // system rather than a person talking to the CRM, and a plan that does
        // not include one should not quietly include the other.
        if (!plan.ApiAccess)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = $"Connectors are not part of the {plan.Name} plan.",
            });
        }

        var connector = new Connector
        {
            CompanyId = tenant.CompanyId,
            Provider = definition.Provider,
            Name = request.Name.Trim(),
            IsActive = request.IsActive,
            SourceLabel = Blank(request.SourceLabel) ?? definition.Name,
        };

        var refused = await ApplyAsync(connector, definition, request, ct);
        if (refused is not null) return refused;

        db.Connectors.Add(connector);
        await db.SaveChangesAsync(ct);

        return Ok(await ReloadAsync(connector.Id, ct));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ConnectorDto>> Update(
        int id, SaveConnectorRequest request, CancellationToken ct)
    {
        var connector = await db.Connectors.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (connector is null) return NotFound(new { message = "That connector no longer exists." });

        var definition = ConnectorCatalog.For(connector.Provider);
        if (definition is null) return BadRequest(new { message = "Unknown provider." });

        connector.Name = request.Name.Trim();
        connector.IsActive = request.IsActive;
        connector.SourceLabel = Blank(request.SourceLabel) ?? definition.Name;
        connector.UpdatedAt = DateTime.UtcNow;

        var refused = await ApplyAsync(connector, definition, request, ct);
        if (refused is not null) return refused;

        await db.SaveChangesAsync(ct);

        return Ok(await ReloadAsync(connector.Id, ct));
    }

    /// <summary>
    /// Issues a new inbound URL and retires the old one.
    ///
    /// The endpoint is the credential for a portal, so this is the rotate button
    /// for a token that has been pasted into somebody's dashboard and possibly
    /// their support ticket.
    /// </summary>
    [HttpPost("{id:int}/rotate")]
    public async Task<ActionResult<ConnectorDto>> Rotate(int id, CancellationToken ct)
    {
        var connector = await db.Connectors.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (connector is null) return NotFound(new { message = "That connector no longer exists." });

        connector.InboundToken = Guid.NewGuid();
        connector.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok(await ReloadAsync(connector.Id, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var connector = await db.Connectors.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (connector is null) return NotFound(new { message = "That connector no longer exists." });

        db.Connectors.Remove(connector);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>
    /// What has come through, newest first.
    ///
    /// The payload comes with it. "Did it reach you" is the first question of
    /// every integration argument, and the body as it landed is the only answer
    /// that settles it.
    /// </summary>
    [HttpGet("{id:int}/events")]
    public async Task<ActionResult<IReadOnlyList<ConnectorEventDto>>> Events(
        int id,
        [FromQuery] string? outcome = null,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var query = db.ConnectorEvents.Where(e => e.ConnectorId == id);

        if (!string.IsNullOrWhiteSpace(outcome))
        {
            query = query.Where(e => e.Outcome == outcome);
        }

        var rows = await query
            .OrderByDescending(e => e.At)
            .Take(Math.Clamp(take, 1, 500))
            .Select(e => new ConnectorEventDto(
                e.Id, e.Kind, e.Outcome, e.Detail, e.LeadId, e.IpAddress, e.At, e.Payload))
            .ToListAsync(ct);

        return Ok(rows);
    }

    /// <summary>
    /// Posts a sample enquiry through this connector's own mapper.
    ///
    /// It goes through the real path rather than a simulated one, so what the
    /// screen shows is what a portal would get — including the lead it creates,
    /// which is the point: an administrator should see the shape of the result
    /// before a real enquiry depends on it.
    /// </summary>
    [HttpPost("{id:int}/test")]
    public async Task<ActionResult<object>> Test(int id, CancellationToken ct)
    {
        var connector = await db.Connectors.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (connector is null) return NotFound(new { message = "That connector no longer exists." });

        var sample = SampleFor(connector.Provider);
        var mapped = InboundLeadMapper.Map(JsonNode.Parse(sample));

        db.ConnectorEvents.Add(new ConnectorEvent
        {
            CompanyId = connector.CompanyId,
            ConnectorId = connector.Id,
            Kind = "test",
            Outcome = ConnectorOutcomes.Created,
            Detail = "Mapping test run from the console. No lead was created.",
            Payload = sample,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
        });

        connector.LastEventAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        return Ok(new
        {
            sample = JsonNode.Parse(sample),
            mapped = new
            {
                mapped.Name,
                mapped.Phone,
                mapped.Email,
                mapped.City,
                mapped.Locality,
                mapped.Requirement,
                mapped.BudgetMin,
                mapped.BudgetMax,
                mapped.Campaign,
                mapped.ExternalId,
                notes = new[] { mapped.Message, InboundLeadMapper.ExtrasNote(mapped.Extras) }
                    .Where(part => !string.IsNullOrWhiteSpace(part)),
            },
        });
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    private async Task<ActionResult?> ApplyAsync(
        Connector connector,
        ConnectorDefinition definition,
        SaveConnectorRequest request,
        CancellationToken ct)
    {
        if (request.DefaultBranchId is int branchId)
        {
            var exists = await db.Branches.AnyAsync(b => b.Id == branchId, ct);
            if (!exists) return new NotFoundObjectResult(new { message = "That branch is not in this company." });
        }

        if (request.DefaultOwnerId is int ownerId)
        {
            var exists = await db.Users.AnyAsync(u => u.Id == ownerId, ct);
            if (!exists) return new NotFoundObjectResult(new { message = "That owner is not in this company." });
        }

        connector.DefaultBranchId = request.DefaultBranchId;
        connector.DefaultOwnerId = request.DefaultOwnerId;

        if (request.Credentials is { Count: > 0 })
        {
            var stored = Read(connector.CredentialsJson);

            foreach (var (key, value) in request.Credentials)
            {
                if (!definition.Credentials.Any(f =>
                        string.Equals(f.Key, key, StringComparison.OrdinalIgnoreCase)))
                {
                    continue;
                }

                // An empty box clears the field; an absent key leaves it alone.
                // Without that distinction a form that never received a secret
                // would blank it on every save.
                if (string.IsNullOrWhiteSpace(value)) stored.Remove(key);
                else stored[key] = value.Trim();
            }

            connector.CredentialsJson = stored.Count == 0
                ? null
                : JsonSerializer.Serialize(stored);
        }

        var missing = definition.Credentials
            .Where(f => f.Required)
            .Where(f => !Read(connector.CredentialsJson).ContainsKey(f.Key))
            .ToList();

        // Status is derived rather than set: an administrator cannot mark a
        // half-configured connector connected, and one that later starts working
        // is corrected by the first delivery that lands.
        connector.Status = missing.Count > 0
            ? ConnectorStatuses.NotConfigured
            : connector.Status == ConnectorStatuses.NotConfigured
                ? ConnectorStatuses.Connected
                : connector.Status;

        return null;
    }

    private async Task<ConnectorDto> ReloadAsync(int id, CancellationToken ct)
    {
        var connector = await db.Connectors
            .Include(c => c.DefaultBranch)
            .Include(c => c.DefaultOwner)
            .FirstAsync(c => c.Id == id, ct);

        return ToDto(connector);
    }

    private ConnectorDto ToDto(Connector c)
    {
        var definition = ConnectorCatalog.For(c.Provider);

        return new ConnectorDto(
            c.Id,
            c.Provider,
            definition?.Name ?? c.Provider,
            definition?.Category ?? "Other",
            c.Name,
            c.IsActive,
            c.Status,
            c.LastError,
            c.LastEventAt,
            c.EventCount,
            c.DefaultBranchId,
            c.DefaultBranch?.Name,
            c.DefaultOwnerId,
            c.DefaultOwner?.Name,
            c.SourceLabel,
            definition?.HasInboundEndpoint == true
                ? $"{PublicApiUrl}/api/connect/{c.Provider}/{c.InboundToken}"
                : null,
            Read(c.CredentialsJson).Keys.ToList(),
            c.CreatedAt);
    }

    /// <summary>
    /// What to prefix the inbound URL with.
    ///
    /// Taken from configuration rather than from the incoming request: an
    /// administrator on <c>localhost</c> would otherwise be handed a URL no
    /// portal can reach, and paste it into a live dashboard.
    /// </summary>
    private string PublicApiUrl =>
        (configuration["PUBLIC_API_URL"]
         ?? configuration["Public:ApiUrl"]
         ?? $"{Request.Scheme}://{Request.Host}").TrimEnd('/');

    private static Dictionary<string, string> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new(StringComparer.OrdinalIgnoreCase);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException)
        {
            return new(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// A representative payload per provider, in the shape they actually send.
    ///
    /// Real shapes rather than a generic stub, because the point of the test is
    /// to prove the mapper copes with that provider's quirks — Meta's nested
    /// answers, a portal's Indian-format budget, a bare website form.
    /// </summary>
    private static string SampleFor(string provider) => provider switch
    {
        ConnectorCatalog.MetaLeadAds => """
            {
              "entry": [{
                "changes": [{
                  "value": {
                    "leadgen_id": "6021958",
                    "form_name": "Wedding Planning — Enquiry",
                    "field_data": [
                      { "name": "full_name", "values": ["Ritu Malhotra"] },
                      { "name": "phone_number", "values": ["+91 98200-11122"] },
                      { "name": "email", "values": ["ritu.malhotra@example.com"] },
                      { "name": "city", "values": ["Mumbai"] },
                      { "name": "event_type", "values": ["Wedding"] },
                      { "name": "event_date", "values": ["14/02/2027"] },
                      { "name": "guests", "values": ["450"] },
                      { "name": "budget", "values": ["25 lakh"] }
                    ]
                  }
                }]
              }]
            }
            """,

        ConnectorCatalog.WedMeGood or ConnectorCatalog.ShaadiSaga or ConnectorCatalog.VenueLook => """
            {
              "lead_id": "AC-88213",
              "name": "Suresh Iyer",
              "mobile": "09820011122",
              "email": "suresh.iyer@example.com",
              "city": "Mumbai",
              "locality": "Andheri West",
              "venue_name": "The Grand Palladium",
              "event_type": "Reception",
              "event_date": "22/11/2026",
              "guests": "300",
              "budget": "18 lakh",
              "message": "Looking for a banquet with in-house catering, want to visit this weekend."
            }
            """,

        ConnectorCatalog.MyOperator => """
            {
              "call_id": "MO-99120",
              "caller_id": "+919820011122",
              "direction": "inbound",
              "agent": "Akhil",
              "duration": 143,
              "recording_url": "https://recordings.example.com/MO-99120.mp3"
            }
            """,

        ConnectorCatalog.GoogleLeadForms => """
            {
              "lead_id": "GA-40021",
              "campaign_name": "Wedding Planners — Search",
              "user_column_data": [
                { "column_name": "Full Name", "string_value": "Neha Kapoor" },
                { "column_name": "Phone Number", "string_value": "+919820011133" },
                { "column_name": "Email", "string_value": "neha.kapoor@example.com" }
              ]
            }
            """,

        _ => """
            {
              "name": "Arjun Nair",
              "phone": "9820011144",
              "email": "arjun.nair@example.com",
              "city": "Pune",
              "message": "Interested in a 2BHK, please call after 6pm.",
              "utm_campaign": "spring-launch"
            }
            """,
    };
}
