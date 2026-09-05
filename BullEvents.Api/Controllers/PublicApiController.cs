using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/* ------------------------------------------------------------------ *
 * DTOs
 * ------------------------------------------------------------------ */

/// <summary>
/// An enquiry arriving from outside — a portal feed, a website form, a lead-ads
/// webhook.
///
/// Deliberately smaller than the CRM's own create request. An integration knows
/// the buyer's name, number and what they asked about; it does not know which
/// branch to file it under or who should own it, and asking it to guess is how
/// portal leads end up in the wrong place.
/// </summary>
public record InboundLeadRequest(
    [Required, MinLength(2)] string Name,
    string? Phone,
    [EmailAddress] string? Email,
    /// <summary>Where it came from. Matched against this company's own source list.</summary>
    string? Source,
    /// <summary>The occasion — wedding, reception, birthday, conference.</summary>
    string? Requirement,
    /// <summary>When it is on. The field that makes an event enquiry actionable.</summary>
    DateTime? EventDate,
    /// <summary>Last day, for a multi-day run. Omitted for a single-day event.</summary>
    DateTime? EventEndDate,
    /// <summary>Expected footfall.</summary>
    int? GuestCount,
    /// <summary>What the planner is being asked to handle, comma separated.</summary>
    string? ServicesNeeded,
    string? Locality,
    string? City,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? Notes,
    /// <summary>Which office. Omitted means the company's first branch.</summary>
    int? BranchId,
    /// <summary>Values for this company's own fields, by key.</summary>
    Dictionary<string, JsonElement>? CustomFields,
    /// <summary>
    /// The sender's own id for this enquiry. Repeating it replaces nothing and
    /// creates nothing — it is what makes a retried delivery safe.
    /// </summary>
    string? ExternalId);

public record PublicLeadDto(
    int Id,
    string Name,
    string? Phone,
    string? Email,
    string Source,
    string Stage,
    string? Priority,
    string BranchName,
    string? OwnerName,
    DateTime CreatedAt);

public record PublicUnitDto(
    int Id,
    string ProjectName,
    string? TowerName,
    string UnitNumber,
    string UnitType,
    decimal SaleableArea,
    string AreaUnit,
    string Configuration,
    string Status,
    decimal Price);

