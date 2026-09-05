using System.Reflection;
using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Finds records that have sat past their target and acts on them.
///
/// The clock runs in working hours, so a four-hour target does not expire over
/// a weekend. Each breach is recorded before the action is taken, and the
/// event table carries a unique index on rule-plus-record — so a rule fires
/// once per record and not again on every sweep. An escalation that repeats is
/// an escalation everyone mutes, and a muted alert is worse than no alert
/// because it looks like coverage.
/// </summary>
public class EscalationSweep(AppDbContext db, ILogger<EscalationSweep> logger)
{
    public record Result(int Checked, int Breached, int Acted, IReadOnlyList<string> Notes);

    public async Task<Result> RunAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var notes = new List<string>();

        var rules = await db.EscalationRules
            .IgnoreQueryFilters()
            .Include(r => r.BusinessHours!).ThenInclude(b => b.Holidays)
            .Where(r => r.IsActive)
            .ToListAsync(ct);

        if (rules.Count == 0) return new Result(0, 0, 0, ["No active escalation rule."]);

        var fallback = await db.BusinessHours
            .IgnoreQueryFilters()
            .Include(b => b.Holidays)
            .FirstOrDefaultAsync(b => b.IsDefault && b.IsActive, ct);

        var checkedCount = 0;
        var breached = 0;
        var acted = 0;

        foreach (var rule in rules)
        {
            var hours = rule.BusinessHours ?? fallback;

            if (hours is null)
            {
                notes.Add($"{rule.Name}: no business hours set, skipped.");
                continue;
            }

            // Leads are the only object with a clock today. The shape below is
            // deliberately object-agnostic so the next one is a query and a
            // switch arm, not a second sweep.
            if (rule.Object != SecuredObjects.Lead)
            {
                notes.Add($"{rule.Name}: {rule.Object} is not swept yet.");
                continue;
            }

            // Only records that arrived after the rule did.
            //
            // Without this, switching on a rule fires it against the entire back
            // catalogue: the first sweep on this database raised fourteen
            // thousand tasks in one pass, which is not an alert — it is a denial
            // of service against the follow-up list, and the desk would simply
            // stop opening it. A rule describes what should happen from now on.
            var candidates = await db.Leads
                .IgnoreQueryFilters()
                .Include(l => l.Owner)
                .Where(l => l.CompanyId == rule.CompanyId
                    && l.CreatedAt >= rule.CreatedAt
                    && l.Stage != LeadStages.Booked
                    && l.Stage != LeadStages.Lost)
                .Take(5000)
                .ToListAsync(ct);

            // Everything this rule has already fired on, fetched once rather
            // than probed per record.
            var already = await db.EscalationEvents
                .IgnoreQueryFilters()
                .Where(e => e.EscalationRuleId == rule.Id)
                .Select(e => e.RecordId)
                .ToHashSetAsync(ct);

            foreach (var lead in candidates)
            {
                if (already.Contains(lead.Id)) continue;
                if (!Matches(rule, lead)) continue;

                checkedCount++;

                var startedAt = rule.StartsFrom switch
                {
                    "LastActivity" => lead.LastActivityAt ?? lead.CreatedAt,
                    _ => lead.CreatedAt,
                };

                var elapsed = WorkingHours.MinutesBetween(hours, startedAt, now);
                if (elapsed < rule.TargetMinutes) continue;

                breached++;

                // Written first. If the action throws, the breach is still on
                // record and the next sweep does not alert a second time.
                db.EscalationEvents.Add(new EscalationEvent
                {
                    CompanyId = rule.CompanyId,
                    EscalationRuleId = rule.Id,
                    Object = rule.Object,
                    RecordId = lead.Id,
                    BreachedAt = now,
                    Action = rule.Action,
                });

                try
                {
                    await ActAsync(rule, lead, elapsed, ct);
                    acted++;
                }
                catch (Exception error)
                {
                    logger.LogError(error,
                        "Escalation {Rule} breached on lead {Lead} but the action failed.",
                        rule.Name, lead.Id);
                }
            }

            await db.SaveChangesAsync(ct);
        }

        if (breached > 0) notes.Add($"{breached} records past target, {acted} acted on.");

        return new Result(checkedCount, breached, acted, notes);
    }

    /// <summary>
    /// Carries out the rule.
    ///
    /// Notifying somebody means raising a follow-up task against the record and
    /// assigning it to them. There is no separate notification inbox in this
    /// product, and inventing one would put escalations somewhere nobody looks —
    /// whereas the follow-up list is a screen the desk already works from every
    /// morning.
    /// </summary>
    private async Task ActAsync(EscalationRule rule, Lead lead, int elapsed, CancellationToken ct)
    {
        var late = elapsed - rule.TargetMinutes;

        switch (rule.Action)
        {
            case EscalationActions.RaisePriority:
                lead.Priority = LeadPriorities.Hot;
                return;

            case EscalationActions.Reassign when rule.ReassignToUserId is int to:
                lead.OwnerId = to;
                lead.UpdatedAt = DateTime.UtcNow;
                return;

            case EscalationActions.NotifyManager:
                {
                    var managerId = await db.Users
                        .IgnoreQueryFilters()
                        .Where(u => u.Id == lead.OwnerId)
                        .Select(u => u.ManagerId)
                        .FirstOrDefaultAsync(ct);

                    if (managerId is int manager) Raise(rule, lead, manager, late);
                    return;
                }

            default:
                if (lead.OwnerId is int owner) Raise(rule, lead, owner, late);
                return;
        }
    }

    /// <summary>Raises the follow-up that carries the escalation to a person.</summary>
    private void Raise(EscalationRule rule, Lead lead, int ownerId, int late)
    {
        db.FollowUps.Add(new FollowUp
        {
            CompanyId = lead.CompanyId,
            BranchId = lead.BranchId,
            Subject = $"Escalated: {lead.Name}",
            Description =
                $"{lead.Name} passed the '{rule.Name}' target by {Humanise(late)} of working time.",
            RelatedType = RelatedTypes.Lead,
            RelatedId = lead.Id,
            RelatedName = lead.Name,
            Channel = FollowUpChannels.Call,
            Status = FollowUpStatuses.Open,

            // Escalations arrive already late, so they open at the top of the
            // list rather than inheriting the lead's own priority.
            Priority = LeadPriorities.Hot,
            DueAt = DateTime.UtcNow,
            OwnerId = ownerId,
        });
    }

    private static string Humanise(int minutes) =>
        minutes < 60 ? $"{minutes} minutes" : $"{minutes / 60} hours";

    private static bool Matches(EscalationRule rule, Lead lead)
    {
        if (string.IsNullOrWhiteSpace(rule.CriteriaField)) return true;
        if (string.IsNullOrWhiteSpace(rule.CriteriaValue)) return true;

        var property = typeof(Lead).GetProperty(
            rule.CriteriaField,
            BindingFlags.IgnoreCase | BindingFlags.Public | BindingFlags.Instance);

        var actual = property?.GetValue(lead)?.ToString();
        if (actual is null) return false;

        return (rule.CriteriaOperator ?? "equals").ToLowerInvariant() switch
        {
            "contains" => actual.Contains(rule.CriteriaValue, StringComparison.OrdinalIgnoreCase),
            "notequals" => !string.Equals(actual, rule.CriteriaValue, StringComparison.OrdinalIgnoreCase),
            _ => string.Equals(actual, rule.CriteriaValue, StringComparison.OrdinalIgnoreCase),
        };
    }
}
