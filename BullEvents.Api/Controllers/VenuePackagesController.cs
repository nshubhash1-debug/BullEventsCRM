using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

[ApiController]
[Route("api/inventory")]
[Authorize]
[SecuredBy(SecuredObjects.Unit)]
public class VenuePackagesController(AppDbContext db) : CrmControllerBase(db)
{
    [HttpGet("packages")]
    public async Task<ActionResult<IReadOnlyList<VenuePackageDto>>> List(
        [FromQuery] int? projectId,
        CancellationToken ct)
    {
        var query = Db.VenuePackages.AsNoTracking()
            .Include(p => p.Project)
            .Include(p => p.Spaces).ThenInclude(s => s.Unit)
            .AsQueryable();

        if (projectId is int pid) query = query.Where(p => p.ProjectId == pid);

        var rows = await query.OrderBy(p => p.Name).ToListAsync(ct);
        return Ok(rows.Select(ToDto).ToList());
    }

    [HttpGet("packages/{id:int}")]
    public async Task<ActionResult<VenuePackageDto>> Get(int id, CancellationToken ct)
    {
        var package = await Db.VenuePackages.AsNoTracking()
            .Include(p => p.Project)
            .Include(p => p.Spaces).ThenInclude(s => s.Unit)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw ApiException.NotFound("Package");

        return Ok(ToDto(package));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("packages")]
    public async Task<ActionResult<VenuePackageDto>> Create(VenuePackageInput input, CancellationToken ct)
    {
        var project = await Db.Projects.FirstOrDefaultAsync(p => p.Id == input.ProjectId, ct)
            ?? throw ApiException.BadRequest("Select a valid venue.");

        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(input.Code))
            throw ApiException.BadRequest("Name and code are required.");

        var dup = await Db.VenuePackages.AnyAsync(
            p => p.ProjectId == project.Id && p.Code == input.Code.Trim(), ct);
        if (dup) throw ApiException.Conflict($"Package code {input.Code} already exists.");

        var package = new VenuePackage
        {
            CompanyId = Db.Tenant.CompanyId,
            ProjectId = project.Id,
            Name = input.Name.Trim(),
            Code = input.Code.Trim().ToUpperInvariant(),
            Description = input.Description,
            PlanningPackage = input.PlanningPackage,
            IndicativeRental = input.IndicativeRental,
            IndicativePerPlate = input.IndicativePerPlate,
            DefaultMinimumPlates = input.DefaultMinimumPlates,
            DefaultGuestCount = input.DefaultGuestCount,
            IsActive = input.IsActive,
        };

        var order = 0;
        foreach (var unitId in input.UnitIds.Distinct())
        {
            var exists = await Db.Units.AnyAsync(u => u.Id == unitId && u.ProjectId == project.Id, ct);
            if (!exists) throw ApiException.BadRequest($"Space #{unitId} is not on this venue.");

            package.Spaces.Add(new VenuePackageSpace
            {
                UnitId = unitId,
                SortOrder = order++,
                DefaultSlot = EventSlots.Evening,
            });
        }

        Db.VenuePackages.Add(package);
        await Db.SaveChangesAsync(ct);

