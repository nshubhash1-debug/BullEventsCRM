using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Week/month diary for venue spaces — the operator home screen.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
[SecuredBy(SecuredObjects.Unit)]
public class InventoryDiaryController(AppDbContext db, SpaceBookingService spaceBookings)
    : CrmControllerBase(db)
{
    [HttpGet("projects/{projectId:int}/diary")]
    public async Task<ActionResult<InventoryDiaryDto>> Diary(
        int projectId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] string? slot,
        CancellationToken ct)
    {
        var project = await Db.Projects.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw ApiException.NotFound("Venue");

        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var end = to ?? start.AddDays(6);
        if (end < start) end = start;
        if (end > start.AddDays(62))
            throw ApiException.BadRequest("Diary window cannot exceed 63 days.");

        var session = string.IsNullOrWhiteSpace(slot) ? EventSlots.Evening : slot;
        if (!EventSlots.All.Contains(session, StringComparer.OrdinalIgnoreCase))
            throw ApiException.BadRequest($"Unknown slot '{slot}'.");

        var spaces = await Db.Units.AsNoTracking()
            .Where(u => u.ProjectId == projectId && u.Status != UnitStatuses.NotForSale)
            .OrderBy(u => u.UnitNumber)
            .ToListAsync(ct);

        var bookings = await Db.SpaceBookings.AsNoTracking()
            .Where(b => b.ProjectId == projectId
                && b.EventDate >= start
                && b.EventDate <= end)
            .ToListAsync(ct);

        var peaks = await Db.VenuePeakDates.AsNoTracking()
            .Where(p => p.ProjectId == projectId
                && p.EndDate >= start
                && p.StartDate <= end)
            .OrderBy(p => p.StartDate)
            .Select(p => new VenuePeakWindowDto(
                p.Id, p.StartDate, p.EndDate, p.Label, p.PremiumFraction))
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var rows = spaces.Select(space =>
        {
            var days = new List<DiaryCellDto>();
            for (var day = start; day <= end; day = day.AddDays(1))
            {
                var match = bookings
                    .Where(b => b.UnitId == space.Id
                        && b.EventDate == day
                        && b.IsBlocking(now)
                        && EventSlots.Overlaps(b.Slot, session))
                    .OrderByDescending(b => b.Status is UnitStatuses.Sold or UnitStatuses.Booked)
                    .ThenByDescending(b => b.Id)
                    .FirstOrDefault();

                var isPeak = peaks.Any(p => p.StartDate <= day && p.EndDate >= day);

                days.Add(match is null
                    ? new DiaryCellDto(
                        day, UnitStatuses.Available, null, null, session, null,
                        null, null, null, null, isPeak, null)
                    : new DiaryCellDto(
                        day,
                        match.Status,
                        match.ClientName,
                        match.EventType,
                        match.Slot,
                        match.GuestCount,
                        match.HoldExpiresAt,
                        match.Id,
                        match.LeadId,
                        match.QuotationId,
                        isPeak,
                        match.Notes));
            }

            return new DiarySpaceRowDto(
                space.Id,
                space.UnitNumber,
                space.Configuration,
                space.SeatingCapacity,
                space.FloatingCapacity,
                space.IsOutdoor,
                space.IsAirConditioned,
                space.MinimumPlates,
                space.PricePerPlate,
                space.BasePrice,
                space.TurnaroundHours,
                space.Status,
                days);
        }).ToList();

        return Ok(new InventoryDiaryDto(
            project.Id,
            project.Name,
            start,
            end,
            session,
            peaks,
            rows));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("projects/{projectId:int}/diary/moves")]
    public async Task<ActionResult<object>> Move(
        int projectId,
        DiaryMoveRequest request,
        CancellationToken ct)
    {
        _ = await Db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
            ?? throw ApiException.NotFound("Venue");

        var unitIds = new List<int> { request.UnitId };
        if (request.AdditionalUnitIds is { Count: > 0 })
            unitIds.AddRange(request.AdditionalUnitIds);

        var end = request.EventEndDate ?? request.EventDate;
        var holdUntil = request.Status == UnitStatuses.Held
            ? DateTime.UtcNow.AddHours(Math.Clamp(request.HoldHours ?? 48, 1, 336))
            : (DateTime?)null;

        var rows = await spaceBookings.UpsertRangeAsync(
            projectId,
            unitIds,
            request.EventDate,
            end,
            request.Slot,
            request.Status,
            holdUntil,
            request.LeadId,
            request.ContactId,
            request.QuotationId,
            request.ClientName,
            request.EventType,
            request.GuestCount,
            request.Notes,
            groupRef: null,
            ct);

        await Db.SaveChangesAsync(ct);

        return Ok(new
        {
            groupRef = rows[0].GroupRef,
            count = rows.Count,
            message = $"{rows.Count} calendar row(s) set to {UnitStatuses.Label(request.Status)}.",
        });
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpDelete("space-bookings/{id:int}")]
    public async Task<IActionResult> Release(int id, CancellationToken ct)
    {
        await spaceBookings.ReleaseBookingAsync(id, ct);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("projects/{projectId:int}/conflicts")]
    public async Task<ActionResult<IReadOnlyList<SpaceConflictDto>>> Conflicts(
        int projectId,
        [FromQuery] DateOnly eventDate,
        [FromQuery] DateOnly? eventEndDate,
        [FromQuery] string? slot,
        [FromQuery] int? unitId,
        CancellationToken ct)
    {
        var ids = unitId is int uid ? new List<int> { uid } : null;
        var list = await spaceBookings.FindConflictsAsync(
            projectId,
            ids,
            eventDate,
            eventEndDate ?? eventDate,
            slot ?? EventSlots.Evening,
            ignoreGroupRef: null,
            ct);
        return Ok(list);
    }

    /// <summary>Event-shaped space dossier extras layered onto the board dossier.</summary>
    [HttpGet("units/{id:int}/event-dossier")]
    public async Task<ActionResult<SpaceDossierExtraDto>> EventDossier(int id, CancellationToken ct)
    {
        var unit = await Db.Units.AsNoTracking()
            .Include(u => u.Project)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ApiException.NotFound("Space");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcoming = await Db.SpaceBookings.AsNoTracking()
            .Where(b => b.UnitId == id && b.EventDate >= today)
            .OrderBy(b => b.EventDate)
            .Take(20)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var cells = upcoming
            .Where(b => b.IsBlocking(now))
            .Select(b => new DiaryCellDto(
                b.EventDate,
                b.Status,
                b.ClientName,
                b.EventType,
                b.Slot,
                b.GuestCount,
                b.HoldExpiresAt,
                b.Id,
                b.LeadId,
                b.QuotationId,
                false,
                b.Notes))
            .ToList();

        var project = unit.Project;

        return Ok(new SpaceDossierExtraDto(
            unit.SeatingCapacity,
            unit.FloatingCapacity,
            unit.TheatreCapacity,
            unit.IsOutdoor,
            unit.IsAirConditioned,
            unit.HasStage,
            unit.HasAttachedKitchen,
            unit.MinimumPlates,
            unit.PricePerPlate,
            unit.PeakDatePremium,
            unit.SecurityDeposit,
            unit.BasePrice,
            unit.TurnaroundHours,
            unit.LayoutImageUrl,
            project?.AllowsOutsideCatering ?? false,
            project?.AllowsAlcohol ?? false,
            project?.AllowsOpenFlame ?? false,
            project?.NoiseCurfew,
            project?.ParkingCapacity,
            project?.GuestRooms,
            cells));
    }
}
