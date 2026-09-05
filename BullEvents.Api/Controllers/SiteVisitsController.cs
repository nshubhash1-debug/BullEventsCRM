using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>Site visits — scheduling, check-in/out and post-visit feedback.</summary>
[ApiController]
[Route("api/site-visits")]
[Authorize]
[SecuredBy(SecuredObjects.SiteVisit)]
public class SiteVisitsController(AppDbContext db) : CrmControllerBase(db)
{
    internal static readonly FieldMap<SiteVisit> Fields = new FieldMap<SiteVisit>()
        .Text("visitCode", "Visit code", searchable: true)
        .Text("visitorName", "Visitor", searchable: true)
        .Text("visitorPhone", "Visitor phone", searchable: true)
        .Number("partySize", "Party size")
        .Select("visitType", "Visit type")
        .Select("status", "Status")
        .Date("scheduledAt", "Scheduled")
        .Date("checkInAt", "Checked in")
        .Date("checkOutAt", "Checked out")
        .Select("hostName", "Host")
        .Number("hostId", "Host ID")
        .Select("projectName", "Project", "Project.Name")
        .Text("unitNumber", "Unit", "Unit.UnitNumber")
        .Text("transportMode", "Transport")
        .Text("pickupLocation", "Pickup")
        .Text("feedback", "Feedback", searchable: true)
        .Select("interestLevel", "Interest level")
        .Number("slotMinutes", "Slot length", "DurationMinutes")
        .Number("rating", "Rating")
        .Number("budgetDiscussed", "Budget discussed")
        .Text("nextAction", "Next action")
        .Select("branchName", "Branch", "Branch.Name")
        .Number("branchId", "Branch ID")
        .Number("leadId", "Lead ID")
        .Number("contactId", "Contact ID")
        .Date("createdAt", "Created");

    private IQueryable<SiteVisit> Base() => Db.SiteVisits
        .Include(v => v.Branch)
        .Include(v => v.Project)
        .Include(v => v.Unit)
        .AsNoTracking();

    private static SiteVisitDto ToDto(SiteVisit v) => new(
        v.Id, v.VisitCode, v.LeadId, v.ContactId, v.OpportunityId,
        v.ProjectId, v.Project?.Name, v.UnitId, v.Unit?.UnitNumber,
        v.VisitorName, v.VisitorPhone, v.PartySize,
        v.VisitType, v.Status, v.ScheduledAt, v.DurationMinutes,
        v.CheckInAt, v.CheckOutAt,
        v.CheckInAt is not null && v.CheckOutAt is not null
            ? (int)(v.CheckOutAt.Value - v.CheckInAt.Value).TotalMinutes
            : null,
        v.HostId, v.HostName, v.TransportMode, v.PickupLocation,
        v.Feedback, v.InterestLevel, v.Rating, v.BudgetDiscussed,
        v.NextAction, v.CancellationReason,
        v.BranchId, v.Branch?.Name ?? "—", v.CreatedAt, v.UpdatedAt);

