namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Events and wedding planning vocabularies.
 *
 * Same convention as CrmEnums: plain string constants, persisted verbatim
 * and rendered as-is, with an `All` array so controllers have one place to
 * validate against.
 * ------------------------------------------------------------------ */

/// <summary>
/// The occasion being planned.
///
/// Kept flat rather than nested under <see cref="EventCategories"/> because an
/// enquiry names the occasion first — "my daughter's sangeet" — and the
/// category is inferred from it, never the other way round.
/// </summary>
public static class EventTypes
{
    /* ---------------- wedding ---------------- */

    public const string Wedding = "Wedding";
    public const string Reception = "Reception";
    public const string Engagement = "Engagement";
    public const string Sangeet = "Sangeet";
    public const string Mehendi = "Mehendi";
    public const string Haldi = "Haldi";
    public const string DestinationWedding = "DestinationWedding";
    public const string PreWeddingShoot = "PreWeddingShoot";

    /* ---------------- social ---------------- */

    public const string Birthday = "Birthday";
    public const string Anniversary = "Anniversary";
    public const string BabyShower = "BabyShower";
    public const string Naming = "Naming";
    public const string HouseWarming = "HouseWarming";
    public const string Festival = "Festival";

    /* ---------------- corporate ---------------- */

    public const string Conference = "Conference";
    public const string ProductLaunch = "ProductLaunch";
    public const string AnnualDay = "AnnualDay";
    public const string Offsite = "Offsite";
    public const string Exhibition = "Exhibition";
    public const string AwardNight = "AwardNight";

    public const string Other = "Other";

    public static readonly string[] All =
    [
        Wedding, Reception, Engagement, Sangeet, Mehendi, Haldi,
        DestinationWedding, PreWeddingShoot,
        Birthday, Anniversary, BabyShower, Naming, HouseWarming, Festival,
        Conference, ProductLaunch, AnnualDay, Offsite, Exhibition, AwardNight,
        Other
    ];

    /// <summary>Occasions that belong to a wedding — the multi-function bookings.</summary>
    public static readonly string[] WeddingFunctions =
    [
        Wedding, Reception, Engagement, Sangeet, Mehendi, Haldi, DestinationWedding
    ];

    /// <summary>Which category an occasion falls under, for routing and reporting.</summary>
    public static string CategoryOf(string? type) => type switch
    {
        Wedding or Reception or Engagement or Sangeet or Mehendi or Haldi
            or DestinationWedding or PreWeddingShoot => EventCategories.Wedding,
        Conference or ProductLaunch or AnnualDay or Offsite or Exhibition
            or AwardNight => EventCategories.Corporate,
        Birthday or Anniversary or BabyShower or Naming or HouseWarming
            or Festival => EventCategories.Social,
        _ => EventCategories.Other,
    };
}

/// <summary>The coarse bucket an event falls into. Drives routing and reporting.</summary>
public static class EventCategories
{
    public const string Wedding = "Wedding";
    public const string Social = "Social";
    public const string Corporate = "Corporate";
    public const string Other = "Other";

    public static readonly string[] All = [Wedding, Social, Corporate, Other];
}

/// <summary>
/// What the client wants the planner to handle.
///
/// A lead carries several of these at once — venue plus catering plus décor is
/// the normal enquiry, not the exception — so they are stored as a
/// comma-separated set on the lead rather than a single choice.
/// </summary>
public static class ServiceCategories
{
    public const string Venue = "Venue";
    public const string Catering = "Catering";
    public const string Decor = "Decor";
    public const string Photography = "Photography";
    public const string Videography = "Videography";
    public const string Makeup = "Makeup";
    public const string Mehendi = "Mehendi";
    public const string Entertainment = "Entertainment";
    public const string SoundAndLight = "SoundAndLight";
    public const string Invitations = "Invitations";
    public const string GiftsAndFavours = "GiftsAndFavours";
    public const string Transport = "Transport";
    public const string Accommodation = "Accommodation";
    public const string Priest = "Priest";
    public const string Choreography = "Choreography";
    public const string FullPlanning = "FullPlanning";

    public static readonly string[] All =
    [
        Venue, Catering, Decor, Photography, Videography, Makeup, Mehendi,
        Entertainment, SoundAndLight, Invitations, GiftsAndFavours, Transport,
        Accommodation, Priest, Choreography, FullPlanning
    ];
}

