using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Ml;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Every model in the workspace, behind one route prefix.
///
/// All of it runs in-process through ML.NET: conversion scoring, deal win
/// probability, revenue forecasting, customer segmentation, note sentiment and
/// operational anomaly detection. No lead, contact or note is ever sent to an
/// external inference service — which is the point of running it locally.
/// </summary>
[ApiController]
[Route("api/intelligence")]
[Authorize]
[SecuredBy(SecuredObjects.Report)]
public class IntelligenceController(
    AppDbContext db,
    LeadScoringService scoring,
    OpportunityWinModel winModel,
    ForecastService forecasting,
    SegmentationService segmentation,
    SentimentService sentiment,
    AnomalyService anomalies,
    MlTrainingCoordinator coordinator) : CrmControllerBase(db)
{
    /* ------------------------------------------------------------------ *
     * Model health
     * ------------------------------------------------------------------ */

    /// <summary>Which engine is behind each score, and how well it tested.</summary>
    [HttpGet("models")]
    public ActionResult<IReadOnlyList<ModelHealth>> GetModels() => Ok(new[]
    {
        scoring.Health,
        winModel.Health,
        sentiment.Health,
    });

    /// <summary>Backwards-compatible single-model status for the lead score.</summary>
    [HttpGet("model")]
    public ActionResult<ModelStatusDto> GetModelStatus() => Ok(new ModelStatusDto(
        scoring.EngineName,
        scoring.TrainedAt,
        scoring.TrainingRows,
        scoring.Accuracy,
        scoring.AreaUnderRocCurve,
        scoring.LastTrainingMessage));

    [PermissionAction(ObjectAction.ModifyAll)]
    [HttpPost("models/train")]
    public async Task<ActionResult<IReadOnlyList<ModelHealth>>> TrainAll(CancellationToken ct) =>
        Ok(await coordinator.TrainAllAsync(ct));

    [PermissionAction(ObjectAction.ModifyAll)]
    [HttpPost("model/train")]
    public async Task<ActionResult<ModelStatusDto>> Train(CancellationToken ct)
    {
        await coordinator.TrainAllAsync(ct);
        return Ok(new ModelStatusDto(
            scoring.EngineName,
            scoring.TrainedAt,
            scoring.TrainingRows,
            scoring.Accuracy,
            scoring.AreaUnderRocCurve,
            scoring.LastTrainingMessage));
    }

    /* ------------------------------------------------------------------ *
     * Leads
     * ------------------------------------------------------------------ */

    [HttpGet("leads/{id:int}")]
    public async Task<ActionResult<LeadInsightsDto>> GetLeadInsights(int id, CancellationToken ct)
    {
        var lead = await Db.Leads
            .Include(l => l.Activities)
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        var now = DateTime.UtcNow;
        var score = scoring.Score(lead, now);

        var pool = await Db.Leads.Where(l => l.Id != id).AsNoTracking().ToListAsync(ct);

        return Ok(new LeadInsightsDto(
            lead.Id,
            score.Score,
            score.Band,
            score.Engine,
            score.Signals.Select(s => new ScoreSignalDto(s.Label, s.Detail, s.Points)).ToList(),
            NextBestAction.For(lead, score, now),
            DuplicateDetector.FindDuplicates(lead, pool)));
    }

    [HttpGet("leads/scores")]
    public async Task<ActionResult<IReadOnlyList<ScoredLeadDto>>> GetAllScores(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var leads = await Db.Leads
            .Include(l => l.Activities)
            .AsNoTracking()
            .ToListAsync(ct);

        return Ok(leads
            .Select(lead =>
            {
                var score = scoring.Score(lead, now);
                return new ScoredLeadDto(lead.Id, lead.Name, lead.Stage, score.Score, score.Band);
            })
            .OrderByDescending(s => s.Score)
            .ToList());
    }

    /// <summary>
    /// Recomputes and persists every lead's score, so the list can sort and
    /// filter on it in SQL instead of inferring on every page load.
    /// </summary>
    [RequirePermission(SecuredObjects.Lead, ObjectAction.ModifyAll)]
    [HttpPost("leads/rescore")]
    public async Task<ActionResult<BulkResultDto>> Rescore(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var leads = await Db.Leads.Include(l => l.Activities).ToListAsync(ct);

        foreach (var lead in leads)
        {
            var score = scoring.Score(lead, now);
            lead.CachedScore = score.Score;
            lead.CachedBand = score.Band;
            lead.ScoredAt = now;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(new BulkResultDto(
            leads.Count, $"{leads.Count} lead(s) rescored using {scoring.EngineName}."));
    }

    [PermissionAction(ObjectAction.View)]
    [HttpPost("duplicates")]
    public async Task<ActionResult<IReadOnlyList<DuplicateMatch>>> CheckDuplicates(
        CheckDuplicatesRequest request,
        CancellationToken ct)
    {
        var candidate = new Lead
        {
            Id = request.ExcludeLeadId ?? 0,
            Name = request.Name,
            Phone = request.Phone,
            Phone2 = request.Phone2,
            Email = request.Email,
            City = request.City,
        };

        var pool = await Db.Leads.AsNoTracking().ToListAsync(ct);
        return Ok(DuplicateDetector.FindDuplicates(candidate, pool));
    }

    /* ------------------------------------------------------------------ *
     * Assignment
     * ------------------------------------------------------------------ */

    [HttpGet("leads/{id:int}/assignment")]
    public async Task<ActionResult<IReadOnlyList<AssignmentSuggestion>>> SuggestOwner(
        int id,
        CancellationToken ct)
    {
        var lead = await Db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        return Ok(AssignmentEngine.Suggest(lead, await LoadAgentsAsync(ct)));
    }

    /// <summary>
    /// Routes everything currently unassigned. Loads the candidate pool once
    /// and updates its counts as it goes, so a single run spreads work evenly
    /// instead of handing every lead to whoever started emptiest.
    /// </summary>
    [RequirePermission(SecuredObjects.Lead, ObjectAction.ModifyAll)]
    [HttpPost("leads/auto-assign")]
    public async Task<ActionResult<AutoAssignResultDto>> AutoAssign(CancellationToken ct)
    {
        var unassigned = await Db.Leads
            .Where(l => l.OwnerId == null && l.Stage != LeadStages.Lost && l.Stage != LeadStages.Booked)
            .OrderByDescending(l => l.CachedScore ?? 0)
            .ToListAsync(ct);

        if (unassigned.Count == 0)
        {
            return Ok(new AutoAssignResultDto(0, [], "Nothing to assign — every open lead already has an owner."));
        }

        var agents = (await LoadAgentsAsync(ct)).ToList();
        if (agents.Count == 0)
        {
            throw ApiException.BadRequest("No eligible sales agents found for these leads.");
        }

        var assignments = new List<AutoAssignEntryDto>();

        foreach (var lead in unassigned)
        {
            var best = AssignmentEngine.Suggest(lead, agents, take: 1).FirstOrDefault();
            if (best is null) continue;

            lead.OwnerId = best.UserId;

            Db.LeadActivities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.OwnerChange,
                Remarks = $"Auto-assigned to {best.Name}. {best.Reason}",
                ActorId = Db.Tenant.UserId,
                ActorName = Db.Tenant.UserName,
            });

            assignments.Add(new AutoAssignEntryDto(lead.Id, lead.Name, best.UserId, best.Name, best.Fit, best.Reason));

            // Reflect the new load immediately so the next lead sees it.
            var index = agents.FindIndex(a => a.UserId == best.UserId);
            if (index >= 0) agents[index] = agents[index] with { OpenLeads = agents[index].OpenLeads + 1 };
        }

        await Db.SaveChangesAsync(ct);

        return Ok(new AutoAssignResultDto(
            assignments.Count,
            assignments,
            $"{assignments.Count} lead(s) routed across {assignments.Select(a => a.OwnerId).Distinct().Count()} agent(s)."));
    }

    private async Task<IReadOnlyList<AgentLoad>> LoadAgentsAsync(CancellationToken ct)
    {
        var since = DateTime.UtcNow.AddDays(-90);
        var now = DateTime.UtcNow;

        var users = await Db.Users
            .Where(u => u.CompanyId == Db.Tenant.CompanyId && u.IsActive)
            .Include(u => u.UserBranches).ThenInclude(ub => ub.Branch)
            .AsNoTracking()
            .ToListAsync(ct);

        var openLeads = await Db.Leads
            .Where(l => l.OwnerId != null)
            .Where(l => l.Stage != LeadStages.Booked && l.Stage != LeadStages.Booked && l.Stage != LeadStages.Lost)
            .GroupBy(l => l.OwnerId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        var overdue = await Db.FollowUps
            .Where(f => f.OwnerId != null && f.DueAt < now)
            .Where(f => f.Status == FollowUpStatuses.Open || f.Status == FollowUpStatuses.InProgress)
            .GroupBy(f => f.OwnerId!.Value)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        var closed = await Db.Leads
            .Where(l => l.OwnerId != null && l.UpdatedAt >= since)
            .Where(l => l.Stage == LeadStages.Booked || l.Stage == LeadStages.Lost || l.Stage == LeadStages.Booked)
            .GroupBy(l => l.OwnerId!.Value)
            .Select(g => new
            {
                Id = g.Key,
                Total = g.Count(),
                Won = g.Count(l => l.Stage == LeadStages.Booked),
            })
            .ToDictionaryAsync(x => x.Id, ct);

        return users.Select(u => new AgentLoad(
            u.Id,
            u.Name,
            u.Role,
            u.UserBranches.Select(ub => ub.BranchId).ToList(),
            openLeads.GetValueOrDefault(u.Id),
            overdue.GetValueOrDefault(u.Id),
            closed.GetValueOrDefault(u.Id)?.Won ?? 0,
            closed.GetValueOrDefault(u.Id)?.Total ?? 0,
            u.UserBranches.FirstOrDefault()?.Branch?.City)).ToList();
    }

    /* ------------------------------------------------------------------ *
     * Opportunities
     * ------------------------------------------------------------------ */

    [HttpGet("opportunities/{id:int}")]
    public async Task<ActionResult<WinInsight>> GetWinInsight(int id, CancellationToken ct)
    {
        var opportunity = await Db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Opportunity");

        var engagement = await coordinator.BuildEngagementAsync([id], ct);

        return Ok(winModel.Predict(
            opportunity,
            engagement.GetValueOrDefault(id, OpportunityEngagement.Empty),
            DateTime.UtcNow));
    }

    /* ------------------------------------------------------------------ *
     * Forecasting
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Monthly forecast for one of three series: won revenue, lead intake or
    /// bookings. History comes out of the database as-is — no smoothing is
    /// applied before the model sees it.
    /// </summary>
    [HttpGet("forecast")]
    public async Task<ActionResult<ForecastResult>> Forecast(
        [FromQuery] string metric = "revenue",
        [FromQuery] int horizon = 3,
        [FromQuery] int months = 24,
        CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddMonths(-Math.Clamp(months, 6, 60)).Date;

        var points = metric.ToLowerInvariant() switch
        {
            "leads" => await Db.Leads
                .Where(l => l.CreatedAt >= since)
                .Select(l => new DatedValue(l.CreatedAt, 1m))
                .ToListAsync(ct),

            "bookings" => await Db.Opportunities
                .Where(o => o.Stage == OpportunityStages.ClosedWon && o.ActualCloseDate >= since)
                .Select(o => new DatedValue(o.ActualCloseDate!.Value, 1m))
                .ToListAsync(ct),

            _ => await Db.Opportunities
                .Where(o => o.Stage == OpportunityStages.ClosedWon && o.ActualCloseDate >= since)
                .Select(o => new DatedValue(o.ActualCloseDate!.Value, o.Amount))
                .ToListAsync(ct),
        };

        return Ok(forecasting.Forecast(RollUpMonthly(points), Math.Clamp(horizon, 1, 12)));
    }

    /// <summary>
    /// Buckets by calendar month in memory. MySQL's date truncation varies by
    /// server version and the row count is already bounded by the date window,
    /// so this stays predictable across environments.
    /// </summary>
    private static List<(DateTime Period, decimal Value)> RollUpMonthly(IReadOnlyList<DatedValue> points) =>
        points
            .GroupBy(p => new DateTime(p.At.Year, p.At.Month, 1, 0, 0, 0, DateTimeKind.Utc))
            .OrderBy(g => g.Key)
            .Select(g => (g.Key, g.Sum(p => p.Value)))
            .ToList();

    /* ------------------------------------------------------------------ *
     * Segmentation
     * ------------------------------------------------------------------ */

    [HttpGet("segments")]
    public async Task<ActionResult<SegmentationResult>> Segments(
        [FromQuery] int clusters = 4,
        CancellationToken ct = default)
        => Ok(segmentation.Segment(await LoadContactFeaturesAsync(ct), clusters));

    /// <summary>Writes the cluster label back onto each contact so lists can filter by it.</summary>
    [RequirePermission(SecuredObjects.Lead, ObjectAction.ModifyAll)]
    [HttpPost("segments/apply")]
    public async Task<ActionResult<BulkResultDto>> ApplySegments(
        [FromQuery] int clusters = 4,
        CancellationToken ct = default)
    {
        var result = segmentation.Segment(await LoadContactFeaturesAsync(ct), clusters);
        var byContact = result.Assignments.ToDictionary(a => a.ContactId, a => a.Segment);

        var contacts = await Db.Contacts.Where(c => byContact.Keys.Contains(c.Id)).ToListAsync(ct);
        foreach (var contact in contacts)
        {
            contact.Segment = byContact.GetValueOrDefault(contact.Id);
        }

        await Db.SaveChangesAsync(ct);

        return Ok(new BulkResultDto(
            contacts.Count,
            $"{contacts.Count} contact(s) segmented into {result.Segments.Count} group(s) via {result.Engine}."));
    }

    private async Task<List<(int ContactId, ContactFeatureRow Features)>> LoadContactFeaturesAsync(
        CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var contacts = await Db.Contacts
            .Select(c => new
            {
                c.Id, c.LifetimeValue, c.DealCount, c.BudgetMin, c.BudgetMax,
                c.LastActivityAt, c.CreatedAt,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var engagement = await Db.CallLogs
            .Where(l => l.RelatedType == RelatedTypes.Contact)
            .GroupBy(l => l.RelatedId)
            .Select(g => new { Id = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);

        return contacts.Select(c => (c.Id, new ContactFeatureRow
        {
            LifetimeValue = (float)c.LifetimeValue,
            DealCount = c.DealCount,
            BudgetMid = (float)(((c.BudgetMin ?? 0m) + (c.BudgetMax ?? 0m)) / 2m),
            DaysSinceLastActivity = (float)(now - (c.LastActivityAt ?? c.CreatedAt)).TotalDays,
            TenureDays = (float)(now - c.CreatedAt).TotalDays,
            EngagementCount = engagement.GetValueOrDefault(c.Id),
        })).ToList();
    }

    /* ------------------------------------------------------------------ *
     * Anomalies
     * ------------------------------------------------------------------ */

    [HttpGet("anomalies")]
    public async Task<ActionResult<AnomalyReport>> Anomalies(
        [FromQuery] string metric = "leads",
        [FromQuery] int days = 90,
        CancellationToken ct = default)
    {
        var since = DateTime.UtcNow.AddDays(-Math.Clamp(days, 21, 365)).Date;

        var series = metric.ToLowerInvariant() switch
        {
            "calls" => await DailyAsync(
                await Db.CallLogs.Where(c => c.StartedAt >= since)
                    .Select(c => c.StartedAt).ToListAsync(ct), since),
            "visits" => await DailyAsync(
                await Db.SiteVisits.Where(v => v.ScheduledAt >= since)
                    .Select(v => v.ScheduledAt).ToListAsync(ct), since),
            _ => await DailyAsync(
                await Db.Leads.Where(l => l.CreatedAt >= since)
                    .Select(l => l.CreatedAt).ToListAsync(ct), since),
        };

        return Ok(anomalies.Detect(metric, series));
    }

    /// <summary>Fills gap days with zero — a day with no leads is data, not a missing row.</summary>
    private static Task<List<(DateTime Period, decimal Value)>> DailyAsync(
        List<DateTime> timestamps,
        DateTime since)
    {
        var counts = timestamps
            .GroupBy(t => t.Date)
            .ToDictionary(g => g.Key, g => (decimal)g.Count());

        var series = new List<(DateTime, decimal)>();
        for (var day = since.Date; day <= DateTime.UtcNow.Date; day = day.AddDays(1))
        {
            series.Add((day, counts.GetValueOrDefault(day)));
        }

        return Task.FromResult(series);
    }

    /* ------------------------------------------------------------------ *
     * Digest
     * ------------------------------------------------------------------ */

    /// <summary>
    /// One call for the intelligence strip at the top of the workspace: the
    /// leads that need attention now, plus the model status behind them.
    /// </summary>
    [HttpGet("digest")]
    public async Task<ActionResult<IntelligenceDigestDto>> Digest(CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var leads = await Db.Leads
            .Include(l => l.Activities)
            .Where(l => l.Stage != LeadStages.Lost && l.Stage != LeadStages.Booked)
            .AsNoTracking()
            .ToListAsync(ct);

        var scored = leads
            .Select(lead => (Lead: lead, Score: scoring.Score(lead, now)))
            .OrderByDescending(x => x.Score.Score)
            .ToList();

        var priority = scored
            .Take(8)
            .Select(x => new PriorityLeadDto(
                x.Lead.Id, x.Lead.Name, x.Lead.Stage, x.Lead.Priority,
                x.Score.Score, x.Score.Band,
                NextBestAction.For(x.Lead, x.Score, now).FirstOrDefault()?.Action ?? "Keep working the lead",
                x.Lead.OwnerId is null ? "Unassigned" : null))
            .ToList();

        var breached = leads.Count(l =>
            l.FirstResponseAt is null && l.SlaDueAt is not null && l.SlaDueAt < now);

        var stale = leads.Count(l => (l.LastActivityAt ?? l.CreatedAt) < now.AddDays(-14));

        var overdueFollowUps = await Db.FollowUps.CountAsync(f =>
            f.DueAt < now &&
            (f.Status == FollowUpStatuses.Open || f.Status == FollowUpStatuses.InProgress), ct);

        var expiringQuotes = await Db.Quotations.CountAsync(q =>
            q.ValidUntil >= now && q.ValidUntil < now.AddDays(7) &&
            q.Status != QuotationStatuses.Accepted && q.Status != QuotationStatuses.Rejected, ct);

        var expiringHolds = await Db.Units.CountAsync(u =>
            u.Status == UnitStatuses.Held && u.HeldUntil != null && u.HeldUntil < now.AddHours(24), ct);

        return Ok(new IntelligenceDigestDto(
            priority,
            scored.Count(x => x.Score.Band == "Hot"),
            leads.Count(l => l.OwnerId is null),
            breached,
            stale,
            overdueFollowUps,
            expiringQuotes,
            expiringHolds,
            [scoring.Health, winModel.Health, sentiment.Health]));
    }
}

/// <summary>A timestamped amount, projected straight out of SQL before roll-up.</summary>
public record DatedValue(DateTime At, decimal Value);

public record CheckDuplicatesRequest(
    string Name,
    string? Phone,
    string? Phone2,
    string? Email,
    string? City,
    int? ExcludeLeadId
);

public record AutoAssignEntryDto(
    int LeadId,
    string LeadName,
    int OwnerId,
    string OwnerName,
    double Fit,
    string Reason
);

public record AutoAssignResultDto(
    int Assigned,
    IReadOnlyList<AutoAssignEntryDto> Assignments,
    string Message
);

public record PriorityLeadDto(
    int LeadId,
    string Name,
    string Stage,
    string Priority,
    int Score,
    string Band,
    string NextAction,
    string? Flag
);

public record IntelligenceDigestDto(
    IReadOnlyList<PriorityLeadDto> PriorityLeads,
    int HotLeads,
    int UnassignedLeads,
    int SlaBreached,
    int StaleLeads,
    int OverdueFollowUps,
    int ExpiringQuotations,
    int ExpiringHolds,
    IReadOnlyList<ModelHealth> Models
);
