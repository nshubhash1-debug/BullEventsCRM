using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The vehicles that move everything, and the trips they run.
///
/// Availability works exactly as it does for crew — an overlapping blocking
/// trip means the vehicle is spoken for — so the only genuinely new idea here
/// is the load check. The props already carry a weight and a packing unit, so a
/// trip can add up what it has been asked to carry and say whether the tempo
/// can legally take it, before the tempo is at the godown door.
/// </summary>
[ApiController]
[Route("api/fleet")]
[Authorize]
[SecuredBy(SecuredObjects.Unit)]
public class FleetController(AppDbContext db) : CrmControllerBase(db)
{
    private IQueryable<Vehicle> Base() => Db.Vehicles
        .Include(v => v.SupplierVendor)
        .Include(v => v.DefaultDriver)
        .Include(v => v.Store)
        .AsNoTracking();

    [HttpGet("meta")]
    public ActionResult<object> Meta() => Ok(new
    {
        vehicleTypes = VehicleTypes.All,
        vehicleStatuses = VehicleStatuses.All,
        tripStatuses = TripStatuses.All,
    });

    /* ------------------------------------------------------------------ *
     * Vehicles
     * ------------------------------------------------------------------ */

    [HttpGet("vehicles")]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> Vehicles(
        [FromQuery] string? status,
        [FromQuery] string? vehicleType,
        CancellationToken ct = default)
    {
        var query = Base();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(v => v.Status == status);
        if (!string.IsNullOrWhiteSpace(vehicleType)) query = query.Where(v => v.VehicleType == vehicleType);

        var rows = await query.OrderBy(v => v.Name).Take(300).ToListAsync(ct);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return Ok(rows.Select(v => ToDto(v, today)).ToList());
    }

    [HttpGet("vehicles/{id:int}")]
    public async Task<ActionResult<VehicleDto>> GetVehicle(int id, CancellationToken ct)
    {
        var vehicle = await Base().FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vehicle");

        return Ok(ToDto(vehicle, DateOnly.FromDateTime(DateTime.UtcNow)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("vehicles")]
    public async Task<ActionResult<VehicleDto>> CreateVehicle(
        VehicleInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.RegistrationNumber))
            throw ApiException.BadRequest("A registration number is required.");

        var registration = input.RegistrationNumber.Trim().ToUpperInvariant();

        if (await Db.Vehicles.AnyAsync(v => v.RegistrationNumber == registration, ct))
            throw ApiException.Conflict($"{registration} is already on the fleet.");

        var vehicle = new Vehicle
        {
            CompanyId = Db.Tenant.CompanyId,
            RegistrationNumber = registration,
            Name = string.IsNullOrWhiteSpace(input.Name) ? registration : input.Name.Trim(),
            VehicleType = Require(input.VehicleType, VehicleTypes.All, "vehicle type"),
            Status = Require(input.Status, VehicleStatuses.All, "status"),
            SupplierVendorId = input.SupplierVendorId,
            PayloadKg = input.PayloadKg,
            CapacityCubicFeet = input.CapacityCubicFeet,
            PassengerSeats = input.PassengerSeats,
            DefaultDriverCrewId = input.DefaultDriverCrewId,
            DayRate = input.DayRate,
            RatePerKm = input.RatePerKm,
            InsuranceExpiry = input.InsuranceExpiry,
            PermitExpiry = input.PermitExpiry,
            PucExpiry = input.PucExpiry,
            FitnessExpiry = input.FitnessExpiry,
            LastServicedOn = input.LastServicedOn,
            OdometerKm = input.OdometerKm,
            StoreId = input.StoreId,
            Notes = input.Notes,
        };

        await ValidateLinksAsync(vehicle, ct);

        Db.Vehicles.Add(vehicle);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetVehicleDtoAsync(vehicle.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("vehicles/{id:int}")]
    public async Task<ActionResult<VehicleDto>> UpdateVehicle(
        int id, VehicleInput input, CancellationToken ct)
    {
        var vehicle = await Db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vehicle");

        var registration = input.RegistrationNumber.Trim().ToUpperInvariant();

        if (registration != vehicle.RegistrationNumber
            && await Db.Vehicles.AnyAsync(v => v.RegistrationNumber == registration, ct))
        {
            throw ApiException.Conflict($"{registration} is already on the fleet.");
        }

        vehicle.RegistrationNumber = registration;
        vehicle.Name = string.IsNullOrWhiteSpace(input.Name) ? registration : input.Name.Trim();
        vehicle.VehicleType = Require(input.VehicleType, VehicleTypes.All, "vehicle type");
        vehicle.Status = Require(input.Status, VehicleStatuses.All, "status");
        vehicle.SupplierVendorId = input.SupplierVendorId;
        vehicle.PayloadKg = input.PayloadKg;
        vehicle.CapacityCubicFeet = input.CapacityCubicFeet;
        vehicle.PassengerSeats = input.PassengerSeats;
        vehicle.DefaultDriverCrewId = input.DefaultDriverCrewId;
        vehicle.DayRate = input.DayRate;
        vehicle.RatePerKm = input.RatePerKm;
        vehicle.InsuranceExpiry = input.InsuranceExpiry;
        vehicle.PermitExpiry = input.PermitExpiry;
        vehicle.PucExpiry = input.PucExpiry;
        vehicle.FitnessExpiry = input.FitnessExpiry;
        vehicle.LastServicedOn = input.LastServicedOn;
        vehicle.OdometerKm = input.OdometerKm;
        vehicle.StoreId = input.StoreId;
        vehicle.Notes = input.Notes;

        await ValidateLinksAsync(vehicle, ct);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetVehicleDtoAsync(vehicle.Id, ct));
    }

