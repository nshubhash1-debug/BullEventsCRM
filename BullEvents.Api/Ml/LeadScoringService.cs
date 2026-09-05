using BullEvents.Api.Models;
using Microsoft.ML;

namespace BullEvents.Api.Ml;

/// <summary>
/// Local lead-conversion scoring. Everything runs in-process through ML.NET —
/// no data leaves the server and no external inference service is involved.
///
/// The model needs labelled history (leads that reached Booked, or were Lost) to
/// be worth anything. Until enough of that exists the service scores with a
/// transparent weighted heuristic instead, and reports which engine produced a
/// score so the UI can be honest about it.
/// </summary>
public class LeadScoringService
{
    private const int MinimumTrainingRows = 40;

    private readonly MLContext _ml = new(seed: 1);
    private readonly ILogger<LeadScoringService> _logger;
    private readonly object _gate = new();

    private PredictionEngine<LeadFeatureRow, LeadScorePrediction>? _engine;

    public string EngineName { get; private set; } = "Heuristic";
    public DateTime? TrainedAt { get; private set; }
    public int TrainingRows { get; private set; }
    public double? Accuracy { get; private set; }
    public double? AreaUnderRocCurve { get; private set; }
    public string? LastTrainingMessage { get; private set; }

    public LeadScoringService(ILogger<LeadScoringService> logger)
    {
        _logger = logger;
    }

    public ModelHealth Health => new(
        "Lead conversion score",
        EngineName,
        _engine is not null,
        TrainedAt,
        TrainingRows,
        AreaUnderRocCurve,
        "AUC",
        LastTrainingMessage);

    /* ------------------------------------------------------------------ *
     * Feature extraction
     * ------------------------------------------------------------------ */

    public static LeadFeatureRow ToFeatures(Lead lead, DateTime now)
    {
        var activities = lead.Activities ?? new List<LeadActivity>();
        var activityCount = activities.Count;

        var lastTouch = activities.Count > 0
            ? activities.Max(a => a.CreatedAt)
            : lead.CreatedAt;

        var daysOpen = Math.Max(0, (now - lead.CreatedAt).TotalDays);
        var weeksOpen = Math.Max(1.0, daysOpen / 7.0);

        return new LeadFeatureRow
        {
            Source = lead.Source ?? LeadSources.Other,
            Priority = lead.Priority ?? LeadPriorities.Medium,
            City = string.IsNullOrWhiteSpace(lead.City) ? "unknown" : lead.City.Trim().ToLowerInvariant(),
            HasEmail = string.IsNullOrWhiteSpace(lead.Email) ? 0f : 1f,
            HasPhone = string.IsNullOrWhiteSpace(lead.Phone) ? 0f : 1f,
            HasCompany = string.IsNullOrWhiteSpace(lead.CompanyName) ? 0f : 1f,
            DaysToEvent = lead.EventDate is DateTime when
                ? (float)Math.Max(0, (when - now).TotalDays)
                : 400f,
            HasBudget = lead.BudgetMin is not null || lead.BudgetMax is not null ? 1f : 0f,
            GuestCount = lead.GuestCount ?? 0,
            DateFlexible = lead.IsDateFlexible ? 1f : 0f,
            QuestionnaireDone = lead.QuestionnaireStatus == QuestionnaireStatuses.Completed ? 1f : 0f,
            ActivityCount = activityCount,
            DaysOpen = (float)daysOpen,
            DaysSinceLastTouch = (float)Math.Max(0, (now - lastTouch).TotalDays),
            TouchesPerWeek = (float)(activityCount / weeksOpen),
            Converted = lead.Stage == LeadStages.Booked
        };
    }

