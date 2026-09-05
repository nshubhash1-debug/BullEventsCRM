using BullEvents.Api.Models;
using Microsoft.ML;

namespace BullEvents.Api.Ml;

/// <summary>
/// Predicts whether an open opportunity will close won, from the deal's own
/// shape plus how much work has actually gone into it (calls, visits, quotes).
///
/// Trained locally on the tenant's own closed deals with a boosted decision
/// tree — the same family a Salesforce Einstein score uses, running in-process
/// so nothing about the pipeline leaves the server.
/// </summary>
public class OpportunityWinModel(ILogger<OpportunityWinModel> logger)
{
    private const int MinimumRows = 30;

    private readonly MLContext _ml = new(seed: 7);
    private readonly Lock _gate = new();

    private PredictionEngine<OpportunityFeatureRow, WinPrediction>? _engine;

    public string Engine { get; private set; } = "Heuristic";
    public DateTime? TrainedAt { get; private set; }
    public int TrainingRows { get; private set; }
    public double? Auc { get; private set; }
    public double? Accuracy { get; private set; }
    public double? F1 { get; private set; }
    public string? Message { get; private set; }

    public ModelHealth Health => new(
        "Opportunity win probability",
        Engine,
        _engine is not null,
        TrainedAt,
        TrainingRows,
        Auc,
        "AUC",
        Message);

    /* ------------------------------------------------------------------ *
     * Features
     * ------------------------------------------------------------------ */

    public static OpportunityFeatureRow ToFeatures(
        Opportunity opportunity,
        OpportunityEngagement engagement,
        DateTime now)
    {
        var reference = opportunity.ActualCloseDate ?? now;

        // For a closed deal, Stage is the label — using it would train the model
        // to read its own answer. LastOpenStage is how far the deal had actually
        // travelled, which is the same thing an open deal's Stage tells us.
        var stage = OpportunityStages.IsClosed(opportunity.Stage)
            ? opportunity.LastOpenStage ?? OpportunityStages.Qualification
            : opportunity.Stage;

        return new OpportunityFeatureRow
        {
            Stage = stage,
            Source = opportunity.Source,
            Type = opportunity.Type,
            City = string.IsNullOrWhiteSpace(engagement.City)
                ? "unknown"
                : engagement.City.Trim().ToLowerInvariant(),
            Amount = (float)Math.Log10((double)Math.Max(1m, opportunity.Amount)),
            AgeDays = (float)Math.Max(0, (reference - opportunity.CreatedAt).TotalDays),
            DaysToExpectedClose = (float)(opportunity.ExpectedCloseDate - reference).TotalDays,
            DaysInStage = (float)Math.Max(0, (reference - opportunity.StageEnteredAt).TotalDays),
            SiteVisitCount = engagement.SiteVisits,
            CallCount = engagement.Calls,
            QuotationCount = engagement.Quotations,
            FollowUpCount = engagement.FollowUps,
            HasOwner = opportunity.OwnerId.HasValue ? 1f : 0f,
            Won = opportunity.Stage == OpportunityStages.ClosedWon,
        };
    }

    /* ------------------------------------------------------------------ *
     * Training
     * ------------------------------------------------------------------ */