    /* ------------------------------------------------------------------ *
     * Reads
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<FilterFieldDto>>> GetFields(CancellationToken ct)
    {
        var hosts = await Db.SiteVisits
            .Where(v => v.HostName != null)
            .Select(v => v.HostName!).Distinct().OrderBy(n => n).ToListAsync(ct);

        var projects = await Db.Projects.Select(p => p.Name).OrderBy(n => n).ToListAsync(ct);

        var branches = await Db.Branches
            .Where(b => b.CompanyId == Db.Tenant.CompanyId)
            .Select(b => b.Name).OrderBy(n => n).ToListAsync(ct);

        return Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["visitType"] = Options(VisitTypes.All),
            ["status"] = Options(VisitStatuses.All),
            ["interestLevel"] = Options(InterestLevels.All),
            ["hostName"] = Options([.. hosts]),
            ["projectName"] = Options([.. projects]),
            ["branchName"] = Options([.. branches]),
        }, new Dictionary<string, string>
        {
            ["visitCode"] = "Visit",
            ["visitType"] = "Visit",
            ["status"] = "Visit",
            ["visitorName"] = "Visitor",
            ["visitorPhone"] = "Visitor",
            ["partySize"] = "Visitor",
            ["scheduledAt"] = "Timing",
            ["checkInAt"] = "Timing",
            ["checkOutAt"] = "Timing",
            ["projectName"] = "Property",
            ["unitNumber"] = "Property",
            ["feedback"] = "Outcome",
            ["interestLevel"] = "Outcome",
            ["rating"] = "Outcome",
            ["budgetDiscussed"] = "Outcome",
            ["nextAction"] = "Outcome",
        }));
    }

    [PermissionAction(ObjectAction.View)]
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<SiteVisitDto>>> Query(QueryRequest request, CancellationToken ct)
    {
        return Ok(await RunQueryAsync(
            Base(),
            request,
            Fields,
            ToDto,
            defaultSortPath: "ScheduledAt",
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["scheduled"] = await filtered.CountAsync(v =>
                    v.Status == VisitStatuses.Scheduled || v.Status == VisitStatuses.Confirmed, ct),
                ["completed"] = await filtered.CountAsync(v => v.Status == VisitStatuses.Completed, ct),
                ["noShow"] = await filtered.CountAsync(v => v.Status == VisitStatuses.NoShow, ct),
                ["highInterest"] = await filtered.CountAsync(v => v.InterestLevel == InterestLevels.High, ct),
            },
            cancellationToken: ct));
    }

    /// <summary>Conversion funnel from scheduled through to high interest.</summary>
    [HttpGet("insights")]
    public async Task<ActionResult<SiteVisitInsightsDto>> Insights(
        [FromQuery] int days = 90,
        CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 7, 730));

        var visits = await Db.SiteVisits
            .Where(v => v.ScheduledAt >= since)
            .Select(v => new
            {
                v.Status, v.InterestLevel, v.Rating, v.VisitType,
                v.ScheduledAt, v.CheckInAt, v.CheckOutAt,
                ProjectName = v.Project != null ? v.Project.Name : "Unassigned",
            })
            .ToListAsync(ct);

        var completed = visits.Count(v => v.Status == VisitStatuses.Completed);
        var noShow = visits.Count(v => v.Status == VisitStatuses.NoShow);
        var durations = visits
            .Where(v => v.CheckInAt is not null && v.CheckOutAt is not null)
            .Select(v => (v.CheckOutAt!.Value - v.CheckInAt!.Value).TotalMinutes)
            .ToList();

        var byProject = visits
            .GroupBy(v => v.ProjectName)
            .Select(g => new VisitProjectStatDto(
                g.Key,
                g.Count(),
                g.Count(v => v.Status == VisitStatuses.Completed),
                g.Count(v => v.InterestLevel == InterestLevels.High),
                g.Any(v => v.Rating is not null)
                    ? Math.Round(g.Where(v => v.Rating is not null).Average(v => v.Rating!.Value), 2)
                    : 0))
            .OrderByDescending(p => p.Visits)
            .Take(12)
            .ToList();

        var byWeekday = visits
            .GroupBy(v => v.ScheduledAt.DayOfWeek)
            .Select(g => new AgendaBucketDto(g.Key.ToString(), g.Key.ToString()[..3], g.Count()))
            .OrderBy(b => Array.IndexOf(Enum.GetNames<DayOfWeek>(), b.Key))
            .ToList();

        return Ok(new SiteVisitInsightsDto(
            visits.Count,
            completed,
            noShow,
            visits.Count == 0 ? 0 : Math.Round(completed * 100.0 / visits.Count, 1),
            visits.Count == 0 ? 0 : Math.Round(noShow * 100.0 / visits.Count, 1),
            durations.Count == 0 ? 0 : Math.Round(durations.Average(), 0),
            visits.Count(v => v.InterestLevel == InterestLevels.High),
            byProject,
            byWeekday));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<SiteVisitDto>> GetOne(int id, CancellationToken ct)
    {
        var visit = await Base().FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Site visit");

        return Ok(ToDto(visit));
    }

    /* ------------------------------------------------------------------ *
     * Writes
     * ------------------------------------------------------------------ */

    [HttpPost]
    public async Task<ActionResult<SiteVisitDto>> Create(SiteVisitInput input, CancellationToken ct)
    {
        var branch = await RequireBranchAsync(input.BranchId, ct);

        var visit = new SiteVisit
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            VisitCode = Code("SV"),
        };

        await ApplyAsync(visit, input, ct);

        // Checked here rather than trusted from the client: the availability
        // grid can go stale between rendering and pressing Schedule.
        if (!input.AllowOverlap && visit.Status != VisitStatuses.Cancelled)
        {
            await SchedulingController.EnsureFreeAsync(
                Db, visit.HostId, visit.ScheduledAt, visit.DurationMinutes,
                $"{visit.VisitorName}'s site visit", ct);
        }