public record PagedPublicResult<T>(IReadOnlyList<T> Items, int Total, int Page, int PageSize);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The versioned surface an integration talks to.
///
/// Separate from the CRM's own controllers on purpose. Those return whatever
/// the screens happen to need and change shape when a screen does; this one is
/// a contract somebody else's code depends on, so it is small, flat, and
/// versioned in the path.
///
/// Authenticated by <c>X-Api-Key</c> and nothing else — see
/// <see cref="ApiKeyMiddleware"/>. Each action names the scope it needs.
/// </summary>
[ApiController]
[Route("api/v1")]
public class PublicApiController(
    AppDbContext db,
    CustomFieldService customFields,
    EntitlementService entitlements,
    WebhookDispatcher webhooks) : ControllerBase
{
    /* ---------------- leads ---------------- */

    /// <summary>
    /// Captures an enquiry.
    ///
    /// This is the endpoint a portal connector posts to, and the one that makes
    /// "a lead lands in the CRM without anyone here writing code" true.
    /// </summary>
    [HttpPost("leads")]
    [RequireScope(ApiScopes.LeadWrite)]
    public async Task<ActionResult<PublicLeadDto>> CaptureLead(
        InboundLeadRequest request, CancellationToken ct)
    {
        var companyId = User.GetCompanyId();

        if (string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.Email))
        {
            return BadRequest(new
            {
                message = "An enquiry needs a phone number or an email address. "
                        + "A lead nobody can contact is not a lead.",
            });
        }

        // The same ceiling the CRM's own form obeys. An integration is the most
        // likely way to blow past it, so it is checked here first.
        await entitlements.EnsureRoomAsync(Limits.Leads, ct: ct);

        // A retried delivery must not create a second lead. The sender's own id
        // is the only thing that can tell a retry from a genuine second enquiry,
        // which is why the contract asks for one.
        if (!string.IsNullOrWhiteSpace(request.ExternalId))
        {
            var already = await db.Leads
                .FirstOrDefaultAsync(l => l.ExternalId == request.ExternalId, ct);

            if (already is not null)
            {
                return Ok(await ProjectAsync(already.Id, ct));
            }
        }

        var branchId = request.BranchId
            ?? await db.Branches.OrderBy(b => b.Id).Select(b => b.Id).FirstOrDefaultAsync(ct);

        if (branchId == 0)
        {
            return BadRequest(new { message = "This workspace has no branches to file a lead under." });
        }

        var source = await ResolveSourceAsync(request.Source, ct);

        var lead = new Lead
        {
            CompanyId = companyId,
            BranchId = branchId,
            Name = request.Name.Trim(),
            Phone = Blank(request.Phone),
            Email = Blank(request.Email),
            Source = source,
            Stage = LeadStages.New,
            // High rather than Medium: a portal enquiry is somebody who went
            // looking, and it arrives with nobody assigned to notice it.
            Priority = LeadPriorities.High,
            City = Blank(request.City),
            PreferredLocality = Blank(request.Locality),
            EventType = Blank(request.Requirement),
            EventCategory = Blank(request.Requirement) is { } occasion
                ? EventTypes.CategoryOf(occasion)
                : null,
            EventDate = request.EventDate,
            EventEndDate = request.EventEndDate,
            GuestCount = request.GuestCount,
            ServicesNeeded = Blank(request.ServicesNeeded),
            BudgetMin = request.BudgetMin,
            BudgetMax = request.BudgetMax,
            Notes = Blank(request.Notes),
            ExternalId = Blank(request.ExternalId),

            // An inbound lead has nobody watching it yet, so the clock starts
            // tight: an hour is the window in which a portal enquiry is still
            // warm.
            SlaDueAt = DateTime.UtcNow.AddHours(1),
        };

        lead.CustomFields = await customFields.NormaliseAsync(
            SecuredObjects.Lead, request.CustomFields, null, ct);

        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        var dto = await ProjectAsync(lead.Id, ct);

        await webhooks.RaiseAsync(WebhookEvents.LeadCreated, dto, ct);

        return CreatedAtAction(nameof(GetLead), new { id = lead.Id }, dto);
    }

    [HttpGet("leads")]
    [RequireScope(ApiScopes.LeadRead)]
    public async Task<ActionResult<PagedPublicResult<PublicLeadDto>>> Leads(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? stage = null,
        [FromQuery] DateTime? since = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Leads.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(stage)) query = query.Where(l => l.Stage == stage);
        if (since is not null) query = query.Where(l => l.CreatedAt >= since);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(l => l.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => new PublicLeadDto(
                l.Id, l.Name, l.Phone, l.Email, l.Source, l.Stage, l.Priority,
                l.Branch!.Name, l.Owner != null ? l.Owner.Name : null, l.CreatedAt))
            .ToListAsync(ct);

        return Ok(new PagedPublicResult<PublicLeadDto>(items, total, page, pageSize));
    }

    [HttpGet("leads/{id:int}")]
    [RequireScope(ApiScopes.LeadRead)]
    public async Task<ActionResult<PublicLeadDto>> GetLead(int id, CancellationToken ct)
    {
        var dto = await ProjectAsync(id, ct);
        return dto is null ? NotFound(new { message = "No such lead." }) : Ok(dto);
    }

    /* ---------------- inventory ---------------- */

    /// <summary>
    /// What is available to sell — the feed a portal listing or a website
    /// availability grid reads.
    /// </summary>
    [HttpGet("units")]
    [RequireScope(ApiScopes.InventoryRead)]
    public async Task<ActionResult<PagedPublicResult<PublicUnitDto>>> Units(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var query = db.Units.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(u => u.Status == status);

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new PublicUnitDto(
                u.Id,
                u.Tower!.Project!.Name,
                u.Tower.Name,
                u.UnitNumber,
                u.Configuration,
                u.SuperArea ?? u.BuiltUpArea ?? u.CarpetArea,
                u.AreaUnit,
                u.Configuration,
                u.Status,
                u.TotalPrice))
            .ToListAsync(ct);

        return Ok(new PagedPublicResult<PublicUnitDto>(items, total, page, pageSize));
    }

    /* ---------------- helpers ---------------- */

    private async Task<PublicLeadDto?> ProjectAsync(int id, CancellationToken ct) =>
        await db.Leads
            .AsNoTracking()
            .Where(l => l.Id == id)
            .Select(l => new PublicLeadDto(
                l.Id, l.Name, l.Phone, l.Email, l.Source, l.Stage, l.Priority,
                l.Branch!.Name, l.Owner != null ? l.Owner.Name : null, l.CreatedAt))
            .FirstOrDefaultAsync(ct);

    /// <summary>
    /// Matches an incoming source against this company's own list.
    ///
    /// A portal sends whatever it sends. Storing that verbatim fills the source
    /// report with near-duplicates — "99acres", "99Acres", "99 acres" — so an
    /// unrecognised value files under Portal instead, and the notes keep what
    /// was actually claimed.
    /// </summary>
    private async Task<string> ResolveSourceAsync(string? claimed, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(claimed)) return "Portal";

        var match = await db.PickListValues
            .Where(v => v.List == PickLists.LeadSource && v.IsActive)
            .Select(v => v.Value)
            .FirstOrDefaultAsync(v => v.ToLower() == claimed.Trim().ToLower(), ct);

        return match ?? "Portal";
    }

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
