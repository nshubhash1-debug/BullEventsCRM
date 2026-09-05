using System.Text.Json;
using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Targets, and how far along they are.
///
/// A goal stores only its definition. Progress is recomputed on every read from
/// the same analytics engine the dashboards use, which is what stops the number
/// on a target from disagreeing with the chart underneath it — there is only one
/// path from records to a figure, and both go through it.
/// </summary>
[ApiController]
[Route("api/goals")]
[Authorize]
[RequireModule(Modules.Goals)]
[SecuredBy(SecuredObjects.Goal)]
public class GoalsController(AppDbContext db) : CrmControllerBase(db)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /* ------------------------------------------------------------------ *
     * Read
     * ------------------------------------------------------------------ */

    [HttpGet]
    public async Task<ActionResult<GoalListResponse>> List(
        [FromQuery] string? status,
        [FromQuery] string? scopeType,
        [FromQuery] int? ownerId,
        [FromQuery] int? branchId,
        /// <summary>Only goals whose window contains today.</summary>
        [FromQuery] bool activeOnly,
        CancellationToken cancellationToken = default)
    {
        var query = Db.Goals
            .Include(g => g.Owner)
            .Include(g => g.Branch)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(g => g.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(scopeType))
        {
            query = query.Where(g => g.ScopeType == scopeType);
        }

        if (ownerId is int owner) query = query.Where(g => g.OwnerId == owner);
        if (branchId is int branch) query = query.Where(g => g.BranchId == branch);

        if (activeOnly)
        {
            var today = DateTime.UtcNow.Date;
            query = query.Where(g => g.StartDate <= today && g.EndDate >= today);
        }

        var goals = await query
            .OrderBy(g => g.EndDate)
            .ThenBy(g => g.Name)
            .ToListAsync(cancellationToken);

        var results = new List<GoalDto>(goals.Count);

        // Sequential: one DbContext is not thread-safe, and each goal is one or
        // two small aggregates.
        foreach (var goal in goals)
        {
            results.Add(await ProjectAsync(goal, cancellationToken));
        }

        return Ok(new GoalListResponse(results, Summarise(results)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<GoalDto>> Get(int id, CancellationToken cancellationToken)
    {
        var goal = await Db.Goals
            .Include(g => g.Owner)
            .Include(g => g.Branch)
            .AsNoTracking()
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Goal");

        return Ok(await ProjectAsync(goal, cancellationToken));
    }

    /* ------------------------------------------------------------------ *
     * Write
     * ------------------------------------------------------------------ */

    [HttpPost]
    public async Task<ActionResult<GoalDto>> Create(
        [FromBody] GoalInput input,
        CancellationToken cancellationToken)
    {
        var goal = new Goal { CompanyId = Db.Tenant.CompanyId };
        await ApplyAsync(goal, input, cancellationToken);

        Db.Goals.Add(goal);
        await Db.SaveChangesAsync(cancellationToken);

        await Db.Entry(goal).Reference(g => g.Owner).LoadAsync(cancellationToken);
        await Db.Entry(goal).Reference(g => g.Branch).LoadAsync(cancellationToken);

        return Ok(await ProjectAsync(goal, cancellationToken));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<GoalDto>> Update(
        int id,
        [FromBody] GoalInput input,
        CancellationToken cancellationToken)
    {
        var goal = await Db.Goals
            .Include(g => g.Owner)
            .Include(g => g.Branch)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Goal");

        await ApplyAsync(goal, input, cancellationToken);
        await Db.SaveChangesAsync(cancellationToken);

        return Ok(await ProjectAsync(goal, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        var goal = await Db.Goals.FirstOrDefaultAsync(g => g.Id == id, cancellationToken)
            ?? throw ApiException.NotFound("Goal");

        SoftDelete(goal);
        await Db.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    /* ------------------------------------------------------------------ *
     * Mapping
     * ------------------------------------------------------------------ */

    private async Task ApplyAsync(Goal goal, GoalInput input, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw ApiException.BadRequest("Give the goal a name.");
        }

        if (input.TargetValue <= 0)
        {
            throw ApiException.BadRequest("A target has to be greater than zero.");
        }

        // Validating through the catalogue means a goal cannot be saved against
        // a dataset or measure the engine would later refuse to run.
        var dataset = AnalyticsCatalog.Require(input.Dataset);

        goal.Name = input.Name.Trim();
        goal.Description = input.Description?.Trim();
        goal.Dataset = dataset.Id;
        goal.Measure = string.IsNullOrWhiteSpace(input.Measure) || input.Measure == "*"
            ? null
            : input.Measure;
        goal.Aggregation = goal.Measure is null ? "count" : input.Aggregation;
        goal.FilterJson = Serialise(input.Filter);
        goal.DateField = string.IsNullOrWhiteSpace(input.DateField)
            ? dataset.DefaultDateField
            : input.DateField;

        goal.IsRatio = input.IsRatio;
        if (input.IsRatio)
        {
            var denominator = AnalyticsCatalog.Require(input.RatioDataset ?? input.Dataset);
            goal.RatioDataset = denominator.Id;
            goal.RatioMeasure = string.IsNullOrWhiteSpace(input.RatioMeasure) || input.RatioMeasure == "*"
                ? null
                : input.RatioMeasure;
            goal.RatioAggregation = goal.RatioMeasure is null ? "count" : input.RatioAggregation ?? "sum";
            goal.RatioFilterJson = Serialise(input.RatioFilter);
            goal.RatioDateField = string.IsNullOrWhiteSpace(input.RatioDateField)
                ? denominator.DefaultDateField
                : input.RatioDateField;
            // A rate is always a percentage, whatever the two measures are.
            goal.Format = "percent";
        }
        else
        {
            goal.RatioDataset = null;
            goal.RatioMeasure = null;
            goal.RatioAggregation = null;
            goal.RatioFilterJson = null;
            goal.RatioDateField = null;
            goal.Format = input.Format;
        }

        goal.TargetValue = input.TargetValue;
        goal.PeriodType = Require(input.PeriodType, GoalPeriods.All, "period");
        var (start, end) = ResolvePeriod(input);
        goal.StartDate = start;
        goal.EndDate = end;

        goal.ScopeType = Require(input.ScopeType, GoalScopes.All, "scope");
        goal.Status = Require(input.Status ?? GoalStatuses.Active, GoalStatuses.All, "status");

        switch (goal.ScopeType)
        {
            case GoalScopes.User:
                if (input.OwnerId is not int owner)
                {
                    throw ApiException.BadRequest("Pick the person this goal belongs to.");
                }
                await RequireOwnerAsync(owner, cancellationToken);
                goal.OwnerId = owner;
                goal.BranchId = null;
                break;

            case GoalScopes.Branch:
                if (input.BranchId is not int branch)
                {
                    throw ApiException.BadRequest("Pick the branch this goal belongs to.");
                }
                await RequireBranchAsync(branch, cancellationToken);
                goal.BranchId = branch;
                goal.OwnerId = null;
                break;

            default:
                goal.OwnerId = null;
                goal.BranchId = null;
                break;
        }
    }

    /// <summary>
    /// The window a goal covers. Named periods are derived from an anchor date
    /// rather than stored as free dates, so "this quarter" means the same thing
    /// to everyone who reads it.
    /// </summary>
    private static (DateTime Start, DateTime End) ResolvePeriod(GoalInput input)
    {
        var anchor = (input.Anchor ?? DateTime.UtcNow).Date;

        switch (input.PeriodType)
        {
            case GoalPeriods.Month:
            {
                var start = new DateTime(anchor.Year, anchor.Month, 1);
                return (start, start.AddMonths(1).AddDays(-1));
            }

            case GoalPeriods.Year:
                return (new DateTime(anchor.Year, 1, 1), new DateTime(anchor.Year, 12, 31));

            case GoalPeriods.Custom:
            {
                if (input.StartDate is not DateTime from || input.EndDate is not DateTime to)
                {
                    throw ApiException.BadRequest("A custom period needs a start and an end date.");
                }
                if (to.Date < from.Date)
                {
                    throw ApiException.BadRequest("The goal ends before it starts.");
                }
                return (from.Date, to.Date);
            }

            default:
            {
                var quarterStart = new DateTime(anchor.Year, ((anchor.Month - 1) / 3 * 3) + 1, 1);
                return (quarterStart, quarterStart.AddMonths(3).AddDays(-1));
            }
        }
    }

    /* ------------------------------------------------------------------ *
     * Progress
     * ------------------------------------------------------------------ */

    private async Task<GoalDto> ProjectAsync(Goal goal, CancellationToken cancellationToken)
    {
        var dataset = AnalyticsCatalog.Require(goal.Dataset);

        var actual = await dataset.ScalarAsync(Db, new ScalarQuery
        {
            Measure = goal.Measure,
            Aggregation = goal.Aggregation,
            Filter = Deserialise(goal.FilterJson),
            DateField = goal.DateField,
            From = goal.StartDate,
            To = goal.EndDate,
            OwnerId = goal.OwnerId,
            BranchId = goal.BranchId,
        }, cancellationToken);

        if (goal.IsRatio)
        {
            var denominatorSet = AnalyticsCatalog.Require(goal.RatioDataset ?? goal.Dataset);

            var denominator = await denominatorSet.ScalarAsync(Db, new ScalarQuery
            {
                Measure = goal.RatioMeasure,
                Aggregation = goal.RatioAggregation ?? "count",
                Filter = Deserialise(goal.RatioFilterJson),
                DateField = goal.RatioDateField,
                From = goal.StartDate,
                To = goal.EndDate,
                OwnerId = goal.OwnerId,
                BranchId = goal.BranchId,
            }, cancellationToken);

            // No denominator is 0%, not an error and not a division by zero.
            actual = denominator == 0 ? 0m : Math.Round(actual / denominator * 100m, 2);
        }

        var today = DateTime.UtcNow.Date;
        var totalDays = Math.Max(1, (goal.EndDate.Date - goal.StartDate.Date).Days + 1);
        var elapsed = Math.Clamp((today - goal.StartDate.Date).Days + 1, 0, totalDays);
        var remaining = Math.Max(0, totalDays - elapsed);

        var percent = goal.TargetValue == 0
            ? 0m
            : Math.Round(actual / goal.TargetValue * 100m, 1);

        // Even pace across the window. Crude on a business that closes in
        // bursts, but it is the only assumption that needs no extra data, and
        // it is stated as "expected by now" rather than a forecast.
        var expected = Math.Round(goal.TargetValue * elapsed / totalDays, 2);
        var pace = expected == 0 ? 0m : Math.Round(actual / expected * 100m, 1);
        var projected = elapsed == 0 ? 0m : Math.Round(actual / elapsed * totalDays, 2);

        return new GoalDto(
            goal.Id,
            goal.Name,
            goal.Description,
            goal.Dataset,
            dataset.Label,
            goal.Measure,
            goal.Measure is null
                ? $"Number of {dataset.RecordLabel}"
                : dataset.Measures.FirstOrDefault(m => m.Id == goal.Measure)?.Label ?? goal.Measure,
            goal.Aggregation,
            goal.DateField,
            goal.IsRatio,
            goal.Format,
            goal.PeriodType,
            goal.StartDate,
            goal.EndDate,
            goal.ScopeType,
            goal.BranchId,
            goal.Branch?.Name,
            goal.OwnerId,
            goal.Owner?.Name,
            goal.TargetValue,
            goal.Status,
            actual,
            percent,
            expected,
            pace,
            projected,
            elapsed,
            remaining,
            totalDays,
            Health(actual, goal.TargetValue, expected, today, goal));
    }

    /// <summary>
    /// Achieved and Missed are terminal and decided by the end date; before that
    /// a goal is only ahead of or behind its own pace.
    /// </summary>
    private static string Health(
        decimal actual,
        decimal target,
        decimal expected,
        DateTime today,
        Goal goal)
    {
        if (actual >= target && target > 0) return GoalHealth.Achieved;
        if (today > goal.EndDate.Date) return GoalHealth.Missed;
        if (today < goal.StartDate.Date) return GoalHealth.NotStarted;
        return actual >= expected ? GoalHealth.OnTrack : GoalHealth.Behind;
    }

    private static GoalSummaryDto Summarise(List<GoalDto> goals) => new(
        goals.Count,
        goals.Count(g => g.Health == GoalHealth.Achieved),
        goals.Count(g => g.Health == GoalHealth.OnTrack),
        goals.Count(g => g.Health == GoalHealth.Behind),
        goals.Count(g => g.Health == GoalHealth.Missed),
        goals.Count == 0 ? 0m : Math.Round(goals.Average(g => g.PercentComplete), 1));

    /* ---------------- json ---------------- */

    private static string? Serialise(FilterNode? filter) =>
        filter is null ? null : JsonSerializer.Serialize(filter, Json);

    private static FilterNode? Deserialise(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;

        try
        {
            return JsonSerializer.Deserialize<FilterNode>(json, Json);
        }
        catch (JsonException)
        {
            // A goal saved against an older filter shape should read as
            // unfiltered rather than take the whole page down.
            return null;
        }
    }
}