        return Created($"/api/inventory/packages/{package.Id}", await Reload(package.Id, ct));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("packages/{id:int}")]
    public async Task<ActionResult<VenuePackageDto>> Update(
        int id, VenuePackageInput input, CancellationToken ct)
    {
        var package = await Db.VenuePackages
            .Include(p => p.Spaces)
            .FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw ApiException.NotFound("Package");

        package.Name = input.Name.Trim();
        package.Code = input.Code.Trim().ToUpperInvariant();
        package.Description = input.Description;
        package.PlanningPackage = input.PlanningPackage;
        package.IndicativeRental = input.IndicativeRental;
        package.IndicativePerPlate = input.IndicativePerPlate;
        package.DefaultMinimumPlates = input.DefaultMinimumPlates;
        package.DefaultGuestCount = input.DefaultGuestCount;
        package.IsActive = input.IsActive;

        Db.VenuePackageSpaces.RemoveRange(package.Spaces);
        package.Spaces.Clear();

        var order = 0;
        foreach (var unitId in input.UnitIds.Distinct())
        {
            package.Spaces.Add(new VenuePackageSpace
            {
                UnitId = unitId,
                SortOrder = order++,
                DefaultSlot = EventSlots.Evening,
            });
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await Reload(package.Id, ct));
    }

    [PermissionAction(ObjectAction.Delete)]
    [HttpDelete("packages/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var package = await Db.VenuePackages.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw ApiException.NotFound("Package");

        package.IsDeleted = true;
        package.DeletedAt = DateTime.UtcNow;
        package.DeletedById = Db.Tenant.UserId;
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- peak dates ---------------- */

    [HttpGet("projects/{projectId:int}/peak-dates")]
    public async Task<ActionResult<IReadOnlyList<VenuePeakWindowDto>>> PeakDates(
        int projectId, CancellationToken ct)
    {
        var rows = await Db.VenuePeakDates.AsNoTracking()
            .Where(p => p.ProjectId == projectId)
            .OrderBy(p => p.StartDate)
            .Select(p => new VenuePeakWindowDto(
                p.Id, p.StartDate, p.EndDate, p.Label, p.PremiumFraction))
            .ToListAsync(ct);
        return Ok(rows);
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("peak-dates")]
    public async Task<ActionResult<VenuePeakWindowDto>> CreatePeak(
        VenuePeakDateInput input, CancellationToken ct)
    {
        _ = await Db.Projects.FirstOrDefaultAsync(p => p.Id == input.ProjectId, ct)
            ?? throw ApiException.BadRequest("Select a valid venue.");

        if (input.EndDate < input.StartDate)
            throw ApiException.BadRequest("End date must be on or after start date.");

        var row = new VenuePeakDate
        {
            CompanyId = Db.Tenant.CompanyId,
            ProjectId = input.ProjectId,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            Label = string.IsNullOrWhiteSpace(input.Label) ? "Peak" : input.Label.Trim(),
            PremiumFraction = Math.Clamp(input.PremiumFraction, 0m, 2m),
            Notes = input.Notes,
        };

        Db.VenuePeakDates.Add(row);
        await Db.SaveChangesAsync(ct);

        return Ok(new VenuePeakWindowDto(
            row.Id, row.StartDate, row.EndDate, row.Label, row.PremiumFraction));
    }

    [PermissionAction(ObjectAction.Delete)]
    [HttpDelete("peak-dates/{id:int}")]
    public async Task<IActionResult> DeletePeak(int id, CancellationToken ct)
    {
        var row = await Db.VenuePeakDates.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw ApiException.NotFound("Peak window");
        Db.VenuePeakDates.Remove(row);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private async Task<VenuePackageDto> Reload(int id, CancellationToken ct)
    {
        var package = await Db.VenuePackages.AsNoTracking()
            .Include(p => p.Project)
            .Include(p => p.Spaces).ThenInclude(s => s.Unit)
            .FirstAsync(p => p.Id == id, ct);
        return ToDto(package);
    }

    private static VenuePackageDto ToDto(VenuePackage p) => new(
        p.Id,
        p.ProjectId,
        p.Project?.Name ?? "—",
        p.Name,
        p.Code,
        p.Description,
        p.PlanningPackage,
        p.IndicativeRental,
        p.IndicativePerPlate,
        p.DefaultMinimumPlates,
        p.DefaultGuestCount,
        p.IsActive,
        p.Spaces.OrderBy(s => s.SortOrder).Select(s => new VenuePackageSpaceDto(
            s.UnitId,
            s.Unit?.UnitNumber ?? $"#{s.UnitId}",
            s.Unit?.Configuration ?? "—",
            s.Unit?.SeatingCapacity ?? 0,
            s.SortOrder,
            s.DefaultSlot)).ToList());
}
