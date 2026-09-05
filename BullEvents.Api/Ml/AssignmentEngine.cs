using BullEvents.Api.Models;

namespace BullEvents.Api.Ml;

/// <summary>An agent's current workload, used to weigh routing decisions.</summary>
public record AgentLoad(
    int UserId,
    string Name,
    string Role,
    IReadOnlyList<int> BranchIds,
    int OpenLeads,
    int OverdueFollowUps,
    int ClosedWonLast90Days,
    int TouchedLast90Days,
    string? City
);

public record AssignmentSuggestion(
    int UserId,
    string Name,
    double Fit,
    string Reason,
    IReadOnlyList<ScoreSignal> Factors
);

/// <summary>
/// Picks who should own a lead.
///
/// Deliberately a scored policy rather than a learned model: routing decides
/// people's commission, so a manager has to be able to read the rule, argue
/// with it and change it. Every input is surfaced as a named factor with its
/// contribution, and the same policy explains itself for any candidate.
/// </summary>
public static class AssignmentEngine
{
    /// <summary>Above this, an agent is treated as full and heavily penalised.</summary>
    private const int CapacityCeiling = 40;

    public static IReadOnlyList<AssignmentSuggestion> Suggest(
        Lead lead,
        IReadOnlyList<AgentLoad> candidates,
        int take = 3)
    {
        var eligible = candidates
            .Where(a => a.Role is Roles.SalesAgent or Roles.TeamLead or Roles.BranchManager)
            .Where(a => a.BranchIds.Count == 0 || a.BranchIds.Contains(lead.BranchId))
            .ToList();

        if (eligible.Count == 0)
        {
            eligible = candidates.Where(a => a.Role == Roles.SalesAgent).ToList();
        }

        return eligible
            .Select(agent => Evaluate(lead, agent))
            .OrderByDescending(s => s.Fit)
            .Take(take)
            .ToList();
    }

    private static AssignmentSuggestion Evaluate(Lead lead, AgentLoad agent)
    {
        var factors = new List<ScoreSignal>();
        var score = 50.0;

        // ---- capacity: the dominant term, so work spreads out ----
        var capacityPenalty = -Math.Min(35, agent.OpenLeads * 35.0 / CapacityCeiling);
        score += capacityPenalty;
        factors.Add(new ScoreSignal(
            "Open workload",
            $"{agent.OpenLeads} open lead(s)",
            (int)Math.Round(capacityPenalty)));

        // ---- reliability: overdue follow-ups mean the queue isn't being worked ----
        var overduePenalty = -Math.Min(20, agent.OverdueFollowUps * 4.0);
        score += overduePenalty;
        factors.Add(new ScoreSignal(
            "Overdue follow-ups",
            agent.OverdueFollowUps == 0
                ? "Nothing overdue"
                : $"{agent.OverdueFollowUps} overdue follow-up(s)",
            (int)Math.Round(overduePenalty)));

        // ---- proven conversion ----
        var conversionRate = agent.TouchedLast90Days == 0
            ? 0
            : agent.ClosedWonLast90Days / (double)agent.TouchedLast90Days;
        var performanceBonus = Math.Min(25, conversionRate * 100);
        score += performanceBonus;
        factors.Add(new ScoreSignal(
            "Recent conversion",
            agent.TouchedLast90Days == 0
                ? "No closed leads in the last 90 days"
                : $"{agent.ClosedWonLast90Days} of {agent.TouchedLast90Days} closed won ({conversionRate:P0})",
            (int)Math.Round(performanceBonus)));

        // ---- local knowledge ----
        var cityMatch = !string.IsNullOrWhiteSpace(lead.City)
            && string.Equals(lead.City, agent.City, StringComparison.OrdinalIgnoreCase);
        if (cityMatch)
        {
            score += 10;
            factors.Add(new ScoreSignal("Territory", $"Based in {agent.City}", 10));
        }

        // ---- seniority is reserved for the leads that justify it ----
        var isHighValue = lead.Priority is LeadPriorities.Hot or LeadPriorities.High
            || (lead.BudgetMax ?? 0) >= 15_000_000m;

        if (isHighValue && agent.Role is Roles.TeamLead or Roles.BranchManager)
        {
            score += 12;
            factors.Add(new ScoreSignal("Seniority", $"{agent.Role} on a high-value lead", 12));
        }
        else if (!isHighValue && agent.Role is Roles.BranchManager)
        {
            score -= 8;
            factors.Add(new ScoreSignal("Seniority", "Manager time reserved for larger leads", -8));
        }

        var fit = Math.Clamp(Math.Round(score, 1), 0, 100);

        return new AssignmentSuggestion(
            agent.UserId,
            agent.Name,
            fit,
            BuildReason(agent, capacityPenalty, conversionRate, cityMatch),
            factors);
    }

    private static string BuildReason(
        AgentLoad agent,
        double capacityPenalty,
        double conversionRate,
        bool cityMatch)
    {
        var parts = new List<string>();

        parts.Add(capacityPenalty > -12
            ? $"has capacity ({agent.OpenLeads} open)"
            : $"carrying {agent.OpenLeads} open leads");

        if (conversionRate > 0.2) parts.Add($"converting at {conversionRate:P0}");
        if (cityMatch) parts.Add($"covers {agent.City}");
        if (agent.OverdueFollowUps > 0) parts.Add($"{agent.OverdueFollowUps} overdue");

        return char.ToUpperInvariant(agent.Name[0]) + agent.Name[1..] + " " + string.Join(", ", parts) + ".";
    }

    /// <summary>
    /// Straight round-robin over the eligible pool, for teams that want an
    /// even split with no scoring involved.
    /// </summary>
    public static AgentLoad? RoundRobin(
        IReadOnlyList<AgentLoad> candidates,
        int branchId,
        int rotationIndex)
    {
        var pool = candidates
            .Where(a => a.Role == Roles.SalesAgent)
            .Where(a => a.BranchIds.Count == 0 || a.BranchIds.Contains(branchId))
            .OrderBy(a => a.UserId)
            .ToList();

        return pool.Count == 0 ? null : pool[Math.Abs(rotationIndex) % pool.Count];
    }
}
