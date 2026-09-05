using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Outdoor Business Meetings — the field-sales side of the workspace. Measured
/// on leads generated and business value rather than on interest level, and
/// carrying geo check-in so a manager can see where the day actually went.
/// </summary>
[ApiController]
[Route("api/obm-visits")]
[Authorize]
[SecuredBy(SecuredObjects.ObmVisit)]
public class ObmVisitsController(AppDbContext db) : CrmControllerBase(db)
{
    internal static readonly FieldMap<ObmVisit> Fields = new FieldMap<ObmVisit>()
        .Text("visitCode", "Visit code", searchable: true)
        .Text("partnerName", "Partner", searchable: true)
        .Select("partnerType", "Partner type")
        .Text("contactPerson", "Contact person", searchable: true)
        .Text("contactPhone", "Contact phone", searchable: true)
        .Select("status", "Status")
        .Date("scheduledAt", "Scheduled")
        .Date("checkInAt", "Checked in")
        .Date("checkOutAt", "Checked out")
        .Select("city", "City")
        .Text("locationLabel", "Location")
        .Number("slotMinutes", "Slot length", "DurationMinutes")
        .Number("distanceKm", "Distance (km)")
        .Number("expenseAmount", "Expense")
        .Text("purpose", "Purpose", searchable: true)
        .Text("outcome", "Outcome", searchable: true)
        .Text("meetingNotes", "Meeting notes", searchable: true)
        .Number("leadsGenerated", "Leads generated")
        .Number("leadId", "Lead ID")
        .Number("businessValue", "Business value")
        .Date("nextMeetingAt", "Next meeting")
        .Select("agentName", "Agent")
        .Number("agentId", "Agent ID")
        .Select("branchName", "Branch", "Branch.Name")
        .Number("branchId", "Branch ID")
        .Date("createdAt", "Created");

    private IQueryable<ObmVisit> Base() => Db.ObmVisits.Include(v => v.Branch).AsNoTracking();

    private static ObmVisitDto ToDto(ObmVisit v, string? leadName = null) => new(
        v.Id, v.VisitCode, v.LeadId, leadName, v.PartnerName, v.PartnerType, v.ContactPerson, v.ContactPhone,
        v.Status, v.ScheduledAt, v.DurationMinutes, v.CheckInAt, v.CheckOutAt,
        v.CheckInAt is not null && v.CheckOutAt is not null
            ? (int)(v.CheckOutAt.Value - v.CheckInAt.Value).TotalMinutes
            : null,
        v.Latitude, v.Longitude, v.LocationLabel, v.City, v.DistanceKm, v.ExpenseAmount,
        v.Purpose, v.Outcome, v.MeetingNotes, v.LeadsGenerated, v.BusinessValue, v.NextMeetingAt,
        v.AgentId, v.AgentName, v.BranchId, v.Branch?.Name ?? "—", v.CreatedAt, v.UpdatedAt);

