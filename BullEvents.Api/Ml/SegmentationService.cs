using Microsoft.ML;

namespace BullEvents.Api.Ml;

public record SegmentProfile(
    int ClusterId,
    string Name,
    string Description,
    int Size,
    decimal AverageLifetimeValue,
    double AverageDeals,
    double AverageDaysSinceActivity
);

public record SegmentAssignment(int ContactId, int ClusterId, string Segment, double Distance);

public record SegmentationResult(
    string Engine,
    IReadOnlyList<SegmentProfile> Segments,
    IReadOnlyList<SegmentAssignment> Assignments,
    double? DaviesBouldinIndex,
    string Message
);

/// <summary>
/// Unsupervised customer segmentation over the contact base — KMeans on value,
/// frequency and recency, run in-process.
///
/// Clusters come out numbered, which is useless to a sales manager, so each one
/// is named after the fact from its own centroid: a cluster with high value and
/// recent activity becomes "Key accounts", one with old activity becomes
/// "Dormant", and so on.
/// </summary>
public class SegmentationService(ILogger<SegmentationService> logger)
{
    private const int MinimumRows = 12;

    private readonly MLContext _ml = new(seed: 13);

    public SegmentationResult Segment(
        IReadOnlyList<(int ContactId, ContactFeatureRow Features)> contacts,
        int clusterCount = 4)
    {
        if (contacts.Count < MinimumRows)
        {
            return new SegmentationResult(
                "Rule-based",
                RuleProfiles(contacts),
                RuleAssignments(contacts),
                null,
                $"{contacts.Count} contacts is too few to cluster meaningfully — " +
                $"segmenting by value and recency rules until there are {MinimumRows}.");
        }

        try
        {
            var k = Math.Clamp(clusterCount, 2, Math.Min(6, contacts.Count / 3));
            var rows = contacts.Select(c => c.Features).ToList();
            var data = _ml.Data.LoadFromEnumerable(rows);

            var pipeline = _ml.Transforms
                .Concatenate(
                    "Features",
                    nameof(ContactFeatureRow.LifetimeValue),
                    nameof(ContactFeatureRow.DealCount),
                    nameof(ContactFeatureRow.BudgetMid),
                    nameof(ContactFeatureRow.DaysSinceLastActivity),
                    nameof(ContactFeatureRow.TenureDays),
                    nameof(ContactFeatureRow.EngagementCount))
                .Append(_ml.Transforms.NormalizeMeanVariance("Features"))
                .Append(_ml.Clustering.Trainers.KMeans("Features", numberOfClusters: k));

            var model = pipeline.Fit(data);
            var metrics = _ml.Clustering.Evaluate(model.Transform(data), featureColumnName: "Features");

            var engine = _ml.Model.CreatePredictionEngine<ContactFeatureRow, ClusterPrediction>(model);

            var assignments = new List<(int ContactId, ContactFeatureRow Features, int Cluster, double Distance)>();

            foreach (var (contactId, features) in contacts)
            {
                var prediction = engine.Predict(features);
                var cluster = (int)prediction.ClusterId;
                var distance = prediction.Distances.Length >= cluster && cluster > 0
                    ? prediction.Distances[cluster - 1]
                    : 0;

                assignments.Add((contactId, features, cluster, distance));
            }

            var profiles = NameClusters(assignments);
            var nameByCluster = profiles.ToDictionary(p => p.ClusterId, p => p.Name);

            return new SegmentationResult(
                "MLNet:KMeans",
                profiles,
                assignments
                    .Select(a => new SegmentAssignment(
                        a.ContactId,
                        a.Cluster,
                        nameByCluster.GetValueOrDefault(a.Cluster, $"Segment {a.Cluster}"),
                        Math.Round(a.Distance, 4)))
                    .ToList(),
                metrics.DaviesBouldinIndex,
                $"KMeans over {contacts.Count} contacts into {k} clusters. " +
                $"Davies-Bouldin {metrics.DaviesBouldinIndex:F3} (lower is tighter).");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Contact segmentation failed");
            return new SegmentationResult(
                "Rule-based",
                RuleProfiles(contacts),
                RuleAssignments(contacts),
                null,
                $"Clustering failed ({ex.Message}). Segmenting by value and recency rules instead.");
        }
    }

