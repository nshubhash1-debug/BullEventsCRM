namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Domain vocabularies.
 *
 * These are plain string constants rather than CLR enums: the values are
 * persisted verbatim, surfaced in the API, and rendered in the UI, so a
 * string keeps the wire format stable and readable while the `All` arrays
 * still give controllers a single place to validate against.
 * ------------------------------------------------------------------ */

public static class ContactTypes
{
    public const string Contact = "Contact";

    /// <summary>Someone who has booked. Surfaced as <i>Client</i>.</summary>
    public const string Customer = "Customer";

    /// <summary>A caterer, decorator, photographer — anyone on the supply side.</summary>
    public const string Vendor = "Vendor";

    /// <summary>The venue's owner or manager.</summary>
    public const string VenueOwner = "VenueOwner";

    /// <summary>A company that puts money behind an event in exchange for billing.</summary>
    public const string Sponsor = "Sponsor";

    /// <summary>A company that books events rather than a private family.</summary>
    public const string Corporate = "Corporate";

    public static readonly string[] All =
    [
        Contact, Customer, Vendor, VenueOwner, Sponsor, Corporate
    ];
}

public static class LifecycleStages
{
    public const string Prospect = "Prospect";
    public const string Engaged = "Engaged";
    public const string Customer = "Customer";
    public const string Repeat = "Repeat";
    public const string Dormant = "Dormant";
    public const string Churned = "Churned";

    public static readonly string[] All = [Prospect, Engaged, Customer, Repeat, Dormant, Churned];
}

public static class OpportunityStages
{
    public const string Qualification = "Qualification";
    public const string NeedsAnalysis = "NeedsAnalysis";
    public const string Proposal = "Proposal";
    public const string Negotiation = "Negotiation";
    public const string ClosedWon = "ClosedWon";
    public const string ClosedLost = "ClosedLost";

    public static readonly string[] All =
    [
        Qualification, NeedsAnalysis, Proposal, Negotiation, ClosedWon, ClosedLost
    ];

    public static readonly string[] Open =
    [
        Qualification, NeedsAnalysis, Proposal, Negotiation
    ];

    /// <summary>Default win probability per stage, used when a rep has not overridden it.</summary>
    public static int DefaultProbability(string stage) => stage switch
    {
        Qualification => 10,
        NeedsAnalysis => 25,
        Proposal => 50,
        Negotiation => 75,
        ClosedWon => 100,
        _ => 0
    };

    public static bool IsClosed(string stage) => stage is ClosedWon or ClosedLost;
}

public static class ForecastCategories
{
    public const string Pipeline = "Pipeline";
    public const string BestCase = "BestCase";
    public const string Commit = "Commit";
    public const string Closed = "Closed";
    public const string Omitted = "Omitted";

    public static readonly string[] All = [Pipeline, BestCase, Commit, Closed, Omitted];

    public static string ForStage(string stage, int probability) => stage switch
    {
        OpportunityStages.ClosedWon or OpportunityStages.ClosedLost => Closed,
        OpportunityStages.Negotiation => probability >= 70 ? Commit : BestCase,
        OpportunityStages.Proposal => BestCase,
        _ => Pipeline
    };
}

public static class OpportunityTypes
{
    public const string NewBusiness = "NewBusiness";

    /// <summary>A past client's next occasion.</summary>
    public const string RepeatClient = "RepeatClient";

    /// <summary>Won on the back of an existing client's word.</summary>
    public const string Referral = "Referral";

    /// <summary>Extra functions or services added to a booking already won.</summary>
    public const string Upsell = "Upsell";

    /// <summary>A company account rather than a family.</summary>
    public const string Corporate = "Corporate";

    public static readonly string[] All =
    [
        NewBusiness, RepeatClient, Referral, Upsell, Corporate
    ];
}

public static class FollowUpStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Open, InProgress, Completed, Cancelled];
}

public static class FollowUpChannels
{
    public const string Call = "Call";
    public const string Email = "Email";
    public const string WhatsApp = "WhatsApp";
    public const string Meeting = "Meeting";
    public const string SiteVisit = "SiteVisit";
    public const string Task = "Task";

