using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace BullEvents.Api.Controllers;

/* ------------------------------------------------------------------ *
 * DTOs
 * ------------------------------------------------------------------ */

/// <summary>One capped resource: what the plan allows and what is in use.</summary>
public record LimitDto(string Key, string Label, int Allowed, int Used, bool Overridden);

public record PlanDto(
    string Key,
    string Name,
    string Tagline,
    int Seats,
    int Leads,
    int Branches,
    string[] Modules,
    bool ApiAccess,
    bool Webhooks,
    bool CustomFields,
    bool Sso,
    int PricePerSeat,
    bool IsCurrent);

public record SubscriptionDto(
    int CompanyId,
    string CompanyName,
    string PlanKey,
    string PlanName,
    string Status,
    DateTime? TrialEndsAt,
    int? TrialDaysLeft,
    DateTime? RenewsAt,
    string? EntitlementNote,
    IReadOnlyList<LimitDto> Limits,
    /// <summary>Modules the plan opens, as keys the client turns into names.</summary>
    string[] Modules,
    bool ApiAccess,
    bool Webhooks,
    bool CustomFields,
    bool Sso,
    /// <summary>Seats × the plan's per-seat price. What the tenant would be billed.</summary>
    int MonthlyCost,
    IReadOnlyList<PlanDto> AvailablePlans);

public record UsagePointDto(
    DateTime Day, int ActiveUsers, int Leads, int LeadsCreated, int SignIns, int Quotations);

/// <summary>Platform-operator changes to a tenant's commercial terms.</summary>
public record UpdateSubscriptionRequest(
    string? PlanTier,
    string? Status,
    DateTime? TrialEndsAt,
    DateTime? RenewsAt,
    int? SeatLimitOverride,
    int? LeadLimitOverride,
    int? BranchLimitOverride,
    string? EntitlementNote);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// What this tenant is paying for, what it is using, and — for a platform
