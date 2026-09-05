using Microsoft.ML.Data;

namespace BullEvents.Api.Ml;

/// <summary>
/// One training/inference row for the lead conversion model. ML.NET reflects
/// over these properties, so names here line up with the column names used in
/// <see cref="LeadScoringService"/>'s pipeline.
/// </summary>
public class LeadFeatureRow
{
    public string Source { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public float HasEmail { get; set; }
    public float HasPhone { get; set; }
    public float HasCompany { get; set; }
    public float DaysToEvent { get; set; }
    public float HasBudget { get; set; }
    public float GuestCount { get; set; }
    public float DateFlexible { get; set; }
    public float QuestionnaireDone { get; set; }

    /// <summary>Total activities logged against the lead.</summary>
    public float ActivityCount { get; set; }

    /// <summary>Days between creation and either conversion or now.</summary>
    public float DaysOpen { get; set; }

    /// <summary>Days since the most recent activity — staleness signal.</summary>
    public float DaysSinceLastTouch { get; set; }

    /// <summary>Activities per week since creation — engagement intensity.</summary>
    public float TouchesPerWeek { get; set; }

    /// <summary>Training label: did this lead reach Booked?</summary>
    public bool Converted { get; set; }
}

public class LeadScorePrediction
{
    [ColumnName("PredictedLabel")]
    public bool WillConvert { get; set; }

    public float Probability { get; set; }

    public float Score { get; set; }
}

/// <summary>A single explainable contribution to a lead's score.</summary>
public record ScoreSignal(string Label, string Detail, int Points);

public record LeadScoreResult(
    int Score,
    string Band,
    string Engine,
    IReadOnlyList<ScoreSignal> Signals
);
