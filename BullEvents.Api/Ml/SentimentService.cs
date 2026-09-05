using Microsoft.ML;

namespace BullEvents.Api.Ml;

public record SentimentResult(double Score, string Label, string Engine);

/// <summary>
/// Reads the tone of a call note or visit feedback.
///
/// Trained on the tenant's own notes, labelled by what actually happened next —
/// a note attached to a call that ended in "Interested" is a positive example,
/// one that ended in "NotInterested" is a negative. That makes the model learn
/// this business's vocabulary ("budget stretch", "loan sanctioned") rather than
/// generic English sentiment.
///
/// Until there are enough labelled notes it scores with a domain lexicon, which
/// is transparent and good enough to sort a call list by.
/// </summary>
public class SentimentService(ILogger<SentimentService> logger)
{
    private const int MinimumRows = 40;

    private readonly MLContext _ml = new(seed: 17);
    private readonly Lock _gate = new();

    private PredictionEngine<SentimentRow, SentimentPrediction>? _engine;

    public string Engine { get; private set; } = "Lexicon";
    public DateTime? TrainedAt { get; private set; }
    public int TrainingRows { get; private set; }
    public double? Accuracy { get; private set; }
    public string? Message { get; private set; }

    public ModelHealth Health => new(
        "Call-note sentiment",
        Engine,
        _engine is not null,
        TrainedAt,
        TrainingRows,
        Accuracy,
        "Accuracy",
        Message);

    /* ---------------- lexicon ---------------- */

    private static readonly (string Term, double Weight)[] Lexicon =
    [
        ("very interested", 1.0), ("interested", 0.7), ("keen", 0.6), ("excited", 0.7),
        ("loved", 0.8), ("liked", 0.5), ("positive", 0.5), ("ready to book", 1.0),
        ("booking", 0.8), ("token", 0.7), ("finalise", 0.7), ("finalize", 0.7),
        ("loan sanctioned", 0.8), ("will visit", 0.5), ("scheduled", 0.4),
        ("follow up next week", 0.2), ("shortlisted", 0.6), ("agreed", 0.6),
        ("not interested", -1.0), ("no budget", -0.8), ("budget mismatch", -0.7),
        ("expensive", -0.5), ("too costly", -0.7), ("over budget", -0.7),
        ("dropped", -0.8), ("cancelled", -0.7), ("postponed", -0.4), ("busy", -0.2),
        ("not reachable", -0.4), ("wrong number", -0.6), ("do not call", -1.0),
        ("bought elsewhere", -1.0), ("competitor", -0.5), ("unhappy", -0.7),
        ("delayed possession", -0.5), ("loan rejected", -0.8), ("no response", -0.4),
    ];

    /* ---------------- training ---------------- */

    public bool Train(IReadOnlyList<SentimentRow> rows)
    {
        var labelled = rows
            .Where(r => !string.IsNullOrWhiteSpace(r.Text) && r.Text.Trim().Length >= 8)
            .ToList();

        var positives = labelled.Count(r => r.Positive);
        var negatives = labelled.Count - positives;

        if (labelled.Count < MinimumRows || positives < 8 || negatives < 8)
        {
            using (_gate.EnterScope())
            {
                _engine = null;
                Engine = "Lexicon";
                TrainingRows = labelled.Count;
                Message =
                    $"{labelled.Count} labelled notes ({positives} positive / {negatives} negative). " +
                    $"Needs {MinimumRows} with 8 of each — scoring against the domain lexicon meanwhile.";
            }

            return false;
        }

        try
        {
            var data = _ml.Data.LoadFromEnumerable(labelled);
            var split = _ml.Data.TrainTestSplit(data, testFraction: 0.25, seed: 17);

            var pipeline = _ml.Transforms.Text
                .FeaturizeText("Features", nameof(SentimentRow.Text))
                .Append(_ml.BinaryClassification.Trainers.SdcaLogisticRegression(
                    labelColumnName: nameof(SentimentRow.Positive),
                    featureColumnName: "Features"));

            var model = pipeline.Fit(split.TrainSet);
            var metrics = _ml.BinaryClassification.Evaluate(
                model.Transform(split.TestSet),
                labelColumnName: nameof(SentimentRow.Positive));

            var engine = _ml.Model.CreatePredictionEngine<SentimentRow, SentimentPrediction>(model);

            using (_gate.EnterScope())
            {
                _engine = engine;
                Engine = "MLNet:TextFeaturizer";
                TrainedAt = DateTime.UtcNow;
                TrainingRows = labelled.Count;
                Accuracy = metrics.Accuracy;
                Message =
                    $"Trained on {labelled.Count} notes ({positives} positive / {negatives} negative). " +
                    $"Accuracy {metrics.Accuracy:P1}, AUC {metrics.AreaUnderRocCurve:F3}.";
            }

            logger.LogInformation("Sentiment model: {Message}", Message);
            return true;
        }
        catch (Exception ex)
        {
            using (_gate.EnterScope())
            {
                _engine = null;
                Engine = "Lexicon";
                Message = $"Training failed ({ex.Message}). Using the domain lexicon.";
            }

            logger.LogError(ex, "Sentiment model training failed");
            return false;
        }
    }

    /* ---------------- inference ---------------- */

    public SentimentResult Analyse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new SentimentResult(0, "Neutral", "None");
        }

        PredictionEngine<SentimentRow, SentimentPrediction>? engine;
        using (_gate.EnterScope())
        {
            engine = _engine;
        }

        if (engine is not null)
        {
            try
            {
                SentimentPrediction prediction;
                using (_gate.EnterScope())
                {
                    prediction = engine.Predict(new SentimentRow { Text = text });
                }

                // Probability is 0..1 for "positive"; map onto -1..1 so both
                // engines report on the same scale.
                var score = Math.Round((prediction.Probability * 2) - 1, 3);
                return new SentimentResult(score, Label(score), "MLNet:TextFeaturizer");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Sentiment inference fell back to the lexicon");
            }
        }

        return LexiconScore(text);
    }

    private static SentimentResult LexiconScore(string text)
    {
        var haystack = text.ToLowerInvariant();
        var total = 0.0;
        var hits = 0;

        foreach (var (term, weight) in Lexicon)
        {
            if (!haystack.Contains(term, StringComparison.Ordinal)) continue;
            total += weight;
            hits++;
        }

        if (hits == 0) return new SentimentResult(0, "Neutral", "Lexicon");

        var score = Math.Clamp(Math.Round(total / Math.Sqrt(hits), 3), -1, 1);
        return new SentimentResult(score, Label(score), "Lexicon");
    }

    private static string Label(double score) => score switch
    {
        >= 0.35 => "Positive",
        <= -0.35 => "Negative",
        _ => "Neutral"
    };
}