    /* ------------------------------------------------------------------ *
     * Reads
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<FilterFieldDto>>> GetFields(CancellationToken ct)
    {
        var agents = await Db.ObmVisits
            .Select(v => v.AgentName).Distinct().OrderBy(n => n).Take(200).ToListAsync(ct);

        var cities = await Db.ObmVisits
            .Where(v => v.City != null)
            .Select(v => v.City!).Distinct().OrderBy(c => c).ToListAsync(ct);

        var branches = await Db.Branches
            .Where(b => b.CompanyId == Db.Tenant.CompanyId)
            .Select(b => b.Name).OrderBy(n => n).ToListAsync(ct);

        return Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["partnerType"] = Options(PartnerTypes.All),
            ["status"] = Options(VisitStatuses.All),
            ["agentName"] = Options([.. agents]),
            ["city"] = Options([.. cities]),
            ["branchName"] = Options([.. branches]),
        }, new Dictionary<string, string>
        {
            ["partnerName"] = "Partner",
            ["partnerType"] = "Partner",
            ["contactPerson"] = "Partner",
            ["contactPhone"] = "Partner",
            ["scheduledAt"] = "Timing",
            ["checkInAt"] = "Timing",
            ["checkOutAt"] = "Timing",
            ["nextMeetingAt"] = "Timing",
            ["city"] = "Field",
            ["locationLabel"] = "Field",
            ["distanceKm"] = "Field",
            ["expenseAmount"] = "Field",
            ["leadsGenerated"] = "Result",
            ["businessValue"] = "Result",
            ["outcome"] = "Result",
        }));
    }

    /// <summary>
    /// Resolves the lead names for a page of meetings in one query. Kept out of
    /// the projection because <see cref="ObmVisit"/> carries the id alone — the
    /// link is optional and most rows do not have one.
    /// </summary>
    private async Task<Dictionary<int, string>> LeadNamesAsync(
        IEnumerable<int?> leadIds, CancellationToken ct)
    {
        var ids = leadIds.OfType<int>().Distinct().ToList();
        if (ids.Count == 0) return [];

        return await Db.Leads
            .Where(l => ids.Contains(l.Id))
            .ToDictionaryAsync(l => l.Id, l => l.Name, ct);
    }

    private async Task<ObmVisitDto> ToDtoAsync(ObmVisit visit, CancellationToken ct)
    {
        var names = await LeadNamesAsync([visit.LeadId], ct);
        return ToDto(visit, visit.LeadId is int id ? names.GetValueOrDefault(id) : null);
    }

    [PermissionAction(ObjectAction.View)]
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<ObmVisitDto>>> Query(QueryRequest request, CancellationToken ct)
    {
        var result = await RunQueryAsync(
            Base(),
            request,
            Fields,
            v => ToDto(v),
            defaultSortPath: "ScheduledAt",
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["visits"] = await filtered.CountAsync(ct),
                ["completed"] = await filtered.CountAsync(v => v.Status == VisitStatuses.Completed, ct),
                ["leadsGenerated"] = await filtered.SumAsync(v => v.LeadsGenerated, ct),
                ["businessValue"] = await filtered.SumAsync(v => v.BusinessValue ?? 0m, ct),
                ["expense"] = await filtered.SumAsync(v => v.ExpenseAmount ?? 0m, ct),
            },
            cancellationToken: ct);

        var names = await LeadNamesAsync(result.Items.Select(i => i.LeadId), ct);

        result.Items = result.Items
            .Select(i => i.LeadId is int id
                ? i with { LeadName = names.GetValueOrDefault(id) }
                : i)
            .ToList();

        return Ok(result);
    }

    /// <summary>
    /// Field-productivity rollup: who is out, how far they travelled, what it
    /// cost and what came back. Cost per lead is the number this view exists
    /// to answer.
    /// </summary>
    [HttpGet("productivity")]
    public async Task<ActionResult<IReadOnlyList<FieldProductivityDto>>> Productivity(
        [FromQuery] int days = 30,
        CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 1, 365));

        var rows = await Db.ObmVisits
            .Where(v => v.ScheduledAt >= since)
            .GroupBy(v => new { v.AgentId, v.AgentName })
            .Select(g => new
            {
                g.Key.AgentId,
                g.Key.AgentName,
                Visits = g.Count(),
                Completed = g.Count(v => v.Status == VisitStatuses.Completed),
                Leads = g.Sum(v => v.LeadsGenerated),
                Value = g.Sum(v => v.BusinessValue ?? 0m),
                Expense = g.Sum(v => v.ExpenseAmount ?? 0m),
                Distance = g.Sum(v => v.DistanceKm ?? 0m),
            })
            .ToListAsync(ct);

        return Ok(rows
            .Select(r => new FieldProductivityDto(
                r.AgentId,
                r.AgentName,
                r.Visits,
                r.Completed,
                r.Leads,
                r.Value,
                r.Expense,
                r.Distance,
                r.Leads == 0 ? 0 : decimal.Round(r.Expense / r.Leads, 2),
                r.Visits == 0 ? 0 : Math.Round(r.Leads / (double)r.Visits, 2)))
            .OrderByDescending(r => r.LeadsGenerated)
            .ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ObmVisitDto>> GetOne(int id, CancellationToken ct)
    {
        var visit = await Base().FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("OBM visit");

        return Ok(await ToDtoAsync(visit, ct));
    }

    /* ------------------------------------------------------------------ *
     * Writes
     * ------------------------------------------------------------------ */

    [HttpPost]
    public async Task<ActionResult<ObmVisitDto>> Create(ObmVisitInput input, CancellationToken ct)
    {
        var branch = await RequireBranchAsync(input.BranchId, ct);

        var visit = new ObmVisit
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            VisitCode = Code("OBM"),
        };

        await ApplyAsync(visit, input, ct);

        if (!input.AllowOverlap && visit.Status != VisitStatuses.Cancelled)
        {
            await SchedulingController.EnsureFreeAsync(
                Db, visit.AgentId, visit.ScheduledAt, visit.DurationMinutes,
                $"the meeting with {visit.PartnerName}", ct);
        }

