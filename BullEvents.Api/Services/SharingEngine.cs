using System.Linq.Expressions;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Turns the sharing rules an administrator wrote into the extra records this
/// request may see.
///
/// Sharing only ever widens. <see cref="AccessScope"/> decides the baseline
/// from the role — own records, the reporting line, or everything — and this
/// adds to it. Nothing here can take a record away, which is what makes the
/// access model predictable enough to audit: the answer to "why can they see
/// this" is always a rule somebody wrote, never the order two rules happened to
/// run in.
///
/// Two flavours, and they resolve differently on purpose:
///
///   * <b>Owner-based</b> — "everything owned by Tele Sales goes to the Tele
///     Sales AGM". This is expressible as a set of owner ids, so it folds
///     straight into the owner filter the scope already applies and costs
///     nothing extra at query time.
///
///   * <b>Criteria-based</b> — "every lead in Varanasi goes to the Varanasi
///     branch". This cannot be an owner id, so it becomes an OR predicate on
///     the record itself.
///
/// Resolved once per request and cached, like the scope it extends.
/// </summary>
public class SharingEngine(AppDbContext db, TenantContext tenant)
{
    private List<SharingRule>? _mine;

    /// <summary>
    /// The active rules that name this user as a beneficiary.
    ///
    /// Filtered here rather than in the query so the "does this rule apply to
    /// me" question is answered once, against the reporting line and branch
    /// list, instead of being pushed into SQL for every object on every screen.
    /// </summary>
    public async Task<IReadOnlyList<SharingRule>> RulesForMeAsync(CancellationToken ct = default)
    {
        if (_mine is not null) return _mine;

        var rules = await db.SharingRules
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        if (rules.Count == 0) return _mine = [];

        var me = tenant.UserId;

        var myBranches = await db.UserBranches
            .Where(ub => ub.UserId == me)
            .Select(ub => ub.BranchId)
            .ToListAsync(ct);

        // Role-and-subordinates targets need the line *upwards*: a rule aimed at
        // the AGM reaches everyone reporting into them, so the question for this
        // user is "is any of my managers a holder of that role".
        var managerRoles = await ManagerRoleKeysAsync(me, ct);
        var myRole = tenant.Role;

        _mine = rules.Where(rule => rule.Target switch
        {
            ShareTarget.User => rule.TargetUserId == me,

            ShareTarget.Branch =>
                rule.TargetBranchId is int branch && myBranches.Contains(branch),

            ShareTarget.Role =>
                string.Equals(rule.TargetRoleKey, myRole, StringComparison.OrdinalIgnoreCase),

            ShareTarget.RoleAndSubordinates =>
                string.Equals(rule.TargetRoleKey, myRole, StringComparison.OrdinalIgnoreCase)
                || managerRoles.Contains(rule.TargetRoleKey ?? "", StringComparer.OrdinalIgnoreCase),

            _ => false,
        }).ToList();

        return _mine;
    }

    /// <summary>
    /// Extra owner ids this user may see for one object, from the owner-based
    /// rules that name them.
    /// </summary>
    public async Task<IReadOnlyCollection<int>> ExtraOwnerIdsAsync(
        string objectName, CancellationToken ct = default)
    {
        var rules = await RulesForMeAsync(ct);

        var roleKeys = rules
            .Where(r => string.Equals(r.Object, objectName, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.OwnerRoleKey)
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(k => k!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (roleKeys.Count == 0) return [];

        return await db.Users
            .Where(u => u.IsActive && roleKeys.Contains(u.Role))
            .Select(u => u.Id)
            .ToListAsync(ct);
    }

    /// <summary>
    /// The criteria-based rules for one object, as field/operator/value triples
    /// the caller can turn into a predicate.
    /// </summary>
    public async Task<IReadOnlyList<SharingRule>> CriteriaRulesAsync(
        string objectName, CancellationToken ct = default)
    {
        var rules = await RulesForMeAsync(ct);

        return rules
            .Where(r => string.Equals(r.Object, objectName, StringComparison.OrdinalIgnoreCase)
                && !string.IsNullOrWhiteSpace(r.CriteriaField)
                && !string.IsNullOrWhiteSpace(r.CriteriaValue))
            .ToList();
    }

    /// <summary>
    /// Builds "this record matches any criteria rule" for one entity type.
    ///
    /// Compared as text through <c>EF.Property</c> rather than against a typed
    /// column, because the field is a name an administrator typed and the set of
    /// shareable fields is open. Anything that does not resolve to a real column
    /// is skipped rather than throwing — a rule naming a field that was renamed
    /// should stop widening access, not take every list view down with it.
    /// </summary>
    public Expression<Func<T, bool>>? CriteriaPredicate<T>(IReadOnlyList<SharingRule> rules)
        where T : class
    {
        if (rules.Count == 0) return null;

        var properties = typeof(T)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var parameter = Expression.Parameter(typeof(T), "x");
        Expression? combined = null;

        foreach (var rule in rules)
        {
            var field = rule.CriteriaField!;
            if (!properties.Contains(field)) continue;

            var member = typeof(T).GetProperty(
                field,
                System.Reflection.BindingFlags.IgnoreCase
                    | System.Reflection.BindingFlags.Public
                    | System.Reflection.BindingFlags.Instance);

            if (member is null) continue;

            Expression left = Expression.Property(parameter, member);

            // Everything is compared as text: the operators an administrator gets
            // are equals and contains, and both read the same on a number, a
            // status string or a city.
            if (member.PropertyType != typeof(string))
            {
                left = Expression.Call(
                    Expression.Convert(left, typeof(object)),
                    typeof(object).GetMethod(nameof(object.ToString))!);
            }

            var value = Expression.Constant(rule.CriteriaValue, typeof(string));

            Expression test = (rule.CriteriaOperator ?? "equals").ToLowerInvariant() switch
            {
                "contains" => Expression.AndAlso(
                    Expression.NotEqual(left, Expression.Constant(null, typeof(string))),
                    Expression.Call(left, StringContains, value)),

                _ => Expression.Equal(left, value),
            };

            combined = combined is null ? test : Expression.OrElse(combined, test);
        }

        return combined is null
            ? null
            : Expression.Lambda<Func<T, bool>>(combined, parameter);
    }

    private static readonly System.Reflection.MethodInfo StringContains =
        typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

    /// <summary>Role keys held by everyone this user reports into, up the line.</summary>
    private async Task<HashSet<string>> ManagerRoleKeysAsync(int userId, CancellationToken ct)
    {
        var edges = await db.Users
            .Select(u => new { u.Id, u.ManagerId, u.Role })
            .ToListAsync(ct);

        var byId = edges.ToDictionary(e => e.Id);
        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var cursor = byId.GetValueOrDefault(userId)?.ManagerId;
        var guard = 0;

        // The guard is what stops a reporting line somebody made circular from
        // walking forever.
        while (cursor is int id && guard++ < 50 && byId.TryGetValue(id, out var manager))
        {
            roles.Add(manager.Role);
            cursor = manager.ManagerId;
        }

        return roles;
    }
}