/// operator — the terms themselves.
///
/// Reading is open to anybody who can see the company record, because a company
/// admin bumping into a seat limit needs to know what the limit is without
/// raising a ticket. Writing is a platform-operator action: a tenant that could
/// lift its own ceilings would not have ceilings.
/// </summary>
[ApiController]
[Route("api/subscription")]
[Authorize]
[SecuredBy(SecuredObjects.Company)]
public class SubscriptionController(
    AppDbContext db,
    EntitlementService entitlements,
    SessionService sessions,
    IMemoryCache cache) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SubscriptionDto>> Current(CancellationToken ct)
    {
        var companyId = User.GetCompanyId();

        var company = await db.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        if (company is null) return NotFound(new { message = "That company no longer exists." });

        return Ok(await BuildAsync(company, ct));
    }

    /// <summary>The daily counters behind the usage chart.</summary>
    [HttpGet("usage")]
    public async Task<ActionResult<IReadOnlyList<UsagePointDto>>> Usage(
        [FromQuery] int days = 30, CancellationToken ct = default)
    {
        var companyId = User.GetCompanyId();
        var since = DateTime.UtcNow.Date.AddDays(-Math.Clamp(days, 7, 365));

        var points = await db.UsageSnapshots
            .IgnoreQueryFilters()
            .Where(u => u.CompanyId == companyId && u.Day >= since)
            .OrderBy(u => u.Day)
            .Select(u => new UsagePointDto(
                u.Day, u.ActiveUsers, u.Leads, u.LeadsCreated, u.SignIns, u.Quotations))
            .ToListAsync(ct);

        return Ok(points);
    }

    /// <summary>
    /// Changes a tenant's commercial terms.
    ///
    /// Deliberately does not refuse a downgrade that would put the company over
    /// a limit. The alternative — refusing until they delete records — is worse
    /// for everybody: the ceiling stops new writes and the console shows the
    /// overage, which is the conversation the account manager wanted anyway.
    /// </summary>
    [HttpPut("{companyId:int}")]
    [RequirePlatformAdmin]
    public async Task<ActionResult<SubscriptionDto>> Update(
        int companyId, UpdateSubscriptionRequest request, CancellationToken ct)
    {
        var company = await db.Companies
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == companyId, ct);

        if (company is null) return NotFound(new { message = "That company no longer exists." });

        if (request.PlanTier is not null)
        {
            if (!PlanCatalog.Exists(request.PlanTier))
            {
                return BadRequest(new { message = $"'{request.PlanTier}' is not a plan on this platform." });
            }

            company.PlanTier = PlanCatalog.For(request.PlanTier).Key;
        }

        var wasSuspended = IsSuspended(company.Status);

        if (request.Status is not null)
        {
            if (!Statuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = $"'{request.Status}' is not a company status." });
            }

            company.Status = Statuses.First(s =>
                string.Equals(s, request.Status, StringComparison.OrdinalIgnoreCase));
        }

        company.TrialEndsAt = request.TrialEndsAt;
        company.RenewsAt = request.RenewsAt;
        company.SeatLimitOverride = Positive(request.SeatLimitOverride);
        company.LeadLimitOverride = Positive(request.LeadLimitOverride);
        company.BranchLimitOverride = Positive(request.BranchLimitOverride);
        company.EntitlementNote = string.IsNullOrWhiteSpace(request.EntitlementNote)
            ? null
            : request.EntitlementNote.Trim();

        await db.SaveChangesAsync(ct);

        // The trading status is cached per request and read on every one, so a
        // change to it has to be published rather than waited out.
        if (IsSuspended(company.Status) != wasSuspended)
        {
            SessionGuardMiddleware.ForgetCompany(cache, company.Id);

            if (IsSuspended(company.Status))
            {
                await sessions.RevokeAllForCompanyAsync(
                    company.Id, RevokeReasons.CompanySuspended, ct: ct);
            }
        }

        return Ok(await BuildAsync(company, ct));
    }

    /* ---------------- helpers ---------------- */

    private static readonly string[] Statuses = ["Active", "Trial", "Suspended"];

    private static bool IsSuspended(string status) =>
        string.Equals(status, "Suspended", StringComparison.OrdinalIgnoreCase);

    /// <summary>Zero and negatives clear an override rather than setting one.</summary>
    private static int? Positive(int? value) => value is > 0 ? value : null;

    private async Task<SubscriptionDto> BuildAsync(Company company, CancellationToken ct)
    {
        var resolved = await entitlements.ResolveAsync(company.Id, ct);
        var plan = resolved.Plan;

        var limits = new[] { Limits.Seats, Limits.Leads, Limits.Branches }
            .Select(key => new LimitDto(
                key,
                Limits.Label(key),
                resolved.LimitOn(key),
                resolved.UsageOn(key),
                Overridden: key switch
                {
                    Limits.Seats => company.SeatLimitOverride is not null,
                    Limits.Leads => company.LeadLimitOverride is not null,
                    _ => company.BranchLimitOverride is not null,
                }))
            .ToList();

        var plans = PlanCatalog.All
            .Select(p => new PlanDto(
                p.Key, p.Name, p.Tagline, p.Seats, p.Leads, p.Branches, p.Modules,
                p.ApiAccess, p.Webhooks, p.CustomFields, p.Sso, p.PricePerSeat,
                IsCurrent: string.Equals(p.Key, plan.Key, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        return new SubscriptionDto(
            company.Id,
            company.Name,
            plan.Key,
            plan.Name,
            company.Status,
            company.TrialEndsAt,
            resolved.TrialDaysLeft,
            company.RenewsAt,
            company.EntitlementNote,
            limits,
            plan.Modules,
            plan.ApiAccess,
            plan.Webhooks,
            plan.CustomFields,
            plan.Sso,
            // Billed on seats in use rather than on the ceiling: a company that
            // bought twenty-five and filled eleven is charged for eleven, which
            // is the only figure they will accept as fair.
            resolved.UsageOn(Limits.Seats) * plan.PricePerSeat,
            plans);
    }
}