    public bool Train(IReadOnlyList<OpportunityFeatureRow> rows)
    {
        var labelled = rows.ToList();
        var won = labelled.Count(r => r.Won);
        var lost = labelled.Count - won;

        if (labelled.Count < MinimumRows || won < 5 || lost < 5)
        {
            using (_gate.EnterScope())
            {
                _engine = null;
                Engine = "Heuristic";
                TrainingRows = labelled.Count;
                Message =
                    $"Only {labelled.Count} closed deals ({won} won / {lost} lost). " +
                    $"Needs {MinimumRows} with at least 5 of each — using stage-weighted probability meanwhile.";
            }

            logger.LogInformation("Opportunity win model: {Message}", Message);
            return false;
        }

        try
        {
            var data = _ml.Data.LoadFromEnumerable(labelled);
            var split = _ml.Data.TrainTestSplit(data, testFraction: 0.25, seed: 7);

            var pipeline = _ml.Transforms.Categorical.OneHotEncoding(
                    [
                        new InputOutputColumnPair("StageEnc", nameof(OpportunityFeatureRow.Stage)),
                        new InputOutputColumnPair("SourceEnc", nameof(OpportunityFeatureRow.Source)),
                        new InputOutputColumnPair("TypeEnc", nameof(OpportunityFeatureRow.Type)),
                        new InputOutputColumnPair("CityEnc", nameof(OpportunityFeatureRow.City)),
                    ])
                .Append(_ml.Transforms.Concatenate(
                    "Features",
                    "StageEnc", "SourceEnc", "TypeEnc", "CityEnc",
                    nameof(OpportunityFeatureRow.Amount),
                    nameof(OpportunityFeatureRow.AgeDays),
                    nameof(OpportunityFeatureRow.DaysToExpectedClose),
                    nameof(OpportunityFeatureRow.DaysInStage),
                    nameof(OpportunityFeatureRow.SiteVisitCount),
                    nameof(OpportunityFeatureRow.CallCount),
                    nameof(OpportunityFeatureRow.QuotationCount),
                    nameof(OpportunityFeatureRow.FollowUpCount),
                    nameof(OpportunityFeatureRow.HasOwner)))
                .Append(_ml.Transforms.NormalizeMinMax("Features"))
                .Append(_ml.BinaryClassification.Trainers.FastTree(
                    labelColumnName: nameof(OpportunityFeatureRow.Won),
                    featureColumnName: "Features",
                    numberOfLeaves: 20,
                    numberOfTrees: 100,
                    minimumExampleCountPerLeaf: 5));

            var model = pipeline.Fit(split.TrainSet);
            var metrics = _ml.BinaryClassification.Evaluate(
                model.Transform(split.TestSet),
                labelColumnName: nameof(OpportunityFeatureRow.Won));

            var engine = _ml.Model.CreatePredictionEngine<OpportunityFeatureRow, WinPrediction>(model);

            using (_gate.EnterScope())
            {
                _engine = engine;
                Engine = "MLNet:FastTree";
                TrainedAt = DateTime.UtcNow;
                TrainingRows = labelled.Count;
                Auc = metrics.AreaUnderRocCurve;
                Accuracy = metrics.Accuracy;
                F1 = metrics.F1Score;
                Message =
                    $"Trained on {labelled.Count} closed deals ({won} won / {lost} lost). " +
                    $"AUC {metrics.AreaUnderRocCurve:F3}, accuracy {metrics.Accuracy:P1}, F1 {metrics.F1Score:F2}.";
            }

            logger.LogInformation("Opportunity win model: {Message}", Message);
            return true;
        }
        catch (Exception ex)
        {
            using (_gate.EnterScope())
            {
                _engine = null;
                Engine = "Heuristic";
                Message = $"Training failed ({ex.Message}). Falling back to stage-weighted probability.";
            }

            logger.LogError(ex, "Opportunity win model training failed");
            return false;
        }
    }

    /* ------------------------------------------------------------------ *
     * Inference
     * ------------------------------------------------------------------ */

    public WinInsight Predict(Opportunity opportunity, OpportunityEngagement engagement, DateTime now)
    {
        var features = ToFeatures(opportunity, engagement, now);
        var baseline = Baseline(opportunity, engagement, now);

        if (OpportunityStages.IsClosed(opportunity.Stage))
        {
            var settled = opportunity.Stage == OpportunityStages.ClosedWon ? 100 : 0;
            return new WinInsight(settled, "Closed", "Settled", Drivers(opportunity, engagement, now));
        }

        PredictionEngine<OpportunityFeatureRow, WinPrediction>? engine;
        using (_gate.EnterScope())
        {
            engine = _engine;
        }

        if (engine is null)
        {
            return new WinInsight(baseline, Band(baseline), "Heuristic", Drivers(opportunity, engagement, now));
        }

        try
        {
            WinPrediction prediction;
            using (_gate.EnterScope())
            {
                prediction = engine.Predict(features);
            }

            var score = Math.Clamp((int)Math.Round(prediction.Probability * 100), 1, 99);
            return new WinInsight(score, Band(score), "MLNet:FastTree", Drivers(opportunity, engagement, now));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Win prediction fell back to heuristic for opportunity {Id}", opportunity.Id);
            return new WinInsight(baseline, Band(baseline), "Heuristic", Drivers(opportunity, engagement, now));
        }
    }