    [PermissionAction(ObjectAction.Delete)]
    [HttpDelete("vehicles/{id:int}")]
    public async Task<IActionResult> DeleteVehicle(int id, CancellationToken ct)
    {
        var vehicle = await Db.Vehicles.FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vehicle");

        var running = await Db.VehicleTrips.AnyAsync(
            t => t.VehicleId == id && TripStatuses.Blocking.Contains(t.Status), ct);

        if (running)
            throw ApiException.BadRequest("This vehicle has trips on the road. Close them first.");

        SoftDelete(vehicle);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task ValidateLinksAsync(Vehicle vehicle, CancellationToken ct)
    {
        if (vehicle.SupplierVendorId is int vendorId
            && !await Db.Vendors.AnyAsync(v => v.Id == vendorId, ct))
        {
            throw ApiException.BadRequest("Select a valid transporter.");
        }

        if (vehicle.DefaultDriverCrewId is int driverId
            && !await Db.CrewMembers.AnyAsync(c => c.Id == driverId, ct))
        {
            throw ApiException.BadRequest("Select a valid driver from the crew roster.");
        }

        if (vehicle.StoreId is int storeId
            && !await Db.PropStores.AnyAsync(s => s.Id == storeId, ct))
        {
            throw ApiException.BadRequest("Select a valid godown.");
        }
    }

    /* ------------------------------------------------------------------ *
     * Availability
     * ------------------------------------------------------------------ */

    private async Task<Dictionary<int, int>> ClashingTripsAsync(
        DateOnly from,
        DateOnly to,
        IReadOnlyCollection<int>? vehicleIds,
        int? excludeTripId,
        CancellationToken ct)
    {
        var query = Db.VehicleTrips.AsNoTracking()
            .Where(t => TripStatuses.Blocking.Contains(t.Status))
            .Where(t => t.FromDate <= to && t.ToDate >= from);

        if (vehicleIds is { Count: > 0 })
            query = query.Where(t => vehicleIds.Contains(t.VehicleId));

        if (excludeTripId is int exclude)
            query = query.Where(t => t.Id != exclude);

        return await query
            .GroupBy(t => t.VehicleId)
            .Select(g => new { VehicleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.VehicleId, x => x.Count, ct);
    }

    [HttpPost("availability")]
    public async Task<ActionResult<IReadOnlyList<VehicleDto>>> Availability(
        VehicleAvailabilityRequest request, CancellationToken ct)
    {
        if (request.To < request.From)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var query = Base().Where(v => v.Status == VehicleStatuses.Active);

        if (!string.IsNullOrWhiteSpace(request.VehicleType))
            query = query.Where(v => v.VehicleType == request.VehicleType);

        if (request.MinimumPayloadKg is decimal minimum)
            query = query.Where(v => v.PayloadKg >= minimum);

        var vehicles = await query.OrderBy(v => v.Name).Take(200).ToListAsync(ct);
        var ids = vehicles.Select(v => v.Id).ToList();
        var clashes = await ClashingTripsAsync(request.From, request.To, ids, request.ExcludeTripId, ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = vehicles
            .Select(v => ToDto(v, today, clashes.GetValueOrDefault(v.Id)))
            .Where(v => !request.AvailableOnly || v.IsAvailable == true)
            // Free first, then smallest vehicle that still does the job — a
            // container sent on a job a tempo could do is a container the next
            // event cannot have.
            .OrderByDescending(v => v.IsAvailable == true)
            .ThenBy(v => v.PayloadKg ?? decimal.MaxValue)
            .ThenBy(v => v.Name)
            .ToList();

        return Ok(rows);
    }

    /// <summary>Vehicles whose papers have lapsed or are about to.</summary>
    [HttpGet("compliance")]
    public async Task<ActionResult<IReadOnlyList<object>>> Compliance(
        [FromQuery] int withinDays = 45, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var horizon = today.AddDays(Math.Clamp(withinDays, 1, 365));

        var rows = await Db.Vehicles.AsNoTracking()
            .Where(v => v.Status != VehicleStatuses.Retired)
            .Where(v =>
                (v.InsuranceExpiry != null && v.InsuranceExpiry <= horizon)
                || (v.PermitExpiry != null && v.PermitExpiry <= horizon)
                || (v.PucExpiry != null && v.PucExpiry <= horizon)
                || (v.FitnessExpiry != null && v.FitnessExpiry <= horizon))
            .OrderBy(v => v.Name)
            .Select(v => new
            {
                v.Id,
                v.Name,
                v.RegistrationNumber,
                v.InsuranceExpiry,
                v.PermitExpiry,
                v.PucExpiry,
                v.FitnessExpiry,
                IsLapsed =
                    (v.InsuranceExpiry != null && v.InsuranceExpiry < today)
                    || (v.PermitExpiry != null && v.PermitExpiry < today)
                    || (v.PucExpiry != null && v.PucExpiry < today)
                    || (v.FitnessExpiry != null && v.FitnessExpiry < today),
            })
            .ToListAsync(ct);

        return Ok(rows);
    }

    /* ------------------------------------------------------------------ *
     * Trips
     * ------------------------------------------------------------------ */

    private IQueryable<VehicleTrip> TripBase() => Db.VehicleTrips
        .Include(t => t.Vehicle)
        .Include(t => t.Driver)
        .Include(t => t.Loads).ThenInclude(l => l.Issue).ThenInclude(i => i!.Lines)
            .ThenInclude(l => l.Item);

    [HttpGet("trips")]
    public async Task<ActionResult<IReadOnlyList<VehicleTripDto>>> Trips(
        [FromQuery] string? status,
        [FromQuery] int? vehicleId,
        [FromQuery] int? leadId,
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int take = 100,
        CancellationToken ct = default)
    {
        var query = TripBase().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);
        if (vehicleId is int vid) query = query.Where(t => t.VehicleId == vid);
        if (leadId is int lid) query = query.Where(t => t.LeadId == lid);
        if (from is DateOnly f) query = query.Where(t => t.ToDate >= f);
        if (to is DateOnly t2) query = query.Where(t => t.FromDate <= t2);

        var rows = await query
            .OrderByDescending(t => t.FromDate).ThenByDescending(t => t.Id)
            .Take(Math.Clamp(take, 1, 500))
            .ToListAsync(ct);

        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpGet("trips/{id:int}")]
    public async Task<ActionResult<VehicleTripDto>> GetTrip(int id, CancellationToken ct)
    {
        var trip = await TripBase().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Trip");

        return Ok(ToDto(trip));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("trips")]
    public async Task<ActionResult<VehicleTripDto>> CreateTrip(
        VehicleTripInput input, CancellationToken ct)
    {
        var vehicle = await Db.Vehicles.FirstOrDefaultAsync(v => v.Id == input.VehicleId, ct)
            ?? throw ApiException.BadRequest("Select a valid vehicle.");

        if (!vehicle.IsBookable)
            throw ApiException.BadRequest(
                $"{vehicle.Name} is {vehicle.Status.ToLowerInvariant()} and cannot be dispatched.");

        var to = input.ToDate ?? input.FromDate;
        if (to < input.FromDate)
            throw ApiException.BadRequest("The end date cannot fall before the start date.");

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (vehicle.HasLapsedPapers(today))
        {
            throw ApiException.BadRequest(
                $"{vehicle.RegistrationNumber} has lapsed papers. Renew them before dispatching it.");
        }

        var clashes = await ClashingTripsAsync(input.FromDate, to, [vehicle.Id], null, ct);
        if (clashes.GetValueOrDefault(vehicle.Id) > 0)
        {
            throw ApiException.BadRequest(
                $"{vehicle.RegistrationNumber} is already committed between " +
                $"{input.FromDate:dd MMM} and {to:dd MMM}.");
        }

        var trip = new VehicleTrip
        {
            CompanyId = Db.Tenant.CompanyId,
            Code = Code("TR"),
            VehicleId = vehicle.Id,
            Status = TripStatuses.Planned,
            Direction = input.Direction,
            DriverCrewId = input.DriverCrewId ?? vehicle.DefaultDriverCrewId,
            FromDate = input.FromDate,
            ToDate = to,
            DepartureTime = input.DepartureTime,
            FromLocation = input.FromLocation,
            ToLocation = input.ToLocation,
            LeadId = input.LeadId,
            ProjectId = input.ProjectId,
            EventName = input.EventName,
            StartOdometerKm = vehicle.OdometerKm,
            Notes = input.Notes,
        };

        if (trip.DriverCrewId is int driverId
            && !await Db.CrewMembers.AnyAsync(c => c.Id == driverId, ct))
        {
            throw ApiException.BadRequest("Select a valid driver from the crew roster.");
        }

        foreach (var (issueId, index) in (input.PropIssueIds ?? []).Distinct().Select((x, n) => (x, n)))
        {
            var exists = await Db.PropIssues.AnyAsync(i => i.Id == issueId, ct);
            if (!exists) throw ApiException.BadRequest($"Gate pass #{issueId} does not exist.");

            trip.Loads.Add(new VehicleTripLoad { PropIssueId = issueId, SortOrder = index });
        }

        Db.VehicleTrips.Add(trip);
        await Db.SaveChangesAsync(ct);

        return Ok(await GetTripDtoAsync(trip.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("trips/{id:int}/loads/{issueId:int}")]
    public async Task<ActionResult<VehicleTripDto>> AddLoad(
        int id, int issueId, CancellationToken ct)
    {
        var trip = await Db.VehicleTrips.Include(t => t.Loads)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Trip");

        if (trip.Status is TripStatuses.Completed or TripStatuses.Cancelled)
            throw ApiException.BadRequest("This trip is finished.");

        if (trip.Loads.Any(l => l.PropIssueId == issueId))
            throw ApiException.BadRequest("That gate pass is already on this trip.");

        var exists = await Db.PropIssues.AnyAsync(i => i.Id == issueId, ct);
        if (!exists) throw ApiException.BadRequest("That gate pass does not exist.");

        trip.Loads.Add(new VehicleTripLoad
        {
            PropIssueId = issueId,
            SortOrder = trip.Loads.Count == 0 ? 0 : trip.Loads.Max(l => l.SortOrder) + 1,
        });

        await Db.SaveChangesAsync(ct);
        return Ok(await GetTripDtoAsync(trip.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpDelete("trips/{id:int}/loads/{issueId:int}")]
    public async Task<ActionResult<VehicleTripDto>> RemoveLoad(
        int id, int issueId, CancellationToken ct)
    {
        var trip = await Db.VehicleTrips.Include(t => t.Loads)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Trip");

        var load = trip.Loads.FirstOrDefault(l => l.PropIssueId == issueId)
            ?? throw ApiException.NotFound("Load");

        trip.Loads.Remove(load);
        Db.VehicleTripLoads.Remove(load);

        await Db.SaveChangesAsync(ct);
        return Ok(await GetTripDtoAsync(trip.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("trips/{id:int}/status")]
    public async Task<ActionResult<VehicleTripDto>> SetTripStatus(
        int id, [FromQuery] string status, CancellationToken ct)
    {
        var trip = await Db.VehicleTrips.Include(t => t.Loads)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Trip");

        trip.Status = Require(status, TripStatuses.All, "status");
        await Db.SaveChangesAsync(ct);

        return Ok(await GetTripDtoAsync(trip.Id, ct));
    }

    /// <summary>
    /// Closes a trip and rolls the odometer forward onto the vehicle, so the
    /// next trip starts from where this one ended without anybody re-reading
    /// the dial.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("trips/{id:int}/close")]
    public async Task<ActionResult<VehicleTripDto>> CloseTrip(
        int id, TripCloseInput input, CancellationToken ct)
    {
        var trip = await Db.VehicleTrips.Include(t => t.Vehicle).Include(t => t.Loads)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Trip");

        if (trip.Status is TripStatuses.Completed or TripStatuses.Cancelled)
            throw ApiException.BadRequest("This trip is already finished.");

        if (input.EndOdometerKm is int end)
        {
            if (trip.StartOdometerKm is int start && end < start)
                throw ApiException.BadRequest("The closing reading is below the opening one.");

            trip.EndOdometerKm = end;

            if (trip.Vehicle is not null) trip.Vehicle.OdometerKm = end;
        }

        trip.FuelCost = input.FuelCost ?? trip.FuelCost;
        trip.TollCost = input.TollCost ?? trip.TollCost;
        trip.OtherCost = input.OtherCost ?? trip.OtherCost;
        trip.Status = TripStatuses.Completed;

        if (!string.IsNullOrWhiteSpace(input.Notes))
        {
            trip.Notes = string.IsNullOrWhiteSpace(trip.Notes)
                ? input.Notes
                : $"{trip.Notes}\n{input.Notes}";
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await GetTripDtoAsync(trip.Id, ct));
    }

    /* ------------------------------------------------------------------ *
     * Projection
     * ------------------------------------------------------------------ */

    private async Task<VehicleDto> GetVehicleDtoAsync(int id, CancellationToken ct)
    {
        var vehicle = await Base().FirstOrDefaultAsync(v => v.Id == id, ct)
            ?? throw ApiException.NotFound("Vehicle");

        return ToDto(vehicle, DateOnly.FromDateTime(DateTime.UtcNow));
    }

    private async Task<VehicleTripDto> GetTripDtoAsync(int id, CancellationToken ct)
    {
        var trip = await TripBase().AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Trip");

        return ToDto(trip);
    }

    internal static VehicleDto ToDto(Vehicle v, DateOnly today, int? clashes = null) => new(
        v.Id, v.RegistrationNumber, v.Name, v.VehicleType, v.Status,
        v.SupplierVendorId, v.SupplierVendor?.Name,
        v.PayloadKg, v.CapacityCubicFeet, v.PassengerSeats,
        v.DefaultDriverCrewId, v.DefaultDriver?.Name,
        v.DayRate, v.RatePerKm,
        v.InsuranceExpiry, v.PermitExpiry, v.PucExpiry, v.FitnessExpiry,
        v.HasLapsedPapers(today),
        v.LastServicedOn, v.OdometerKm,
        v.StoreId, v.Store?.Name, v.Notes,
        clashes,
        clashes is null ? null : clashes == 0,
        v.CreatedAt);

    internal static VehicleTripDto ToDto(VehicleTrip trip)
    {
        var loads = trip.Loads.OrderBy(l => l.SortOrder).Select(l =>
        {
            var lines = l.Issue?.Lines ?? [];

            // What actually left, falling back to what was reserved for a pass
            // that has not been dispatched yet — a load plan is drawn up before
            // the truck is loaded, not after.
            var pieces = lines.Sum(x => x.IssuedQuantity > 0 ? x.IssuedQuantity : x.ReservedQuantity);

            var weight = lines
                .Where(x => x.Item?.WeightKg is not null)
                .Sum(x => x.Item!.WeightKg!.Value
                    * (x.IssuedQuantity > 0 ? x.IssuedQuantity : x.ReservedQuantity));

            return new TripLoadDto(
                l.Id, l.PropIssueId, l.Issue?.Code ?? "",
                l.Issue?.EventName, l.Issue?.VenueName,
                pieces, weight == 0 ? null : weight, l.SortOrder);
        }).ToList();

        var planned = loads.Any(l => l.WeightKg is not null)
            ? loads.Sum(l => l.WeightKg ?? 0)
            : (decimal?)null;

        return new VehicleTripDto(
            trip.Id, trip.Code, trip.Status, trip.Direction,
            trip.VehicleId,
            trip.Vehicle?.RegistrationNumber ?? "", trip.Vehicle?.Name ?? "",
            trip.Vehicle?.PayloadKg,
            trip.DriverCrewId, trip.Driver?.Name, trip.Driver?.Phone,
            trip.FromDate, trip.ToDate, trip.DepartureTime,
            trip.FromLocation, trip.ToLocation,
            trip.LeadId, trip.ProjectId, trip.EventName,
            trip.StartOdometerKm, trip.EndOdometerKm, trip.DistanceKm,
            trip.FuelCost, trip.TollCost, trip.OtherCost, trip.TotalRunningCost,
            trip.LoadedWeightKg, planned,
            planned is decimal p && trip.Vehicle?.PayloadKg is decimal cap && p > cap,
            trip.Notes, loads, trip.CreatedAt);
    }
}
