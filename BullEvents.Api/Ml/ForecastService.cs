using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;

namespace BullEvents.Api.Ml;

public record ForecastPoint(string Period, decimal Value, decimal? LowerBound, decimal? UpperBound, bool IsForecast);

public record ForecastResult(
    string Engine,
    IReadOnlyList<ForecastPoint> Series,
    decimal NextPeriodValue,
    decimal HorizonTotal,
    double? Confidence,
    string Message
);

/// <summary>
/// Revenue and volume forecasting over a monthly history, using ML.NET's
/// Singular Spectrum Analysis forecaster — the same technique behind Excel's
/// FORECAST.ETS, run locally.
///
/// SSA needs a real seasonal window to say anything useful; with a short
/// history the service falls back to a damped linear trend and says so, rather
/// than dressing up noise as a prediction.
/// </summary>
public class ForecastService(ILogger<ForecastService> logger)
{
    private const int MinimumPointsForSsa = 12;

    private readonly MLContext _ml = new(seed: 11);

    public ForecastResult Forecast(
        IReadOnlyList<(DateTime Period, decimal Value)> history,
        int horizon = 3)
    {
        var ordered = history.OrderBy(p => p.Period).ToList();

        var observed = ordered
            .Select(p => new ForecastPoint(
                p.Period.ToString("yyyy-MM"), decimal.Round(p.Value, 2), null, null, false))
            .ToList();

        if (ordered.Count < 4)
        {
            return new ForecastResult(
                "Insufficient history",
                observed,
                0m,
                0m,
                null,
                $"Only {ordered.Count} month(s) of history — at least 4 are needed before forecasting.");
        }

        return ordered.Count >= MinimumPointsForSsa
            ? Ssa(ordered, observed, horizon)
            : Trend(ordered, observed, horizon);
    }

    /* ---------------- SSA ---------------- */

    private ForecastResult Ssa(
        List<(DateTime Period, decimal Value)> ordered,
        List<ForecastPoint> observed,
        int horizon)
    {
        try
        {
            var rows = ordered
                .Select(p => new TimePoint { Period = p.Period, Value = (float)p.Value })
                .ToList();

            var data = _ml.Data.LoadFromEnumerable(rows);

            // Window has to fit inside the series: SSA requires
            // seriesLength > trainSize > windowSize * 2.
            var windowSize = Math.Max(2, Math.Min(12, ordered.Count / 3));

            var estimator = _ml.Forecasting.ForecastBySsa(
                outputColumnName: nameof(ForecastOutput.Forecast),
                inputColumnName: nameof(TimePoint.Value),
                windowSize: windowSize,
                seriesLength: ordered.Count,
                trainSize: ordered.Count,
                horizon: horizon,
                confidenceLevel: 0.95f,
                confidenceLowerBoundColumn: nameof(ForecastOutput.LowerBound),
                confidenceUpperBoundColumn: nameof(ForecastOutput.UpperBound));

            var transformer = estimator.Fit(data);
            var engine = transformer.CreateTimeSeriesEngine<TimePoint, ForecastOutput>(_ml);
            var output = engine.Predict();

            var lastPeriod = ordered[^1].Period;
            var series = new List<ForecastPoint>(observed);

            for (var i = 0; i < output.Forecast.Length; i++)
            {
                var period = lastPeriod.AddMonths(i + 1);
                series.Add(new ForecastPoint(
                    period.ToString("yyyy-MM"),
                    Round(output.Forecast[i]),
                    Round(output.LowerBound.ElementAtOrDefault(i)),
                    Round(output.UpperBound.ElementAtOrDefault(i)),
                    true));
            }

            var forecasts = series.Where(p => p.IsForecast).ToList();

            return new ForecastResult(
                "MLNet:SSA",
                series,
                forecasts.Count > 0 ? forecasts[0].Value : 0m,
                forecasts.Sum(p => p.Value),
                0.95,
                $"Singular Spectrum Analysis over {ordered.Count} months, window {windowSize}, " +
                $"{horizon}-month horizon at 95% confidence.");
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SSA forecast failed; falling back to trend projection");
            return Trend(ordered, observed, horizon);
        }
    }

    /* ---------------- damped linear trend ---------------- */

    private static ForecastResult Trend(
        List<(DateTime Period, decimal Value)> ordered,
        List<ForecastPoint> observed,
        int horizon)
    {
        // Ordinary least squares on (index, value), then damp the slope so a
        // short, noisy history cannot project a runaway line.
        var n = ordered.Count;
        var meanX = (n - 1) / 2.0;
        var meanY = (double)ordered.Average(p => p.Value);

        var covariance = 0.0;
        var variance = 0.0;

        for (var i = 0; i < n; i++)
        {
            var dx = i - meanX;
            covariance += dx * ((double)ordered[i].Value - meanY);
            variance += dx * dx;
        }

        var slope = variance == 0 ? 0 : covariance / variance;
        var intercept = meanY - (slope * meanX);
        const double damping = 0.75;

        // Residual spread gives an honest band rather than a fabricated one.
        var residuals = ordered
            .Select((p, i) => (double)p.Value - (intercept + (slope * i)))
            .ToList();
        var sigma = residuals.Count > 1
            ? Math.Sqrt(residuals.Sum(r => r * r) / (residuals.Count - 1))
            : 0;

        var lastPeriod = ordered[^1].Period;
        var series = new List<ForecastPoint>(observed);

        for (var i = 1; i <= horizon; i++)
        {
            var projected = intercept + (slope * (n - 1 + (i * damping)));
            projected = Math.Max(0, projected);
            var band = 1.96 * sigma;

            series.Add(new ForecastPoint(
                lastPeriod.AddMonths(i).ToString("yyyy-MM"),
                Round(projected),
                Round(Math.Max(0, projected - band)),
                Round(projected + band),
                true));
        }

        var forecasts = series.Where(p => p.IsForecast).ToList();

        return new ForecastResult(
            "Damped linear trend",
            series,
            forecasts.Count > 0 ? forecasts[0].Value : 0m,
            forecasts.Sum(p => p.Value),
            null,
            $"Only {n} months of history — projecting a damped least-squares trend. " +
            $"SSA takes over at {MinimumPointsForSsa} months.");
    }

    private static decimal Round(double value) =>
        decimal.Round((decimal)(double.IsFinite(value) ? value : 0), 2);

    private static decimal Round(float value) => Round((double)value);
}
