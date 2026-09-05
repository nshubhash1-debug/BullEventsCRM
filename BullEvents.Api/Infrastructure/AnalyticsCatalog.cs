using System.Globalization;
using BullEvents.Api.Controllers;
using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Infrastructure;

/* ------------------------------------------------------------------ *
 * Dataset definition
 * ------------------------------------------------------------------ */

/// <summary>A numeric the widget editor offers, plus how the client formats it.</summary>
public record MeasureSpec(string Id, string Label, string Format = "number");

/// <summary>
/// One number out of a dataset: the shape a goal's progress is measured with.
///
/// Deliberately narrower than <see cref="AggregateRequest"/> — no grouping, no
/// sort, no limit — because a target is a scalar and everything a grouped query
/// carries would be discarded on the way out.
/// </summary>
public class ScalarQuery
{
    public string? Measure { get; set; }
    public string Aggregation { get; set; } = "count";
    public FilterNode? Filter { get; set; }
    public string? DateField { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }

    /// <summary>Narrows to one person's records, using whichever field the dataset owns them by.</summary>
    public int? OwnerId { get; set; }
    public int? BranchId { get; set; }
}

/// <summary>
/// One queryable object as the dashboard sees it.
///
/// Non-generic on purpose: the registry holds datasets over eight different
/// entity types, and the controller should not have to know which. The generic
/// subclass below carries the entity, its field map and its source query.
/// </summary>
public abstract class AnalyticsDataset
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required string Description { get; init; }

    /// <summary>Plural noun for a record count, e.g. "leads".</summary>
    public required string RecordLabel { get; init; }

    /// <summary>Field ids offered as group-by dimensions, in menu order.</summary>
    public required IReadOnlyList<string> Dimensions { get; init; }

    public required IReadOnlyList<MeasureSpec> Measures { get; init; }
    public required string DefaultDateField { get; init; }
    public required string DefaultDimension { get; init; }

    /// <summary>
    /// Canonical order for dimensions that have one — pipeline stages run
    /// Qualification → Closed, not biggest-bar-first. Sorting a funnel by value
    /// puts "Closed Won" above "Proposal" and makes the drop-off column between
    /// them meaningless, so the domain order has to come from the domain.
    /// </summary>
    public IReadOnlyDictionary<string, string[]> Sequences { get; init; } =
        new Dictionary<string, string[]>();

    /// <summary>
    /// The id field a record is "owned" by. Not uniformly `ownerId` — a call
    /// belongs to its agent and a site visit to its host — so scoping a goal to
    /// a person has to ask the dataset which column that is.
    /// </summary>
    public string OwnerField { get; init; } = "ownerId";

    public string BranchField { get; init; } = "branchId";

    public abstract DatasetDto Describe();

    public abstract Task<AggregateResponse> RunAsync(
        AppDbContext db, AggregateRequest request, CancellationToken cancellationToken);

    /// <summary>Distinct values for one field — fills the filter builder's value dropdown.</summary>
    public abstract Task<List<FacetBucket>> ValuesAsync(
        AppDbContext db, string field, CancellationToken cancellationToken);

    /// <summary>One number — what a goal's progress is measured with.</summary>
    public abstract Task<decimal> ScalarAsync(
        AppDbContext db, ScalarQuery query, CancellationToken cancellationToken);
}

/// <summary>The entity-typed half of a dataset.</summary>
public sealed class AnalyticsDataset<T> : AnalyticsDataset where T : class
{
    public required Func<AppDbContext, IQueryable<T>> Source { get; init; }
    public required FieldMap<T> Map { get; init; }

    /* ---------------- metadata ---------------- */