    /* ------------------------------------------------------------------ *
     * Naming
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Turns anonymous clusters into names a manager can act on, by ranking
    /// each cluster's centroid against the others on value and recency.
    /// </summary>
    private static List<SegmentProfile> NameClusters(
        List<(int ContactId, ContactFeatureRow Features, int Cluster, double Distance)> assignments)
    {
        var groups = assignments
            .GroupBy(a => a.Cluster)
            .Select(g => new
            {
                ClusterId = g.Key,
                Size = g.Count(),
                Value = g.Average(a => a.Features.LifetimeValue),
                Deals = g.Average(a => a.Features.DealCount),
                Recency = g.Average(a => a.Features.DaysSinceLastActivity),
                Engagement = g.Average(a => a.Features.EngagementCount),
            })
            .ToList();

        var medianValue = Median(groups.Select(g => (double)g.Value));
        var medianRecency = Median(groups.Select(g => (double)g.Recency));

        return groups
            .Select(g =>
            {
                var highValue = g.Value >= medianValue;
                var recent = g.Recency <= medianRecency;

                var (name, description) = (highValue, recent) switch
                {
                    (true, true) => ("Key accounts",
                        "High lifetime value and recently active — protect and upsell."),
                    (true, false) => ("At-risk value",
                        "Valuable but has gone quiet. Worth a senior touch before they lapse."),
                    (false, true) => ("Emerging",
                        "Engaged but yet to spend. The pipeline's growth pool."),
                    _ => ("Dormant",
                        "Low value and no recent activity — nurture campaigns rather than direct sales time."),
                };

                return new SegmentProfile(
                    g.ClusterId,
                    name,
                    description,
                    g.Size,
                    decimal.Round((decimal)g.Value, 0),
                    Math.Round(g.Deals, 2),
                    Math.Round(g.Recency, 1));
            })
            .OrderByDescending(p => p.AverageLifetimeValue)
            .ToList();
    }

    /* ------------------------------------------------------------------ *
     * Rule fallback
     * ------------------------------------------------------------------ */

    private static string RuleName(ContactFeatureRow features) => features switch
    {
        { DealCount: >= 1, DaysSinceLastActivity: <= 60 } => "Key accounts",
        { DealCount: >= 1 } => "At-risk value",
        { EngagementCount: >= 3, DaysSinceLastActivity: <= 45 } => "Emerging",
        _ => "Dormant",
    };

    private static List<SegmentAssignment> RuleAssignments(
        IReadOnlyList<(int ContactId, ContactFeatureRow Features)> contacts) =>
        contacts
            .Select(c => new SegmentAssignment(c.ContactId, 0, RuleName(c.Features), 0))
            .ToList();

    private static List<SegmentProfile> RuleProfiles(
        IReadOnlyList<(int ContactId, ContactFeatureRow Features)> contacts) =>
        contacts
            .GroupBy(c => RuleName(c.Features))
            .Select((g, index) => new SegmentProfile(
                index,
                g.Key,
                "Assigned by value and recency rules.",
                g.Count(),
                decimal.Round((decimal)g.Average(c => c.Features.LifetimeValue), 0),
                Math.Round(g.Average(c => (double)c.Features.DealCount), 2),
                Math.Round(g.Average(c => (double)c.Features.DaysSinceLastActivity), 1)))
            .OrderByDescending(p => p.AverageLifetimeValue)
            .ToList();

    private static double Median(IEnumerable<double> values)
    {
        var sorted = values.OrderBy(v => v).ToList();
        if (sorted.Count == 0) return 0;
        var mid = sorted.Count / 2;
        return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) / 2;
    }
}
