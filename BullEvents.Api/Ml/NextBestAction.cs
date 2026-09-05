using BullEvents.Api.Models;

namespace BullEvents.Api.Ml;

public record RecommendedAction(
    string Action,
    string Reason,
    string Urgency,
    string ActivityType
);

/// <summary>
/// A small, auditable rules engine that turns a lead's current state into the
/// next thing a rep should actually do. Deliberately rule-based rather than
/// learned: managers need to be able to read and change the policy.
/// </summary>
public static class NextBestAction
{
    public static List<RecommendedAction> For(Lead lead, LeadScoreResult score, DateTime now)
    {
        var actions = new List<RecommendedAction>();
        var activities = lead.Activities ?? new List<LeadActivity>();

        var lastTouch = activities.Count > 0
            ? activities.Max(a => a.CreatedAt)
            : lead.CreatedAt;
        var daysSinceTouch = (int)Math.Max(0, (now - lastTouch).TotalDays);

        if (lead.Stage is LeadStages.Lost or LeadStages.Booked or LeadStages.Nurture)
        {
            actions.Add(new RecommendedAction(
                lead.Stage == LeadStages.Lost ? "Review the loss reason" : "Archive the record",
                $"This lead is already {lead.Stage.ToLowerInvariant()} — no active follow-up is needed.",
                "Low",
                LeadActivityTypes.Note));
            return actions;
        }

        if (!lead.OwnerId.HasValue)
        {
            actions.Add(new RecommendedAction(
                "Assign an owner",
                "Unassigned leads have no one accountable for follow-up.",
                "High",
                LeadActivityTypes.Note));
        }

        if (activities.Count == 0)
        {
            actions.Add(new RecommendedAction(
                "Make the first call",
                "Nothing has been logged against this lead since it was captured.",
                score.Score >= 70 ? "High" : "Medium",
                LeadActivityTypes.Call));
        }
        else if (daysSinceTouch >= 7)
        {
            actions.Add(new RecommendedAction(
                "Follow up now",
                $"No contact in {daysSinceTouch} days — the lead is going cold.",
                daysSinceTouch >= 14 ? "High" : "Medium",
                LeadActivityTypes.Call));
        }

        if (lead.QuestionnaireStatus != QuestionnaireStatuses.Completed
            && lead.Stage is LeadStages.New or LeadStages.Contacted)
        {
            actions.Add(new RecommendedAction(
                "Send the qualification questionnaire",
                "Date, guests and budget should be on file before a consult hour is spent.",
                "High",
                LeadActivityTypes.Questionnaire));
        }

        var hasSiteVisit = activities.Any(a => a.Type == LeadActivityTypes.SiteVisit);
        if (lead.Stage == LeadStages.Contacted && !hasSiteVisit)
        {
            actions.Add(new RecommendedAction(
                "Book a consult or venue walkthrough",
                "Responded enquiries convert once a meeting is on the calendar.",
                "Medium",
                LeadActivityTypes.ObmVisit));
        }

        if (lead.Stage is LeadStages.ProposalSent or LeadStages.Negotiation)
        {
            actions.Add(new RecommendedAction(
                "Follow up on the proposal",
                "Outstanding proposals need a four-touch chase, not a silent wait.",
                "High",
                LeadActivityTypes.Email));
        }

        if (lead.Stage == LeadStages.ContractSent)
        {
            actions.Add(new RecommendedAction(
                "Chase the signed contract",
                "Contract sent is where wedding bookings stall most often.",
                "High",
                LeadActivityTypes.Email));
        }

        if (string.IsNullOrWhiteSpace(lead.Email))
        {
            actions.Add(new RecommendedAction(
                "Capture an email address",
                "Without an email you cannot send quotations or booking documents.",
                "Low",
                LeadActivityTypes.Note));
        }

        if (score.Score >= 75 && lead.Stage == LeadStages.New)
        {
            actions.Insert(0, new RecommendedAction(
                "Call today — high scoring lead",
                $"Scored {score.Score}/100 ({score.Band}) but still sitting in New.",
                "High",
                LeadActivityTypes.Call));
        }

        if (actions.Count == 0)
        {
            actions.Add(new RecommendedAction(
                "Keep working the lead",
                "Nothing is overdue — log the next interaction as it happens.",
                "Low",
                LeadActivityTypes.Note));
        }

        return actions;
    }
}
