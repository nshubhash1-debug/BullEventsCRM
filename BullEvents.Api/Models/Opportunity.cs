namespace BullEvents.Api.Models;

/// <summary>
/// A revenue-bearing deal. Leads become opportunities on conversion; from then
/// on the opportunity — not the lead — carries amount, close date and forecast.
/// </summary>
public class Opportunity : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>
    /// Values for the fields this company added, keyed by the definition's key.
    ///
    /// A jsonb column rather than a row-per-value table: the values are read
    /// with the record every time and almost never on their own, so a join per
    /// field would cost more than it saves. Postgres can index inside it if a
    /// filter ever needs to.
    /// </summary>
    public string? CustomFields { get; set; }
    public int BranchId { get; set; }

    public string Name { get; set; } = string.Empty;
    public int? ContactId { get; set; }
    public int? LeadId { get; set; }
    public int? ProjectId { get; set; }
    public int? UnitId { get; set; }

    public string Stage { get; set; } = OpportunityStages.Qualification;
    public string Type { get; set; } = OpportunityTypes.NewBusiness;
    public string Source { get; set; } = LeadSources.Other;
    public string ForecastCategory { get; set; } = ForecastCategories.Pipeline;

    public decimal Amount { get; set; }
    public decimal? ExpectedCommission { get; set; }
    public string Currency { get; set; } = "INR";

    /// <summary>Rep-entered probability. The ML model's view lives alongside it, never overwriting it.</summary>
    public int Probability { get; set; } = 10;

    public DateTime ExpectedCloseDate { get; set; } = DateTime.UtcNow.AddDays(30);
    public DateTime? ActualCloseDate { get; set; }

    public int? OwnerId { get; set; }
    public string? NextStep { get; set; }
    public DateTime? NextStepDueAt { get; set; }
    public string? LossReason { get; set; }
    public string? CompetitorName { get; set; }
    public string? Description { get; set; }

    /// <summary>Days spent in the current stage — refreshed whenever the stage moves.</summary>
    public DateTime StageEnteredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The last open stage this deal sat in before it closed.
    ///
    /// Without it the win model cannot be trained honestly: a closed deal's
    /// Stage *is* the outcome, so feeding it in would let the model read the
    /// answer off its own input. This records how far the deal had genuinely
    /// travelled, which is what an open deal's stage actually means.
    /// </summary>
    public string? LastOpenStage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Branch? Branch { get; set; }
    public Contact? Contact { get; set; }
    public User? Owner { get; set; }
    public Project? Project { get; set; }
    public Unit? Unit { get; set; }
}
