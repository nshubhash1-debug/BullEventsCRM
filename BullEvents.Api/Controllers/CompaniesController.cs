using System.Text.RegularExpressions;
using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize]
[SecuredBy(SecuredObjects.Company)]
public partial class CompaniesController(
    AppDbContext db,
    SessionService sessions,
    TenantContext tenant,
    IMemoryCache cache) : ControllerBase
{
    private static readonly string[] PlanTiers = ["Starter", "Growth", "Professional", "Enterprise"];
    private static readonly string[] Statuses = ["Active", "Trial", "Suspended"];

    private bool IsPlatformAdmin => User.IsInRole(Roles.SuperAdmin);

    /// <summary>
    /// Every tenant for a platform admin; only the caller's own for everyone
    /// else. Company rows carry no CompanyId, so they sit outside the DbContext's
    /// global tenant filter and the scoping has to happen here.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> GetCompanies()
    {
        var query = db.Companies.AsQueryable();

        if (!IsPlatformAdmin)
        {
            var companyId = User.GetCompanyId();
            query = query.Where(c => c.Id == companyId);
        }

        var companies = await query
            .OrderBy(c => c.Name)
            .Select(c => new CompanyDto(c.Id, c.Name, c.Slug, c.PlanTier, c.Status, c.CreatedAt))
            .ToListAsync();

        return Ok(companies);
    }

    [RequirePlatformAdmin]
    [HttpPost]
    public async Task<ActionResult<CompanyDto>> CreateCompany(CreateCompanyRequest request)
    {
        var planTier = Normalise(request.PlanTier, PlanTiers, "Starter");

        if (await db.Users.AnyAsync(u => u.Email == request.AdminEmail))
        {
            return Conflict(new { message = "A user with this email already exists." });
        }

        var slug = await UniqueSlugAsync(request.Slug ?? request.Name);

        var company = new Company
        {
            Name = request.Name.Trim(),
            Slug = slug,
            PlanTier = planTier,
            Status = "Active",
        };

        db.Companies.Add(company);
        await db.SaveChangesAsync();

        var branch = new Branch
        {
            CompanyId = company.Id,
            Name = string.IsNullOrWhiteSpace(request.HeadOfficeName)
                ? $"{request.HeadOfficeCity.Trim()} HQ"
                : request.HeadOfficeName.Trim(),
            City = request.HeadOfficeCity.Trim(),
        };

        db.Branches.Add(branch);

        var admin = new User
        {
            CompanyId = company.Id,
            Name = request.AdminName.Trim(),
            Email = request.AdminEmail.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            Role = Roles.CompanyAdmin,
        };

        db.Users.Add(admin);
        await db.SaveChangesAsync();

        db.UserBranches.Add(new UserBranch { UserId = admin.Id, BranchId = branch.Id });
        await db.SaveChangesAsync();

        return CreatedAtAction(nameof(GetCompanies), new { id = company.Id }, new CompanyDto(
            company.Id, company.Name, company.Slug, company.PlanTier, company.Status, company.CreatedAt
        ));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<CompanyDto>> UpdateCompany(int id, UpdateCompanyRequest request)
    {
        // A company admin can only rename its own tenant; a platform admin can
        // edit any of them, including plan and status.
        if (!IsPlatformAdmin && id != User.GetCompanyId()) return Forbid();

        var company = await db.Companies.FindAsync(id);
        if (company is null) return NotFound();

        company.Name = request.Name.Trim();

        var wasSuspended = IsSuspended(company.Status);

        if (IsPlatformAdmin)
        {
            company.PlanTier = Normalise(request.PlanTier, PlanTiers, company.PlanTier);
            company.Status = Normalise(request.Status, Statuses, company.Status);
        }

        await db.SaveChangesAsync();

        // Status is load-bearing now, so a change to it has to reach the people
        // already inside. Dropping the cached verdict is what makes it immediate;
        // ending the open sessions is what makes it real, because a token issued
        // ten minutes ago would otherwise keep working until it expired.
        if (IsSuspended(company.Status) != wasSuspended)
        {
            SessionGuardMiddleware.ForgetCompany(cache, company.Id);

            if (IsSuspended(company.Status))
            {
                // Everyone except the operator running the suspension, whose own
                // session is against this tenant and who still has to be able to
                // lift it again.
                await sessions.RevokeAllForCompanyAsync(
                    company.Id, RevokeReasons.CompanySuspended, tenant.TokenId);
            }
        }

        return Ok(new CompanyDto(
            company.Id, company.Name, company.Slug, company.PlanTier, company.Status, company.CreatedAt
        ));
    }

    private static bool IsSuspended(string status) =>
        string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase);

    private static string Normalise(string? value, string[] allowed, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return allowed.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase))
            ?? fallback;
    }

    private async Task<string> UniqueSlugAsync(string source)
    {
        var basis = SlugPattern().Replace(source.ToLowerInvariant(), "-").Trim('-');
        if (basis.Length == 0) basis = "company";
        if (basis.Length > 48) basis = basis[..48].Trim('-');

        var candidate = basis;
        var suffix = 2;
        while (await db.Companies.AnyAsync(c => c.Slug == candidate))
        {
            candidate = $"{basis}-{suffix++}";
        }

        return candidate;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugPattern();
}