/// <summary>
/// How the client intends to settle the bill.
///
/// Distinct from <see cref="PaymentModes"/>, which is the instrument a single
/// receipt came in on — NEFT, cheque, cash. This is the intent captured at
/// enquiry, before any money moves.
/// </summary>
public static class PaymentPreferences
{
    public const string SelfFunded = "SelfFunded";
    public const string Instalments = "Instalments";
    public const string CorporatePo = "CorporatePo";
    /// <summary>Realty leftover — still accepted on old rows, hidden on new forms.</summary>
    public const string Loan = "Loan";
    public const string Sponsored = "Sponsored";
    public const string Mixed = "Mixed";

    public static readonly string[] All =
    [
        SelfFunded, Instalments, CorporatePo, Loan, Sponsored, Mixed
    ];

    public static readonly string[] ForEvents =
    [
        SelfFunded, Instalments, CorporatePo, Sponsored, Mixed
    ];
}

/// <summary>Who submitted the enquiry versus who the event is for.</summary>
public static class InquirerRoles
{
    public const string Couple = "Couple";
    public const string Parent = "Parent";
    public const string Relative = "Relative";
    public const string CorporateAdmin = "CorporateAdmin";
    public const string Other = "Other";

    public static readonly string[] All = [Couple, Parent, Relative, CorporateAdmin, Other];
}

/// <summary>How much of the occasion the planner is being hired to run.</summary>
public static class PlanningPackages
{
    public const string FullPlanning = "FullPlanning";
    public const string Partial = "Partial";
    public const string DayOf = "DayOf";
    public const string VenueStyling = "VenueStyling";
    public const string ConsultOnly = "ConsultOnly";

    public static readonly string[] All =
    [
        FullPlanning, Partial, DayOf, VenueStyling, ConsultOnly
    ];
}

/// <summary>
/// Where the couple is with their venue choice — on the enquiry, not the
/// catalogue status of a listed property (<see cref="VenueStatuses"/>).
/// </summary>
public static class EnquiryVenueStatuses
{
    public const string Searching = "Searching";
    public const string Shortlisted = "Shortlisted";
    public const string Booked = "Booked";

    public static readonly string[] All = [Searching, Shortlisted, Booked];
}

public static class QuestionnaireStatuses
{
    public const string None = "None";
    public const string Sent = "Sent";
    public const string Completed = "Completed";

    public static readonly string[] All = [None, Sent, Completed];
}

public static class CeremonyStyles
{
    public const string Hindu = "Hindu";
    public const string Muslim = "Muslim";
    public const string Christian = "Christian";
    public const string Sikh = "Sikh";
    public const string Civil = "Civil";
    public const string Other = "Other";

    public static readonly string[] All = [Hindu, Muslim, Christian, Sikh, Civil, Other];
}

public static class EventPortals
{
    public const string WedMeGood = "WedMeGood";
    public const string WeddingWire = "WeddingWire";
    public const string ShaadiSaga = "ShaadiSaga";
    public const string VenueLook = "VenueLook";
    public const string TheKnot = "TheKnot";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        WedMeGood, WeddingWire, ShaadiSaga, VenueLook, TheKnot, Other
    ];
}

/// <summary>
/// Meal service style. Drives the per-plate maths on a catering quote, so it is
/// captured at enquiry rather than left to the proposal.
/// </summary>
public static class MealPreferences
{
    public const string Vegetarian = "Vegetarian";
    public const string NonVegetarian = "NonVegetarian";
    public const string Jain = "Jain";
    public const string Vegan = "Vegan";
    public const string Mixed = "Mixed";

    public static readonly string[] All =
    [
        Vegetarian, NonVegetarian, Jain, Vegan, Mixed
    ];
}

/// <summary>
/// The slot a space is taken for on a given day.
///
/// A banquet hall is sold by session, not by day — the same lawn hosts a
/// mehendi at noon and a sangeet at night. FullDay overlaps both and is what
/// makes the availability check a slot-overlap test rather than a date equality
/// test.
/// </summary>
public static class EventSlots
{
    public const string Morning = "Morning";
    public const string Afternoon = "Afternoon";
    public const string Evening = "Evening";
    public const string Night = "Night";
    public const string FullDay = "FullDay";

    public static readonly string[] All = [Morning, Afternoon, Evening, Night, FullDay];

    /// <summary>
    /// Whether two slots on the same date collide.
    ///
    /// FullDay collides with everything including itself; the named sessions
    /// collide only with their own kind.
    /// </summary>
    public static bool Overlaps(string a, string b) =>
        a == FullDay || b == FullDay || string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