    /// <summary>Stage-weighted probability, nudged by engagement and staleness.</summary>
    private static int Baseline(Opportunity opportunity, OpportunityEngagement engagement, DateTime now)
    {
        var score = OpportunityStages.DefaultProbability(opportunity.Stage);

        score += Math.Min(15, engagement.SiteVisits * 8);
        score += Math.Min(8, engagement.Quotations * 4);
        score += Math.Min(6, engagement.Calls);

        var daysInStage = (now - opportunity.StageEnteredAt).TotalDays;
        if (daysInStage > 45) score -= 18;
        else if (daysInStage > 21) score -= 8;

        if (opportunity.ExpectedCloseDate < now) score -= 12;
        if (!opportunity.OwnerId.HasValue) score -= 10;

        return Math.Clamp(score, 1, 99);
    }

    /// <summary>
    /// Human-readable drivers behind the number. The model itself is a tree
    /// ensemble, so rather than pretending to read it back we show the same
    /// signals a manager would check by hand.
    /// </summary>
    private static List<ScoreSignal> Drivers(
        Opportunity opportunity,
        OpportunityEngagement engagement,
        DateTime now)
    {
        var daysInStage = (int)Math.Max(0, (now - opportunity.StageEnteredAt).TotalDays);
        var daysToClose = (int)(opportunity.ExpectedCloseDate - now).TotalDays;

        return
        [
            new ScoreSignal("Stage", $"At {opportunity.Stage} for {daysInStage} day(s)",
                OpportunityStages.DefaultProbability(opportunity.Stage) / 4),
            new ScoreSignal("Site visits", engagement.SiteVisits == 0
                ? "No site visit recorded"
                : $"{engagement.SiteVisits} site visit(s) completed",
                engagement.SiteVisits == 0 ? -12 : Math.Min(15, engagement.SiteVisits * 8)),
            new ScoreSignal("Commercials", engagement.Quotations == 0
                ? "No quotation sent yet"
                : $"{engagement.Quotations} quotation(s) issued",
                engagement.Quotations == 0 ? -8 : Math.Min(8, engagement.Quotations * 4)),
            new ScoreSignal("Contact cadence", $"{engagement.Calls} call(s) logged",
                Math.Min(6, engagement.Calls)),
            new ScoreSignal("Close date", daysToClose < 0
                ? $"Expected close was {-daysToClose} day(s) ago"
                : $"Expected close in {daysToClose} day(s)",
                daysToClose < 0 ? -12 : 4),
            new ScoreSignal("Deal size",
                $"{opportunity.Currency} {opportunity.Amount:N0}",
                opportunity.Amount > 20_000_000m ? -4 : 2),
        ];
    }

    private static string Band(int score) => score switch
    {
        >= 75 => "Strong",
        >= 50 => "Likely",
        >= 25 => "At risk",
        _ => "Long shot"
    };
}

/// <summary>Activity counts rolled up per opportunity, computed once per batch.</summary>
public record OpportunityEngagement(
    int SiteVisits,
    int Calls,
    int Quotations,
    int FollowUps,
    string? City
)
{
    public static readonly OpportunityEngagement Empty = new(0, 0, 0, 0, null);
}

public record WinInsight(
    int Probability,
    string Band,
    string Engine,
    IReadOnlyList<ScoreSignal> Drivers
);