    /* ------------------------------------------------------------------ *
     * Training
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Trains on closed leads only — Booked counts as a positive outcome, Lost
    /// and Closed as negatives. Open leads have no known outcome yet, so
    /// including them would teach the model that "still in progress" means
    /// "will not convert".
    /// </summary>
    public bool Train(IReadOnlyList<Lead> leads)
    {
        var now = DateTime.UtcNow;

        var labelled = leads
            .Where(l => l.Stage is LeadStages.Booked or LeadStages.Lost)
            .Select(l => ToFeatures(l, now))
            .ToList();

        var positives = labelled.Count(r => r.Converted);
        var negatives = labelled.Count - positives;

        if (labelled.Count < MinimumTrainingRows || positives < 5 || negatives < 5)
        {
            lock (_gate)
            {
                _engine = null;
                EngineName = "Heuristic";
                TrainingRows = labelled.Count;
                LastTrainingMessage =
                    $"Not enough closed leads to train ({labelled.Count} rows, {positives} won / {negatives} lost). " +
                    $"Need at least {MinimumTrainingRows} rows with 5 of each outcome. Scoring with the weighted heuristic.";
            }

            _logger.LogInformation("Lead scoring: {Message}", LastTrainingMessage);
            return false;
        }

        try
        {
            var data = _ml.Data.LoadFromEnumerable(labelled);
            var split = _ml.Data.TrainTestSplit(data, testFraction: 0.25, seed: 1);

            var pipeline = _ml.Transforms.Categorical.OneHotEncoding(
                    [
                        new InputOutputColumnPair("SourceEnc", nameof(LeadFeatureRow.Source)),
                        new InputOutputColumnPair("PriorityEnc", nameof(LeadFeatureRow.Priority)),
                        new InputOutputColumnPair("CityEnc", nameof(LeadFeatureRow.City))
                    ])
                .Append(_ml.Transforms.Concatenate(
                    "Features",
                    "SourceEnc",
                    "PriorityEnc",
                    "CityEnc",
                    nameof(LeadFeatureRow.HasEmail),
                    nameof(LeadFeatureRow.HasPhone),
                    nameof(LeadFeatureRow.HasCompany),
                    nameof(LeadFeatureRow.DaysToEvent),
                    nameof(LeadFeatureRow.HasBudget),
                    nameof(LeadFeatureRow.GuestCount),
                    nameof(LeadFeatureRow.DateFlexible),
                    nameof(LeadFeatureRow.QuestionnaireDone),
                    nameof(LeadFeatureRow.ActivityCount),
                    nameof(LeadFeatureRow.DaysOpen),
                    nameof(LeadFeatureRow.DaysSinceLastTouch),
                    nameof(LeadFeatureRow.TouchesPerWeek)))
                .Append(_ml.Transforms.NormalizeMinMax("Features"))
                // FastTree captures the interactions that matter here — a stale
                // lead from a strong source behaves differently from a stale one
                // from a weak source, which a linear model cannot express.
                .Append(_ml.BinaryClassification.Trainers.FastTree(
                    labelColumnName: nameof(LeadFeatureRow.Converted),
                    featureColumnName: "Features",
                    numberOfLeaves: 20,
                    numberOfTrees: 100,
                    minimumExampleCountPerLeaf: 5));

            var model = pipeline.Fit(split.TrainSet);

            var metrics = _ml.BinaryClassification.Evaluate(
                model.Transform(split.TestSet),
                labelColumnName: nameof(LeadFeatureRow.Converted));

            var engine = _ml.Model.CreatePredictionEngine<LeadFeatureRow, LeadScorePrediction>(model);

            lock (_gate)
            {
                _engine = engine;
                EngineName = "MLNet:FastTree";
                TrainedAt = DateTime.UtcNow;
                TrainingRows = labelled.Count;
                Accuracy = metrics.Accuracy;
                AreaUnderRocCurve = metrics.AreaUnderRocCurve;
                LastTrainingMessage =
                    $"Trained on {labelled.Count} closed leads ({positives} won / {negatives} lost). " +
                    $"Accuracy {metrics.Accuracy:P1}, AUC {metrics.AreaUnderRocCurve:F3}.";
            }

            _logger.LogInformation("Lead scoring: {Message}", LastTrainingMessage);
            return true;
        }
        catch (Exception ex)
        {
            lock (_gate)
            {
                _engine = null;
                EngineName = "Heuristic";
                LastTrainingMessage = $"Training failed ({ex.Message}). Falling back to the weighted heuristic.";
            }

            _logger.LogError(ex, "Lead scoring model training failed");
            return false;
        }
    }

    /* ------------------------------------------------------------------ *
     * Scoring
     * ------------------------------------------------------------------ */

    public LeadScoreResult Score(Lead lead, DateTime now)
    {
        var features = ToFeatures(lead, now);
        var signals = BuildSignals(lead, features);
        var heuristic = Clamp(50 + signals.Sum(s => s.Points));

        PredictionEngine<LeadFeatureRow, LeadScorePrediction>? engine;
        lock (_gate)
        {
            engine = _engine;
        }

        if (engine is null)
        {
            return new LeadScoreResult(heuristic, Band(heuristic), "Heuristic", signals);
        }

        try
        {
            // PredictionEngine is not thread-safe, so calls are serialised.
            LeadScorePrediction prediction;
            lock (_gate)
            {
                prediction = engine.Predict(features);
            }

            var score = Clamp((int)Math.Round(prediction.Probability * 100));
            return new LeadScoreResult(score, Band(score), "MLNet", signals);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falling back to heuristic score for lead {LeadId}", lead.Id);
            return new LeadScoreResult(heuristic, Band(heuristic), "Heuristic", signals);
        }
    }

