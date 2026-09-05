using System.Linq.Expressions;
using System.Reflection;

namespace BullEvents.Api.Infrastructure;

/* ------------------------------------------------------------------ *
 * Wire contract
 * ------------------------------------------------------------------ */

/// <summary>How a date dimension is rolled up before grouping.</summary>
public enum DateBucket { None, Day, Month, Quarter, Year }

/// <summary>The aggregate applied to the measure inside each group.</summary>
public enum AggFn { Count, Sum, Avg, Min, Max, CountDistinct }

/// <summary>
/// One flattened fact row: the group-by key, the optional breakdown series and
/// the single numeric the aggregate is computed over. Projecting into this
/// fixed shape first is what lets the grouping itself be ordinary typed LINQ —
/// only the projection needs an expression tree, and the aggregate that follows
/// is a plain lambda EF has no trouble translating.
/// </summary>
public class AggregateRow
{
    public string Dimension { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public decimal? Measure { get; set; }
}

/// <summary>One group: a dimension value, a series value and the aggregate.</summary>
public class AggregateBucket
{
    public string Dimension { get; set; } = string.Empty;
    public string Series { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public int Count { get; set; }
}

/* ------------------------------------------------------------------ *
 * Engine
 * ------------------------------------------------------------------ */

/// <summary>
/// Turns "group this object by that field and aggregate this measure" into
/// composed LINQ, using the same <see cref="FieldMap{T}"/> whitelist the filter
/// builder is generated from. Nothing outside the map is groupable or
/// measurable, so a dashboard widget cannot reach a column the list view would
/// have refused.
///
/// Everything it emits is EF-translatable: the grouping and the aggregate both
/// resolve in MySQL, so a widget over a hundred thousand leads returns a dozen
/// rows rather than pulling the table into memory to count it.
/// </summary>
public static class AnalyticsEngine
{
    /// <summary>Group values that come back null or blank read as this.</summary>
    public const string Blank = "—";

    /* ---------------- projection ---------------- */

    /// <summary>
    /// Projects the filtered set into <see cref="AggregateRow"/>: dimension key,
    /// series key and measure, each built from the field map.
    /// </summary>
    public static IQueryable<AggregateRow> Project<T>(
        IQueryable<T> source,
        FieldMap<T> map,
        string dimensionField,
        DateBucket dimensionBucket,
        string? seriesField,
        DateBucket seriesBucket,
        string? measureField)
    {
        var parameter = Expression.Parameter(typeof(T), "e");

        var dimension = KeyExpression(parameter, map, dimensionField, dimensionBucket)
            ?? throw ApiException.BadRequest(
                $"'{dimensionField}' is not a groupable field on this dataset.");

        var series = string.IsNullOrWhiteSpace(seriesField)
            ? Expression.Constant(string.Empty, typeof(string))
            : KeyExpression(parameter, map, seriesField!, seriesBucket)
              ?? throw ApiException.BadRequest(
                  $"'{seriesField}' is not a groupable field on this dataset.");

        var measure = string.IsNullOrWhiteSpace(measureField)
            ? Expression.Constant(null, typeof(decimal?))
            : MeasureExpression(parameter, map, measureField!)
              ?? throw ApiException.BadRequest(
                  $"'{measureField}' is not a numeric field on this dataset.");

        var body = Expression.MemberInit(
            Expression.New(typeof(AggregateRow)),
            Expression.Bind(Property(nameof(AggregateRow.Dimension)), dimension),
            Expression.Bind(Property(nameof(AggregateRow.Series)), series),
            Expression.Bind(Property(nameof(AggregateRow.Measure)), measure));

        return source.Select(Expression.Lambda<Func<T, AggregateRow>>(body, parameter));
    }

    /// <summary>
    /// Just the measure column, for callers that want one number rather than a
    /// grouped result — a goal's progress is a scalar, and projecting a
    /// dimension it would immediately discard would be wasted work in SQL.
    /// </summary>
    public static IQueryable<decimal?> ProjectMeasure<T>(
        IQueryable<T> source,
        FieldMap<T> map,
        string measureField)
    {
        var parameter = Expression.Parameter(typeof(T), "e");

        var measure = MeasureExpression(parameter, map, measureField)
            ?? throw ApiException.BadRequest(
                $"'{measureField}' is not a numeric field on this dataset.");

        return source.Select(Expression.Lambda<Func<T, decimal?>>(measure, parameter));
    }