        Db.ObmVisits.Add(visit);
        await Db.SaveChangesAsync(ct);

        visit.Branch = branch;
        return CreatedAtAction(nameof(GetOne), new { id = visit.Id }, ToDto(visit));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<ObmVisitDto>> Update(int id, ObmVisitInput input, CancellationToken ct)
    {
        var visit = await Db.ObmVisits.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("OBM visit");

        var branch = await RequireBranchAsync(input.BranchId, ct);
        visit.BranchId = branch.Id;
        await ApplyAsync(visit, input, ct);

        await Db.SaveChangesAsync(ct);

        visit.Branch = branch;
        return Ok(await ToDtoAsync(visit, ct));
    }

    /// <summary>Geo check-in from the field. Coordinates are optional — GPS fails.</summary>
    [HttpPatch("{id:int}/check-in")]
    public async Task<ActionResult<ObmVisitDto>> CheckIn(int id, GeoCheckInRequest request, CancellationToken ct)
    {
        var visit = await Db.ObmVisits.Include(v => v.Branch)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("OBM visit");

        visit.CheckInAt ??= DateTime.UtcNow;
        visit.Status = VisitStatuses.Confirmed;
        visit.Latitude = request.Latitude ?? visit.Latitude;
        visit.Longitude = request.Longitude ?? visit.Longitude;
        visit.LocationLabel = request.LocationLabel ?? visit.LocationLabel;

        await Db.SaveChangesAsync(ct);
        return Ok(await ToDtoAsync(visit, ct));
    }

    [HttpPatch("{id:int}/check-out")]
    public async Task<ActionResult<ObmVisitDto>> CheckOut(int id, ObmCheckOutRequest request, CancellationToken ct)
    {
        var visit = await Db.ObmVisits.Include(v => v.Branch)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("OBM visit");

        if (visit.CheckInAt is null)
        {
            throw ApiException.Conflict("This meeting has not been checked in yet.");
        }

        visit.CheckOutAt = DateTime.UtcNow;
        visit.Status = VisitStatuses.Completed;
        visit.Outcome = request.Outcome ?? visit.Outcome;
        visit.MeetingNotes = request.MeetingNotes ?? visit.MeetingNotes;
        visit.LeadsGenerated = request.LeadsGenerated ?? visit.LeadsGenerated;
        visit.BusinessValue = request.BusinessValue ?? visit.BusinessValue;
        visit.ExpenseAmount = request.ExpenseAmount ?? visit.ExpenseAmount;
        visit.DistanceKm = request.DistanceKm ?? visit.DistanceKm;
        visit.NextMeetingAt = request.NextMeetingAt ?? visit.NextMeetingAt;

        await AdvanceLeadToVisitAsync(visit.LeadId, visit.VisitCode, ct);

        await Db.SaveChangesAsync(ct);
        return Ok(await ToDtoAsync(visit, ct));
    }

    /// <summary>
    /// A completed meeting is a viewing, so the lead moves stage on it exactly
    /// as it does on a site visit.
    ///
    /// The two are alternatives — the buyer comes to the project, or the team
    /// goes to the buyer — and a desk that logs only OBMs would otherwise watch
    /// its whole pipeline sit in Contacted forever. Forward-only out of the
    /// pre-visit stages, so a late-logged meeting never drags a booked lead back.
    /// </summary>
    private async Task AdvanceLeadToVisitAsync(int? leadId, string visitCode, CancellationToken ct)
    {
        if (leadId is not int id) return;

        var lead = await Db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null || !LeadStages.BeforeObmVisit.Contains(lead.Stage)) return;

