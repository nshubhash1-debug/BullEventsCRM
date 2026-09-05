using Microsoft.ML.Data;

namespace BullEvents.Api.Ml;

/* ------------------------------------------------------------------ *
 * Opportunity win model
 * ------------------------------------------------------------------ */

public class OpportunityFeatureRow
{
    public string Stage { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;

    public float Amount { get; set; }
    public float AgeDays { get; set; }
    public float DaysToExpectedClose { get; set; }
    public float DaysInStage { get; set; }
    public float SiteVisitCount { get; set; }
    public float CallCount { get; set; }
    public float QuotationCount { get; set; }
    public float FollowUpCount { get; set; }
    public float HasOwner { get; set; }

    public bool Won { get; set; }
}

public class WinPrediction
{
    [ColumnName("PredictedLabel")]
    public bool Won { get; set; }
    public float Probability { get; set; }
    public float Score { get; set; }
}

/* ------------------------------------------------------------------ *
 * Contact segmentation (KMeans)
 * ------------------------------------------------------------------ */

public class ContactFeatureRow
{
    public float LifetimeValue { get; set; }
    public float DealCount { get; set; }
    public float BudgetMid { get; set; }
    public float DaysSinceLastActivity { get; set; }
    public float TenureDays { get; set; }
    public float EngagementCount { get; set; }
}

public class ClusterPrediction
{
    [ColumnName("PredictedLabel")]
    public uint ClusterId { get; set; }

    [ColumnName("Score")]
    public float[] Distances { get; set; } = [];
}

/* ------------------------------------------------------------------ *
 * Call-note sentiment
 * ------------------------------------------------------------------ */

public class SentimentRow
{
    public string Text { get; set; } = string.Empty;
    public bool Positive { get; set; }
}

public class SentimentPrediction
{
    [ColumnName("PredictedLabel")]
    public bool Positive { get; set; }
    public float Probability { get; set; }
    public float Score { get; set; }
}

/* ------------------------------------------------------------------ *
 * Time series — booking revenue forecast and lead-volume anomalies
 * ------------------------------------------------------------------ */

public class TimePoint
{
    public float Value { get; set; }
    public DateTime Period { get; set; }
}

public class ForecastOutput
{
    public float[] Forecast { get; set; } = [];
    public float[] LowerBound { get; set; } = [];
    public float[] UpperBound { get; set; } = [];
}

public class AnomalyOutput
{
    /// <summary>[alert, score, p-value] as emitted by the IID spike detector.</summary>
    [VectorType(3)]
    public double[] Prediction { get; set; } = [];
}

/* ------------------------------------------------------------------ *
 * Shared result records
 * ------------------------------------------------------------------ */

public record ModelHealth(
    string Name,
    string Engine,
    bool IsTrained,
    DateTime? TrainedAt,
    int TrainingRows,
    double? PrimaryMetric,
    string? MetricName,
    string? Message
);
