using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using BullEvents.Api.Infrastructure;

namespace BullEvents.Api.Services;

/// <summary>
/// Writes today's usage for every tenant, and ends the trials that have run
/// out.
///
/// Both belong to the same job because both are "once a day, per company, over
/// the whole table" — and because a trial that expires needs its final usage
/// row written before the tenant goes quiet.
/// </summary>
public class UsageRecorder(AppDbContext db, IMemoryCache cache, ILogger<UsageRecorder> logger)
{
    public async Task RunAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        // Filters off throughout: this walks every tenant, and the ambient one
        // is whatever the last request happened to leave behind — or nothing at
        // all, since this runs outside a request.
        var companies = await db.Companies
            .IgnoreQueryFilters()
            .Select(c => new { c.Id, c.PlanTier, c.Status, c.TrialEndsAt, c.Name })
            .ToListAsync(ct);

        foreach (var company in companies)
        {
            await RecordAsync(company.Id, company.PlanTier, today, ct);
        }

        await ExpireTrialsAsync(companies
            .Where(c => c.TrialEndsAt is not null)
            .Select(c => (c.Id, c.Name, c.Status, c.TrialEndsAt!.Value))
            .ToList(), ct);
    }

    private async Task RecordAsync(int companyId, string planTier, DateTime day, CancellationToken ct)
    {
        var snapshot = await db.UsageSnapshots
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.CompanyId == companyId && u.Day == day, ct);

        if (snapshot is null)
        {
            snapshot = new UsageSnapshot { CompanyId = companyId, Day = day };
            db.UsageSnapshots.Add(snapshot);
        }

        var since = day;
        var until = day.AddDays(1);

        snapshot.PlanTier = planTier;
        snapshot.RecordedAt = DateTime.UtcNow;

        snapshot.ActiveUsers = await db.Users.IgnoreQueryFilters()
            .CountAsync(u => u.CompanyId == companyId && u.IsActive, ct);

        snapshot.Leads = await db.Leads.IgnoreQueryFilters()
            .CountAsync(l => l.CompanyId == companyId && !l.IsDeleted, ct);

        snapshot.Branches = await db.Branches.IgnoreQueryFilters()
            .CountAsync(b => b.CompanyId == companyId, ct);

        snapshot.Quotations = await db.Quotations.IgnoreQueryFilters()
            .CountAsync(q => q.CompanyId == companyId && !q.IsDeleted, ct);

        snapshot.Units = await db.Units.IgnoreQueryFilters()
            .CountAsync(u => u.CompanyId == companyId && !u.IsDeleted, ct);

        snapshot.LeadsCreated = await db.Leads.IgnoreQueryFilters()
            .CountAsync(l => l.CompanyId == companyId
                             && l.CreatedAt >= since && l.CreatedAt < until, ct);

        snapshot.SignIns = await db.UserSessions.IgnoreQueryFilters()
            .Where(s => s.CompanyId == companyId && s.IssuedAt >= since && s.IssuedAt < until)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(ct);

        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Suspends the trials whose date has passed.
    ///
    /// Suspension rather than deletion, and reversible from the console — a
    /// trial that lapses over a weekend is usually a customer who was going to
    /// buy on Monday, not one who left.
    /// </summary>
    private async Task ExpireTrialsAsync(
        IReadOnlyList<(int Id, string Name, string Status, DateTime EndsAt)> trials,
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        foreach (var trial in trials)
        {
            if (trial.EndsAt > now) continue;
            if (string.Equals(trial.Status, "Suspended", StringComparison.OrdinalIgnoreCase)) continue;

            var company = await db.Companies.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Id == trial.Id, ct);

            if (company is null) continue;

            company.Status = "Suspended";
            await db.SaveChangesAsync(ct);

            // The status is read from a short cache on every request, and the
            // sessions already open would otherwise outlive the trial by up to
            // twelve hours.
            SessionGuardMiddleware.ForgetCompany(cache, company.Id);

            var sessions = new SessionService(db, cache);
            var ended = await sessions.RevokeAllForCompanyAsync(
                company.Id, RevokeReasons.CompanySuspended, ct: ct);

            logger.LogInformation(
                "Trial ended for {Company} (expired {EndsAt:d}); {Sessions} sessions closed.",
                trial.Name, trial.EndsAt, ended);
        }
    }
}

/// <summary>
/// Runs the recorder at startup and then once an hour.
///
/// Hourly rather than daily because a trial that lapsed at nine in the morning
/// should not stay open until midnight, and because writing the same day's
/// snapshot again is an update rather than an insert — the unique index sees to
/// that.
/// </summary>
public class SubscriptionHost(IServiceProvider services, ILogger<SubscriptionHost> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A short delay so the first pass does not compete with migrations and
        // seeding for the same connection pool.
        await Task.Delay(TimeSpan.FromSeconds(20), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var recorder = scope.ServiceProvider.GetRequiredService<UsageRecorder>();
                await recorder.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // A failed pass is not worth taking the host down for: the next
                // one recomputes the same numbers from scratch.
                logger.LogError(error, "Usage and trial pass failed; will retry.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
