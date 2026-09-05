using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// One person's calendar, as a grid of bookable slots.
///
/// Site visits, field meetings and scheduled follow-ups all take the same
/// person's time, so availability is computed across all three rather than per
/// object. A slot is free only if nothing overlaps it — which is also what
/// stops two reps promising the same 11:00 to different customers.
/// </summary>
[ApiController]
[Route("api/scheduling")]
[Authorize]
[RequireModule(Modules.Calendar)]
public class SchedulingController(AppDbContext db) : CrmControllerBase(db)
{
    /// <summary>
    /// The working day. Slots outside it are shown but not bookable, so the
    /// grid still reads as a full day rather than starting abruptly at nine.
    /// </summary>
    private const int DayStartHour = 8;
    private const int DayEndHour = 21;
    private const int BusinessStartHour = 9;
    private const int BusinessEndHour = 19;

    /// <summary>
    /// Slot statuses, in the order they take precedence: a past slot reads as
    /// past even if something is booked in it.
    /// </summary>
    public static class SlotStates
    {
        public const string Available = "Available";
        public const string Busy = "Busy";
        public const string Past = "Past";
        public const string OutsideHours = "OutsideHours";
    }

    [HttpGet("availability")]
    public async Task<ActionResult<AvailabilityDto>> GetAvailability(
        [FromQuery] DateTime date,
        [FromQuery] int? userId,
        [FromQuery] int slotMinutes = 30,
        [FromQuery] int durationMinutes = 60,
        CancellationToken ct = default)
    {
        var step = Math.Clamp(slotMinutes, 15, 120);
        var duration = Math.Clamp(durationMinutes, step, 480);
        var owner = userId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null);

        var dayStart = date.Date;
        var dayEnd = dayStart.AddDays(1);

        var bookings = await LoadBookingsAsync(owner, dayStart, dayEnd, ct);

        var now = DateTime.UtcNow;
        var slots = new List<SlotDto>();

        for (var cursor = dayStart.AddHours(DayStartHour);
             cursor < dayStart.AddHours(DayEndHour);
             cursor = cursor.AddMinutes(step))
        {
            var slotEnd = cursor.AddMinutes(step);

            // A meeting is only bookable here if the whole thing fits, so a
            // 90-minute visit cannot be dropped into the last half hour.
            var meetingEnd = cursor.AddMinutes(duration);

            var overlapping = bookings
                .Where(b => b.Start < meetingEnd && b.End > cursor)
                .ToList();

            var state =
                slotEnd <= now ? SlotStates.Past
                : cursor.Hour < BusinessStartHour || meetingEnd.Hour > BusinessEndHour
                    || (meetingEnd.Hour == BusinessEndHour && meetingEnd.Minute > 0)
                    ? SlotStates.OutsideHours
                : overlapping.Count > 0 ? SlotStates.Busy
                : SlotStates.Available;

            slots.Add(new SlotDto(
                cursor,
                slotEnd,
                cursor.ToString("HH:mm"),
                state,
                overlapping.Select(b => new SlotBookingDto(
                    b.Kind, b.Id, b.Title, b.Subtitle, b.Start, b.End)).ToList()));
        }

        var ownerName = owner is null
            ? Db.Tenant.UserName
            : await Db.Users.Where(u => u.Id == owner).Select(u => u.Name)
                .FirstOrDefaultAsync(ct) ?? "Unassigned";

