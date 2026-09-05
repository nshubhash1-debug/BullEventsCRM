namespace BullEvents.Api.Models;

/// <summary>
/// A number someone is expected to hit by a date.
///
/// The measurement half of a goal is deliberately the same shape as a dashboard
/// widget's query — object, measure, aggregate, filter, date field. That is what
/// lets a goal be set on anything the CRM can already count, rather than on a
/// fixed list of metrics somebody has to extend in code every time sales want to
/// track something new. Progress is never stored: it is recomputed from the same
/// query engine the dashboards use, so a goal cannot drift from the records it
/// claims to measure.
/// </summary>
public class Goal : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /* ---------------- what is measured ---------------- */

    /// <summary>Analytics dataset id, e.g. "opportunities".</summary>
    public string Dataset { get; set; } = "opportunities";

    /// <summary>Field to aggregate. Null counts records instead.</summary>
    public string? Measure { get; set; }

    public string Aggregation { get; set; } = "sum";

    /// <summary>Serialised <c>FilterNode</c> — the same tree the list views send.</summary>
    public string? FilterJson { get; set; }

    /// <summary>Date field the goal's window is applied to.</summary>
    public string DateField { get; set; } = "createdAt";

    /* ---------------- ratio goals ---------------- */

    /// <summary>
    /// A conversion-rate goal is two queries, not one: "12% of leads booked" is
    /// bookings over leads. When set, progress divides the main measure by this
    /// one and reports a percentage.
    /// </summary>
    public bool IsRatio { get; set; }

    public string? RatioDataset { get; set; }
    public string? RatioMeasure { get; set; }
    public string? RatioAggregation { get; set; }
    public string? RatioFilterJson { get; set; }
    public string? RatioDateField { get; set; }

    /// <summary>number, currency or percent — picks the client's formatter.</summary>
    public string Format { get; set; } = "currency";

    /* ---------------- period ---------------- */

    public string PeriodType { get; set; } = GoalPeriods.Quarter;

    /// <summary>Inclusive window the goal is measured over.</summary>
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }

    /* ---------------- who it belongs to ---------------- */

    public string ScopeType { get; set; } = GoalScopes.Company;

    /// <summary>Set when <see cref="ScopeType"/> is Branch.</summary>
    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }

    /// <summary>Set when <see cref="ScopeType"/> is User.</summary>
    public int? OwnerId { get; set; }
    public User? Owner { get; set; }

    public decimal TargetValue { get; set; }

    public string Status { get; set; } = GoalStatuses.Active;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }
}

public static class GoalPeriods
{
    public const string Month = "Month";
    public const string Quarter = "Quarter";
    public const string Year = "Year";
    public const string Custom = "Custom";

    public static readonly string[] All = [Month, Quarter, Year, Custom];
}

public static class GoalScopes
{
    public const string Company = "Company";
    public const string Branch = "Branch";
    public const string User = "User";

    public static readonly string[] All = [Company, Branch, User];
}

public static class GoalStatuses
{
    public const string Active = "Active";
    public const string Archived = "Archived";

    public static readonly string[] All = [Active, Archived];
}
