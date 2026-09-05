using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

[ApiController]
[Route("api/branches")]
[Authorize]
[SecuredBy(SecuredObjects.Branch)]
public class BranchesController(AppDbContext db, EntitlementService entitlements) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> GetBranches()
    {
        var companyId = User.GetCompanyId();

        var branches = await db.Branches
            .Where(b => b.CompanyId == companyId)
            .Select(b => new BranchDto(
                b.Id, b.Name, b.City, b.Address, b.ContactPhone,
                b.UserBranches.Count
            ))
            .ToListAsync();

        return Ok(branches);
    }

    [HttpPost]
    public async Task<ActionResult<BranchDto>> CreateBranch(CreateBranchRequest request)
    {
        var companyId = User.GetCompanyId();

        await entitlements.EnsureRoomAsync(Limits.Branches);

        var branch = new Branch
        {
            CompanyId = companyId,
            Name = request.Name,
            City = request.City,
            Address = request.Address,
            ContactPhone = request.ContactPhone,
        };

        db.Branches.Add(branch);
        await db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetBranches),
            new BranchDto(branch.Id, branch.Name, branch.City, branch.Address, branch.ContactPhone, 0)
        );
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<BranchDto>> UpdateBranch(int id, UpdateBranchRequest request)
    {
        var companyId = User.GetCompanyId();

        var branch = await db.Branches
            .Include(b => b.UserBranches)
            .FirstOrDefaultAsync(b => b.Id == id && b.CompanyId == companyId);

        if (branch is null) return NotFound();

        branch.Name = request.Name;
        branch.City = request.City;
        branch.Address = request.Address;
        branch.ContactPhone = request.ContactPhone;
        await db.SaveChangesAsync();

        return Ok(new BranchDto(
            branch.Id, branch.Name, branch.City, branch.Address, branch.ContactPhone,
            branch.UserBranches.Count
        ));
    }
}