    public override DatasetDto Describe()
    {
        var dimensions = Dimensions
            .Select(id => Map.Find(id))
            .Where(f => f is not null)
            .Select(f => new DimensionDto(
                f!.Id,
                f.Label,
                f.Kind == FieldKind.Date ? "date" : "category",
                null,
                Sequences.ContainsKey(f.Id)))
            .ToList();

        var measures = new List<MeasureDto>
        {
            // The implicit measure every dataset has: how many records are in
            // the group. Listed first because it is what most widgets want.
            new("*", $"Number of {RecordLabel}", "number", ["count"]),
        };

        measures.AddRange(Measures
            .Where(spec => Map.Find(spec.Id) is not null)
            .Select(spec => new MeasureDto(
                spec.Id,
                spec.Label,
                spec.Format,
                Map.Find(spec.Id)!.Kind == FieldKind.Boolean
                    ? ["sum", "avg"]
                    : ["sum", "avg", "max", "min", "countDistinct"])));

        var dateFields = Map.All
            .Where(f => f.Kind == FieldKind.Date)
            .Select(f => new DimensionDto(f.Id, f.Label, "date", null))
            .ToList();

        var filterFields = Map.All
            .Select(f => new FilterFieldDto(
                f.Id,
                f.Label,
                f.Kind switch
                {
                    FieldKind.Number => "number",
                    FieldKind.Date => "date",
                    FieldKind.Boolean => "boolean",
                    FieldKind.Select => "select",
                    _ => "text",
                },
                null,
                null))
            .ToList();

        return new DatasetDto(
            Id, Label, Description, RecordLabel,
            dimensions, measures, dateFields,
            DefaultDateField, DefaultDimension, filterFields);
    }

    /* ---------------- values ---------------- */

    public override async Task<List<FacetBucket>> ValuesAsync(
        AppDbContext db, string field, CancellationToken cancellationToken)
    {
        if (Map.Find(field) is null)
        {
            throw ApiException.BadRequest($"'{field}' is not a field on {Label}.");
        }

        return await QueryEngine
            .Facet(Source(db), Map, field)
            .OrderByDescending(bucket => bucket.Count)
            .Take(200)
            .ToListAsync(cancellationToken);
    }

    /* ---------------- scalar ---------------- */

    public override async Task<decimal> ScalarAsync(
        AppDbContext db, ScalarQuery query, CancellationToken cancellationToken)
    {
        var filtered = QueryEngine.ApplyFilter(Source(db), query.Filter, Map);
        filtered = QueryEngine.ApplyFilter(filtered, WindowFilter(query), Map);
        filtered = QueryEngine.ApplyFilter(filtered, ScopeFilter(query), Map);

        var function = ParseFunction(query.Aggregation);
        var measure = query.Measure;

        if (function == AggFn.Count || string.IsNullOrWhiteSpace(measure) || measure == "*")
        {
            return await filtered.CountAsync(cancellationToken);
        }

        var values = AnalyticsEngine.ProjectMeasure(filtered, Map, measure);

        return function switch
        {
            AggFn.Sum => await values.SumAsync(cancellationToken) ?? 0m,
            AggFn.Avg => await values.AnyAsync(cancellationToken)
                ? await values.AverageAsync(cancellationToken) ?? 0m
                : 0m,
            AggFn.Min => await values.MinAsync(cancellationToken) ?? 0m,
            AggFn.Max => await values.MaxAsync(cancellationToken) ?? 0m,
            AggFn.CountDistinct => await values.Distinct().CountAsync(cancellationToken),
            _ => await filtered.CountAsync(cancellationToken),
        };
    }

