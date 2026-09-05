using System.Reflection;
using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Decides who a new record belongs to.
///
/// Rules are tried in order and the first match wins. That is the convention
/// every CRM administrator has been trained on, and the reason the screen shows
/// the order rather than hiding it: a rule that never fires because a broader
/// one sits above it is the single most common way lead routing goes wrong, and
/// it is only diagnosable if the order is visible.
///
/// A record that matches nothing is left unowned rather than being handed to
/// somebody arbitrary. An unassigned lead is visible to the whole desk and gets
/// picked up; a lead quietly assigned to the wrong person does not.
/// </summary>
public class AssignmentRuleEngine(AppDbContext db, ILogger<AssignmentRuleEngine> logger)
{
    /// <summary>
    /// The user a record should go to, or null when no rule matched.
    ///
    /// Takes the record itself so criteria can be tested against its fields —
    /// "leads from Facebook go to the tele desk" needs the source, not just the
    /// type.
    /// </summary>
    public async Task<int?> ResolveOwnerAsync<T>(
        string objectName, T record, CancellationToken ct = default)
        where T : class
    {
        var rules = await db.AssignmentRules
            .Where(r => r.IsActive && r.Object == objectName)
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
            .ToListAsync(ct);

        if (rules.Count == 0) return null;

        foreach (var rule in rules)
        {
            if (!Matches(rule, record)) continue;

            var owner = await PickAsync(rule, ct);
            if (owner is null) continue;

            logger.LogInformation(
                "Assignment rule {Rule} routed a {Object} to user {User}.",
                rule.Name, objectName, owner);

            return owner;
        }

        return null;
    }

    /// <summary>
    /// Whether a rule's criterion holds for a record.
    ///
    /// A rule with no criterion is the catch-all and matches everything, which
    /// is why it belongs last in the order.
    /// </summary>
    private static bool Matches<T>(AssignmentRule rule, T record) where T : class
    {
        if (string.IsNullOrWhiteSpace(rule.CriteriaField)) return true;
        if (string.IsNullOrWhiteSpace(rule.CriteriaValue)) return true;

        var property = typeof(T).GetProperty(
            rule.CriteriaField,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        if (property is null) return false;

        var actual = property.GetValue(record)?.ToString();
        if (actual is null) return false;

        return (rule.CriteriaOperator ?? "equals").ToLowerInvariant() switch
        {
            "contains" => actual.Contains(rule.CriteriaValue, StringComparison.OrdinalIgnoreCase),
            "notequals" => !string.Equals(actual, rule.CriteriaValue, StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(actual, rule.CriteriaValue, StringComparison.OrdinalIgnoreCase),
        };
    }

    /// <summary>Runs the rule's strategy over its pool.</summary>
    private async Task<int?> PickAsync(AssignmentRule rule, CancellationToken ct)
    {
        if (rule.Strategy == AssignmentStrategies.Fixed)
        {
            // Confirmed active: a rule pointing at somebody who has left would
            // otherwise route every matching lead into a dead account.
            return await db.Users
                .Where(u => u.Id == rule.FixedUserId && u.IsActive)
                .Select(u => (int?)u.Id)
                .FirstOrDefaultAsync(ct);
        }

        var pool = await PoolAsync(rule, ct);
        if (pool.Count == 0) return null;

        if (rule.Strategy == AssignmentStrategies.LeastLoaded)
        {
            // Open leads only. Counting closed ones would make somebody who has
            // worked here for years permanently "loaded" and starve them of new
            // business, which is the opposite of what the strategy is for.
            var load = await db.Leads
                .Where(l => l.OwnerId != null
                    && pool.Contains(l.OwnerId.Value)
                    && l.Stage != LeadStages.Booked
                    && l.Stage != LeadStages.Lost)
                .GroupBy(l => l.OwnerId!.Value)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.UserId, x => x.Count, ct);

            return pool.OrderBy(id => load.GetValueOrDefault(id)).ThenBy(id => id).First();
        }

        // Round robin. The cursor is stored on the rule so the next record goes
        // to the next person even across restarts — an in-memory counter would
        // hand every lead to the same person after every deploy.
        var index = rule.RoundRobinCursor % pool.Count;
        rule.RoundRobinCursor = (rule.RoundRobinCursor + 1) % pool.Count;

        await db.SaveChangesAsync(ct);

        return pool[index];
    }

    /// <summary>The candidates a rule picks from, ordered so the rotation is stable.</summary>
    private async Task<List<int>> PoolAsync(AssignmentRule rule, CancellationToken ct)
    {
        var ids = new List<int>();

        if (!string.IsNullOrWhiteSpace(rule.PoolUserIds))
        {
            ids.AddRange(rule.PoolUserIds
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(x => int.TryParse(x, out var id) ? id : 0)
                .Where(id => id > 0));
        }

        var query = db.Users.Where(u => u.IsActive);

        query = !string.IsNullOrWhiteSpace(rule.PoolRoleKey)
            ? query.Where(u => u.Role == rule.PoolRoleKey || ids.Contains(u.Id))
            : query.Where(u => ids.Contains(u.Id));

        return await query.OrderBy(u => u.Id).Select(u => u.Id).ToListAsync(ct);
    }
}