    /// <summary>
    /// Explainable signal breakdown. These are the weighted heuristic's own
    /// contributions — shown alongside the score so a rep can see why a lead
    /// ranks where it does, whichever engine produced the number.
    /// </summary>
    private static List<ScoreSignal> BuildSignals(Lead lead, LeadFeatureRow features)
    {
        var signals = new List<ScoreSignal>();

        var priorityPoints = lead.Priority switch
        {
            LeadPriorities.Hot => 22,
            LeadPriorities.High => 14,
            LeadPriorities.Medium => 4,
            _ => -4
        };
        signals.Add(new ScoreSignal("Priority", $"{lead.Priority} priority", priorityPoints));

        var sourcePoints = lead.Source switch
        {
            LeadSources.Referral => 14,
            LeadSources.VendorPartner => 11,
            LeadSources.WalkIn => 10,
            LeadSources.Website => 7,
            LeadSources.EventPortal => 5,
            LeadSources.SocialAds => 1,
            _ => 0
        };
        signals.Add(new ScoreSignal("Source", $"Came in via {lead.Source}", sourcePoints));

        var stagePoints = lead.Stage switch
        {
            LeadStages.Booked => 25,
            LeadStages.ContractSent => 22,
            LeadStages.ProposalSent or LeadStages.Negotiation => 18,
            LeadStages.SiteVisit => 14,
            LeadStages.Qualified => 9,
            LeadStages.Contacted => 5,
            LeadStages.Nurture => -8,
            LeadStages.Lost => -35,
            _ => 0
        };
        signals.Add(new ScoreSignal("Pipeline stage", $"Currently at {lead.Stage}", stagePoints));

        var engagement = Math.Min(12, (int)Math.Round(features.ActivityCount * 2.5));
        signals.Add(new ScoreSignal(
            "Engagement",
            features.ActivityCount == 0
                ? "No activity logged yet"
                : $"{(int)features.ActivityCount} activities logged",
            features.ActivityCount == 0 ? -6 : engagement));

        var staleness = features.DaysSinceLastTouch switch
        {
            <= 3 => 8,
            <= 7 => 3,
            <= 14 => -3,
            <= 30 => -9,
            _ => -15
        };
        signals.Add(new ScoreSignal(
            "Recency",
            $"Last touched {(int)features.DaysSinceLastTouch} day(s) ago",
            staleness));

        var completeness =
            (features.HasEmail > 0 ? 4 : -5) +
            (features.HasPhone > 0 ? 4 : -8) +
            (features.HasCompany > 0 ? 3 : 0);
        signals.Add(new ScoreSignal(
            "Data completeness",
            features is { HasEmail: > 0, HasPhone: > 0 }
                ? "Phone and email on file"
                : "Missing contact details",
            completeness));

        var ownerPoints = lead.OwnerId.HasValue ? 3 : -10;
        signals.Add(new ScoreSignal(
            "Ownership",
            lead.OwnerId.HasValue ? "Assigned to an owner" : "Unassigned",
            ownerPoints));

        var daysToEvent = features.DaysToEvent;
        var datePoints = daysToEvent switch
        {
            < 21 => 12,
            < 90 => 8,
            < 180 => 4,
            > 400 => -6,
            _ => 0
        };
        signals.Add(new ScoreSignal(
            "Event date",
            lead.EventDate is null ? "Date not fixed" : $"Event in {(int)daysToEvent} days",
            datePoints));

        if (features.HasBudget > 0)
            signals.Add(new ScoreSignal("Budget", "Budget range on file", 6));
        else
            signals.Add(new ScoreSignal("Budget", "No budget yet", -4));

        if (features.GuestCount >= 50)
            signals.Add(new ScoreSignal("Guests", $"{(int)features.GuestCount} expected guests", 4));

        if (features.DateFlexible > 0)
            signals.Add(new ScoreSignal("Flexibility", "Date can move", 5));

        if (features.QuestionnaireDone > 0)
            signals.Add(new ScoreSignal("Questionnaire", "Qualification form completed", 8));

        return signals;
    }

    private static int Clamp(int value) => Math.Clamp(value, 1, 99);

    private static string Band(int score) => score switch
    {
        >= 75 => "Hot",
        >= 50 => "Warm",
        >= 25 => "Cool",
        _ => "Cold"
    };
}