    /// <summary>The goal's window, as a filter node on its chosen date field.</summary>
    private FilterNode? WindowFilter(ScalarQuery query)
    {
        if (query.From is null && query.To is null) return null;

        var dateField = string.IsNullOrWhiteSpace(query.DateField)
            ? DefaultDateField
            : query.DateField!;

        if (Map.Find(dateField) is not { Kind: FieldKind.Date }) return null;

        var from = query.From ?? new DateTime(2000, 1, 1);
        var to = query.To ?? DateTime.UtcNow.Date.AddYears(1);

        return new FilterNode
        {
            Field = dateField,
            Operator = "between",
            Value = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Value2 = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    /// <summary>Narrows to one owner or branch, skipping silently if the dataset has no such column.</summary>
    private FilterNode? ScopeFilter(ScalarQuery query)
    {
        var clauses = new List<FilterNode>();

        if (query.OwnerId is int owner && Map.Find(OwnerField) is not null)
        {
            clauses.Add(new FilterNode
            {
                Field = OwnerField,
                Operator = "equals",
                Value = owner.ToString(CultureInfo.InvariantCulture),
            });
        }

        if (query.BranchId is int branch && Map.Find(BranchField) is not null)
        {
            clauses.Add(new FilterNode
            {
                Field = BranchField,
                Operator = "equals",
                Value = branch.ToString(CultureInfo.InvariantCulture),
            });
        }

        if (clauses.Count == 0) return null;
        return new FilterNode { Conjunction = "and", Children = clauses };
    }

    /* ---------------- aggregate ---------------- */

    public override async Task<AggregateResponse> RunAsync(
        AppDbContext db, AggregateRequest request, CancellationToken cancellationToken)
    {
        var dimension = string.IsNullOrWhiteSpace(request.Dimension)
            ? DefaultDimension
            : request.Dimension;

        var field = Map.Find(dimension)
            ?? throw ApiException.BadRequest($"'{dimension}' is not a field on {Label}.");

        var isDate = field.Kind == FieldKind.Date;
        var bucket = isDate ? ParseBucket(request.Bucket) : DateBucket.None;

        var breakdownBucket = DateBucket.None;
        if (!string.IsNullOrWhiteSpace(request.Breakdown)
            && Map.Find(request.Breakdown!) is { Kind: FieldKind.Date })
        {
            breakdownBucket = ParseBucket(request.BreakdownBucket);
        }

        var function = ParseFunction(request.Aggregation);
        var measureId = NormaliseMeasure(request.Measure, ref function);

        /* --- filter --- */

        var filtered = QueryEngine.ApplySearch(Source(db), request.Search, Map);
        filtered = QueryEngine.ApplyFilter(filtered, request.Filter, Map);
        filtered = QueryEngine.ApplyFilter(filtered, PeriodFilter(request), Map);

        var matched = await filtered.CountAsync(cancellationToken);

        /* --- project once, aggregate twice --- */

        var projected = AnalyticsEngine.Project(
            filtered, Map, dimension, bucket, request.Breakdown, breakdownBucket, measureId);

        // The overall total is computed over the same projection rather than by
        // summing the groups: an average of group averages is not the average.
        var total = function switch
        {
            AggFn.Count => matched,
            AggFn.Sum => await projected.SumAsync(row => row.Measure, cancellationToken) ?? 0m,
            AggFn.Avg => matched == 0
                ? 0m
                : await projected.AverageAsync(row => row.Measure, cancellationToken) ?? 0m,
            AggFn.Min => await projected.MinAsync(row => row.Measure, cancellationToken) ?? 0m,
            AggFn.Max => await projected.MaxAsync(row => row.Measure, cancellationToken) ?? 0m,
            _ => await projected.Select(row => row.Measure).Distinct().CountAsync(cancellationToken),
        };

        var buckets = await AnalyticsEngine
            .Aggregate(projected, function)
            .ToListAsync(cancellationToken);

        /* --- shape --- */

        var measureLabel = measureId is null
            ? $"Number of {RecordLabel}"
            : Measures.FirstOrDefault(m => m.Id == measureId)?.Label
              ?? Map.Find(measureId)?.Label
              ?? measureId;

        var format = measureId is null
            ? "number"
            : Measures.FirstOrDefault(m => m.Id == measureId)?.Format ?? "number";

        var (rows, series, truncated) = Pivot(
            buckets,
            request,
            isDate,
            bucket,
            hasBreakdown: !string.IsNullOrWhiteSpace(request.Breakdown),
            breakdownIsDate: breakdownBucket != DateBucket.None,
            breakdownBucket,
            seriesFallback: measureLabel,
            sequence: Sequences.GetValueOrDefault(dimension));

        return new AggregateResponse(
            Id,
            Label,
            dimension,
            field.Label,
            isDate ? "date" : "category",
            isDate ? bucket.ToString().ToLowerInvariant() : null,
            request.Breakdown,
            measureId ?? "*",
            measureLabel,
            format,
            function.ToString().ToLowerInvariant(),
            series,
            rows,
            total,
            matched,
            truncated);
    }

    /* ---------------- shaping ---------------- */

    /// <summary>
    /// Turns the flat (dimension, series, value) buckets into one row per
    /// dimension with a value per series — the shape every chart library wants,
    /// computed here so each widget renderer does not repeat it.
    /// </summary>
    private static (List<AggregatePointDto> Rows, List<string> Series, int Truncated) Pivot(
        List<AggregateBucket> buckets,
        AggregateRequest request,
        bool isDate,
        DateBucket bucket,
        bool hasBreakdown,
        bool breakdownIsDate,
        DateBucket breakdownBucket,
        string seriesFallback,
        string[]? sequence)
    {
        // Series names are formatted once, here, because they are also the keys
        // of each row's value dictionary — the legend and the rows have to agree
        // on the string or the chart looks up a series that is not there.
        string SeriesName(AggregateBucket b) => string.IsNullOrEmpty(b.Series)
            ? seriesFallback
            : FormatKey(b.Series, breakdownIsDate, breakdownBucket);

        // Series ordered by overall contribution, so the biggest slice is
        // coloured first and stays put as the dimension is refiltered.
        var series = buckets
            .GroupBy(SeriesName)
            .Select(g => (Name: g.Key, Weight: g.Sum(b => b.Value)))
            .OrderByDescending(x => x.Weight)
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Name)
            .ToList();

        if (series.Count == 0) series.Add(seriesFallback);

        var grouped = buckets
            .GroupBy(b => b.Dimension)
            .Select(g => new AggregatePointDto(
                g.Key,
                FormatKey(g.Key, isDate, bucket),
                g.ToDictionary(SeriesName, b => Round(b.Value)),
                Round(g.Sum(b => b.Value)),
                g.Sum(b => b.Count)))
            .ToList();

        // Dates read chronologically, fields with a domain order read in it, and
        // everything else defaults to biggest-first.
        var sort = request.Sort?.Trim();
        if (string.IsNullOrEmpty(sort) || sort == "auto")
        {
            sort = isDate ? "keyAsc" : sequence is not null ? "sequence" : "valueDesc";
        }

        grouped = sort switch
        {
            "keyAsc" => grouped.OrderBy(r => r.Key, StringComparer.Ordinal).ToList(),
            "keyDesc" => grouped.OrderByDescending(r => r.Key, StringComparer.Ordinal).ToList(),
            "valueAsc" => grouped.OrderBy(r => r.Total).ToList(),
            "sequence" when sequence is not null => grouped
                // Anything outside the declared vocabulary sorts after it rather
                // than silently landing at position zero.
                .OrderBy(r => IndexIn(sequence, r.Key))
                .ThenByDescending(r => r.Total)
                .ToList(),
            _ => grouped.OrderByDescending(r => r.Total).ToList(),
        };

        var truncated = 0;

        if (request.Limit is int limit && limit > 0 && grouped.Count > limit)
        {
            var kept = grouped.Take(limit).ToList();
            var rest = grouped.Skip(limit).ToList();

            // Only groups that were actually dropped count as truncated. Rolling
            // them into "Other" keeps them on the chart, and reporting them as
            // hidden as well would have the widget claim it is missing rows it
            // is in fact showing.
            truncated = request.GroupOther ? 0 : rest.Count;

            if (request.GroupOther && rest.Count > 0)
            {
                var merged = new Dictionary<string, decimal>();
                foreach (var value in rest.SelectMany(r => r.Values))
                {
                    merged[value.Key] = merged.GetValueOrDefault(value.Key) + value.Value;
                }

                kept.Add(new AggregatePointDto(
                    "__other__",
                    $"Other ({rest.Count})",
                    merged.ToDictionary(pair => pair.Key, pair => Round(pair.Value)),
                    Round(rest.Sum(r => r.Total)),
                    rest.Sum(r => r.Records)));
            }

            grouped = kept;
        }

        // Only a breakdown produces meaningful multi-series; without one there
        // is a single series and the legend would just repeat the title.
        if (!hasBreakdown) series = [seriesFallback];

        return (grouped, series, truncated);
    }

    /// <summary>
    /// SQL sums a count into a `decimal` with the column's full scale, which
    /// serialises as `257.00000000000000000000000000`. Nothing downstream wants
    /// that precision and it more than doubles the payload, so values are cut to
    /// four places — past what any currency or rate on this data needs.
    /// </summary>
    private static decimal Round(decimal value) =>
        decimal.Round(value, 4, MidpointRounding.AwayFromZero);

    /// <summary>Position in the canonical order, or past the end when unknown.</summary>
    private static int IndexIn(string[] sequence, string key)
    {
        var index = Array.IndexOf(sequence, key);
        return index < 0 ? sequence.Length : index;
    }

    /// <summary>
    /// Renders a group key for display: date keys become readable periods, and
    /// the PascalCase codes stored for enum-ish columns get spaced out the same
    /// way the filter dropdowns space them.
    /// </summary>
    private static string FormatKey(string key, bool isDate, DateBucket bucket)
    {
        if (key == AnalyticsEngine.Blank) return key;
        if (!isDate) return CrmControllerBase.Humanise(key);

        if (!int.TryParse(key, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numeric))
        {
            return key;
        }

        return bucket switch
        {
            DateBucket.Year => numeric.ToString(CultureInfo.InvariantCulture),

            DateBucket.Quarter =>
                $"Q{numeric % 10} {numeric / 10}",

            DateBucket.Day => SafeDate(numeric / 10000, numeric / 100 % 100, numeric % 100)
                is DateTime day
                ? day.ToString("d MMM yyyy", CultureInfo.InvariantCulture)
                : key,

            _ => SafeDate(numeric / 100, numeric % 100, 1) is DateTime month
                ? month.ToString("MMM yyyy", CultureInfo.InvariantCulture)
                : key,
        };
    }

    private static DateTime? SafeDate(int year, int month, int day)
    {
        if (year < 1 || year > 9999 || month < 1 || month > 12 || day < 1 || day > 31)
        {
            return null;
        }

        try
        {
            return new DateTime(year, month, day);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /* ---------------- request parsing ---------------- */

    /// <summary>
    /// Reconciles measure and aggregation. `count` needs no measure, and a
    /// measure with no sensible aggregation falls back to summing it.
    /// </summary>
    private string? NormaliseMeasure(string? measure, ref AggFn function)
    {
        if (function == AggFn.Count) return null;

        if (string.IsNullOrWhiteSpace(measure) || measure == "*")
        {
            function = AggFn.Count;
            return null;
        }

        if (!AnalyticsEngine.IsMeasurable(Map, measure))
        {
            throw ApiException.BadRequest(
                $"'{measure}' is not a numeric field on {Label} and cannot be aggregated.");
        }

        return measure;
    }

    private static DateBucket ParseBucket(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "day" => DateBucket.Day,
        "quarter" => DateBucket.Quarter,
        "year" => DateBucket.Year,
        _ => DateBucket.Month,
    };

    private static AggFn ParseFunction(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "sum" => AggFn.Sum,
        "avg" or "average" => AggFn.Avg,
        "min" => AggFn.Min,
        "max" => AggFn.Max,
        "countdistinct" or "distinct" => AggFn.CountDistinct,
        _ => AggFn.Count,
    };

    /// <summary>
    /// The period window as a filter node, so it composes with the widget's own
    /// filter through the same engine rather than a second `Where` the filter
    /// builder cannot see.
    /// </summary>
    private FilterNode? PeriodFilter(AggregateRequest request)
    {
        var period = request.Period?.Trim();
        if (string.IsNullOrEmpty(period) || period is "all") return null;

        var dateField = string.IsNullOrWhiteSpace(request.DateField)
            ? DefaultDateField
            : request.DateField!;

        if (Map.Find(dateField) is not { Kind: FieldKind.Date }) return null;

        var today = DateTime.UtcNow.Date;

        var (from, to) = period switch
        {
            "today" => (today, today),
            "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
            "last7" => (today.AddDays(-6), today),
            "last30" => (today.AddDays(-29), today),
            "last90" => (today.AddDays(-89), today),
            "last180" => (today.AddDays(-179), today),
            "last12Months" => (today.AddMonths(-11).AddDays(1 - today.Day), today),
            "thisWeek" => (today.AddDays(-(int)today.DayOfWeek), today.AddDays(6 - (int)today.DayOfWeek)),
            "thisMonth" => (StartOfMonth(today), StartOfMonth(today).AddMonths(1).AddDays(-1)),
            "lastMonth" => (StartOfMonth(today).AddMonths(-1), StartOfMonth(today).AddDays(-1)),
            "thisQuarter" => (StartOfQuarter(today), StartOfQuarter(today).AddMonths(3).AddDays(-1)),
            "lastQuarter" => (StartOfQuarter(today).AddMonths(-3), StartOfQuarter(today).AddDays(-1)),
            "thisYear" => (new DateTime(today.Year, 1, 1), new DateTime(today.Year, 12, 31)),
            "lastYear" => (new DateTime(today.Year - 1, 1, 1), new DateTime(today.Year - 1, 12, 31)),
            _ => (DateTime.MinValue, DateTime.MinValue),
        };

        if (from == DateTime.MinValue) return null;

        return new FilterNode
        {
            Field = dateField,
            Operator = "between",
            Value = from.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            Value2 = to.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        };
    }

    private static DateTime StartOfMonth(DateTime value) => new(value.Year, value.Month, 1);

    private static DateTime StartOfQuarter(DateTime value) =>
        new(value.Year, ((value.Month - 1) / 3 * 3) + 1, 1);
}

/* ------------------------------------------------------------------ *
 * Registry
 * ------------------------------------------------------------------ */

/// <summary>
/// Every object the dashboard builder can chart.
///
/// Each entry reuses the controller's own <see cref="FieldMap{T}"/> rather than
/// declaring its own, so a field added to the leads list view is immediately
/// groupable on a dashboard, and a field removed there stops being reachable
/// here too. The curated dimension and measure lists are only about what the
/// editor's menus offer — the map is still what decides what is legal.
/// </summary>
public static class AnalyticsCatalog
{
    private static readonly List<AnalyticsDataset> All =
    [
        new AnalyticsDataset<Lead>
        {
            Id = "leads",
            Label = "Leads",
            Description = "Enquiries from every capture channel",
            RecordLabel = "leads",
            Source = db => db.Leads.AsNoTracking(),
            Map = LeadsController.Fields,
            DefaultDateField = "createdAt",
            DefaultDimension = "stage",
            Dimensions =
            [
                "stage", "source", "priority", "subStatus", "ownerName", "branchName",
                "city", "state", "requirementType", "configuration", "productGroup",
                "projectName", "campaign", "utmSource", "utmMedium", "fundingMode",
                "occupation", "cachedBand", "lossReason", "supportingManagerName",
                "createdAt", "lastActivityAt", "convertedAt",
            ],
            Measures =
            [
                new("budgetMax", "Budget (upper)", "currency"),
                new("budgetMin", "Budget (lower)", "currency"),
                new("cachedScore", "AI score", "number"),
                new("possessionTimelineMonths", "Possession timeline", "number"),
                new("isConverted", "Converted", "number"),
            ],
            Sequences = new Dictionary<string, string[]>
            {
                ["stage"] = LeadStages.All,
                ["priority"] = LeadPriorities.All,
            },
        },

        new AnalyticsDataset<Opportunity>
        {
            Id = "opportunities",
            Label = "Deals",
            Description = "Opportunities in the sales pipeline",
            RecordLabel = "deals",
            Source = db => db.Opportunities.AsNoTracking(),
            Map = OpportunitiesController.Fields,
            DefaultDateField = "createdAt",
            DefaultDimension = "stage",
            Dimensions =
            [
                "stage", "forecastCategory", "type", "source", "ownerName", "branchName",
                "projectName", "lossReason", "competitorName",
                "createdAt", "expectedCloseDate", "actualCloseDate", "stageEnteredAt",
            ],
            Measures =
            [
                new("amount", "Deal value", "currency"),
                new("expectedCommission", "Expected commission", "currency"),
                new("probability", "Win probability", "percent"),
            ],
            Sequences = new Dictionary<string, string[]>
            {
                ["stage"] = OpportunityStages.All,
                ["forecastCategory"] = ForecastCategories.All,
            },
        },

        new AnalyticsDataset<CallLog>
        {
            Id = "calls",
            Label = "Calls",
            Description = "Telephony activity from the call log",
            RecordLabel = "calls",
            Source = db => db.CallLogs.AsNoTracking(),
            Map = CallsController.Fields,
            DefaultDateField = "startedAt",
            DefaultDimension = "outcome",
            OwnerField = "agentId",
            Dimensions =
            [
                "outcome", "disposition", "direction", "sentimentLabel", "agentName",
                "branchName", "relatedType", "startedAt", "createdAt",
            ],
            Measures =
            [
                new("durationSeconds", "Talk time", "number"),
                new("waitSeconds", "Wait time", "number"),
                new("sentimentScore", "Sentiment score", "number"),
            ],
        },

        new AnalyticsDataset<SiteVisit>
        {
            Id = "siteVisits",
            Label = "Site visits",
            Description = "Project walkthroughs and their outcomes",
            RecordLabel = "site visits",
            Source = db => db.SiteVisits.AsNoTracking(),
            Map = SiteVisitsController.Fields,
            DefaultDateField = "scheduledAt",
            DefaultDimension = "status",
            OwnerField = "hostId",
            Dimensions =
            [
                "status", "visitType", "interestLevel", "projectName", "hostName",
                "branchName", "transportMode", "scheduledAt", "checkInAt", "createdAt",
            ],
            Measures =
            [
                new("budgetDiscussed", "Budget discussed", "currency"),
                new("rating", "Visit rating", "number"),
                new("partySize", "Party size", "number"),
                new("slotMinutes", "Visit length", "number"),
            ],
            Sequences = new Dictionary<string, string[]>
            {
                ["status"] = VisitStatuses.All,
                ["visitType"] = VisitTypes.All,
                ["interestLevel"] = InterestLevels.All,
            },
        },

        new AnalyticsDataset<Quotation>
        {
            Id = "quotations",
            Label = "Quotations",
            Description = "Priced proposals and their status",
            RecordLabel = "quotations",
            Source = db => db.Quotations.AsNoTracking(),
            Map = QuotationsController.Fields,
            DefaultDateField = "issueDate",
            DefaultDimension = "status",
            Dimensions =
            [
                "status", "projectName", "ownerName", "branchName",
                "issueDate", "validUntil", "sentAt", "respondedAt", "createdAt",
            ],
            Measures =
            [
                new("total", "Quoted total", "currency"),
                new("subtotal", "Subtotal", "currency"),
                new("discountAmount", "Discount", "currency"),
                new("discountPercent", "Discount %", "percent"),
                new("taxAmount", "Tax", "currency"),
            ],
            Sequences = new Dictionary<string, string[]>
            {
                ["status"] = QuotationStatuses.All,
            },
        },

        new AnalyticsDataset<FollowUp>
        {
            Id = "followUps",
            Label = "Follow-ups",
            Description = "Tasks, calls and touchpoints in the queue",
            RecordLabel = "follow-ups",
            Source = db => db.FollowUps.AsNoTracking(),
            Map = FollowUpsController.Fields,
            DefaultDateField = "dueAt",
            DefaultDimension = "status",
            Dimensions =
            [
                "status", "channel", "priority", "ownerName", "branchName",
                "relatedType", "dueAt", "completedAt", "createdAt",
            ],
            Measures = [new("slaMinutes", "SLA minutes", "number")],
            Sequences = new Dictionary<string, string[]>
            {
                ["status"] = FollowUpStatuses.All,
                ["priority"] = LeadPriorities.All,
            },
        },

        new AnalyticsDataset<Contact>
        {
            Id = "contacts",
            Label = "Contacts",
            Description = "The customer database",
            RecordLabel = "contacts",
            Source = db => db.Contacts.AsNoTracking(),
            Map = ContactsController.Fields,
            DefaultDateField = "createdAt",
            DefaultDimension = "lifecycleStage",
            Dimensions =
            [
                "lifecycleStage", "type", "source", "segment", "ownerName", "branchName",
                "city", "state", "preferredConfiguration",
                "createdAt", "lastActivityAt",
            ],
            Measures =
            [
                new("lifetimeValue", "Lifetime value", "currency"),
                new("dealCount", "Deals", "number"),
                new("budgetMax", "Budget (upper)", "currency"),
                new("whatsAppOptIn", "WhatsApp opt-in", "number"),
            ],
            Sequences = new Dictionary<string, string[]>
            {
                ["lifecycleStage"] = LifecycleStages.All,
                ["type"] = ContactTypes.All,
            },
        },

        new AnalyticsDataset<ObmVisit>
        {
            Id = "obmVisits",
            Label = "OBM visits",
            Description = "Channel-partner field meetings",
            RecordLabel = "OBM visits",
            Source = db => db.ObmVisits.AsNoTracking(),
            Map = ObmVisitsController.Fields,
            DefaultDateField = "scheduledAt",
            DefaultDimension = "status",
            OwnerField = "agentId",
            Dimensions =
            [
                "status", "partnerType", "agentName", "branchName", "city",
                "scheduledAt", "checkInAt", "createdAt",
            ],
            Measures =
            [
                new("businessValue", "Business value", "currency"),
                new("expenseAmount", "Expense", "currency"),
                new("distanceKm", "Distance", "number"),
                new("leadsGenerated", "Leads generated", "number"),
            ],
            Sequences = new Dictionary<string, string[]>
            {
                ["status"] = VisitStatuses.All,
                ["partnerType"] = PartnerTypes.All,
            },
        },
    ];

    public static IReadOnlyList<AnalyticsDataset> Datasets => All;

    public static AnalyticsDataset Require(string? id) =>
        All.FirstOrDefault(d => string.Equals(d.Id, id, StringComparison.OrdinalIgnoreCase))
        ?? throw ApiException.BadRequest(
            $"'{id ?? "(none)"}' is not a dataset. Expected one of: {string.Join(", ", All.Select(d => d.Id))}.");
}
