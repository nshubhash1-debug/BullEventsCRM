using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>What one tenant is allowed and what it is currently using.</summary>
public record Entitlements(
    PlanDefinition Plan,
    string Status,
    DateTime? TrialEndsAt,
    DateTime? RenewsAt,
    string? Note,
    /// <summary>Limit name to the ceiling in force, after any per-company override.</summary>
    IReadOnlyDictionary<string, int> Allowed,
    IReadOnlyDictionary<string, int> Used)
{
    public int LimitOn(string limit) => Allowed.GetValueOrDefault(limit, PlanCatalog.Unlimited);

    public int UsageOn(string limit) => Used.GetValueOrDefault(limit);

    public bool HasRoomFor(string limit, int more = 1)
    {
        var ceiling = LimitOn(limit);
        return ceiling == PlanCatalog.Unlimited || UsageOn(limit) + more <= ceiling;
    }

    /// <summary>How full a limit is, 0–1, for the meter on the subscription screen.</summary>
    public double FractionOf(string limit)
    {
        var ceiling = LimitOn(limit);
        if (ceiling <= 0) return 0;
        return Math.Min(1, UsageOn(limit) / (double)ceiling);
    }

    /// <summary>Days left on the trial, or null when the company is not on one.</summary>
    public int? TrialDaysLeft => TrialEndsAt is DateTime ends
        ? Math.Max(0, (int)Math.Ceiling((ends - DateTime.UtcNow).TotalDays))
        : null;
}

/// <summary>
/// Reads what a tenant's plan allows, and refuses the writes that would exceed
/// it.
///
/// The refusal happens on the write path rather than in the UI, because a limit
/// enforced only in a screen is enforced only against people using the screen.
/// The number the check reads is the live count, not a stored counter: a
/// counter and a table disagree eventually, and the disagreement always
/// surfaces as a customer who cannot invite somebody they are entitled to.
/// </summary>
public class EntitlementService(AppDbContext db, TenantContext tenant)
{
    private Entitlements? _cached;

    public async Task<Entitlements> ResolveAsync(int? companyId = null, CancellationToken ct = default)
    {
        var id = companyId ?? tenant.CompanyId;

        if (_cached is not null && companyId is null) return _cached;

        var company = await db.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw new InvalidOperationException($"Company {id} does not exist.");

        var plan = PlanCatalog.For(company.PlanTier);

        var allowed = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            [Limits.Seats] = company.SeatLimitOverride ?? plan.Seats,
            [Limits.Leads] = company.LeadLimitOverride ?? plan.Leads,
            [Limits.Branches] = company.BranchLimitOverride ?? plan.Branches,
        };

        var used = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            // Inactive accounts do not consume a seat. Charging for somebody who
            // left is the fastest way to make a customer distrust the number.
            [Limits.Seats] = await db.Users
                .IgnoreQueryFilters()
                .CountAsync(u => u.CompanyId == id && u.IsActive, ct),

            [Limits.Leads] = await db.Leads
                .IgnoreQueryFilters()
                .CountAsync(l => l.CompanyId == id && !l.IsDeleted, ct),

            [Limits.Branches] = await db.Branches
                .IgnoreQueryFilters()
                .CountAsync(b => b.CompanyId == id, ct),
        };

        var resolved = new Entitlements(
            plan, company.Status, company.TrialEndsAt, company.RenewsAt,
            company.EntitlementNote, allowed, used);

        if (companyId is null) _cached = resolved;
        return resolved;
    }

    /// <summary>
    /// Throws when adding <paramref name="more"/> would break the plan.
    ///
    /// The message names the plan, the limit and the number, because "limit
    /// reached" sends the customer to support to find out which of three it was.
    /// </summary>
    public async Task EnsureRoomAsync(string limit, int more = 1, CancellationToken ct = default)
    {
        var entitlements = await ResolveAsync(ct: ct);
        if (entitlements.HasRoomFor(limit, more)) return;

        var ceiling = entitlements.LimitOn(limit);

        throw ApiException.Forbidden(
            $"The {entitlements.Plan.Name} plan includes {ceiling:N0} {Limits.Label(limit)} "
            + $"and this company is using {entitlements.UsageOn(limit):N0}. "
            + "Upgrade the plan, or free one up first.");
    }

    /// <summary>Throws when the plan does not include a module at all.</summary>
    public async Task EnsureModuleAsync(string module, CancellationToken ct = default)
    {
        var entitlements = await ResolveAsync(ct: ct);
        if (entitlements.Plan.Includes(module)) return;

        throw ApiException.Forbidden(
            $"{Modules.Label(module)} is not part of the {entitlements.Plan.Name} plan.");
    }
}
