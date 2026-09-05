using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Date-level holds and bookings for spaces.
///
/// A banquet hall is sold many times a year; <see cref="Unit.Status"/> is the
/// catalogue standing, and this service answers "free on the 14th evening?".
/// </summary>
public class SpaceBookingService(AppDbContext db)
{
    public static string GroupRefForQuotation(int quotationId) => $"Q-{quotationId}";

    public static string GroupRefManual(string suffix) => $"G-{suffix}";

    /// <summary>
    /// Holds or confirms every date in the quotation's event window for its space.
    /// </summary>
    public async Task UpsertForQuotationAsync(
        Quotation quotation,
        string status,
        DateTime? holdExpiresAt,
        CancellationToken ct = default)
    {
        if (quotation.UnitId is not int unitId) return;
        if (quotation.EventDate is not DateTime startDt) return;
        if (quotation.ProjectId is not int projectId)
        {
            projectId = await db.Units.AsNoTracking()
                .Where(u => u.Id == unitId)
                .Select(u => u.ProjectId)
                .FirstOrDefaultAsync(ct);
            if (projectId == 0) return;
        }

        var slot = string.IsNullOrWhiteSpace(quotation.EventSlot)
            ? EventSlots.Evening
            : RequireSlot(quotation.EventSlot);

        var start = DateOnly.FromDateTime(startDt);
        var end = quotation.EventEndDate is DateTime endDt
            ? DateOnly.FromDateTime(endDt)
            : start;
        if (end < start) end = start;

        var groupRef = GroupRefForQuotation(quotation.Id);

        await ReplaceGroupAsync(
            groupRef,
            projectId,
            [unitId],
            start,
            end,
            slot,
            status,
            holdExpiresAt,
            quotation.LeadId,
            quotation.ContactId,
            quotation.Id,
            quotation.CustomerName,
            quotation.EventType,
            quotation.GuestCount > 0 ? quotation.GuestCount : null,
            $"{quotation.QuoteNumber} · {status}",
            ct);
    }

    public async Task ReleaseForQuotationAsync(int quotationId, CancellationToken ct = default)
    {
        var groupRef = GroupRefForQuotation(quotationId);
        var rows = await db.SpaceBookings
            .Where(b => b.CompanyId == db.Tenant.CompanyId && b.GroupRef == groupRef)
            .ToListAsync(ct);
        db.SpaceBookings.RemoveRange(rows);
    }

    /// <summary>
    /// Manual board hold/book for one date. Returns the row written.
    /// </summary>
    public async Task<SpaceBooking> UpsertManualAsync(
        Unit unit,
        DateOnly eventDate,
        string slot,
        string status,
        DateTime? holdExpiresAt,
        int? leadId,
        int? contactId,
        int? quotationId,
        string? clientName,
        string? notes,
        CancellationToken ct = default)
    {
        slot = RequireSlot(slot);
        var groupRef = quotationId is int qid
            ? GroupRefForQuotation(qid)
            : $"M-{unit.Id}-{eventDate:yyyyMMdd}-{slot}";

        var rows = await ReplaceGroupAsync(
            groupRef,
            unit.ProjectId,
            [unit.Id],
            eventDate,
            eventDate,
            slot,
            status,
            holdExpiresAt,
            leadId,
            contactId,
            quotationId,
            clientName,
            eventType: null,
            guestCount: null,
            notes,
            ct);

        return rows[0];
    }

    /// <summary>
    /// Multi-day / multi-space diary move (hold, book, confirm, blackout).
    /// </summary>
    public async Task<IReadOnlyList<SpaceBooking>> UpsertRangeAsync(
        int projectId,
        IReadOnlyList<int> unitIds,
        DateOnly start,
        DateOnly end,
        string slot,
        string status,
        DateTime? holdExpiresAt,
        int? leadId,
        int? contactId,
        int? quotationId,
        string? clientName,
        string? eventType,
        int? guestCount,
        string? notes,
        string? groupRef,
        CancellationToken ct = default)
    {
        if (unitIds.Count == 0)
            throw ApiException.BadRequest("Pick at least one space.");

        if (end < start) end = start;
        slot = RequireSlot(slot);

        var allowed = new[]
        {
            UnitStatuses.Held, UnitStatuses.Booked, UnitStatuses.Sold,
            UnitStatuses.Blocked, UnitStatuses.Blackout,
        };
        if (!allowed.Contains(status, StringComparer.OrdinalIgnoreCase))
            throw ApiException.BadRequest($"Cannot place status '{status}' on the diary.");

        groupRef ??= GroupRefManual($"{Guid.NewGuid():N}"[..12]);

        return await ReplaceGroupAsync(
            groupRef,
            projectId,
            unitIds.Distinct().ToList(),
            start,
            end,
            slot,
            status,
            holdExpiresAt,
            leadId,
            contactId,
            quotationId,
            clientName,
            eventType,
            guestCount,
            notes,
            ct);
    }

    public async Task ReleaseGroupAsync(string groupRef, CancellationToken ct = default)
    {
        var rows = await db.SpaceBookings
            .Where(b => b.CompanyId == db.Tenant.CompanyId && b.GroupRef == groupRef)
            .ToListAsync(ct);
        db.SpaceBookings.RemoveRange(rows);
    }

    public async Task ReleaseBookingAsync(int spaceBookingId, CancellationToken ct = default)
    {
        var row = await db.SpaceBookings
            .FirstOrDefaultAsync(b => b.Id == spaceBookingId, ct)
            ?? throw ApiException.NotFound("Space booking");

        if (!string.IsNullOrEmpty(row.GroupRef))
        {
            await ReleaseGroupAsync(row.GroupRef, ct);
            return;
        }

        db.SpaceBookings.Remove(row);
    }

