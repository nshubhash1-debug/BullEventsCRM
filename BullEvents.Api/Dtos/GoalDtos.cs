using BullEvents.Api.Infrastructure;

namespace BullEvents.Api.Dtos;

/* ------------------------------------------------------------------ *
 * Write
 * ------------------------------------------------------------------ */

public class GoalInput
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public string Dataset { get; set; } = "opportunities";
    public string? Measure { get; set; }
    public string Aggregation { get; set; } = "sum";
    public FilterNode? Filter { get; set; }
    public string DateField { get; set; } = "createdAt";

    public bool IsRatio { get; set; }
    public string? RatioDataset { get; set; }
    public string? RatioMeasure { get; set; }
    public string? RatioAggregation { get; set; }
    public FilterNode? RatioFilter { get; set; }
    public string? RatioDateField { get; set; }

    public string Format { get; set; } = "currency";

    public string PeriodType { get; set; } = "Quarter";

    /// <summary>Only read when <see cref="PeriodType"/> is Custom; otherwise derived.</summary>
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }

    /// <summary>Anchors a non-custom period, e.g. which quarter. Defaults to today.</summary>
    public DateTime? Anchor { get; set; }

    public string ScopeType { get; set; } = "Company";
    public int? BranchId { get; set; }
    public int? OwnerId { get; set; }

    public decimal TargetValue { get; set; }
    public string? Status { get; set; }
}

/* ------------------------------------------------------------------ *
 * Read
 * ------------------------------------------------------------------ */

/// <summary>
/// A goal plus where it stands.
///
/// Progress is computed on read rather than stored: the underlying records move
/// constantly, and a cached number would be wrong within the hour.
/// </summary>
public record GoalDto(
    int Id,
    string Name,
    string? Description,
    string Dataset,
    string DatasetLabel,
    string? Measure,
    string MeasureLabel,
    string Aggregation,
    string DateField,
    bool IsRatio,
    string Format,
    string PeriodType,
    DateTime StartDate,
    DateTime EndDate,
    string ScopeType,
    int? BranchId,
    string? BranchName,
    int? OwnerId,
    string? OwnerName,
    decimal TargetValue,
    string Status,

    /* --- progress --- */

    decimal Actual,
    /// <summary>Actual over target, as a percentage. Capped for display at the client.</summary>
    decimal PercentComplete,
    /// <summary>What the goal should be at today if progress were even across the window.</summary>
    decimal ExpectedByNow,
    /// <summary>Actual over expected — above 100 means ahead of pace.</summary>
    decimal PacePercent,
    /// <summary>Where this lands if the current rate holds to the end date.</summary>
    decimal Projected,
    int DaysElapsed,
    int DaysRemaining,
    int DaysTotal,
    /// <summary>OnTrack, Behind, Achieved, Missed or NotStarted.</summary>
    string Health
);

public static class GoalHealth
{
    public const string NotStarted = "NotStarted";
    public const string OnTrack = "OnTrack";
    public const string Behind = "Behind";
    public const string Achieved = "Achieved";
    public const string Missed = "Missed";
}

/// <summary>Roll-up across a set of goals, for the page header.</summary>
public record GoalSummaryDto(
    int Total,
    int Achieved,
    int OnTrack,
    int Behind,
    int Missed,
    /// <summary>Average completion across every goal in the set.</summary>
    decimal AveragePercent
);

public record GoalListResponse(
    IReadOnlyList<GoalDto> Goals,
    GoalSummaryDto Summary
);
