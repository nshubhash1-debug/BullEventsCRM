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
/// Sellable inventory: projects, towers and units.
///
/// The important behaviour here is the hold — a soft, expiring lock on a unit
/// that stops two reps promising the same flat. Holds are enforced on write
/// with a concurrency check rather than trusted from the client.
/// </summary>
[ApiController]
[Route("api/inventory")]
[Authorize]
[SecuredBy(SecuredObjects.Unit)]
public class InventoryController(AppDbContext db, SpaceBookingService spaceBookings)
    : CrmControllerBase(db)
{
    private static readonly FieldMap<Unit> Fields = new FieldMap<Unit>()
        .Text("unitNumber", "Space name", searchable: true)
        .Select("projectName", "Venue", "Project.Name")
        .Select("towerName", "Block", "Tower.Name")
        .Number("floor", "Level")
        .Select("configuration", "Space type")
        .Number("carpetArea", "Area")
        .Number("builtUpArea", "Area with foyer")
        .Number("superArea", "Total footprint")
        .Number("seatingCapacity", "Seating capacity")
        .Number("floatingCapacity", "Floating capacity")
        .Number("theatreCapacity", "Theatre capacity")
        .Bool("isAirConditioned", "Air conditioned")
        .Bool("isOutdoor", "Outdoor")
        .Bool("hasStage", "Has stage")
        .Bool("hasAttachedKitchen", "Attached kitchen")
        .Select("facing", "Facing")
        .Select("viewType", "View")
        .Number("bathrooms", "Washrooms")
        .Number("balconies", "Adjoining open areas")
        .Number("parkingSlots", "Parking")
        .Bool("isCornerUnit", "Standalone")
        .Bool("vastuCompliant", "Vastu compliant")
        .Select("status", "Status")
        .Number("basePrice", "Hall rental")
        .Number("pricePerSqft", "Rate per sqft")
        .Number("totalPrice", "Indicative total")
        .Number("pricePerPlate", "Price per plate")
        .Number("minimumPlates", "Minimum plates")
        .Number("peakDatePremium", "Peak date premium")
        .Number("securityDeposit", "Security deposit")
        .Date("heldUntil", "Held until")
        .Date("bookedAt", "Booked")
        .Number("projectId", "Venue ID")
        .Date("createdAt", "Created");

    private IQueryable<Unit> Base() => Db.Units
        .Include(u => u.Project)
        .Include(u => u.Tower)
        .AsNoTracking();

    private static UnitDto ToDto(Unit u, string? heldByName = null) => new(
        u.Id, u.ProjectId, u.Project?.Name ?? "—", u.TowerId, u.Tower?.Name,
        u.UnitNumber, u.Floor, u.Configuration,
        u.CarpetArea, u.BuiltUpArea, u.SuperArea, u.AreaUnit,
        u.Facing, u.ViewType, u.Bathrooms, u.Balconies, u.ParkingSlots,
        u.IsCornerUnit, u.VastuCompliant,
        u.SeatingCapacity, u.FloatingCapacity, u.TheatreCapacity,
        u.IsAirConditioned, u.IsOutdoor, u.HasStage, u.HasAttachedKitchen,
        EffectiveStatus(u),
        u.BasePrice, u.PricePerSqft, u.FloorRisePremium, u.PlcCharges, u.TotalPrice,
        u.PricePerPlate, u.MinimumPlates, u.PeakDatePremium, u.SecurityDeposit,
        u.HeldByUserId, heldByName, u.HeldUntil, u.HoldReason,
        u.BookedByContactId, u.BookedAt, u.CreatedAt);

    /// <summary>
    /// A hold that has run out is available again. Reporting it that way beats
    /// a nightly job that sweeps expired holds — nothing can be stale.
    /// </summary>
    private static string EffectiveStatus(Unit unit) =>
        unit.Status == UnitStatuses.Held && unit.HeldUntil < DateTime.UtcNow
            ? UnitStatuses.Available
            : unit.Status;

    /* ------------------------------------------------------------------ *
     * Projects
     * ------------------------------------------------------------------ */

    [HttpGet("projects")]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> GetProjects(CancellationToken ct)
    {
        var projects = await Db.Projects.AsNoTracking().OrderBy(p => p.Name).ToListAsync(ct);

        var stats = await Db.Units
            .GroupBy(u => u.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Total = g.Count(),
                Available = g.Count(u => u.Status == UnitStatuses.Available),
                Booked = g.Count(u => u.Status == UnitStatuses.Booked || u.Status == UnitStatuses.Sold),
                Value = g.Where(u => u.Status == UnitStatuses.Available).Sum(u => u.TotalPrice),
            })
            .ToDictionaryAsync(x => x.ProjectId, ct);

        return Ok(projects.Select(p =>
        {
            var stat = stats.GetValueOrDefault(p.Id);

            return new ProjectDto(
                p.Id, p.Name, p.Code, p.Developer, p.Type, p.Status,
                p.City, p.Locality, p.Address, p.ReraNumber,
                p.LaunchDate, p.PossessionDate, p.PriceMin, p.PriceMax,
                p.Amenities, p.Description,
                stat?.Total ?? 0, stat?.Available ?? 0, stat?.Booked ?? 0, stat?.Value ?? 0m,
                p.CreatedAt);
        }).ToList());
    }

    [HttpGet("projects/{id:int}/towers")]
    public async Task<ActionResult<IReadOnlyList<TowerDto>>> GetTowers(int id, CancellationToken ct)
    {
        var towers = await Db.Towers
            .Where(t => t.ProjectId == id)
            .Select(t => new TowerDto(
                t.Id, t.Name, t.FloorCount, t.UnitsPerFloor, t.Status, t.Units.Count))
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

        return Ok(towers);
    }

    /* ------------------------------------------------------------------ *
     * Units
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<FilterFieldDto>>> GetFields(CancellationToken ct)
    {
        var projects = await Db.Projects.Select(p => p.Name).OrderBy(n => n).ToListAsync(ct);
        var towers = await Db.Towers.Select(t => t.Name).Distinct().OrderBy(n => n).ToListAsync(ct);
        var facings = await Db.Units
            .Where(u => u.Facing != null)
            .Select(u => u.Facing!).Distinct().OrderBy(f => f).ToListAsync(ct);
        var views = await Db.Units
            .Where(u => u.ViewType != null)
            .Select(u => u.ViewType!).Distinct().OrderBy(v => v).ToListAsync(ct);

        return Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["status"] = Options(UnitStatuses.All),
            ["configuration"] = Options(SpaceTypes.All),
            ["projectName"] = Options([.. projects]),
            ["towerName"] = Options([.. towers]),
            ["facing"] = Options([.. facings]),
            ["viewType"] = Options([.. views]),
        }, new Dictionary<string, string>
        {
            ["unitNumber"] = "Unit",
            ["projectName"] = "Unit",
            ["towerName"] = "Unit",
            ["floor"] = "Unit",
            ["configuration"] = "Unit",
            ["status"] = "Availability",
            ["heldUntil"] = "Availability",
            ["bookedAt"] = "Availability",
            ["carpetArea"] = "Area",
            ["builtUpArea"] = "Area",
            ["superArea"] = "Area",
            ["basePrice"] = "Pricing",
            ["pricePerSqft"] = "Pricing",
            ["floorRisePremium"] = "Pricing",
            ["plcCharges"] = "Pricing",
            ["totalPrice"] = "Pricing",
            ["facing"] = "Attributes",
            ["viewType"] = "Attributes",
            ["bathrooms"] = "Attributes",
            ["balconies"] = "Attributes",
            ["parkingSlots"] = "Attributes",
            ["isCornerUnit"] = "Attributes",
            ["vastuCompliant"] = "Attributes",
        }));
    }

    [PermissionAction(ObjectAction.View)]
    [HttpPost("units/query")]
    public async Task<ActionResult<PagedResult<UnitDto>>> QueryUnits(QueryRequest request, CancellationToken ct)
    {
        var result = await RunQueryAsync(
            Base(),
            request,
            Fields,
            u => ToDto(u),
            defaultSortPath: "TotalPrice",
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["units"] = await filtered.CountAsync(ct),
                ["available"] = await filtered.CountAsync(u => u.Status == UnitStatuses.Available, ct),
                ["held"] = await filtered.CountAsync(u => u.Status == UnitStatuses.Held, ct),
                ["booked"] = await filtered.CountAsync(u =>
                    u.Status == UnitStatuses.Booked || u.Status == UnitStatuses.Sold, ct),
                ["inventoryValue"] = await filtered
                    .Where(u => u.Status == UnitStatuses.Available)
                    .SumAsync(u => u.TotalPrice, ct),
            },
            cancellationToken: ct);

        // Resolve holder names in one query rather than joining on every row.
        var holderIds = result.Items
            .Where(i => i.HeldByUserId is not null)
            .Select(i => i.HeldByUserId!.Value)
            .Distinct()
            .ToList();

        if (holderIds.Count > 0)
        {
            var names = await Db.Users
                .Where(u => holderIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Name, ct);

            result.Items = result.Items
                .Select(i => i.HeldByUserId is int held
                    ? i with { HeldByName = names.GetValueOrDefault(held) }
                    : i)
                .ToList();
        }

        return Ok(result);
    }

    /// <summary>
    /// Availability grid for a project — one row per floor, one cell per unit.
    /// This is the view a sales desk actually works from.
    /// </summary>
    [HttpGet("projects/{id:int}/availability")]
    public async Task<ActionResult<AvailabilityGridDto>> Availability(int id, CancellationToken ct)
    {
        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw ApiException.NotFound("Project");

        var units = await Db.Units
            .Where(u => u.ProjectId == id)
            .Include(u => u.Tower)
            .AsNoTracking()
            .ToListAsync(ct);

        var floors = units
            .GroupBy(u => u.Floor)
            .OrderByDescending(g => g.Key)
            .Select(g => new AvailabilityFloorDto(
                g.Key,
                g.OrderBy(u => u.UnitNumber)
                    .Select(u => new AvailabilityCellDto(
                        u.Id, u.UnitNumber, u.Configuration, EffectiveStatus(u),
                        u.TotalPrice, u.CarpetArea, u.Tower?.Name))
                    .ToList()))
            .ToList();

        var byStatus = units
            .GroupBy(EffectiveStatus)
            .Select(g => new AgendaBucketDto(g.Key, Humanise(g.Key), g.Count()))
            .ToList();

        var byConfiguration = units
            .GroupBy(u => u.Configuration)
            .Select(g => new ConfigurationStatDto(
                g.Key,
                g.Count(),
                g.Count(u => EffectiveStatus(u) == UnitStatuses.Available),
                g.Average(u => u.TotalPrice) is var avg ? decimal.Round(avg, 0) : 0m,
                decimal.Round(g.Average(u => u.PricePerSqft), 0)))
            .OrderBy(c => c.Configuration)
            .ToList();

        return Ok(new AvailabilityGridDto(
            project.Id,
            project.Name,
            units.Count,
            units.Count(u => EffectiveStatus(u) == UnitStatuses.Available),
            units.Where(u => EffectiveStatus(u) == UnitStatuses.Available).Sum(u => u.TotalPrice),
            floors,
            byStatus,
            byConfiguration));
    }

    [HttpGet("units/{id:int}")]
    public async Task<ActionResult<UnitDto>> GetUnit(int id, CancellationToken ct)
    {
        var unit = await Base().FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ApiException.NotFound("Unit");

        var heldBy = unit.HeldByUserId is null
            ? null
            : await Db.Users.Where(u => u.Id == unit.HeldByUserId)
                .Select(u => u.Name).FirstOrDefaultAsync(ct);

        return Ok(ToDto(unit, heldBy));
    }

    /* ------------------------------------------------------------------ *
     * Holds
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Places a soft hold. Rejects the request if someone else already holds
    /// the unit and their hold has not expired — the double-booking safeguard.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("units/{id:int}/hold")]
    public async Task<ActionResult<UnitDto>> Hold(int id, HoldUnitRequest request, CancellationToken ct)
    {
        var unit = await Db.Units
            .Include(u => u.Project).Include(u => u.Tower)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ApiException.NotFound("Space");

        if (unit.Status == UnitStatuses.NotForSale)
            throw ApiException.Conflict($"{unit.UnitNumber} is not bookable.");

        var eventDate = request.EventDate
            ?? throw ApiException.BadRequest("An event date is required to hold a space.");

        var hours = Math.Clamp(request.Hours <= 0 ? 48 : request.Hours, 1, 336);
        var holdUntil = DateTime.UtcNow.AddHours(hours);

        await spaceBookings.UpsertManualAsync(
            unit,
            eventDate,
            request.EventSlot ?? EventSlots.Evening,
            UnitStatuses.Held,
            holdUntil,
            leadId: null,
            contactId: null,
            quotationId: null,
            request.ClientName,
            request.Reason,
            ct);

        // Catalogue stays available — the calendar row is the lock.
        unit.Status = UnitStatuses.Available;
        unit.HeldByUserId = Db.Tenant.UserId;
        unit.HeldUntil = holdUntil;
        unit.HoldReason = request.Reason
            ?? $"Held for {eventDate:dd MMM} ({request.EventSlot ?? EventSlots.Evening})";

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(unit, Db.Tenant.UserName));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("units/{id:int}/release")]
    public async Task<ActionResult<UnitDto>> Release(
        int id,
        [FromQuery] DateOnly? eventDate,
        [FromQuery] string? eventSlot,
        CancellationToken ct)
    {
        var unit = await Db.Units
            .Include(u => u.Project).Include(u => u.Tower)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ApiException.NotFound("Space");

        if (eventDate is DateOnly day)
        {
            var slot = eventSlot ?? EventSlots.Evening;
            var groupRef = $"M-{unit.Id}-{day:yyyyMMdd}-{slot}";
            await spaceBookings.ReleaseGroupAsync(groupRef, ct);
        }

        unit.HeldByUserId = null;
        unit.HeldUntil = null;
        unit.HoldReason = null;
        if (unit.Status == UnitStatuses.Held) unit.Status = UnitStatuses.Available;

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(unit));
    }

    /* ------------------------------------------------------------------ *
     * Unit maintenance
     * ------------------------------------------------------------------ */

    [HttpPost("units")]
    public async Task<ActionResult<UnitDto>> CreateUnit(UnitInput input, CancellationToken ct)
    {
        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == input.ProjectId, ct)
            ?? throw ApiException.BadRequest("Select a valid project.");

        var duplicate = await Db.Units.AnyAsync(
            u => u.ProjectId == project.Id && u.UnitNumber == input.UnitNumber, ct);

        if (duplicate)
        {
            throw ApiException.Conflict($"Unit {input.UnitNumber} already exists in {project.Name}.");
        }

        var unit = new Unit
        {
            CompanyId = Db.Tenant.CompanyId,
            ProjectId = project.Id,
        };

        Apply(unit, input);

        Db.Units.Add(unit);
        await Db.SaveChangesAsync(ct);

        unit.Project = project;
        return CreatedAtAction(nameof(GetUnit), new { id = unit.Id }, ToDto(unit));
    }

    [HttpPut("units/{id:int}")]
    public async Task<ActionResult<UnitDto>> UpdateUnit(int id, UnitInput input, CancellationToken ct)
    {
        var unit = await Db.Units
            .Include(u => u.Project).Include(u => u.Tower)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw ApiException.NotFound("Unit");

        Apply(unit, input);
        await Db.SaveChangesAsync(ct);

        return Ok(ToDto(unit));
    }

    private static void Apply(Unit unit, UnitInput input)
    {
        unit.TowerId = input.TowerId;
        unit.UnitNumber = input.UnitNumber.Trim();
        unit.Floor = input.Floor;
        unit.Configuration = input.Configuration;
        unit.CarpetArea = Math.Max(0m, input.CarpetArea);
        unit.BuiltUpArea = input.BuiltUpArea;
        unit.SuperArea = input.SuperArea;
        unit.Facing = input.Facing;
        unit.ViewType = input.ViewType;
        unit.Bathrooms = Math.Clamp(input.Bathrooms, 0, 10);
        unit.Balconies = Math.Clamp(input.Balconies, 0, 10);
        unit.ParkingSlots = Math.Clamp(input.ParkingSlots, 0, 10);
        unit.IsCornerUnit = input.IsCornerUnit;
        unit.VastuCompliant = input.VastuCompliant;

        unit.SeatingCapacity = Math.Max(0, input.SeatingCapacity);
        unit.FloatingCapacity = Math.Max(0, input.FloatingCapacity);
        unit.TheatreCapacity = input.TheatreCapacity;
        unit.IsAirConditioned = input.IsAirConditioned;
        unit.IsOutdoor = input.IsOutdoor;
        unit.HasStage = input.HasStage;
        unit.HasAttachedKitchen = input.HasAttachedKitchen;

        unit.PricePerPlate = Math.Max(0m, input.PricePerPlate);
        unit.MinimumPlates = Math.Max(0, input.MinimumPlates);
        unit.PeakDatePremium = Math.Clamp(input.PeakDatePremium, 0m, 3m);
        unit.SecurityDeposit = Math.Max(0m, input.SecurityDeposit);
        unit.TurnaroundHours = Math.Clamp(input.TurnaroundHours, 0, 72);
        unit.LayoutImageUrl = string.IsNullOrWhiteSpace(input.LayoutImageUrl)
            ? null
            : input.LayoutImageUrl.Trim();

        // Blackout is a calendar status, not a catalogue one.
        var catalogueStatuses = UnitStatuses.All.Where(s => s != UnitStatuses.Blackout).ToArray();
        unit.Status = Require(input.Status, catalogueStatuses, "unit status");
        unit.BasePrice = Math.Max(0m, input.BasePrice);
        unit.PricePerSqft = Math.Max(0m, input.PricePerSqft);
        unit.FloorRisePremium = Math.Max(0m, input.FloorRisePremium);
        unit.PlcCharges = Math.Max(0m, input.PlcCharges);
        unit.TotalPrice = unit.BasePrice + unit.FloorRisePremium + unit.PlcCharges;
    }
}

public record AvailabilityCellDto(
    int UnitId,
    string UnitNumber,
    string Configuration,
    string Status,
    decimal TotalPrice,
    decimal CarpetArea,
    string? TowerName
);

public record AvailabilityFloorDto(int Floor, IReadOnlyList<AvailabilityCellDto> Units);

public record ConfigurationStatDto(
    string Configuration,
    int Total,
    int Available,
    decimal AveragePrice,
    decimal AveragePricePerSqft
);

public record AvailabilityGridDto(
    int ProjectId,
    string ProjectName,
    int TotalUnits,
    int AvailableUnits,
    decimal AvailableValue,
    IReadOnlyList<AvailabilityFloorDto> Floors,
    IReadOnlyList<AgendaBucketDto> ByStatus,
    IReadOnlyList<ConfigurationStatDto> ByConfiguration
);
