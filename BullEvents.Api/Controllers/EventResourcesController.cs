using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Everything committed to one event, across all four inventories at once.
///
/// The four modules each answer their own question well and none of them
/// answers the one a coordinator actually asks the morning of a wedding: what
/// is going out, who is going with it, which suppliers are due, and what is
/// carrying it. Four separate calls would work and would also mean four
/// loading states and four chances to show a stale figure, so it is one call.
/// </summary>
[ApiController]
[Route("api/event-resources")]
[Authorize]
[SecuredBy(SecuredObjects.Lead)]
public class EventResourcesController(AppDbContext db) : CrmControllerBase(db)
{
    /// <summary>Everything booked against one lead.</summary>
    [HttpGet("lead/{leadId:int}")]
    public async Task<ActionResult<EventResourceSheetDto>> ForLead(
        int leadId, CancellationToken ct)
    {
        var lead = await Db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == leadId, ct)
            ?? throw ApiException.NotFound("Lead");

        var gatePasses = await Db.PropIssues.AsNoTracking()
            .Include(i => i.Store)
            .Include(i => i.SiteInCharge)
            .Include(i => i.Lines).ThenInclude(l => l.Item).ThenInclude(x => x!.Category)
            .Include(i => i.Lines).ThenInclude(l => l.Item).ThenInclude(x => x!.Photos)
            .Where(i => i.LeadId == leadId && i.Status != PropIssueStatuses.Cancelled)
            .OrderBy(i => i.DispatchDate)
            .ToListAsync(ct);

        var crew = await Db.CrewAssignments.AsNoTracking()
            .Include(a => a.CrewMember)
            .Where(a => a.LeadId == leadId && a.Status != CrewAssignmentStatuses.Cancelled)
            .OrderBy(a => a.FromDate).ThenBy(a => a.Role)
            .ToListAsync(ct);

        var orders = await Db.VendorPurchaseOrders.AsNoTracking()
            .Include(o => o.Vendor)
            .Include(o => o.Coordinator)
            .Include(o => o.Lines)
            .Include(o => o.Payments)
            .Where(o => o.LeadId == leadId && o.Status != PurchaseOrderStatuses.Cancelled)
            .OrderBy(o => o.ServiceDate)
            .ToListAsync(ct);

        var trips = await Db.VehicleTrips.AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Include(t => t.Loads).ThenInclude(l => l.Issue).ThenInclude(i => i!.Lines)
                .ThenInclude(l => l.Item)
            .Where(t => t.LeadId == leadId && t.Status != TripStatuses.Cancelled)
            .OrderBy(t => t.FromDate)
            .ToListAsync(ct);

        var passDtos = gatePasses.Select(i => PropsController.Project(i, null)).ToList();
        var crewDtos = crew.Select(CrewController.ToDto).ToList();
        var orderDtos = orders.Select(VendorsController.ToDto).ToList();
        var tripDtos = trips.Select(FleetController.ToDto).ToList();

        var vendorCost = orderDtos.Sum(o => o.TotalCost);
        var crewCost = crewDtos.Sum(a => a.TotalCost);

        // A trip's running cost is only known once it is closed, so a planned
        // trip contributes its vehicle's day rate over the window instead —
        // otherwise a wedding still a fortnight out reads as having no
        // transport cost at all.
        var transportCost = trips.Sum(t =>
            t.TotalRunningCost > 0
                ? t.TotalRunningCost
                : (t.Vehicle?.DayRate ?? 0) * (t.ToDate.DayNumber - t.FromDate.DayNumber + 1));

        return Ok(new EventResourceSheetDto(
            leadId, lead.Name, lead.EventType,
            passDtos, crewDtos, orderDtos, tripDtos,
            passDtos.Sum(p => p.TotalIssued > 0 ? p.TotalIssued : p.TotalReserved),
            crewDtos.Count,
            vendorCost, crewCost, transportCost,
            vendorCost + crewCost + transportCost));
    }

    /// <summary>
    /// What the whole operation is committed to over a window — the day sheet.
    ///
    /// Deliberately not per lead: on a Saturday in season the useful view is
    /// every truck, every crew and every supplier due out, whoever they belong
    /// to.
    /// </summary>
    [HttpGet("day-sheet")]
    public async Task<ActionResult<object>> DaySheet(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        CancellationToken ct = default)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var end = to ?? start.AddDays(6);

        if (end < start)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var dispatches = await Db.PropIssues.AsNoTracking()
            .Where(i => i.Status != PropIssueStatuses.Cancelled
                && i.DispatchDate <= end && i.ExpectedReturnDate >= start)
            .OrderBy(i => i.DispatchDate)
            .Select(i => new
            {
                i.Id,
                i.Code,
                i.Status,
                i.EventName,
                i.ClientName,
                i.VenueName,
                i.DispatchDate,
                i.ExpectedReturnDate,
                Pieces = i.Lines.Sum(l => l.IssuedQuantity > 0 ? l.IssuedQuantity : l.ReservedQuantity),
            })
            .Take(200)
            .ToListAsync(ct);

        var crew = await Db.CrewAssignments.AsNoTracking()
            .Include(a => a.CrewMember)
            .Where(a => a.Status != CrewAssignmentStatuses.Cancelled
                && a.FromDate <= end && a.ToDate >= start)
            .OrderBy(a => a.FromDate)
            .Select(a => new
            {
                a.Id,
                Name = a.CrewMember!.Name,
                a.CrewMember.Phone,
                a.Role,
                a.Status,
                a.EventName,
                a.VenueName,
                a.FromDate,
                a.ToDate,
                a.ReportingTime,
            })
            .Take(300)
            .ToListAsync(ct);

        var orders = await Db.VendorPurchaseOrders.AsNoTracking()
            .Include(o => o.Vendor)
            .Where(o => o.Status != PurchaseOrderStatuses.Cancelled
                && o.ServiceDate <= end && o.ServiceEndDate >= start)
            .OrderBy(o => o.ServiceDate)
            .Select(o => new
            {
                o.Id,
                o.Code,
                VendorName = o.Vendor!.Name,
                o.Vendor.Phone,
                o.Service,
                o.Status,
                o.EventName,
                o.VenueName,
                o.ServiceDate,
                o.ReportingTime,
                o.TotalCost,
            })
            .Take(200)
            .ToListAsync(ct);

        var trips = await Db.VehicleTrips.AsNoTracking()
            .Include(t => t.Vehicle)
            .Include(t => t.Driver)
            .Where(t => t.Status != TripStatuses.Cancelled
                && t.FromDate <= end && t.ToDate >= start)
            .OrderBy(t => t.FromDate)
            .Select(t => new
            {
                t.Id,
                t.Code,
                Vehicle = t.Vehicle!.RegistrationNumber,
                DriverName = t.Driver != null ? t.Driver.Name : null,
                t.Status,
                t.Direction,
                t.EventName,
                t.ToLocation,
                t.FromDate,
                t.DepartureTime,
                Loads = t.Loads.Count,
            })
            .Take(200)
            .ToListAsync(ct);

        return Ok(new
        {
            from = start,
            to = end,
            dispatches,
            crew,
            orders,
            trips,
            summary = new
            {
                gatePasses = dispatches.Count,
                pieces = dispatches.Sum(d => d.Pieces),
                crewBooked = crew.Count,
                vendorsDue = orders.Count,
                vendorCost = orders.Sum(o => o.TotalCost),
                tripsRunning = trips.Count,
            },
        });
    }
}