        Db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.StageChange,
            FromStage = lead.Stage,
            ToStage = LeadStages.ObmVisit,
            Remarks = $"OBM visit {visitCode} completed.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        lead.Stage = LeadStages.ObmVisit;
    }

    /// <summary>
    /// Move a meeting to any status from wherever it is listed. Mirrors the
    /// site-visit endpoint so both grids drive the same control.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<ObmVisitDto>> SetStatus(
        int id,
        VisitStatusRequest request,
        CancellationToken ct)
    {
        var visit = await Db.ObmVisits
            .Include(v => v.Branch)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("OBM visit");

        var status = Require(request.Status, VisitStatuses.All, "status");
        var previous = visit.Status;

        if (status == previous && request.ScheduledAt is null)
        {
            return Ok(await ToDtoAsync(visit, ct));
        }

        switch (status)
        {
            case VisitStatuses.Rescheduled:
                if (request.ScheduledAt is not DateTime moved)
                {
                    throw ApiException.BadRequest("Pick a new date and time to reschedule to.");
                }

                visit.ScheduledAt = moved;
                visit.CheckInAt = null;
                visit.CheckOutAt = null;
                break;

            case VisitStatuses.Completed:
                visit.CheckInAt ??= request.ScheduledAt ?? visit.ScheduledAt;
                visit.CheckOutAt ??= DateTime.UtcNow;
                break;

            case VisitStatuses.Cancelled:
            case VisitStatuses.NoShow:
                // An OBM carries no cancellation field of its own; the reason a
                // meeting did not happen is its outcome.
                visit.Outcome = request.Reason ?? visit.Outcome;
                break;

            case VisitStatuses.Scheduled:
            case VisitStatuses.Confirmed:
                if (previous is VisitStatuses.Completed or VisitStatuses.Cancelled
                    or VisitStatuses.NoShow)
                {
                    visit.CheckOutAt = null;
                }

                if (request.ScheduledAt is DateTime slot) visit.ScheduledAt = slot;
                break;
        }

        visit.Status = status;

        await LogLeadActivityAsync(
            visit.LeadId,
            LeadActivityTypes.ObmVisit,
            VisitRemark("OBM", visit.VisitCode, previous, status, visit.ScheduledAt, request.Reason),
            ct);

        if (status == VisitStatuses.Completed)
        {
            await AdvanceLeadToVisitAsync(visit.LeadId, visit.VisitCode, ct);
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await ToDtoAsync(visit, ct));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var visit = await Db.ObmVisits.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("OBM visit");

        SoftDelete(visit);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ApplyAsync(ObmVisit visit, ObmVisitInput input, CancellationToken ct)
    {
        // Optional: most OBMs are partner-facing and belong to no single lead.
        // When one is named it must be a lead in this tenant, or the lead grid
        // would show an OBM status sourced from another company's row.
        if (input.LeadId is not null &&
            !await Db.Leads.AnyAsync(l => l.Id == input.LeadId, ct))
        {
            throw ApiException.BadRequest("Select a valid lead.");
        }

        visit.LeadId = input.LeadId;
        visit.PartnerName = input.PartnerName.Trim();
        visit.PartnerType = Require(input.PartnerType, PartnerTypes.All, "partner type");
        visit.ContactPerson = input.ContactPerson;
        visit.ContactPhone = input.ContactPhone;
        visit.Status = Require(input.Status, VisitStatuses.All, "status");
        visit.ScheduledAt = input.ScheduledAt;
        visit.DurationMinutes = Math.Clamp(input.DurationMinutes, 15, 480);
        visit.CheckInAt = input.CheckInAt;
        visit.CheckOutAt = input.CheckOutAt;
        visit.Latitude = input.Latitude;
        visit.Longitude = input.Longitude;
        visit.LocationLabel = input.LocationLabel;
        visit.City = input.City;
        visit.DistanceKm = input.DistanceKm;
        visit.ExpenseAmount = input.ExpenseAmount;
        visit.Purpose = input.Purpose;
        visit.Outcome = input.Outcome;
        visit.MeetingNotes = input.MeetingNotes;
        visit.LeadsGenerated = Math.Max(0, input.LeadsGenerated);
        visit.BusinessValue = input.BusinessValue;
        visit.NextMeetingAt = input.NextMeetingAt;

        var agentId = input.AgentId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null);
        visit.AgentId = agentId;
        visit.AgentName = agentId is null
            ? Db.Tenant.UserName
            : (await Db.Users.FindAsync([agentId], ct))?.Name ?? Db.Tenant.UserName;
    }
}

public record GeoCheckInRequest(double? Latitude, double? Longitude, string? LocationLabel);

public record ObmCheckOutRequest(
    string? Outcome,
    string? MeetingNotes,
    int? LeadsGenerated,
    decimal? BusinessValue,
    decimal? ExpenseAmount,
    decimal? DistanceKm,
    DateTime? NextMeetingAt
);

public record FieldProductivityDto(
    int? AgentId,
    string AgentName,
    int Visits,
    int Completed,
    int LeadsGenerated,
    decimal BusinessValue,
    decimal Expense,
    decimal DistanceKm,
    decimal CostPerLead,
    double LeadsPerVisit
);
