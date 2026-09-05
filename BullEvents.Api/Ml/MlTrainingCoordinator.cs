using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Ml;

/// <summary>
/// Assembles training sets from the database and refits every local model.
///
/// Training reads across tenants on purpose: with one workspace per deployment
/// that is the whole dataset, and the queries below explicitly bypass the
/// tenant filter so the intent is visible rather than accidental.
/// </summary>
public class MlTrainingCoordinator(
    AppDbContext db,
    LeadScoringService leadScoring,
    OpportunityWinModel winModel,
    SentimentService sentiment,
    ILogger<MlTrainingCoordinator> logger)
{
    public async Task<IReadOnlyList<ModelHealth>> TrainAllAsync(CancellationToken cancellationToken = default)
    {
        var started = DateTime.UtcNow;

        await TrainLeadScoringAsync(cancellationToken);
        await TrainWinModelAsync(cancellationToken);
        await TrainSentimentAsync(cancellationToken);

        // Scores are cached on the lead so list views can sort and filter on
        // them in SQL. A refit changes what those scores mean, so they are
        // rewritten in the same pass rather than left to drift.
        await RescoreLeadsAsync(cancellationToken);
        await BackfillSentimentAsync(cancellationToken);

        logger.LogInformation(
            "Local model refit completed in {Elapsed:N1}s",
            (DateTime.UtcNow - started).TotalSeconds);

        return [leadScoring.Health, winModel.Health, sentiment.Health];
    }

    /* ---------------- lead conversion ---------------- */

    private async Task TrainLeadScoringAsync(CancellationToken cancellationToken)
    {
        var leads = await db.Leads
            .IgnoreQueryFilters()
            .Where(l => !l.IsDeleted)
            .Include(l => l.Activities)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        leadScoring.Train(leads);
    }

    /// <summary>
    /// Writes each lead's current score back onto the row. Runs across tenants
    /// deliberately — same reason as training.
    /// </summary>
    public async Task<int> RescoreLeadsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var leads = await db.Leads
            .IgnoreQueryFilters()
            .Where(l => !l.IsDeleted)
            .Include(l => l.Activities)
            .ToListAsync(cancellationToken);

        foreach (var lead in leads)
        {
            var score = leadScoring.Score(lead, now);
            lead.CachedScore = score.Score;
            lead.CachedBand = score.Band;
            lead.ScoredAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return leads.Count;
    }

    /// <summary>
    /// Scores call notes that arrived without a sentiment label — bulk imports,
    /// seeded history, or anything written while the model was still cold.
    /// New calls are scored on write, so this only ever has a backlog to clear.
    /// </summary>
    public async Task<int> BackfillSentimentAsync(CancellationToken cancellationToken = default)
    {
        var pending = await db.CallLogs
            .IgnoreQueryFilters()
            .Where(c => !c.IsDeleted && c.Notes != null && c.SentimentLabel == null)
            // Ordered because the batch is capped: without one, which five
            // thousand rows come back is undefined, and a backlog larger than
            // the cap could hand back the same rows every pass and never drain.
            .OrderBy(c => c.Id)
            .Take(5000)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0) return 0;

        foreach (var call in pending)
        {
            var analysis = sentiment.Analyse(call.Notes);
            call.SentimentScore = analysis.Score;
            call.SentimentLabel = analysis.Label;
        }

        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Scored sentiment for {Count} call note(s)", pending.Count);
        return pending.Count;
    }

    /* ---------------- opportunity win ---------------- */

    private async Task TrainWinModelAsync(CancellationToken cancellationToken)
    {
        var closed = await db.Opportunities
            .IgnoreQueryFilters()
            .Where(o => !o.IsDeleted)
            .Where(o => o.Stage == OpportunityStages.ClosedWon || o.Stage == OpportunityStages.ClosedLost)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        if (closed.Count == 0)
        {
            winModel.Train([]);
            return;
        }

        var engagement = await BuildEngagementAsync(
            closed.Select(o => o.Id).ToList(), cancellationToken);

        var now = DateTime.UtcNow;
        var rows = closed
            .Select(o => OpportunityWinModel.ToFeatures(
                o, engagement.GetValueOrDefault(o.Id, OpportunityEngagement.Empty), now))
            .ToList();

        winModel.Train(rows);
    }

    /// <summary>
    /// Rolls up per-opportunity activity in four grouped queries rather than
    /// one per deal — the difference between a few round trips and a few
    /// thousand once the pipeline is real.
    /// </summary>
    public async Task<Dictionary<int, OpportunityEngagement>> BuildEngagementAsync(
        IReadOnlyList<int> opportunityIds,
        CancellationToken cancellationToken = default)
    {
        if (opportunityIds.Count == 0) return [];

        var ids = opportunityIds.ToHashSet();

        var visits = await db.SiteVisits.IgnoreQueryFilters()
            .Where(v => !v.IsDeleted && v.OpportunityId != null && ids.Contains(v.OpportunityId.Value))
            .GroupBy(v => v.OpportunityId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var quotations = await db.Quotations.IgnoreQueryFilters()
            .Where(q => !q.IsDeleted && q.OpportunityId != null && ids.Contains(q.OpportunityId.Value))
            .GroupBy(q => q.OpportunityId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var calls = await db.CallLogs.IgnoreQueryFilters()
            .Where(c => !c.IsDeleted && c.RelatedType == RelatedTypes.Opportunity && ids.Contains(c.RelatedId))
            .GroupBy(c => c.RelatedId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var followUps = await db.FollowUps.IgnoreQueryFilters()
            .Where(f => !f.IsDeleted && f.RelatedType == RelatedTypes.Opportunity && ids.Contains(f.RelatedId))
            .GroupBy(f => f.RelatedId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, cancellationToken);

        var cities = await db.Opportunities.IgnoreQueryFilters()
            .Where(o => ids.Contains(o.Id))
            .Select(o => new { o.Id, City = o.Contact != null ? o.Contact.City : o.Branch!.City })
            .ToDictionaryAsync(x => x.Id, x => x.City, cancellationToken);

        return ids.ToDictionary(
            id => id,
            id => new OpportunityEngagement(
                visits.GetValueOrDefault(id),
                calls.GetValueOrDefault(id),
                quotations.GetValueOrDefault(id),
                followUps.GetValueOrDefault(id),
                cities.GetValueOrDefault(id)));
    }

    /* ---------------- sentiment ---------------- */

    private async Task TrainSentimentAsync(CancellationToken cancellationToken)
    {
        // Call notes labelled by what the call actually led to. Ambiguous
        // dispositions (call back later, wrong number) carry no signal and are
        // left out rather than guessed at.
        var positive = new[]
        {
            CallDispositions.Interested,
            CallDispositions.SiteVisitScheduled,
            CallDispositions.Converted,
        };

        var negative = new[]
        {
            CallDispositions.NotInterested,
            CallDispositions.BudgetMismatch,
            CallDispositions.DoNotCall,
        };

        var callNotes = await db.CallLogs
            .IgnoreQueryFilters()
            .Where(c => !c.IsDeleted && c.Notes != null && c.Disposition != null)
            .Where(c => positive.Contains(c.Disposition!) || negative.Contains(c.Disposition!))
            .Select(c => new SentimentRow
            {
                Text = c.Notes!,
                Positive = positive.Contains(c.Disposition!),
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        // Visit feedback carries the same kind of signal, labelled by how
        // interested the visitor turned out to be.
        var visitNotes = await db.SiteVisits
            .IgnoreQueryFilters()
            .Where(v => !v.IsDeleted && v.Feedback != null && v.InterestLevel != null)
            .Where(v => v.InterestLevel == InterestLevels.High || v.InterestLevel == InterestLevels.Low)
            .Select(v => new SentimentRow
            {
                Text = v.Feedback!,
                Positive = v.InterestLevel == InterestLevels.High,
            })
            .AsNoTracking()
            .ToListAsync(cancellationToken);

        sentiment.Train([.. callNotes, .. visitNotes]);
    }
}

/// <summary>
/// Keeps the local models current: one fit at startup, then a periodic refit so
/// scores follow the data without anyone having to press a button.
/// </summary>
public class ModelTrainingHost(
    IServiceScopeFactory scopeFactory,
    ILogger<ModelTrainingHost> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let the app finish starting (and migrations finish running) first.
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        using var timer = new PeriodicTimer(Interval);

        do
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var coordinator = scope.ServiceProvider.GetRequiredService<MlTrainingCoordinator>();
                await coordinator.TrainAllAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Scheduled model refit failed — keeping the previously fitted models");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
