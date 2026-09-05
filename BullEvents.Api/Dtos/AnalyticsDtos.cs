using BullEvents.Api.Infrastructure;

namespace BullEvents.Api.Dtos;

/* ------------------------------------------------------------------ *
 * Metadata — drives the dashboard widget editor
 * ------------------------------------------------------------------ */

/// <summary>A field a widget can group by.</summary>
public record DimensionDto(
    string Id,
    string Label,
    /// <summary>"category" or "date" — a date dimension offers bucket options.</summary>
    string Kind,
    string? Group,
    /// <summary>True when the field has a canonical order, e.g. pipeline stages.</summary>
    bool Ordered = false
);

/// <summary>A numeric a widget can aggregate, plus how to render it.</summary>
public record MeasureDto(
    string Id,
    string Label,
    /// <summary>"number", "currency" or "percent" — picks the client's formatter.</summary>
    string Format,
    /// <summary>Aggregations that make sense for this measure.</summary>
    IReadOnlyList<string> Aggregations
);

/// <summary>One queryable object, as offered in the widget editor.</summary>
public record DatasetDto(
    string Id,
    string Label,
    string Description,
    /// <summary>Plural noun for the record count, e.g. "leads".</summary>
    string RecordLabel,
    IReadOnlyList<DimensionDto> Dimensions,
    IReadOnlyList<MeasureDto> Measures,
    /// <summary>Date fields a period filter can be applied to.</summary>
    IReadOnlyList<DimensionDto> DateFields,
    string DefaultDateField,
    string DefaultDimension,
    IReadOnlyList<FilterFieldDto> FilterFields
);

/* ------------------------------------------------------------------ *
 * Request
 * ------------------------------------------------------------------ */

/// <summary>
/// Everything one widget sends to compute itself. The shape deliberately mirrors
/// the widget's own config object on the client, so the editor's state serialises
/// straight onto the wire with no translation layer in between.
/// </summary>
public class AggregateRequest
{
    public string Dataset { get; set; } = string.Empty;

    /// <summary>Field to group by.</summary>
    public string Dimension { get; set; } = string.Empty;

    /// <summary>Roll-up for a date dimension: day, month, quarter or year.</summary>
    public string? Bucket { get; set; }

    /// <summary>Optional second dimension — becomes the chart's series.</summary>
    public string? Breakdown { get; set; }

    public string? BreakdownBucket { get; set; }

    /// <summary>Numeric to aggregate. Omitted for a straight record count.</summary>
    public string? Measure { get; set; }

    /// <summary>count, sum, avg, min, max or countDistinct.</summary>
    public string Aggregation { get; set; } = "count";

    /// <summary>Free-text search across the dataset's searchable fields.</summary>
    public string? Search { get; set; }

    /// <summary>The widget's own filter tree, evaluated in SQL.</summary>
    public FilterNode? Filter { get; set; }

    /// <summary>Date field the period window applies to.</summary>
    public string? DateField { get; set; }

    /// <summary>Relative window: thisMonth, lastMonth, thisQuarter, thisYear, last30, last90, last12Months, all.</summary>
    public string? Period { get; set; }

    /// <summary>valueDesc, valueAsc, keyAsc or keyDesc.</summary>
    public string Sort { get; set; } = "valueDesc";

    /// <summary>Keep only the top N groups.</summary>
    public int? Limit { get; set; }

    /// <summary>Roll everything past <see cref="Limit"/> into a single "Other" group.</summary>
    public bool GroupOther { get; set; }
}

/* ------------------------------------------------------------------ *
 * Response
 * ------------------------------------------------------------------ */

/// <summary>One group: its key, its display label and a value per series.</summary>
public record AggregatePointDto(
    string Key,
    string Label,
    Dictionary<string, decimal> Values,
    decimal Total,
    int Records
);

public record AggregateResponse(
    string Dataset,
    /// <summary>Display name for the object, e.g. "Site visits".</summary>
    string DatasetLabel,
    string Dimension,
    /// <summary>Display name for the group-by field, e.g. "Branch".</summary>
    string DimensionLabel,
    string DimensionKind,
    string? Bucket,
    string? Breakdown,
    string Measure,
    string MeasureLabel,
    string Format,
    string Aggregation,
    /// <summary>Series names, in the order the chart should colour them.</summary>
    IReadOnlyList<string> Series,
    IReadOnlyList<AggregatePointDto> Rows,
    /// <summary>The aggregate over every matching record, ignoring the grouping.</summary>
    decimal Total,
    /// <summary>How many records matched the filter.</summary>
    int MatchedRecords,
    /// <summary>Groups dropped by the limit, if any.</summary>
    int TruncatedGroups
);
