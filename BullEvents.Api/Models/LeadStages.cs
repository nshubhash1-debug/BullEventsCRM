namespace BullEvents.Api.Models;

/// <summary>
/// The stages an event enquiry walks through, in order.
///
/// Modelled on how a planning desk actually books rather than on a generic
/// funnel:
///
/// <b>Qualified</b> is the gate that matters — an enquiry is worth a planner's
/// time only once the date, the guest count, the budget and which services are
/// wanted are known, because dates and crew are the scarce resources. A
/// wedding enquiry with no date is not a lead, it is a conversation.
///
/// <b>ObmVisit</b> then <b>SiteVisit</b>: the planner goes to the client first
/// — at their home or office, to take the brief — and the client tours the
/// venue after. Surfaced as <i>Client Meeting</i> and <i>Venue Visit</i>. A
/// lead may skip either; the path is clickable precisely because the real move
/// is often a jump.
///
/// <b>ProposalSent</b> then <b>ContractSent</b> line up with the paperwork.
/// Follow-up is an activity on every stage, not a column; leftover
/// <c>FollowUp</c> / <c>Negotiation</c> values are still accepted so old rows
/// load, and are remapped on migrate.
///
/// <b>Nurture</b> is terminal-ish: the date is too far out to work now, but
/// it is not a loss. Booked and Lost stay the hard terminals.
///
/// The two visit stages keep their stored names — <c>ObmVisit</c> and
/// <c>SiteVisit</c> — deliberately. Those strings are also permission keys,
/// activity types and table names across the CRM; renaming the label is a
/// change the product can take, renaming the key is a migration of half the
/// system for no gain. Read them as "client meeting" and "venue visit".
/// </summary>
public static class LeadStages
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";

    /// <summary>Surfaced as <i>Client Meeting</i> — the planner visits the client.</summary>
    public const string ObmVisit = "ObmVisit";

    /// <summary>Surfaced as <i>Venue Visit</i> — the client tours the venue.</summary>
    public const string SiteVisit = "SiteVisit";

    /// <summary>Kept so existing rows and in-flight clients still validate.</summary>
    public const string FollowUp = "FollowUp";

    /// <summary>Legacy name for proposal-out. Prefer <see cref="ProposalSent"/>.</summary>
    public const string Negotiation = "Negotiation";

    public const string ProposalSent = "ProposalSent";
    public const string ContractSent = "ContractSent";
    public const string Booked = "Booked";
    public const string Lost = "Lost";
    public const string Nurture = "Nurture";

    public static readonly string[] All =
    [
        New, Contacted, Qualified, ObmVisit, SiteVisit,
        ProposalSent, ContractSent, Booked, Lost, Nurture,
        FollowUp, Negotiation,
    ];

    /// <summary>Columns on the enquiry board. Terminals and the junk-drawer stage are out.</summary>
    public static readonly string[] Pipeline =
    [
        New, Contacted, Qualified, ObmVisit, SiteVisit, ProposalSent, ContractSent
    ];

    /// <summary>The forward-only ladder. Terminal stages are deliberately absent.</summary>
    public static readonly string[] Ladder =
    [
        New, Contacted, Qualified, ObmVisit, SiteVisit, ProposalSent, ContractSent
    ];

    /// <summary>Stages a client meeting may advance a lead out of — never backwards.</summary>
    public static readonly string[] BeforeObmVisit = [New, Contacted, Qualified];

    /// <summary>Stages a venue visit may advance a lead out of.</summary>
    public static readonly string[] BeforeSiteVisit = [New, Contacted, Qualified, ObmVisit];

    /// <summary>Reached an outcome — excluded from "open pipeline" everywhere.</summary>
    public static readonly string[] Terminal = [Booked, Lost, Nurture];

    /// <summary>The label to show. Only the two visit stages differ from their key.</summary>
    public static string Label(string stage) => stage switch
    {
        Contacted => "Responded",
        ObmVisit => "Consult booked",
        SiteVisit => "Venue walkthrough",
        FollowUp => "Follow-ups",
        Negotiation => "Proposal sent",
        ProposalSent => "Proposal sent",
        ContractSent => "Contract sent",
        Nurture => "Nurture",
        _ => stage,
    };
}

/// <summary>
/// Where an event enquiry came from.
///
/// <see cref="EventPortal"/> covers the listing sites a planner is discovered
/// on — WedMeGood, ShaadiSaga, VenueLook — and is kept apart from
/// <see cref="Website"/> because a portal lead arrives already shopping three
/// competitors and is worked differently.
/// </summary>
public static class LeadSources
{
    public const string Website = "Website";
    public const string Referral = "Referral";
    public const string WalkIn = "WalkIn";

    /// <summary>A wedding or events listing portal.</summary>
    public const string EventPortal = "EventPortal";

    public const string SocialAds = "SocialAds";

    /// <summary>Another vendor in the circuit — a venue, caterer or photographer passing work on.</summary>
    public const string VendorPartner = "VendorPartner";

    /// <summary>A wedding expo or trade show stall.</summary>
    public const string Exhibition = "Exhibition";

    /// <summary>A past client booking their next occasion. The cheapest lead there is.</summary>
    public const string RepeatClient = "RepeatClient";

    public const string Other = "Other";

    public static readonly string[] All =
    [
        Website, Referral, WalkIn, EventPortal, SocialAds, VendorPartner,
        Exhibition, RepeatClient, Other
    ];
}

public static class LeadPriorities
{
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";

    /// <summary>Above High — the handful a manager wants chased today.</summary>
    public const string Hot = "Hot";

    public static readonly string[] All = [Low, Medium, High, Hot];
}

/// <summary>
/// What can appear on a lead's timeline.
///
/// Split into what a person logs and what the system records: StageChange and
/// OwnerChange are written by the CRM as they happen and must never be
/// hand-posted, which is why <see cref="Loggable"/> exists separately and is
/// what the activity endpoint validates against.
/// </summary>
public static class LeadActivityTypes
{
    public const string Note = "Note";
    public const string Call = "Call";
    public const string Email = "Email";
    public const string WhatsApp = "WhatsApp";

    /// <summary>Surfaced as <i>Venue Visit</i>.</summary>
    public const string SiteVisit = "SiteVisit";

    /// <summary>Surfaced as <i>Client Meeting</i>.</summary>
    public const string ObmVisit = "ObmVisit";

    public const string Task = "Task";
    public const string Questionnaire = "Questionnaire";

    /* ---------------- written by the system ---------------- */

    public const string Created = "Created";
    public const string StageChange = "StageChange";
    public const string OwnerChange = "OwnerChange";

    /// <summary>The types a user may post by hand.</summary>
    public static readonly string[] Loggable =
    [
        Note, Call, Email, WhatsApp, SiteVisit, ObmVisit, Task, Questionnaire
    ];

    public static readonly string[] All =
    [
        Note, Call, Email, WhatsApp, SiteVisit, ObmVisit, Task, Questionnaire,
        Created, StageChange, OwnerChange
    ];
}
