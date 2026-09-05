using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Everything scheduled, from every object, in one window.
///
/// Distinct from <c>SchedulingController</c>, which answers "is this person free
/// at 3pm" for the booking flow and therefore only counts the things that block
/// a slot. A calendar has the opposite job: it should show the task due on
/// Thursday even though a task blocks nothing, because the person reading it
/// wants to know what their week holds — not whether it can be booked over.
/// </summary>
[ApiController]
[Route("api/calendar")]
[Authorize]
[RequireModule(Modules.Calendar)]
public class CalendarController(AppDbContext db) : CrmControllerBase(db)
{
    /// <summary>Widest window the grid will ask for, guarding a year-long fetch.</summary>
    private const int MaxWindowDays = 120;

    [HttpGet("events")]
    public async Task<ActionResult<CalendarResponse>> Events(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        [FromQuery] int? ownerId,
        [FromQuery] int? branchId,
        /// <summary>Comma-separated: siteVisit, obmVisit, followUp.</summary>
        [FromQuery] string? sources,
        /// <summary>Comma-separated follow-up channels, e.g. Task,Call.</summary>
        [FromQuery] string? channels,
        [FromQuery] bool includeCancelled,
        CancellationToken cancellationToken = default)
    {
        var start = (from ?? DateTime.UtcNow.Date).Date;
        var end = (to ?? start.AddDays(42)).Date.AddDays(1);

        if (end <= start) throw ApiException.BadRequest("The window ends before it starts.");
        if ((end - start).TotalDays > MaxWindowDays)
        {
            throw ApiException.BadRequest($"Ask for at most {MaxWindowDays} days at a time.");
        }

        var wanted = Split(sources);
        var wantedChannels = Split(channels);

        var events = new List<CalendarEventDto>();
        var now = DateTime.UtcNow;

        /* ---------------- site visits ---------------- */

        if (Includes(wanted, "siteVisit"))
        {
            var visits = await Db.SiteVisits
                .Where(v => v.ScheduledAt >= start && v.ScheduledAt < end)
                .Where(v => ownerId == null || v.HostId == ownerId)
                .Where(v => branchId == null || v.BranchId == branchId)
                .Where(v => includeCancelled || v.Status != VisitStatuses.Cancelled)
                .Select(v => new
                {
                    v.Id,
                    v.VisitCode,
                    v.VisitorName,
                    v.ScheduledAt,
                    v.DurationMinutes,
                    v.Status,
                    v.VisitType,
                    v.HostId,
                    v.HostName,
                    v.BranchId,
                    BranchName = v.Branch != null ? v.Branch.Name : null,
                    ProjectName = v.Project != null ? v.Project.Name : null,
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            events.AddRange(visits.Select(v => new CalendarEventDto(
                $"siteVisit:{v.Id}",
                "siteVisit",
                v.Id,
                $"{v.VisitorName} · {v.VisitCode}",
                v.ProjectName,
                v.ScheduledAt,
                v.ScheduledAt.AddMinutes(v.DurationMinutes),
                IsPoint: false,
                v.Status,
                v.VisitType,
                Priority: null,
                v.HostId,
                v.HostName,
                v.BranchId,
                v.BranchName,
                "/dashboard/engagement/site-visits",
                v.ProjectName,
                IsOverdue: v.ScheduledAt < now && IsOpenVisit(v.Status))));
        }

        /* ---------------- OBM visits ---------------- */

        if (Includes(wanted, "obmVisit"))
        {
            var visits = await Db.ObmVisits
                .Where(v => v.ScheduledAt >= start && v.ScheduledAt < end)
                .Where(v => ownerId == null || v.AgentId == ownerId)
                .Where(v => branchId == null || v.BranchId == branchId)
                .Where(v => includeCancelled || v.Status != VisitStatuses.Cancelled)
                .Select(v => new
                {
                    v.Id,
                    v.VisitCode,
                    v.PartnerName,
                    v.PartnerType,
                    v.LocationLabel,
                    v.ScheduledAt,
                    v.DurationMinutes,
                    v.Status,
                    v.AgentId,
                    v.AgentName,
                    v.BranchId,
                    BranchName = v.Branch != null ? v.Branch.Name : null,
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            events.AddRange(visits.Select(v => new CalendarEventDto(
                $"obmVisit:{v.Id}",
                "obmVisit",
                v.Id,
                $"{v.PartnerName} · {v.VisitCode}",
                v.LocationLabel,
                v.ScheduledAt,
                v.ScheduledAt.AddMinutes(v.DurationMinutes),
                IsPoint: false,
                v.Status,
                v.PartnerType,
                Priority: null,
                v.AgentId,
                v.AgentName,
                v.BranchId,
                v.BranchName,
                "/dashboard/engagement/obm-visits",
                v.LocationLabel,
                IsOverdue: v.ScheduledAt < now && IsOpenVisit(v.Status))));
        }

        /* ---------------- follow-ups and tasks ---------------- */

        if (Includes(wanted, "followUp"))
        {
            var query = Db.FollowUps
                .Where(f => f.DueAt >= start && f.DueAt < end)
                .Where(f => ownerId == null || f.OwnerId == ownerId)
                .Where(f => branchId == null || f.BranchId == branchId)
                .Where(f => includeCancelled || f.Status != FollowUpStatuses.Cancelled);

            if (wantedChannels.Count > 0)
            {
                query = query.Where(f => wantedChannels.Contains(f.Channel));
            }

            var followUps = await query
                .Select(f => new
                {
                    f.Id,
                    f.Subject,
                    f.RelatedName,
                    f.RelatedType,
                    f.Channel,
                    f.Status,
                    f.Priority,
                    f.DueAt,
                    f.OwnerId,
                    OwnerName = f.Owner != null ? f.Owner.Name : null,
                    f.BranchId,
                    BranchName = f.Branch != null ? f.Branch.Name : null,
                })
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            events.AddRange(followUps.Select(f => new CalendarEventDto(
                $"followUp:{f.Id}",
                "followUp",
                f.Id,
                f.Subject,
                f.RelatedName,
                f.DueAt,
                // Appointment-shaped channels hold a slot; the rest are a moment
                // on the day, and the grid draws them as a line rather than a block.
                f.Channel is FollowUpChannels.Meeting or FollowUpChannels.SiteVisit
                    ? f.DueAt.AddMinutes(30)
                    : f.DueAt,
                IsPoint: f.Channel is not (FollowUpChannels.Meeting or FollowUpChannels.SiteVisit),
                f.Status,
                f.Channel,
                f.Priority,
                f.OwnerId,
                f.OwnerName,
                f.BranchId,
                f.BranchName,
                f.Channel == FollowUpChannels.Task
                    ? "/dashboard/engagement/tasks"
                    : "/dashboard/leads/follow-ups",
                Location: null,
                IsOverdue: f.DueAt < now
                    && f.Status is FollowUpStatuses.Open or FollowUpStatuses.InProgress)));
        }

        var ordered = events.OrderBy(e => e.Start).ThenBy(e => e.Title).ToList();

        return Ok(new CalendarResponse(
            start,
            end.AddDays(-1),
            ordered,
            new CalendarSummaryDto(
                ordered.Count,
                ordered.Count(e => e.Source == "siteVisit"),
                ordered.Count(e => e.Source == "obmVisit"),
                ordered.Count(e => e.Source == "followUp"),
                ordered.Count(e => e.IsOverdue),
                ordered.Count(e => e.Status is VisitStatuses.Completed or FollowUpStatuses.Completed))));
    }

    /// <summary>
    /// Who can own something on the calendar — fills the owner filter without
    /// pulling the whole user list through the admin endpoint.
    /// </summary>
    [HttpGet("owners")]
    public async Task<ActionResult<IReadOnlyList<CalendarOwnerDto>>> Owners(
        CancellationToken cancellationToken = default)
    {
        var owners = await Db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new CalendarOwnerDto(u.Id, u.Name, u.Role))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        return Ok(owners);
    }

    /* ---------------- helpers ---------------- */

    private static bool IsOpenVisit(string status) =>
        status is VisitStatuses.Scheduled or VisitStatuses.Confirmed or VisitStatuses.Rescheduled;

    /// <summary>An empty list means "no filter", not "nothing".</summary>
    private static bool Includes(List<string> wanted, string source) =>
        wanted.Count == 0 || wanted.Contains(source, StringComparer.OrdinalIgnoreCase);

    private static List<string> Split(string? csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? []
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
}

public record CalendarOwnerDto(int Id, string Name, string Role);
