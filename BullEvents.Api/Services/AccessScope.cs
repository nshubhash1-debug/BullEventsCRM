using System.Linq.Expressions;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Answers "whose records may this request see".
///
/// Tenant isolation already stops one company reading another's data — that is
/// enforced by the DbContext's global query filters and cannot be forgotten.
/// This sits one level in from that and answers the sharper question inside a
/// single company: a Sales Executive sees their own pipeline, an AGM sees their
/// whole reporting line, Back Office sees all of it.
///
/// Resolved once per request and cached, because the reporting line is a
/// recursive walk and every list view on a screen would otherwise repeat it.
/// </summary>
public class AccessScope(AppDbContext db, TenantContext tenant, SharingEngine sharing)
{
    private IReadOnlyCollection<int>? _visibleOwnerIds;

    /// <summary>
    /// Which secured object an entity type is, so a sharing rule written against
    /// "Lead" can be matched to a query over <c>Lead</c>.
    ///
    /// Kept as an explicit list rather than derived from the class name: the two
    /// happen to agree today, and the day one is renamed the access model must
    /// not quietly stop applying.
    /// </summary>
    private static string? ObjectNameFor(Type type) => type.Name switch
    {
        nameof(Lead) => SecuredObjects.Lead,
        nameof(Contact) => SecuredObjects.Contact,
        nameof(Opportunity) => SecuredObjects.Opportunity,
        nameof(Quotation) => SecuredObjects.Quotation,
        nameof(FollowUp) => SecuredObjects.FollowUp,
        nameof(Booking) => SecuredObjects.Booking,
        nameof(HrEmployee) => SecuredObjects.Employee,
        _ => null,
    };

    public RoleDefinition Role => RoleCatalog.For(tenant.Role);

    public DataScope Scope => Role.Scope;

    /// <summary>True when no owner filter applies — the caller sees the whole company.</summary>
    public bool SeesEverything => Scope is DataScope.Company or DataScope.Platform;

    /// <summary>
    /// The user ids whose records this request may read.
    ///
    /// Null when the scope is company-wide or platform-wide, which callers read
    /// as "do not filter at all" — distinct from an empty set, which means this
    /// user owns nothing and should see nothing.
    /// </summary>
    public async Task<IReadOnlyCollection<int>?> VisibleOwnerIdsAsync(CancellationToken ct = default)
    {
        if (SeesEverything) return null;
        if (_visibleOwnerIds is not null) return _visibleOwnerIds;

        var me = tenant.UserId;

        if (Scope == DataScope.Own)
        {
            return _visibleOwnerIds = [me];
        }

        // Team scope: this user plus everyone under them. Walked breadth-first
        // over the whole company's reporting edges in one query rather than a
        // query per level — the alternative is a round trip for every rung of
        // the ladder, and a deep org would spend more time waiting than reading.
        var edges = await db.Users
            .Where(u => u.ManagerId != null)
            .Select(u => new { u.Id, ManagerId = u.ManagerId!.Value })
            .ToListAsync(ct);

        var childrenOf = edges
            .GroupBy(e => e.ManagerId)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList());

        var team = new HashSet<int> { me };
        var queue = new Queue<int>();
        queue.Enqueue(me);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!childrenOf.TryGetValue(current, out var children)) continue;

            foreach (var child in children)
            {
                // The Add guard is what stops a cycle — someone made their own
                // manager's manager — turning this into an infinite walk.
                if (team.Add(child)) queue.Enqueue(child);
            }
        }

        return _visibleOwnerIds = team;
    }

    /// <summary>
    /// Narrows a query to the records this request may see.
    ///
    /// Applied in the shared list path, so every grid in the product inherits it
    /// without each controller remembering to ask.
    /// </summary>
    /// <remarks>
    /// Constrained to <c>class</c> rather than to <see cref="IOwnedRecord"/> so
    /// the caller's concrete entity type survives into this method. It matters:
    /// the shared list path used to cast to <c>IQueryable&lt;IOwnedRecord&gt;</c>
    /// first, and a criteria rule reflecting over an interface would have found
    /// only <c>OwnerId</c> — every rule naming a real field would have silently
    /// matched nothing. An entity that is not owned at all is returned untouched.
    /// </remarks>
    public async Task<IQueryable<T>> ApplyAsync<T>(
        IQueryable<T> source, CancellationToken ct = default)
        where T : class
    {
        if (!typeof(IOwnedRecord).IsAssignableFrom(typeof(T))) return source;

        var owners = await VisibleOwnerIdsAsync(ct);

        // Company-wide already sees everything, so there is nothing a sharing
        // rule could add. Checked before the rules are loaded rather than after,
        // because an administrator opening a list should not pay for a lookup
        // that cannot change the answer.
        if (owners is null) return source;

        var ids = owners.ToList();
        var objectName = ObjectNameFor(typeof(T));

        Expression<Func<T, bool>>? criteria = null;

        if (objectName is not null)
        {
            // Owner-based sharing folds into the same owner filter: the rule
            // resolves to a set of people whose records open up, which is the
            // shape this query already has.
            var extra = await sharing.ExtraOwnerIdsAsync(objectName, ct);
            if (extra.Count > 0) ids = ids.Union(extra).ToList();

            // Criteria-based sharing cannot be an owner id, so it rides alongside
            // as an OR: seen because you own it, or because a rule says this
            // record is yours to see.
            var rules = await sharing.CriteriaRulesAsync(objectName, ct);
            criteria = sharing.CriteriaPredicate<T>(rules);
        }

        var owned = BuildOwnerPredicate<T>(ids);

        return source.Where(criteria is null ? owned : Or(owned, criteria));
    }

    /// <summary>
    /// "Unowned, or owned by somebody I can see", built against the entity's own
    /// <c>OwnerId</c> property.
    ///
    /// An unowned record is visible to anyone who can see the object at all. A
    /// lead sitting in the unassigned queue is exactly what a rep is meant to be
    /// able to pick up, and hiding it would leave the queue looking empty to the
    /// only people who work it.
    /// </summary>
    private static Expression<Func<T, bool>> BuildOwnerPredicate<T>(List<int> ids)
        where T : class
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var owner = Expression.Property(parameter, nameof(IOwnedRecord.OwnerId));

        var isNull = Expression.Equal(owner, Expression.Constant(null, typeof(int?)));

        var contains = Expression.Call(
            typeof(Enumerable),
            nameof(Enumerable.Contains),
            [typeof(int)],
            Expression.Constant(ids),
            Expression.Property(owner, "Value"));

        return Expression.Lambda<Func<T, bool>>(
            Expression.OrElse(isNull, contains), parameter);
    }

    /// <summary>
    /// Ors two predicates over the same parameter.
    ///
    /// The rebind is the point: two lambdas built separately have different
    /// parameter instances, and combining them without this produces a tree EF
    /// cannot translate.
    /// </summary>
    private static Expression<Func<T, bool>> Or<T>(
        Expression<Func<T, bool>> left, Expression<Func<T, bool>> right)
    {
        var parameter = Expression.Parameter(typeof(T), "x");

        var body = Expression.OrElse(
            new Rebind(left.Parameters[0], parameter).Visit(left.Body)!,
            new Rebind(right.Parameters[0], parameter).Visit(right.Body)!);

        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    private sealed class Rebind(ParameterExpression from, ParameterExpression to)
        : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
            => node == from ? to : base.VisitParameter(node);
    }

    /// <summary>Whether this seat may write at all. Reporting roles may not.</summary>
    public bool CanWrite => !Role.ReadOnly;
}