    /// <summary>Conflicts for a proposed window — used by public enquire and diary.</summary>
    public async Task<IReadOnlyList<SpaceConflictDto>> FindConflictsAsync(
        int? projectId,
        IReadOnlyList<int>? unitIds,
        DateOnly start,
        DateOnly end,
        string slot,
        string? ignoreGroupRef,
        CancellationToken ct = default)
    {
        slot = RequireSlot(slot);
        if (end < start) end = start;

        var query = db.SpaceBookings.AsNoTracking()
            .Where(b => b.EventDate >= start && b.EventDate <= end);

        if (projectId is int pid) query = query.Where(b => b.ProjectId == pid);
        if (unitIds is { Count: > 0 }) query = query.Where(b => unitIds.Contains(b.UnitId));
        if (!string.IsNullOrEmpty(ignoreGroupRef))
            query = query.Where(b => b.GroupRef != ignoreGroupRef);

        var rows = await query.Include(b => b.Unit).ToListAsync(ct);
        var now = DateTime.UtcNow;

        return rows
            .Where(b => b.IsBlocking(now) && EventSlots.Overlaps(b.Slot, slot))
            .Select(b => new SpaceConflictDto(
                b.UnitId,
                b.Unit?.UnitNumber ?? $"#{b.UnitId}",
                b.EventDate,
                b.Slot,
                b.Status,
                b.ClientName))
            .ToList();
    }

    public async Task<bool> IsPeakAsync(int projectId, DateOnly day, CancellationToken ct)
    {
        return await db.VenuePeakDates.AsNoTracking()
            .AnyAsync(p => p.ProjectId == projectId
                && p.StartDate <= day
                && p.EndDate >= day, ct);
    }

    public async Task<decimal> PeakPremiumAsync(int projectId, DateOnly day, CancellationToken ct)
    {
        var peak = await db.VenuePeakDates.AsNoTracking()
            .Where(p => p.ProjectId == projectId && p.StartDate <= day && p.EndDate >= day)
            .OrderByDescending(p => p.PremiumFraction)
            .FirstOrDefaultAsync(ct);

        return peak?.PremiumFraction ?? 0m;
    }

    private async Task<List<SpaceBooking>> ReplaceGroupAsync(
        string groupRef,
        int projectId,
        IReadOnlyList<int> unitIds,
        DateOnly start,
        DateOnly end,
        string slot,
        string status,
        DateTime? holdExpiresAt,
        int? leadId,
        int? contactId,
        int? quotationId,
        string? clientName,
        string? eventType,
        int? guestCount,
        string? notes,
        CancellationToken ct)
    {
        var existing = await db.SpaceBookings
            .Where(b => b.CompanyId == db.Tenant.CompanyId && b.GroupRef == groupRef)
            .ToListAsync(ct);
        db.SpaceBookings.RemoveRange(existing);

        var units = await db.Units
            .Where(u => unitIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, ct);

        var written = new List<SpaceBooking>();

        foreach (var unitId in unitIds)
        {
            if (!units.TryGetValue(unitId, out var unit))
                throw ApiException.NotFound("Space");

            if (unit.Status is UnitStatuses.NotForSale)
                throw ApiException.Conflict($"{unit.UnitNumber} is not bookable.");

            for (var day = start; day <= end; day = day.AddDays(1))
            {
                await EnsureNoConflictAsync(unitId, day, slot, groupRef, ct);

                // Turnaround: block adjacent same-day sessions when hours &gt; 0 and
                // another booking sits on an overlapping slot the same day — already
                // covered by Overlaps. Cross-day turnaround soft-warns via notes.
                var row = new SpaceBooking
                {
                    CompanyId = db.Tenant.CompanyId,
                    UnitId = unitId,
                    ProjectId = projectId,
                    EventDate = day,
                    Slot = slot,
                    Status = status,
                    GroupRef = groupRef,
                    HoldExpiresAt = status == UnitStatuses.Held ? holdExpiresAt : null,
                    LeadId = leadId,
                    ContactId = contactId,
                    QuotationId = quotationId,
                    ClientName = clientName,
                    EventType = eventType,
                    GuestCount = guestCount,
                    OwnerId = db.Tenant.UserId,
                    Notes = notes,
                };
                db.SpaceBookings.Add(row);
                written.Add(row);
            }
        }

        return written;
    }

    private async Task EnsureNoConflictAsync(
        int unitId,
        DateOnly day,
        string slot,
        string groupRef,
        CancellationToken ct)
    {
        var others = await db.SpaceBookings
            .Where(b => b.UnitId == unitId && b.EventDate == day && b.GroupRef != groupRef)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var clash = others.FirstOrDefault(b =>
            b.IsBlocking(now) && EventSlots.Overlaps(b.Slot, slot));

        if (clash is null) return;

        throw ApiException.Conflict(
            $"That space is already {UnitStatuses.Label(clash.Status).ToLowerInvariant()} on {day:dd MMM} "
            + $"({clash.Slot}) for {clash.ClientName ?? "another client"}.");
    }

    private static string RequireSlot(string slot) =>
        EventSlots.All.Contains(slot, StringComparer.OrdinalIgnoreCase)
            ? EventSlots.All.First(s => s.Equals(slot, StringComparison.OrdinalIgnoreCase))
            : throw ApiException.BadRequest($"Unknown event slot '{slot}'.");
}