        return Ok(new AvailabilityDto(
            dayStart,
            owner,
            ownerName,
            step,
            duration,
            $"{BusinessStartHour:D2}:00",
            $"{BusinessEndHour:D2}:00",
            slots,
            slots.Count(s => s.State == SlotStates.Available),
            slots.Count(s => s.State == SlotStates.Busy),
            bookings.Count));
    }

    /// <summary>
    /// The day's meetings in order — the queue behind a busy grid, so a rep can
    /// see what they would be scheduling around.
    /// </summary>
    [HttpGet("day")]
    public async Task<ActionResult<IReadOnlyList<SlotBookingDto>>> GetDay(
        [FromQuery] DateTime date,
        [FromQuery] int? userId,
        CancellationToken ct = default)
    {
        var owner = userId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null);
        var dayStart = date.Date;

        var bookings = await LoadBookingsAsync(owner, dayStart, dayStart.AddDays(1), ct);

        return Ok(bookings
            .OrderBy(b => b.Start)
            .Select(b => new SlotBookingDto(b.Kind, b.Id, b.Title, b.Subtitle, b.Start, b.End))
            .ToList());
    }

    /* ------------------------------------------------------------------ *
     * Shared booking lookup
     * ------------------------------------------------------------------ */

    private record Booking(string Kind, int Id, string Title, string? Subtitle, DateTime Start, DateTime End);

    /// <summary>
    /// Everything holding this person's time in the window. Cancelled and
    /// no-show visits are excluded — they are not occupying anything.
    /// </summary>
    private async Task<List<Booking>> LoadBookingsAsync(
        int? userId,
        DateTime from,
        DateTime to,
        CancellationToken ct)
    {
        var bookings = new List<Booking>();

        var siteVisits = await Db.SiteVisits
            .Where(v => v.ScheduledAt >= from && v.ScheduledAt < to)
            .Where(v => v.Status != VisitStatuses.Cancelled && v.Status != VisitStatuses.NoShow)
            .Where(v => userId == null || v.HostId == userId)
            .Select(v => new
            {
                v.Id,
                v.VisitCode,
                v.VisitorName,
                v.ScheduledAt,
                v.DurationMinutes,
                ProjectName = v.Project != null ? v.Project.Name : null,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        bookings.AddRange(siteVisits.Select(v => new Booking(
            "SiteVisit",
            v.Id,
            $"{v.VisitorName} · {v.VisitCode}",
            v.ProjectName,
            v.ScheduledAt,
            v.ScheduledAt.AddMinutes(v.DurationMinutes))));

        var obmVisits = await Db.ObmVisits
            .Where(v => v.ScheduledAt >= from && v.ScheduledAt < to)
            .Where(v => v.Status != VisitStatuses.Cancelled && v.Status != VisitStatuses.NoShow)
            .Where(v => userId == null || v.AgentId == userId)
            .Select(v => new
            {
                v.Id,
                v.VisitCode,
                v.PartnerName,
                v.LocationLabel,
                v.ScheduledAt,
                v.DurationMinutes,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        bookings.AddRange(obmVisits.Select(v => new Booking(
            "ObmVisit",
            v.Id,
            $"{v.PartnerName} · {v.VisitCode}",
            v.LocationLabel,
            v.ScheduledAt,
            v.ScheduledAt.AddMinutes(v.DurationMinutes))));

        // Only the follow-ups that are actually an appointment. A "send the
        // quotation" task has a due date but does not block a time slot.
        var meetings = await Db.FollowUps
            .Where(f => f.DueAt >= from && f.DueAt < to)
            .Where(f => f.Status == FollowUpStatuses.Open || f.Status == FollowUpStatuses.InProgress)
            .Where(f => f.Channel == FollowUpChannels.Meeting || f.Channel == FollowUpChannels.SiteVisit)
            .Where(f => userId == null || f.OwnerId == userId)
            .Select(f => new { f.Id, f.Subject, f.RelatedName, f.DueAt })
            .AsNoTracking()
            .ToListAsync(ct);

        bookings.AddRange(meetings.Select(f => new Booking(
            "FollowUp",
            f.Id,
            f.Subject,
            f.RelatedName,
            f.DueAt,
            f.DueAt.AddMinutes(30))));

        return bookings;
    }

    /* ------------------------------------------------------------------ *
     * Conflict checking, used by the visit controllers on write
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Throws if the person already has something in the window.
    ///
    /// Called from the create paths rather than trusted from the client: the
    /// availability grid can go stale between the moment it renders and the
    /// moment someone presses Schedule.
    /// </summary>
    public static async Task EnsureFreeAsync(
        AppDbContext db,
        int? userId,
        DateTime start,
        int durationMinutes,
        string subject,
        CancellationToken ct)
    {
        if (userId is null) return;

        var end = start.AddMinutes(durationMinutes);
        var controller = new SchedulingController(db);
        var bookings = await controller.LoadBookingsAsync(userId, start.Date, start.Date.AddDays(1), ct);

        var clash = bookings.FirstOrDefault(b => b.Start < end && b.End > start);
        if (clash is null) return;

        throw ApiException.Conflict(
            $"That slot is taken — {clash.Title} runs from {clash.Start:HH:mm} to {clash.End:HH:mm}. " +
            $"Pick another time for {subject}, or reschedule the existing meeting.");
    }
}

/* ------------------------------------------------------------------ *
 * Wire shapes
 * ------------------------------------------------------------------ */

public record SlotBookingDto(
    string Kind,
    int Id,
    string Title,
    string? Subtitle,
    DateTime Start,
    DateTime End
);

public record SlotDto(
    DateTime Start,
    DateTime End,
    string Label,
    /// <summary>Available, Busy, Past or OutsideHours.</summary>
    string State,
    IReadOnlyList<SlotBookingDto> Bookings
);

public record AvailabilityDto(
    DateTime Date,
    int? UserId,
    string UserName,
    int SlotMinutes,
    int DurationMinutes,
    string BusinessStart,
    string BusinessEnd,
    IReadOnlyList<SlotDto> Slots,
    int AvailableCount,
    int BusyCount,
    int BookingCount
);