    public static readonly string[] All = [Call, Email, WhatsApp, Meeting, SiteVisit, Task];
}

public static class RelatedTypes
{
    public const string Lead = "Lead";
    public const string Contact = "Contact";
    public const string Opportunity = "Opportunity";

    public static readonly string[] All = [Lead, Contact, Opportunity];
}

public static class CallDirections
{
    public const string Inbound = "Inbound";
    public const string Outbound = "Outbound";
    public const string Missed = "Missed";

    public static readonly string[] All = [Inbound, Outbound, Missed];
}

public static class CallOutcomes
{
    public const string Connected = "Connected";
    public const string NoAnswer = "NoAnswer";
    public const string Busy = "Busy";
    public const string WrongNumber = "WrongNumber";
    public const string Voicemail = "Voicemail";
    public const string SwitchedOff = "SwitchedOff";

    public static readonly string[] All = [Connected, NoAnswer, Busy, WrongNumber, Voicemail, SwitchedOff];
}

public static class CallDispositions
{
    public const string Interested = "Interested";
    public const string NotInterested = "NotInterested";
    public const string CallBackLater = "CallBackLater";

    /// <summary>A venue visit was booked off this call. Surfaced as <i>Venue visit scheduled</i>.</summary>
    public const string SiteVisitScheduled = "SiteVisitScheduled";

    public const string BudgetMismatch = "BudgetMismatch";

    /// <summary>Nothing free on the date they need. The commonest loss in events.</summary>
    public const string DateUnavailable = "DateUnavailable";

    public const string Converted = "Converted";
    public const string DoNotCall = "DoNotCall";

    public static readonly string[] All =
    [
        Interested, NotInterested, CallBackLater, SiteVisitScheduled,
        BudgetMismatch, DateUnavailable, Converted, DoNotCall
    ];
}

public static class VisitStatuses
{
    public const string Scheduled = "Scheduled";
    public const string Confirmed = "Confirmed";
    public const string Completed = "Completed";
    public const string NoShow = "NoShow";
    public const string Cancelled = "Cancelled";
    public const string Rescheduled = "Rescheduled";

    public static readonly string[] All =
    [
        Scheduled, Confirmed, Completed, NoShow, Cancelled, Rescheduled
    ];
}

public static class VisitTypes
{
    public const string FirstVisit = "FirstVisit";
    public const string RepeatVisit = "RepeatVisit";
    public const string ClosingVisit = "ClosingVisit";

    /// <summary>The menu tasting. Its own type because it converts better than any other visit.</summary>
    public const string Tasting = "Tasting";

    /// <summary>A technical recce — measuring the space for décor, sound and rigging.</summary>
    public const string Recce = "Recce";

    /// <summary>Walking the set-up on the day before the event.</summary>
    public const string Handover = "Handover";

    public static readonly string[] All =
    [
        FirstVisit, RepeatVisit, ClosingVisit, Tasting, Recce, Handover
    ];
}

public static class InterestLevels
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";

    public static readonly string[] All = [High, Medium, Low];
}

/// <summary>
/// The kinds of partner a planning desk meets off-site.
///
/// <c>ChannelPartner</c> keeps its stored name — it is a filter value the
/// engagement screens already index on — and reads as "referral partner": the
/// venue manager or vendor who passes enquiries along.
/// </summary>
public static class PartnerTypes
{
    /// <summary>A vendor or venue who refers work. Surfaced as <i>Referral partner</i>.</summary>
    public const string ChannelPartner = "ChannelPartner";

    public const string Venue = "Venue";
    public const string Caterer = "Caterer";
    public const string Decorator = "Decorator";
    public const string Photographer = "Photographer";
    public const string Entertainment = "Entertainment";
    public const string Corporate = "Corporate";
    public const string TravelAgent = "TravelAgent";

    public static readonly string[] All =
    [
        ChannelPartner, Venue, Caterer, Decorator, Photographer,
        Entertainment, Corporate, TravelAgent
    ];
}