    /* ---------------- aggregation ---------------- */

    /// <summary>
    /// Groups the projected rows and applies the aggregate. The lambdas here are
    /// ordinary typed C# — <see cref="AggregateRow"/> has a known shape, so no
    /// expression building is needed past the projection.
    /// </summary>
    public static IQueryable<AggregateBucket> Aggregate(
        IQueryable<AggregateRow> projected,
        AggFn function)
    {
        var groups = projected.GroupBy(row => new { row.Dimension, row.Series });

        return function switch
        {
            AggFn.Sum => groups.Select(g => new AggregateBucket
            {
                Dimension = g.Key.Dimension,
                Series = g.Key.Series,
                Value = g.Sum(row => row.Measure) ?? 0m,
                Count = g.Count(),
            }),

            AggFn.Avg => groups.Select(g => new AggregateBucket
            {
                Dimension = g.Key.Dimension,
                Series = g.Key.Series,
                Value = g.Average(row => row.Measure) ?? 0m,
                Count = g.Count(),
            }),

            AggFn.Min => groups.Select(g => new AggregateBucket
            {
                Dimension = g.Key.Dimension,
                Series = g.Key.Series,
                Value = g.Min(row => row.Measure) ?? 0m,
                Count = g.Count(),
            }),

            AggFn.Max => groups.Select(g => new AggregateBucket
            {
                Dimension = g.Key.Dimension,
                Series = g.Key.Series,
                Value = g.Max(row => row.Measure) ?? 0m,
                Count = g.Count(),
            }),

            AggFn.CountDistinct => groups.Select(g => new AggregateBucket
            {
                Dimension = g.Key.Dimension,
                Series = g.Key.Series,
                Value = g.Select(row => row.Measure).Distinct().Count(),
                Count = g.Count(),
            }),

            _ => groups.Select(g => new AggregateBucket
            {
                Dimension = g.Key.Dimension,
                Series = g.Key.Series,
                Value = g.Count(),
                Count = g.Count(),
            }),
        };
    }

    /* ---------------- key expressions ---------------- */

    /// <summary>
    /// The group-by key for one field, as a string.
    ///
    /// Date fields are bucketed into a fixed-width numeric key
    /// (`202608` for August 2026) rather than a formatted date. Fixed width is
    /// the point: the key sorts lexicographically in the same order it sorts
    /// chronologically, so the server can order the groups without a second
    /// pass, and the client renders the label it wants from the parts.
    /// </summary>
    private static Expression? KeyExpression<T>(
        ParameterExpression parameter,
        FieldMap<T> map,
        string fieldId,
        DateBucket bucket)
    {
        var field = map.Find(fieldId);
        if (field is null) return null;

        var member = Resolve(parameter, field.Path);
        var underlying = Nullable.GetUnderlyingType(member.Type) ?? member.Type;

        if (underlying == typeof(DateTime) && bucket != DateBucket.None)
        {
            return DateKey(member, bucket);
        }

        if (member.Type == typeof(string))
        {
            // A null or an empty string both read as one "no value" group.
            return Expression.Condition(
                Expression.OrElse(
                    Expression.Equal(member, Expression.Constant(null, typeof(string))),
                    Expression.Equal(member, Expression.Constant(string.Empty))),
                Expression.Constant(Blank),
                member);
        }

        return NonStringToString(member, underlying);
    }

    /// <summary>
    /// `Year`, `Year*100+Month`, `Year*10+Quarter` or `Year*10000+Month*100+Day`
    /// — plain integer arithmetic, which every provider translates.
    /// </summary>
    private static Expression DateKey(Expression member, DateBucket bucket)
    {
        var nullable = Nullable.GetUnderlyingType(member.Type) is not null;
        var value = nullable ? Expression.Convert(member, typeof(DateTime)) : member;

        var year = Expression.Property(value, nameof(DateTime.Year));
        var month = Expression.Property(value, nameof(DateTime.Month));
        var day = Expression.Property(value, nameof(DateTime.Day));

        Expression key = bucket switch
        {
            DateBucket.Year => year,

            // Quarter is picked with a CASE rather than `(month - 1) / 3 + 1`:
            // MySQL's `/` is decimal division, so the arithmetic form yields
            // 20244.3333 instead of 20244 and the key stops being parseable.
            DateBucket.Quarter => Expression.Add(
                Expression.Multiply(year, Expression.Constant(10)),
                QuarterOf(month)),

            DateBucket.Day => Expression.Add(
                Expression.Add(
                    Expression.Multiply(year, Expression.Constant(10000)),
                    Expression.Multiply(month, Expression.Constant(100))),
                day),

            _ => Expression.Add(
                Expression.Multiply(year, Expression.Constant(100)),
                month),
        };

        var asString = Expression.Call(key, IntToString);

        if (!nullable) return asString;

        return Expression.Condition(
            Expression.Equal(member, Expression.Constant(null, member.Type)),
            Expression.Constant(Blank),
            asString);
    }

