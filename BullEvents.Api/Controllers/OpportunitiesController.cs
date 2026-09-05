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
/// Opportunities and the pipeline board they render on. Stage moves go through
/// here rather than a generic update so the forecast category, stage clock and
/// close date stay consistent with the stage.
/// </summary>
[ApiController]
[Route("api/opportunities")]
[Authorize]
[SecuredBy(SecuredObjects.Opportunity)]
public class OpportunitiesController(
    AppDbContext db,
    OpportunityWinModel winModel,
    MlTrainingCoordinator coordinator) : CrmControllerBase(db)
{
    internal static readonly FieldMap<Opportunity> Fields = new FieldMap<Opportunity>()
        .Text("name", "Opportunity name", searchable: true)
        .Text("contactName", "Contact", "Contact.FullName", searchable: true)
        .Text("description", "Description")
        .Select("stage", "Stage")
        .Select("type", "Type")
        .Select("source", "Source")
        .Select("forecastCategory", "Forecast category")
        .Number("amount", "Amount")
        .Number("expectedCommission", "Expected commission")
        .Number("probability", "Probability")
        .Date("expectedCloseDate", "Expected close")
        .Date("actualCloseDate", "Actual close")
        .Date("stageEnteredAt", "Stage entered")
        .Date("createdAt", "Created")
        .Date("updatedAt", "Last modified")
        .Select("ownerName", "Owner", "Owner.Name")
        .Select("branchName", "Branch", "Branch.Name")
        .Select("projectName", "Project", "Project.Name")
        .Text("nextStep", "Next step")
        .Date("nextStepDueAt", "Next step due")
        .Select("lossReason", "Loss reason")
        .Text("competitorName", "Competitor", searchable: true)
        .Number("ownerId", "Owner ID")
        .Number("branchId", "Branch ID")
        .Number("contactId", "Contact ID");

    private IQueryable<Opportunity> Base() => Db.Opportunities
        .Include(o => o.Branch)
        .Include(o => o.Owner)
        .Include(o => o.Contact)
        .Include(o => o.Project)
        .Include(o => o.Unit)
        .AsNoTracking();

    private static OpportunityDto ToDto(Opportunity o, int? aiProbability = null, string? aiBand = null) => new(
        o.Id, o.Name, o.ContactId, o.Contact?.FullName, o.LeadId,
        o.ProjectId, o.Project?.Name, o.UnitId, o.Unit?.UnitNumber,
        o.Stage, o.Type, o.Source, o.ForecastCategory,
        o.Amount, o.ExpectedCommission, o.Currency, o.Probability, aiProbability, aiBand,
        o.ExpectedCloseDate, o.ActualCloseDate,
        (int)Math.Max(0, (DateTime.UtcNow - o.StageEnteredAt).TotalDays),
        o.BranchId, o.Branch?.Name ?? "—", o.OwnerId, o.Owner?.Name,
        o.NextStep, o.NextStepDueAt, o.LossReason, o.CompetitorName, o.Description,
        o.CreatedAt, o.UpdatedAt);

    /* ------------------------------------------------------------------ *
     * Reads
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<FilterFieldDto>>> GetFields(CancellationToken ct)
    {
        var owners = await Db.Users
            .Where(u => u.CompanyId == Db.Tenant.CompanyId && u.IsActive)
            .Select(u => u.Name).OrderBy(n => n).ToListAsync(ct);

        var branches = await Db.Branches
            .Where(b => b.CompanyId == Db.Tenant.CompanyId)
            .Select(b => b.Name).OrderBy(n => n).ToListAsync(ct);

        var projects = await Db.Projects.Select(p => p.Name).OrderBy(n => n).ToListAsync(ct);

        var lossReasons = await Db.Opportunities
            .Where(o => o.LossReason != null)
            .Select(o => o.LossReason!).Distinct().ToListAsync(ct);

        return Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["stage"] = Options(OpportunityStages.All),
            ["type"] = Options(OpportunityTypes.All),
            ["source"] = Options(LeadSources.All),
            ["forecastCategory"] = Options(ForecastCategories.All),
            ["ownerName"] = Options([.. owners]),
            ["branchName"] = Options([.. branches]),
            ["projectName"] = Options([.. projects]),
            ["lossReason"] = Options([.. lossReasons]),
        }, new Dictionary<string, string>
        {
            ["name"] = "Deal",
            ["contactName"] = "Deal",
            ["description"] = "Deal",
            ["stage"] = "Pipeline",
            ["type"] = "Pipeline",
            ["source"] = "Pipeline",
            ["forecastCategory"] = "Pipeline",
            ["amount"] = "Commercial",
            ["expectedCommission"] = "Commercial",
            ["probability"] = "Commercial",
            ["expectedCloseDate"] = "Timing",
            ["actualCloseDate"] = "Timing",
            ["stageEnteredAt"] = "Timing",
            ["nextStepDueAt"] = "Timing",
        }));
    }

    [PermissionAction(ObjectAction.View)]
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<OpportunityDto>>> Query(QueryRequest request, CancellationToken ct)
    {
        var result = await RunQueryAsync(
            Base(),
            request,
            Fields,
            o => ToDto(o),
            defaultSortPath: "ExpectedCloseDate",
            defaultSortDescending: false,
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["pipelineValue"] = await filtered.SumAsync(o => o.Amount, ct),
                ["weightedValue"] = await filtered.SumAsync(o => o.Amount * o.Probability / 100m, ct),
                ["commission"] = await filtered.SumAsync(o => o.ExpectedCommission ?? 0m, ct),
                ["won"] = await filtered.CountAsync(o => o.Stage == OpportunityStages.ClosedWon, ct),
            },
            cancellationToken: ct);

        return Ok(await DecorateAsync(result.Items, result, ct));
    }

    /// <summary>
    /// The Kanban board: every open stage with its deals, value and weighted
    /// value. Returned as one payload so the board renders in a single request
    /// instead of one per column.
    /// </summary>
    [PermissionAction(ObjectAction.View)]
    [HttpPost("pipeline")]
    public async Task<ActionResult<PipelineBoardDto>> Pipeline(QueryRequest request, CancellationToken ct)
    {
        var filtered = QueryEngine.Apply(Base(), request, Fields);

        var deals = await filtered
            .OrderByDescending(o => o.Amount)
            .Take(600)
            .ToListAsync(ct);

        var engagement = await coordinator.BuildEngagementAsync(
            deals.Select(d => d.Id).ToList(), ct);

        var now = DateTime.UtcNow;

        var columns = OpportunityStages.All
            .Select(stage =>
            {
                var inStage = deals.Where(d => d.Stage == stage).ToList();

                return new PipelineColumnDto(
                    stage,
                    Humanise(stage),
                    inStage.Count,
                    inStage.Sum(d => d.Amount),
                    inStage.Sum(d => d.Amount * d.Probability / 100m),
                    inStage.Count == 0 ? 0 : Math.Round(inStage.Average(d =>
                        (now - d.StageEnteredAt).TotalDays), 1),
                    inStage
                        .Select(d =>
                        {
                            var insight = winModel.Predict(
                                d, engagement.GetValueOrDefault(d.Id, OpportunityEngagement.Empty), now);
                            return ToDto(d, insight.Probability, insight.Band);
                        })
                        .ToList());
            })
            .ToList();

        var open = deals.Where(d => OpportunityStages.Open.Contains(d.Stage)).ToList();
        var closed = deals.Where(d => OpportunityStages.IsClosed(d.Stage)).ToList();
        var won = closed.Count(d => d.Stage == OpportunityStages.ClosedWon);

        return Ok(new PipelineBoardDto(
            columns,
            deals.Count,
            open.Sum(d => d.Amount),
            open.Sum(d => d.Amount * d.Probability / 100m),
            closed.Count == 0 ? 0 : Math.Round(won * 100.0 / closed.Count, 1),
            open.Count == 0 ? 0 : Math.Round(open.Average(d => (now - d.CreatedAt).TotalDays), 1),
            winModel.Engine));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<OpportunityDto>> GetOne(int id, CancellationToken ct)
    {
        var opportunity = await Base().FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Opportunity");

        var engagement = await coordinator.BuildEngagementAsync([id], ct);
        var insight = winModel.Predict(
            opportunity, engagement.GetValueOrDefault(id, OpportunityEngagement.Empty), DateTime.UtcNow);

        return Ok(ToDto(opportunity, insight.Probability, insight.Band));
    }

    /* ------------------------------------------------------------------ *
     * Writes
     * ------------------------------------------------------------------ */

    [HttpPost]
    public async Task<ActionResult<OpportunityDto>> Create(OpportunityInput input, CancellationToken ct)
    {
        var branch = await RequireBranchAsync(input.BranchId, ct);
        await RequireOwnerAsync(input.OwnerId, ct);

        var opportunity = new Opportunity
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            OwnerId = input.OwnerId,
            StageEnteredAt = DateTime.UtcNow,
        };

        Apply(opportunity, input);

        Db.Opportunities.Add(opportunity);
        await Db.SaveChangesAsync(ct);

        opportunity.Branch = branch;
        return CreatedAtAction(nameof(GetOne), new { id = opportunity.Id }, ToDto(opportunity));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<OpportunityDto>> Update(int id, OpportunityInput input, CancellationToken ct)
    {
        var opportunity = await Db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Opportunity");

        var branch = await RequireBranchAsync(input.BranchId, ct);
        await RequireOwnerAsync(input.OwnerId, ct);

        if (opportunity.Stage != input.Stage)
        {
            if (OpportunityStages.IsClosed(input.Stage) && !OpportunityStages.IsClosed(opportunity.Stage))
            {
                opportunity.LastOpenStage = opportunity.Stage;
            }

            opportunity.StageEnteredAt = DateTime.UtcNow;
        }

        opportunity.BranchId = branch.Id;
        opportunity.OwnerId = input.OwnerId;
        Apply(opportunity, input);

        await Db.SaveChangesAsync(ct);

        opportunity.Branch = branch;
        return Ok(ToDto(opportunity));
    }

    /// <summary>Drag-and-drop on the board lands here.</summary>
    [HttpPatch("{id:int}/stage")]
    public async Task<ActionResult<OpportunityDto>> MoveStage(int id, MoveStageRequest request, CancellationToken ct)
    {
        var opportunity = await Db.Opportunities
            .Include(o => o.Branch).Include(o => o.Owner).Include(o => o.Contact)
            .FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Opportunity");

        var stage = Require(request.Stage, OpportunityStages.All, "stage");

        if (stage == OpportunityStages.ClosedLost && string.IsNullOrWhiteSpace(request.LossReason))
        {
            throw ApiException.BadRequest("A loss reason is required when closing a deal as lost.");
        }

        if (opportunity.Stage != stage)
        {
            // Remember how far the deal had travelled before it closed — the
            // win model trains on this rather than on the outcome itself.
            if (OpportunityStages.IsClosed(stage) && !OpportunityStages.IsClosed(opportunity.Stage))
            {
                opportunity.LastOpenStage = opportunity.Stage;
            }

            opportunity.StageEnteredAt = DateTime.UtcNow;
            opportunity.Probability = request.Probability ?? OpportunityStages.DefaultProbability(stage);
        }

        opportunity.Stage = stage;
        opportunity.LossReason = stage == OpportunityStages.ClosedLost ? request.LossReason : null;
        opportunity.ForecastCategory = ForecastCategories.ForStage(stage, opportunity.Probability);
        opportunity.ActualCloseDate = OpportunityStages.IsClosed(stage) ? DateTime.UtcNow : null;

        // Winning a deal takes the unit off the market — the safeguard against
        // the same unit being sold twice.
        if (stage == OpportunityStages.ClosedWon && opportunity.UnitId is int unitId)
        {
            var unit = await Db.Units.FirstOrDefaultAsync(u => u.Id == unitId, ct);
            if (unit is not null)
            {
                unit.Status = UnitStatuses.Booked;
                unit.BookedByContactId = opportunity.ContactId;
                unit.BookedAt = DateTime.UtcNow;
                unit.HeldByUserId = null;
                unit.HeldUntil = null;
            }
        }

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(opportunity));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var opportunity = await Db.Opportunities.FirstOrDefaultAsync(o => o.Id == id, ct)
            ?? throw ApiException.NotFound("Opportunity");

        SoftDelete(opportunity);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ------------------------------------------------------------------ *
     * Helpers
     * ------------------------------------------------------------------ */

    private async Task<PagedResult<OpportunityDto>> DecorateAsync(
        IReadOnlyList<OpportunityDto> items,
        PagedResult<OpportunityDto> result,
        CancellationToken ct)
    {
        if (items.Count == 0) return result;

        var ids = items.Select(i => i.Id).ToList();
        var entities = await Db.Opportunities
            .Where(o => ids.Contains(o.Id))
            .AsNoTracking()
            .ToDictionaryAsync(o => o.Id, ct);

        var engagement = await coordinator.BuildEngagementAsync(ids, ct);
        var now = DateTime.UtcNow;

        result.Items = items
            .Select(dto =>
            {
                if (!entities.TryGetValue(dto.Id, out var entity)) return dto;

                var insight = winModel.Predict(
                    entity, engagement.GetValueOrDefault(dto.Id, OpportunityEngagement.Empty), now);

                return dto with { AiProbability = insight.Probability, AiBand = insight.Band };
            })
            .ToList();

        return result;
    }

    private static void Apply(Opportunity opportunity, OpportunityInput input)
    {
        opportunity.Name = input.Name.Trim();
        opportunity.ContactId = input.ContactId;
        opportunity.LeadId = input.LeadId;
        opportunity.ProjectId = input.ProjectId;
        opportunity.UnitId = input.UnitId;
        opportunity.Stage = Require(input.Stage, OpportunityStages.All, "stage");
        opportunity.Type = Require(input.Type, OpportunityTypes.All, "opportunity type");
        opportunity.Source = Require(input.Source, LeadSources.All, "source");
        opportunity.Amount = Math.Max(0, input.Amount);
        opportunity.ExpectedCommission = input.ExpectedCommission;
        opportunity.Probability = Math.Clamp(input.Probability, 0, 100);
        opportunity.ExpectedCloseDate = input.ExpectedCloseDate;
        opportunity.NextStep = input.NextStep;
        opportunity.NextStepDueAt = input.NextStepDueAt;
        opportunity.LossReason = input.LossReason;
        opportunity.CompetitorName = input.CompetitorName;
        opportunity.Description = input.Description;
        opportunity.ForecastCategory = ForecastCategories.ForStage(
            opportunity.Stage, opportunity.Probability);
        opportunity.ActualCloseDate = OpportunityStages.IsClosed(opportunity.Stage)
            ? opportunity.ActualCloseDate ?? DateTime.UtcNow
            : null;
    }
}

public record MoveStageRequest(string Stage, int? Probability, string? LossReason);

public record PipelineColumnDto(
    string Stage,
    string Label,
    int Count,
    decimal Value,
    decimal WeightedValue,
    double AverageDaysInStage,
    IReadOnlyList<OpportunityDto> Deals
);

public record PipelineBoardDto(
    IReadOnlyList<PipelineColumnDto> Columns,
    int TotalDeals,
    decimal OpenValue,
    decimal WeightedValue,
    double WinRate,
    double AverageAgeDays,
    string ScoringEngine
);
