using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Infrastructure;

/* ------------------------------------------------------------------ *
 * Wire contract
 * ------------------------------------------------------------------ */

/// <summary>
/// One node of a filter tree. A node is either a leaf (Field + Operator, and a
/// Value unless the operator is unary) or a group (Conjunction + Children).
/// Groups nest arbitrarily, which is what lets the UI express
/// `A AND (B OR C) AND NOT D`.
/// </summary>
public class FilterNode
{
    public string? Field { get; set; }
    public string? Operator { get; set; }
    public string? Value { get; set; }

    /// <summary>Second operand for range operators (`between`).</summary>
    public string? Value2 { get; set; }

    /// <summary>Multi-value operand for `in` / `notIn`.</summary>
    public List<string>? Values { get; set; }

    /// <summary>"and" or "or" — how this group's children combine.</summary>
    public string? Conjunction { get; set; }

    /// <summary>Inverts the whole node.</summary>
    public bool Negate { get; set; }

    public List<FilterNode>? Children { get; set; }

    public bool IsGroup => Children is { Count: > 0 };
}

public class SortSpec
{
    public string Field { get; set; } = string.Empty;
    public bool Descending { get; set; }
}

/// <summary>Everything a list view sends to the server in one object.</summary>
public class QueryRequest
{
    public string? Search { get; set; }
    public FilterNode? Filter { get; set; }
    public List<SortSpec>? Sort { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;

    /// <summary>Field to group counts by, returned alongside the page.</summary>
    public string? GroupBy { get; set; }

    /// <summary>Fields to return live facet counts for.</summary>
    public List<string>? Facets { get; set; }
}

public class FacetBucket
{
    public string Value { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class PagedResult<T>
{
    public IReadOnlyList<T> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int PageCount => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public Dictionary<string, List<FacetBucket>> Facets { get; set; } = [];
    public Dictionary<string, decimal> Aggregates { get; set; } = [];
}

/* ------------------------------------------------------------------ *
 * Field registry
 * ------------------------------------------------------------------ */

public enum FieldKind { Text, Number, Date, Boolean, Select }

/// <summary>
/// The set of fields a given entity is allowed to be filtered on, plus how to
/// reach each one. Nothing outside this map is queryable, so a caller cannot
/// probe arbitrary properties or walk navigations we did not intend to expose.
/// </summary>
public class FieldMap<T>
{
    private readonly Dictionary<string, FieldEntry> _fields = new(StringComparer.OrdinalIgnoreCase);

    public record FieldEntry(string Id, string Label, FieldKind Kind, string Path, bool Searchable);

    public FieldMap<T> Text(string id, string label, string? path = null, bool searchable = false)
        => Add(id, label, FieldKind.Text, path ?? id, searchable);

    public FieldMap<T> Select(string id, string label, string? path = null)
        => Add(id, label, FieldKind.Select, path ?? id, false);

    public FieldMap<T> Number(string id, string label, string? path = null)
        => Add(id, label, FieldKind.Number, path ?? id, false);

    public FieldMap<T> Date(string id, string label, string? path = null)
        => Add(id, label, FieldKind.Date, path ?? id, false);

    public FieldMap<T> Bool(string id, string label, string? path = null)
        => Add(id, label, FieldKind.Boolean, path ?? id, false);

    private FieldMap<T> Add(string id, string label, FieldKind kind, string path, bool searchable)
    {
        // Resolved once, here, rather than on the first request that happens to
        // touch this field. A path naming a navigation the entity does not have
        // is a typo in the map, and it should surface when the map is built —
        // not as a 400 the day someone facets on that column.
        Validate(path);

        _fields[id] = new FieldEntry(id, label, kind, path, searchable);
        return this;
    }

    private static void Validate(string path)
    {
        var current = typeof(T);

        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            var property = current.GetProperty(
                segment,
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

            if (property is null)
            {
                throw new InvalidOperationException(
                    $"Field map for {typeof(T).Name} declares path '{path}', but '{segment}' " +
                    $"is not a property of {current.Name}. Add the navigation property or fix the path.");
            }

            current = property.PropertyType;
        }
    }

    public FieldEntry? Find(string id) => _fields.GetValueOrDefault(id);

    public IReadOnlyCollection<FieldEntry> All => _fields.Values;

    public IEnumerable<FieldEntry> SearchableFields => _fields.Values.Where(f => f.Searchable);
}

/* ------------------------------------------------------------------ *
 * Engine
 * ------------------------------------------------------------------ */

/// <summary>
/// Turns a <see cref="QueryRequest"/> into composed LINQ over an
/// <see cref="IQueryable{T}"/>. Everything it emits is EF-translatable, so
/// filtering, sorting, faceting and paging all happen in the database — the API never
/// pulls a table into memory to filter it.
/// </summary>
public static class QueryEngine
{
    private static readonly MethodInfo StringContains =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo StringStartsWith =
        typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
    private static readonly MethodInfo StringEndsWith =
        typeof(string).GetMethod(nameof(string.EndsWith), [typeof(string)])!;
    private static readonly MethodInfo StringToLower =
        typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
    // Collate<TProperty> is generic — GetMethod can't be handed the parameter
    // types directly, it has to find the open definition and close it over
    // string itself.
    private static readonly MethodInfo EfFunctionsCollate =
        typeof(RelationalDbFunctionsExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == nameof(RelationalDbFunctionsExtensions.Collate)
                && m.IsGenericMethodDefinition)
            .MakeGenericMethod(typeof(string));
    private static readonly Expression EfFunctions =
        Expression.Property(null, typeof(EF).GetProperty(nameof(EF.Functions))!);

    /// <summary>
    /// Every searchable column carries <see cref="BullEvents.Api.Data.AppDbContext.CaseInsensitiveCollation"/>
    /// — a non-deterministic ICU collation, which is what lets an equality check
    /// or an ORDER BY treat "Rajesh" and "rajesh" alike without a LOWER() on
    /// every read. Postgres will not run LIKE, ILIKE or any other pattern match
    /// against a non-deterministic collation at all — it refuses at the planner
    /// rather than at read time, so this is not a data problem, it is every
    /// Contains/StartsWith/EndsWith filter in the product.
    ///
    /// The fix folds the column to lower-case (translated to <c>lower(...)</c>)
    /// and re-collates the result to the built-in deterministic <c>"C"</c>
    /// collation, which Postgres will pattern-match against. Comparing against
    /// an already-lower-cased needle keeps the match case-insensitive; it is a
    /// plain ASCII fold rather than the ICU collation's locale-aware one, which
    /// is the trade this table's actual content — names, phone numbers, emails,
    /// unit numbers — never notices.
    /// </summary>
    private static Expression CaseInsensitiveText(Expression member)
    {
        var lowered = Expression.Call(member, StringToLower);
        return Expression.Call(
            null, EfFunctionsCollate, EfFunctions, lowered, Expression.Constant("C"));
    }

    /* ---------------- public surface ---------------- */

    public static IQueryable<T> Apply<T>(
        IQueryable<T> source,
        QueryRequest request,
        FieldMap<T> map)
    {
        source = ApplySearch(source, request.Search, map);
        source = ApplyFilter(source, request.Filter, map);
        return source;
    }

    public static IQueryable<T> ApplySearch<T>(
        IQueryable<T> source,
        string? search,
        FieldMap<T> map)
    {
        var needle = search?.Trim();
        if (string.IsNullOrEmpty(needle)) return source;

        var needleLower = Expression.Constant(needle.ToLowerInvariant());

        var parameter = Expression.Parameter(typeof(T), "e");
        Expression? combined = null;

        foreach (var field in map.SearchableFields)
        {
            var member = Resolve(parameter, field.Path);
            if (member.Type != typeof(string)) continue;

            var test = Expression.AndAlso(
                Expression.NotEqual(member, Expression.Constant(null, typeof(string))),
                Expression.Call(CaseInsensitiveText(member), StringContains, needleLower));

            combined = combined is null ? test : Expression.OrElse(combined, test);
        }

        if (combined is null) return source;

        return source.Where(Expression.Lambda<Func<T, bool>>(combined, parameter));
    }

    public static IQueryable<T> ApplyFilter<T>(
        IQueryable<T> source,
        FilterNode? filter,
        FieldMap<T> map)
    {
        if (filter is null) return source;

        var parameter = Expression.Parameter(typeof(T), "e");
        var body = Build(filter, parameter, map);
        if (body is null) return source;

        return source.Where(Expression.Lambda<Func<T, bool>>(body, parameter));
    }

    public static IQueryable<T> ApplySort<T>(
        IQueryable<T> source,
        IReadOnlyList<SortSpec>? sort,
        FieldMap<T> map,
        string fallbackPath,
        bool fallbackDescending = true)
    {
        var specs = (sort ?? [])
            .Select(s => (Spec: s, Field: map.Find(s.Field)))
            .Where(x => x.Field is not null)
            .ToList();

        if (specs.Count == 0)
        {
            return Stabilise(OrderByPath(source, fallbackPath, fallbackDescending, first: true));
        }

        IQueryable<T> ordered = source;
        var first = true;

        foreach (var (spec, field) in specs)
        {
            ordered = OrderByPath(ordered, field!.Path, spec.Descending, first);
            first = false;
        }

        return Stabilise(ordered);
    }

    /// <summary>
    /// Appends the primary key as the last ordering, so paging is repeatable.
    ///
    /// Almost every column a grid sorts on has ties — two props share a name,
    /// forty leads share a stage — and SQL leaves the order of tied rows
    /// undefined. Each page is a separate query, so the database is free to
    /// break the tie differently on page two than it did on page one, and a row
    /// on the boundary is then returned twice or not at all. It is invisible on
    /// screen and ruinous to anything that walks the whole list: an export drops
    /// records, a bulk edit misses them.
    ///
    /// Ordering by the key last costs nothing — it only ever decides ties — and
    /// makes the sequence total, which is what paging needs to be correct.
    /// Entities without an <c>Id</c> are left alone rather than guessed at.
    /// </summary>
    private static IQueryable<T> Stabilise<T>(IQueryable<T> ordered)
    {
        var key = typeof(T).GetProperty("Id");
        if (key is null || ordered is not IOrderedQueryable<T>) return ordered;

        var parameter = Expression.Parameter(typeof(T), "e");
        var selector = Expression.Lambda(Expression.Property(parameter, key), parameter);

        var call = Expression.Call(
            typeof(Queryable),
            nameof(Queryable.ThenBy),
            [typeof(T), key.PropertyType],
            ordered.Expression,
            Expression.Quote(selector));

        return ordered.Provider.CreateQuery<T>(call);
    }

    public static async Task<PagedResult<TOut>> ToPageAsync<T, TOut>(
        IQueryable<T> source,
        QueryRequest request,
        Func<T, TOut> project,
        Func<IQueryable<T>, Task<List<T>>> materialise,
        Func<IQueryable<T>, Task<int>> count)
    {
        var total = await count(source);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 50 : request.PageSize, 1, 500);
        var page = Math.Max(1, request.Page);

        var pageQuery = source.Skip((page - 1) * pageSize).Take(pageSize);
        var rows = await materialise(pageQuery);

        return new PagedResult<TOut>
        {
            Items = rows.Select(project).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <summary>
    /// Live counts per distinct value for one field, computed over the same
    /// filtered set the page came from — this is what makes the faceted filter
    /// counts honest instead of counting the whole table.
    /// </summary>
    public static IQueryable<FacetBucket> Facet<T>(
        IQueryable<T> filtered,
        FieldMap<T> map,
        string fieldId)
    {
        var field = map.Find(fieldId)
            ?? throw new ArgumentException($"Unknown facet field '{fieldId}'.", nameof(fieldId));

        var parameter = Expression.Parameter(typeof(T), "e");
        var member = Resolve(parameter, field.Path);

        Expression asString = member.Type == typeof(string)
            ? member
            : Expression.Call(
                Expression.Convert(member, typeof(object)),
                typeof(object).GetMethod(nameof(ToString))!);

        var selector = Expression.Lambda<Func<T, string>>(
            Expression.Coalesce(asString, Expression.Constant("—")), parameter);

        return filtered
            .GroupBy(selector)
            .Select(g => new FacetBucket { Value = g.Key, Count = g.Count() });
    }

    /* ---------------- predicate construction ---------------- */

    private static Expression? Build<T>(FilterNode node, ParameterExpression parameter, FieldMap<T> map)
    {
        Expression? result;

        if (node.IsGroup)
        {
            var useOr = string.Equals(node.Conjunction, "or", StringComparison.OrdinalIgnoreCase);
            Expression? combined = null;

            foreach (var child in node.Children!)
            {
                var childExpression = Build(child, parameter, map);
                if (childExpression is null) continue;

                combined = combined is null
                    ? childExpression
                    : useOr
                        ? Expression.OrElse(combined, childExpression)
                        : Expression.AndAlso(combined, childExpression);
            }

            result = combined;
        }
        else
        {
            result = Leaf(node, parameter, map);
        }

        if (result is null) return null;
        return node.Negate ? Expression.Not(result) : result;
    }

    private static Expression? Leaf<T>(FilterNode node, ParameterExpression parameter, FieldMap<T> map)
    {
        if (string.IsNullOrWhiteSpace(node.Field) || string.IsNullOrWhiteSpace(node.Operator))
        {
            return null;
        }

        var field = map.Find(node.Field);
        if (field is null) return null;

        var member = Resolve(parameter, field.Path);
        var op = node.Operator.Trim();

        return field.Kind switch
        {
            FieldKind.Number => NumberLeaf(member, op, node),
            FieldKind.Date => DateLeaf(member, op, node),
            FieldKind.Boolean => BooleanLeaf(member, op, node),
            _ => TextLeaf(member, op, node),
        };
    }

    /* ---------------- text / select ---------------- */

    private static Expression? TextLeaf(Expression member, string op, FilterNode node)
    {
        var nullConstant = Expression.Constant(null, typeof(string));
        var notNull = Expression.NotEqual(member, nullConstant);

        switch (op)
        {
            case "isEmpty":
                return Expression.OrElse(
                    Expression.Equal(member, nullConstant),
                    Expression.Equal(member, Expression.Constant(string.Empty)));

            case "isNotEmpty":
                return Expression.AndAlso(
                    notNull,
                    Expression.NotEqual(member, Expression.Constant(string.Empty)));

            case "in":
            case "notIn":
            {
                var values = node.Values ?? [];
                if (values.Count == 0) return null;

                var list = Expression.Constant(values, typeof(List<string>));
                var contains = Expression.Call(
                    list,
                    typeof(List<string>).GetMethod(nameof(List<string>.Contains), [typeof(string)])!,
                    member);

                return op == "in" ? contains : Expression.Not(contains);
            }
        }

        var value = node.Value;
        if (string.IsNullOrEmpty(value)) return null;
        var constant = Expression.Constant(value, typeof(string));

        // equals/notEquals stay on the raw member: they compile to `=`, which
        // Postgres runs against a non-deterministic collation without
        // complaint — it is only pattern matching that refuses.
        if (op is "equals" or "notEquals")
        {
            return op == "equals"
                ? Expression.Equal(member, constant)
                : Expression.NotEqual(member, constant);
        }

        var foldedMember = CaseInsensitiveText(member);
        var foldedConstant = Expression.Constant(value.ToLowerInvariant());

        return op switch
        {
            "contains" => Expression.AndAlso(
                notNull, Expression.Call(foldedMember, StringContains, foldedConstant)),
            "notContains" => Expression.OrElse(
                Expression.Equal(member, nullConstant),
                Expression.Not(Expression.Call(foldedMember, StringContains, foldedConstant))),
            "startsWith" => Expression.AndAlso(
                notNull, Expression.Call(foldedMember, StringStartsWith, foldedConstant)),
            "endsWith" => Expression.AndAlso(
                notNull, Expression.Call(foldedMember, StringEndsWith, foldedConstant)),
            _ => null,
        };
    }

    /* ---------------- number ---------------- */

    private static Expression? NumberLeaf(Expression member, string op, FilterNode node)
    {
        var underlying = Nullable.GetUnderlyingType(member.Type) ?? member.Type;

        if (op is "isNull" or "isNotNull")
        {
            if (Nullable.GetUnderlyingType(member.Type) is null) return null;
            var isNull = Expression.Equal(member, Expression.Constant(null, member.Type));
            return op == "isNull" ? isNull : Expression.Not(isNull);
        }

        if (!TryConvert(node.Value, underlying, out var left)) return null;

        var comparable = Nullable.GetUnderlyingType(member.Type) is null
            ? member
            : Expression.Convert(member, underlying);

        var hasValue = Nullable.GetUnderlyingType(member.Type) is null
            ? null
            : Expression.NotEqual(member, Expression.Constant(null, member.Type));

        Expression? comparison = op switch
        {
            "equals" => Expression.Equal(comparable, left!),
            "notEquals" => Expression.NotEqual(comparable, left!),
            "greaterThan" => Expression.GreaterThan(comparable, left!),
            "greaterOrEqual" => Expression.GreaterThanOrEqual(comparable, left!),
            "lessThan" => Expression.LessThan(comparable, left!),
            "lessOrEqual" => Expression.LessThanOrEqual(comparable, left!),
            "between" => TryConvert(node.Value2, underlying, out var upper)
                ? Expression.AndAlso(
                    Expression.GreaterThanOrEqual(comparable, left!),
                    Expression.LessThanOrEqual(comparable, upper!))
                : null,
            _ => null,
        };

        if (comparison is null) return null;
        return hasValue is null ? comparison : Expression.AndAlso(hasValue, comparison);
    }

    /* ---------------- date ---------------- */

    private static Expression? DateLeaf(Expression member, string op, FilterNode node)
    {
        var nullable = Nullable.GetUnderlyingType(member.Type) is not null;

        if (op is "isNull" or "isNotNull")
        {
            if (!nullable) return null;
            var isNull = Expression.Equal(member, Expression.Constant(null, member.Type));
            return op == "isNull" ? isNull : Expression.Not(isNull);
        }

        var comparable = nullable ? Expression.Convert(member, typeof(DateTime)) : member;
        var hasValue = nullable
            ? Expression.NotEqual(member, Expression.Constant(null, member.Type))
            : null;

        var now = DateTime.UtcNow;
        Expression? comparison = null;

        switch (op)
        {
            case "lastNDays":
            case "olderThanNDays":
            case "nextNDays":
            {
                if (!int.TryParse(node.Value, out var days)) return null;

                if (op == "nextNDays")
                {
                    comparison = Expression.AndAlso(
                        Expression.GreaterThanOrEqual(comparable, Expression.Constant(now)),
                        Expression.LessThanOrEqual(comparable, Expression.Constant(now.AddDays(days))));
                }
                else
                {
                    var cutoff = Expression.Constant(now.AddDays(-days));
                    comparison = op == "lastNDays"
                        ? Expression.GreaterThanOrEqual(comparable, cutoff)
                        : Expression.LessThan(comparable, cutoff);
                }
                break;
            }

            case "today":
                comparison = Between(comparable, now.Date, now.Date.AddDays(1));
                break;

            case "thisWeek":
            {
                var start = now.Date.AddDays(-(int)now.DayOfWeek);
                comparison = Between(comparable, start, start.AddDays(7));
                break;
            }

            case "thisMonth":
            {
                var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
                comparison = Between(comparable, start, start.AddMonths(1));
                break;
            }

            case "thisQuarter":
            {
                var quarterStartMonth = ((now.Month - 1) / 3 * 3) + 1;
                var start = new DateTime(now.Year, quarterStartMonth, 1, 0, 0, 0, DateTimeKind.Utc);
                comparison = Between(comparable, start, start.AddMonths(3));
                break;
            }

            case "overdue":
                comparison = Expression.LessThan(comparable, Expression.Constant(now));
                break;

            case "between":
            {
                if (!TryParseDate(node.Value, out var from) || !TryParseDate(node.Value2, out var to))
                {
                    return null;
                }
                comparison = Between(comparable, from, to.AddDays(1));
                break;
            }

            default:
            {
                if (!TryParseDate(node.Value, out var bound)) return null;
                var constant = Expression.Constant(bound);

                comparison = op switch
                {
                    "onOrAfter" or "after" => Expression.GreaterThanOrEqual(comparable, constant),
                    "onOrBefore" or "before" => Expression.LessThanOrEqual(
                        comparable, Expression.Constant(bound.AddDays(1).AddTicks(-1))),
                    "on" => Between(comparable, bound.Date, bound.Date.AddDays(1)),
                    _ => null,
                };
                break;
            }
        }

        if (comparison is null) return null;
        return hasValue is null ? comparison : Expression.AndAlso(hasValue, comparison);
    }

    private static Expression Between(Expression member, DateTime fromInclusive, DateTime toExclusive) =>
        Expression.AndAlso(
            Expression.GreaterThanOrEqual(member, Expression.Constant(fromInclusive)),
            Expression.LessThan(member, Expression.Constant(toExclusive)));

    /* ---------------- boolean ---------------- */

    private static Expression? BooleanLeaf(Expression member, string op, FilterNode node)
    {
        var wanted = op switch
        {
            "isTrue" => true,
            "isFalse" => false,
            "equals" => string.Equals(node.Value, "true", StringComparison.OrdinalIgnoreCase),
            _ => (bool?)null,
        };

        if (wanted is null) return null;

        var comparable = Nullable.GetUnderlyingType(member.Type) is null
            ? member
            : Expression.Convert(member, typeof(bool));

        return Expression.Equal(comparable, Expression.Constant(wanted.Value));
    }

    /* ---------------- helpers ---------------- */

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

    private static bool TryConvert(string? raw, Type target, out Expression? constant)
    {
        constant = null;
        if (string.IsNullOrWhiteSpace(raw)) return false;

        try
        {
            var value = Convert.ChangeType(raw.Trim(), target, CultureInfo.InvariantCulture);
            constant = Expression.Constant(value, target);
            return true;
        }
        catch (Exception ex) when (ex is FormatException or InvalidCastException or OverflowException)
        {
            return false;
        }
    }

    private static bool TryParseDate(string? raw, out DateTime value) =>
        DateTime.TryParse(
            raw,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out value);

    private static IQueryable<T> OrderByPath<T>(
        IQueryable<T> source,
        string path,
        bool descending,
        bool first)
    {
        var parameter = Expression.Parameter(typeof(T), "e");
        var member = Resolve(parameter, path);
        var selector = Expression.Lambda(member, parameter);

        var method = (first, descending) switch
        {
            (true, false) => nameof(Queryable.OrderBy),
            (true, true) => nameof(Queryable.OrderByDescending),
            (false, false) => nameof(Queryable.ThenBy),
            _ => nameof(Queryable.ThenByDescending),
        };

        var call = Expression.Call(
            typeof(Queryable),
            method,
            [typeof(T), member.Type],
            source.Expression,
            Expression.Quote(selector));

        return source.Provider.CreateQuery<T>(call);
    }
}
