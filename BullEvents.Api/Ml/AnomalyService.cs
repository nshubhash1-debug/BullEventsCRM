using Microsoft.ML;

namespace BullEvents.Api.Ml;

public record AnomalyPoint(
    string Period,
    decimal Value,
    bool IsAnomaly,
    double Score,
    double PValue,
    string? Note
);

public record AnomalyReport(
    string Engine,
    string Metric,
    IReadOnlyList<AnomalyPoint> Points,
    int AnomalyCount,
    string Message
);

/// <summary>
/// Watches daily operational series — lead intake, call volume, booking value —
/// for points that break the recent pattern.
///
/// Uses ML.NET's IID spike detector, which is the right tool for "is today
/// unusual given the last N days" without assuming a seasonal shape. A drop in
/// lead intake or a spike in lost deals shows up here before anyone notices it
/// on a dashboard.
/// </summary>
public class AnomalyService(ILogger<AnomalyService> logger)
{
    private const int MinimumPoints = 14;

    private readonly MLContext _ml = new(seed: 19);

    public AnomalyReport Detect(
        string metric,
        IReadOnlyList<(DateTime Period, decimal Value)> series,
        double confidence = 95.0)
    {
        var ordered = series.OrderBy(p => p.Period).ToList();

        if (ordered.Count < MinimumPoints)
        {
            return new AnomalyReport(
                "Insufficient history",
                metric,
                ordered.Select(p => new AnomalyPoint(
                    p.Period.ToString("yyyy-MM-dd"), p.Value, false, 0, 1, null)).ToList(),
                0,
                $"{ordered.Count} data point(s) — spike detection needs at least {MinimumPoints}.");
        }

        try
        {
            var rows = ordered
                .Select(p => new TimePoint { Period = p.Period, Value = (float)p.Value })
                .ToList();

            var data = _ml.Data.LoadFromEnumerable(rows);

            // The detector compares each point against a sliding history; the
            // window has to stay well inside the series or it has nothing to
            // compare against.
            var pvalueHistory = Math.Max(4, Math.Min(30, ordered.Count / 4));

            var estimator = _ml.Transforms.DetectIidSpike(
                outputColumnName: nameof(AnomalyOutput.Prediction),
                inputColumnName: nameof(TimePoint.Value),
                confidence: confidence,
                pvalueHistoryLength: pvalueHistory);

            var transformed = estimator.Fit(data).Transform(data);
            var predictions = _ml.Data
                .CreateEnumerable<AnomalyOutput>(transformed, reuseRowObject: false)
                .ToList();

            var mean = ordered.Average(p => (double)p.Value);
            var points = new List<AnomalyPoint>();

            for (var i = 0; i < ordered.Count; i++)
            {
                var prediction = predictions.ElementAtOrDefault(i)?.Prediction ?? [0, 0, 1];
                var isAnomaly = prediction[0] == 1;
                var value = (double)ordered[i].Value;

                points.Add(new AnomalyPoint(
                    ordered[i].Period.ToString("yyyy-MM-dd"),
                    ordered[i].Value,
                    isAnomaly,
                    Math.Round(prediction[1], 4),
                    Math.Round(prediction[2], 5),
                    isAnomaly
                        ? value >= mean
                            ? $"Unusually high — {value / Math.Max(mean, 0.01):F1}× the period average."
                            : $"Unusually low — {value / Math.Max(mean, 0.01):P0} of the period average."
                        : null));
            }

            var count = points.Count(p => p.IsAnomaly);

            return new AnomalyReport(
                "MLNet:IidSpikeDetector",
                metric,
                points,
                count,
                count == 0
                    ? $"No unusual movement across {ordered.Count} days at {confidence:F0}% confidence."
                    : $"{count} unusual point(s) across {ordered.Count} days at {confidence:F0}% confidence.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Spike detection failed for {Metric}; falling back to z-score", metric);
            return ZScore(metric, ordered);
        }
    }

    /// <summary>Plain three-sigma test, used when the detector cannot run.</summary>
    private static AnomalyReport ZScore(
        string metric,
        List<(DateTime Period, decimal Value)> ordered)
    {
        var values = ordered.Select(p => (double)p.Value).ToList();
        var mean = values.Average();
        var sigma = values.Count > 1
            ? Math.Sqrt(values.Sum(v => Math.Pow(v - mean, 2)) / (values.Count - 1))
            : 0;

        var points = ordered.Select(p =>
        {
            var z = sigma == 0 ? 0 : ((double)p.Value - mean) / sigma;
            var isAnomaly = Math.Abs(z) >= 3;

            return new AnomalyPoint(
                p.Period.ToString("yyyy-MM-dd"),
                p.Value,
                isAnomaly,
                Math.Round(z, 4),
                isAnomaly ? 0.003 : 1,
                isAnomaly ? $"{Math.Abs(z):F1} standard deviations from the mean." : null);
        }).ToList();

        return new AnomalyReport(
            "Z-score (3σ)",
            metric,
            points,
            points.Count(p => p.IsAnomaly),
            $"Spike detector unavailable — flagged {points.Count(p => p.IsAnomaly)} point(s) beyond three standard deviations.");
    }
}