public static class QuotationStatuses
{
    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string UnderReview = "UnderReview";
    public const string Negotiation = "Negotiation";
    public const string Accepted = "Accepted";
    public const string Rejected = "Rejected";
    public const string Expired = "Expired";

    public static readonly string[] All =
    [
        Draft, Sent, UnderReview, Negotiation, Accepted, Rejected, Expired
    ];
}

/// <summary>What kind of place the venue is. Drives shortlisting more than any other field.</summary>
public static class VenueTypes
{
    public const string BanquetHall = "BanquetHall";
    public const string Hotel = "Hotel";
    public const string Resort = "Resort";
    public const string Lawn = "Lawn";
    public const string FarmHouse = "FarmHouse";
    public const string ConventionCentre = "ConventionCentre";
    public const string Rooftop = "Rooftop";
    public const string Palace = "Palace";
    public const string Club = "Club";
    public const string Destination = "Destination";

    public static readonly string[] All =
    [
        BanquetHall, Hotel, Resort, Lawn, FarmHouse, ConventionCentre,
        Rooftop, Palace, Club, Destination
    ];
}

/// <summary>
/// Whether the venue can be sold at all right now.
///
/// <see cref="Seasonal"/> earns its place because open venues genuinely go off
/// the market for the monsoon, and a lawn that cannot be booked in July is not
/// the same thing as a lawn that has closed.
/// </summary>
public static class VenueStatuses
{
    public const string Active = "Active";
    public const string Onboarding = "Onboarding";
    public const string Seasonal = "Seasonal";
    public const string Renovation = "Renovation";
    public const string Inactive = "Inactive";

    public static readonly string[] All =
    [
        Active, Onboarding, Seasonal, Renovation, Inactive
    ];
}

/// <summary>
/// A space's standing in the catalogue — <b>not</b> whether it is free on a
/// given date. Date-level availability is <c>SpaceBooking</c>.
///
/// <see cref="Held"/> and <see cref="Booked"/> remain because the board still
/// shows a space's state for the date being looked at; the values are read off
/// the booking rows rather than stored on the space.
/// </summary>
public static class UnitStatuses
{
    public const string Available = "Available";
    public const string Held = "Held";
    public const string Blocked = "Blocked";
    public const string Booked = "Booked";

    /// <summary>Contract signed and paid. Surfaced as <i>Confirmed</i>.</summary>
    public const string Sold = "Sold";

    /// <summary>Never released — house use, storage, staff areas. Surfaced as <i>Not bookable</i>.</summary>
    public const string NotForSale = "NotForSale";

    /// <summary>
    /// Ops blackout on the calendar (maintenance, weather, festival close).
    /// Used on <see cref="SpaceBooking"/> — not as a permanent catalogue status.
    /// </summary>
    public const string Blackout = "Blackout";

    public static readonly string[] All =
    [
        Available, Held, Blocked, Booked, Sold, NotForSale, Blackout
    ];

    /// <summary>Statuses that take a space out of bookable inventory.</summary>
    public static readonly string[] Unavailable = [Booked, Sold, NotForSale];

    /// <summary>Statuses that block a date/slot on the diary.</summary>
    public static readonly string[] CalendarBlocking =
    [
        Held, Booked, Sold, Blocked, Blackout
    ];

    public static string Label(string status) => status switch
    {
        Sold => "Confirmed",
        NotForSale => "Not bookable",
        Blackout => "Blackout",
        _ => status,
    };
}

/// <summary>The kind of space, stored on <c>Unit.Configuration</c>.</summary>
public static class SpaceTypes
{
    public const string BanquetHall = "BanquetHall";

    public static readonly string[] All =
    [
        BanquetHall, "Ballroom", "Lawn", "Poolside", "Terrace", "Rooftop",
        "Courtyard", "Amphitheatre", "ConferenceRoom", "Boardroom",
        "PreFunctionArea", "MandapArea", "DiningHall", "Marquee"
    ];
}

public static class AuditActions
{
    public const string Create = "Create";
    public const string Update = "Update";
    public const string Delete = "Delete";
}