        Db.SiteVisits.Add(visit);
        await Db.SaveChangesAsync(ct);

        visit.Branch = branch;
        return CreatedAtAction(nameof(GetOne), new { id = visit.Id }, ToDto(visit));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<SiteVisitDto>> Update(int id, SiteVisitInput input, CancellationToken ct)
    {
        var visit = await Db.SiteVisits
            .Include(v => v.Project).Include(v => v.Unit)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Site visit");

        var branch = await RequireBranchAsync(input.BranchId, ct);
        visit.BranchId = branch.Id;
        await ApplyAsync(visit, input, ct);

        await Db.SaveChangesAsync(ct);

        visit.Branch = branch;
        return Ok(ToDto(visit));
    }

    /// <summary>Field check-in — flips status and stamps the arrival time.</summary>
    [HttpPatch("{id:int}/check-in")]
    public async Task<ActionResult<SiteVisitDto>> CheckIn(int id, CancellationToken ct)
    {
        var visit = await Db.SiteVisits
            .Include(v => v.Branch).Include(v => v.Project).Include(v => v.Unit)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Site visit");

        if (visit.Status is VisitStatuses.Cancelled or VisitStatuses.NoShow)
        {
            throw ApiException.Conflict($"This visit is marked {visit.Status} and cannot be checked in.");
        }

        visit.CheckInAt ??= DateTime.UtcNow;
        visit.Status = VisitStatuses.Confirmed;

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(visit));
    }

    /// <summary>Check-out closes the visit and captures the outcome in one move.</summary>
    [HttpPatch("{id:int}/check-out")]
    public async Task<ActionResult<SiteVisitDto>> CheckOut(
        int id,
        CheckOutRequest request,
        CancellationToken ct)
    {
        var visit = await Db.SiteVisits
            .Include(v => v.Branch).Include(v => v.Project).Include(v => v.Unit)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Site visit");

        if (visit.CheckInAt is null)
        {
            throw ApiException.Conflict("This visit has not been checked in yet.");
        }

        visit.CheckOutAt = DateTime.UtcNow;
        visit.Status = VisitStatuses.Completed;
        visit.Feedback = request.Feedback ?? visit.Feedback;
        visit.InterestLevel = request.InterestLevel is null
            ? visit.InterestLevel
            : Require(request.InterestLevel, InterestLevels.All, "interest level");
        visit.Rating = request.Rating ?? visit.Rating;
        visit.BudgetDiscussed = request.BudgetDiscussed ?? visit.BudgetDiscussed;
        visit.NextAction = request.NextAction ?? visit.NextAction;

        await LogLeadActivityAsync(
            visit.LeadId,
            LeadActivityTypes.SiteVisit,
            $"Site visit {visit.VisitCode} completed at check-out.",
            ct);

        await AdvanceLeadToSiteVisitAsync(visit.LeadId, visit.VisitCode, ct);

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(visit));
    }

    /// <summary>
    /// A completed visit is the strongest buying signal there is, so the lead
    /// moves stage automatically rather than waiting for a manual edit — but
    /// only forward out of the two stages that precede it, so a booked lead is
    /// never dragged backwards by a late-logged visit.
    /// </summary>
    private async Task AdvanceLeadToSiteVisitAsync(int? leadId, string visitCode, CancellationToken ct)
    {
        if (leadId is not int id) return;

        var lead = await Db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct);
        if (lead is null || !LeadStages.BeforeSiteVisit.Contains(lead.Stage)) return;

        Db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.StageChange,
            FromStage = lead.Stage,
            ToStage = LeadStages.SiteVisit,
            Remarks = $"Site visit {visitCode} completed.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        lead.Stage = LeadStages.SiteVisit;
    }

    /// <summary>
    /// Move a visit to any status from wherever it is listed — the visits grid,
    /// the lead record, the day view.
    ///
    /// One endpoint rather than complete/cancel/reschedule siblings: the states
    /// share the same guards and the same timeline write, and splitting them
    /// would mean three places to keep those in step.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<SiteVisitDto>> SetStatus(
        int id,
        VisitStatusRequest request,
        CancellationToken ct)
    {
        var visit = await Db.SiteVisits
            .Include(v => v.Branch).Include(v => v.Project).Include(v => v.Unit)
            .FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Site visit");

        var status = Require(request.Status, VisitStatuses.All, "status");
        var previous = visit.Status;

        if (status == previous && request.ScheduledAt is null)
        {
            return Ok(ToDto(visit));
        }

        switch (status)
        {
            case VisitStatuses.Rescheduled:
                if (request.ScheduledAt is not DateTime moved)
                {
                    throw ApiException.BadRequest("Pick a new date and time to reschedule to.");
                }

                visit.ScheduledAt = moved;
                // A moved visit has not happened yet, so any arrival stamped
                // against the old slot would misreport the new one.
                visit.CheckInAt = null;
                visit.CheckOutAt = null;
                break;

            case VisitStatuses.Completed:
                // Completing straight from the list is normal — plenty of reps
                // never check in — so the stamps are filled in rather than
                // demanded.
                visit.CheckInAt ??= request.ScheduledAt ?? visit.ScheduledAt;
                visit.CheckOutAt ??= DateTime.UtcNow;
                break;

            case VisitStatuses.Cancelled:
            case VisitStatuses.NoShow:
                visit.CancellationReason = request.Reason ?? visit.CancellationReason;
                break;

            case VisitStatuses.Scheduled:
            case VisitStatuses.Confirmed:
                // Reopening a closed visit clears the closing stamps, otherwise
                // it reads as both open and checked out.
                if (previous is VisitStatuses.Completed or VisitStatuses.Cancelled
                    or VisitStatuses.NoShow)
                {
                    visit.CheckOutAt = null;
                    visit.CancellationReason = null;
                }

                if (request.ScheduledAt is DateTime slot) visit.ScheduledAt = slot;
                break;
        }

        visit.Status = status;

        await LogLeadActivityAsync(
            visit.LeadId,
            LeadActivityTypes.SiteVisit,
            VisitRemark("Site visit", visit.VisitCode, previous, status, visit.ScheduledAt, request.Reason),
            ct);

        // A completed visit is the strongest buying signal there is, so the
        // lead moves stage automatically — the same rule check-out applies.
        if (status == VisitStatuses.Completed) await AdvanceLeadToSiteVisitAsync(visit.LeadId, visit.VisitCode, ct);

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(visit));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var visit = await Db.SiteVisits.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Site visit");

        SoftDelete(visit);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ApplyAsync(SiteVisit visit, SiteVisitInput input, CancellationToken ct)
    {
        visit.LeadId = input.LeadId;
        visit.ContactId = input.ContactId;
        visit.OpportunityId = input.OpportunityId;
        visit.ProjectId = input.ProjectId;
        visit.UnitId = input.UnitId;
        visit.VisitorName = input.VisitorName.Trim();
        visit.VisitorPhone = input.VisitorPhone;
        visit.PartySize = Math.Clamp(input.PartySize, 1, 50);
        visit.VisitType = Require(input.VisitType, VisitTypes.All, "visit type");
        visit.Status = Require(input.Status, VisitStatuses.All, "status");
        visit.ScheduledAt = input.ScheduledAt;
        visit.DurationMinutes = Math.Clamp(input.DurationMinutes, 15, 480);
        visit.CheckInAt = input.CheckInAt;
        visit.CheckOutAt = input.CheckOutAt;
        visit.TransportMode = input.TransportMode;
        visit.PickupLocation = input.PickupLocation;
        visit.Feedback = input.Feedback;
        visit.InterestLevel = input.InterestLevel is null
            ? null
            : Require(input.InterestLevel, InterestLevels.All, "interest level");
        visit.Rating = input.Rating is null ? null : Math.Clamp(input.Rating.Value, 1, 5);
        visit.BudgetDiscussed = input.BudgetDiscussed;
        visit.NextAction = input.NextAction;
        visit.CancellationReason = input.CancellationReason;

        visit.HostId = input.HostId;
        visit.HostName = input.HostId is null
            ? null
            : (await Db.Users.FindAsync([input.HostId], ct))?.Name;
    }
}

public record CheckOutRequest(
    string? Feedback,
    string? InterestLevel,
    int? Rating,
    decimal? BudgetDiscussed,
    string? NextAction
);

public record VisitProjectStatDto(
    string ProjectName,
    int Visits,
    int Completed,
    int HighInterest,
    double AverageRating
);

public record SiteVisitInsightsDto(
    int Total,
    int Completed,
    int NoShow,
    double CompletionRate,
    double NoShowRate,
    double AverageDurationMinutes,
    int HighInterest,
    IReadOnlyList<VisitProjectStatDto> ByProject,
    IReadOnlyList<AgendaBucketDto> ByWeekday
);