    /// <summary>1–4 from a month number, as a nested CASE the provider can translate.</summary>
    private static Expression QuarterOf(Expression month) =>
        Expression.Condition(
            Expression.LessThanOrEqual(month, Expression.Constant(3)),
            Expression.Constant(1),
            Expression.Condition(
                Expression.LessThanOrEqual(month, Expression.Constant(6)),
                Expression.Constant(2),
                Expression.Condition(
                    Expression.LessThanOrEqual(month, Expression.Constant(9)),
                    Expression.Constant(3),
                    Expression.Constant(4))));

    /// <summary>Non-string keys (enum-ish ints, bools, ids) rendered as text.</summary>
    private static Expression NonStringToString(Expression member, Type underlying)
    {
        var nullable = Nullable.GetUnderlyingType(member.Type) is not null;
        var value = nullable ? Expression.Convert(member, underlying) : member;

        var toString = underlying.GetMethod(nameof(ToString), Type.EmptyTypes)
            ?? typeof(object).GetMethod(nameof(ToString), Type.EmptyTypes)!;

        Expression asString = Expression.Call(value, toString);

        if (!nullable) return asString;

        return Expression.Condition(
            Expression.Equal(member, Expression.Constant(null, member.Type)),
            Expression.Constant(Blank),
            asString);
    }

    /// <summary>The measure, widened to `decimal?` so one group-by covers every numeric type.</summary>
    private static Expression? MeasureExpression<T>(
        ParameterExpression parameter,
        FieldMap<T> map,
        string fieldId)
    {
        var field = map.Find(fieldId);
        if (field is null) return null;

        var member = Resolve(parameter, field.Path);
        var underlying = Nullable.GetUnderlyingType(member.Type) ?? member.Type;

        if (underlying == typeof(bool))
        {
            // A boolean measure is "how many are true" — sum of 1s and 0s.
            var asBool = Nullable.GetUnderlyingType(member.Type) is null
                ? member
                : Expression.Equal(member, Expression.Constant(true, member.Type));

            return Expression.Condition(
                asBool,
                Expression.Constant(1m, typeof(decimal?)),
                Expression.Constant(0m, typeof(decimal?)));
        }

        if (!Numeric.Contains(underlying)) return null;

        return Expression.Convert(member, typeof(decimal?));
    }

    /* ---------------- helpers ---------------- */

    private static readonly HashSet<Type> Numeric =
    [
        typeof(int), typeof(long), typeof(short), typeof(byte),
        typeof(decimal), typeof(double), typeof(float),
    ];

    private static readonly MethodInfo IntToString =
        typeof(int).GetMethod(nameof(ToString), Type.EmptyTypes)!;

    private static PropertyInfo Property(string name) =>
        typeof(AggregateRow).GetProperty(name)!;

    /// <summary>Walks a dotted path (`owner.name`) into a member expression.</summary>
    private static Expression Resolve(Expression root, string path)
    {
        var current = root;

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var property = current.Type.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase)
                ?? throw new ArgumentException(
                    $"'{segment}' is not a readable property of {current.Type.Name}.", nameof(path));

            current = Expression.Property(current, property);
        }

        return current;
    }

    /// <summary>True when the field exists and holds a date.</summary>
    public static bool IsDate<T>(FieldMap<T> map, string fieldId) =>
        map.Find(fieldId) is { Kind: FieldKind.Date };

    /// <summary>True when the field can be summed or averaged.</summary>
    public static bool IsMeasurable<T>(FieldMap<T> map, string fieldId) =>
        map.Find(fieldId) is { Kind: FieldKind.Number or FieldKind.Boolean };
}
