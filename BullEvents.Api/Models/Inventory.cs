namespace BullEvents.Api.Models;

/// <summary>
/// Venue → Block → Space. The bookable inventory hierarchy.
///
/// The class keeps the name <c>Project</c> because that string is the
/// permission key, the foreign key and the API route across a dozen modules;
/// everything the user sees calls it a <b>venue</b>.
/// </summary>
public class Project : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;

    /// <summary>Who runs the venue — the hotel group, the trust, the owner.</summary>
    public string? Developer { get; set; }

    public string Type { get; set; } = VenueTypes.BanquetHall;
    public string Status { get; set; } = VenueStatuses.Active;

    public string? City { get; set; }
    public string? Locality { get; set; }
    public string? Address { get; set; }

    /// <summary>Licence or registration number, where the venue carries one.</summary>
    public string? ReraNumber { get; set; }

    public DateTime? LaunchDate { get; set; }
    public DateTime? PossessionDate { get; set; }

    public decimal? PriceMin { get; set; }
    public decimal? PriceMax { get; set; }
    public string? Amenities { get; set; }
    public string? Description { get; set; }
    public string? CoverImageUrl { get; set; }

    /* ---------------- venue operations ---------------- */

    /// <summary>
    /// When music has to stop, as a local time of day.
    ///
    /// The first question every sangeet client asks, and the reason a booking
    /// falls through after the deposit — so it belongs on the venue record
    /// rather than in a notes field somebody forgot to read.
    /// </summary>
    public TimeSpan? NoiseCurfew { get; set; }

    /// <summary>Whether the client may bring their own caterer instead of the in-house kitchen.</summary>
    public bool AllowsOutsideCatering { get; set; }

    /// <summary>Whether alcohol is permitted, licence in place.</summary>
    public bool AllowsAlcohol { get; set; }

    /// <summary>Whether open flame — havan, fireworks, food stations — is permitted.</summary>
    public bool AllowsOpenFlame { get; set; }

    /// <summary>On-site rooms for the party, where the venue has them.</summary>
    public int? GuestRooms { get; set; }

    /// <summary>Total parking across the venue, in cars.</summary>
    public int? ParkingCapacity { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public ICollection<Tower> Towers { get; set; } = new List<Tower>();
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}

/// <summary>A wing or block within a venue. Surfaced as <b>Block</b>.</summary>
public class Tower
{
    public int Id { get; set; }
    public int ProjectId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>How many levels the block has. Surfaced as <i>Levels</i>.</summary>
    public int FloorCount { get; set; }

    /// <summary>Spaces per level. Surfaced as <i>Spaces per level</i>.</summary>
    public int UnitsPerFloor { get; set; }

    public string Status { get; set; } = VenueStatuses.Active;

    public Project? Project { get; set; }
    public ICollection<Unit> Units { get; set; } = new List<Unit>();
}

/// <summary>
/// One bookable space inside a venue — a banquet hall, a lawn, a poolside, a
/// terrace. Surfaced as <b>Space</b>; <c>Unit</c> is the stored name.
///
/// <see cref="Status"/> is the space's standing in the catalogue — in service,
/// under renovation, retained for house use. It deliberately does <i>not</i>
/// mean "free on the 12th": a space is free on some dates and taken on others,
/// and that lives in <see cref="SpaceBooking"/>. Conflating the two is the
/// mistake a property CRM makes and an events CRM cannot afford.
/// </summary>
public class Unit : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }
    public int? TowerId { get; set; }

    /// <summary>The space's name or number — "Emerald Hall", "Lawn 2".</summary>
    public string UnitNumber { get; set; } = string.Empty;

    /// <summary>Which level of the block it is on. Ground is 0.</summary>
    public int Floor { get; set; }

    /// <summary>The kind of space — a value from <see cref="SpaceTypes"/>.</summary>
    public string Configuration { get; set; } = SpaceTypes.BanquetHall;

    /* ---------------- size and capacity ---------------- */

    /// <summary>Usable floor area. Surfaced as <i>Area</i>.</summary>
    public decimal CarpetArea { get; set; }

    /// <summary>Area including the pre-function or foyer space attached to it.</summary>
    public decimal? BuiltUpArea { get; set; }

    /// <summary>Total footprint including lawns and setback the booking takes over.</summary>
    public decimal? SuperArea { get; set; }

    public string AreaUnit { get; set; } = "sqft";

    /// <summary>
    /// Guests seated at tables. The number that decides whether a space can hold
    /// the wedding — and always well below <see cref="FloatingCapacity"/>.
    /// </summary>
    public int SeatingCapacity { get; set; }

    /// <summary>Guests standing, cocktail style. The larger, softer number.</summary>
    public int FloatingCapacity { get; set; }

    /// <summary>Guests seated theatre style, for a conference or an award night.</summary>
    public int? TheatreCapacity { get; set; }

    /* ---------------- character ---------------- */

    public string? Facing { get; set; }
    public string? ViewType { get; set; }

    /// <summary>Washrooms attached to the space.</summary>
    public int Bathrooms { get; set; } = 2;

    /// <summary>Adjoining open areas — a terrace or balcony the party spills onto.</summary>
    public int Balconies { get; set; } = 1;

    /// <summary>Parking reserved for this space's bookings, in cars.</summary>
    public int ParkingSlots { get; set; } = 1;

    /// <summary>A corner or standalone space — no shared wall with another booking.</summary>
    public bool IsCornerUnit { get; set; }

    /// <summary>Kept for venues that market a space as vastu-aligned.</summary>
    public bool VastuCompliant { get; set; } = true;

    /// <summary>Whether the space is air conditioned. Decides the summer season entirely.</summary>
    public bool IsAirConditioned { get; set; } = true;

    /// <summary>An open space — a lawn, terrace or poolside — which needs a weather plan.</summary>
    public bool IsOutdoor { get; set; }

    /// <summary>Whether a built stage or mandap platform is in place.</summary>
    public bool HasStage { get; set; }

    /// <summary>Whether the space has its own kitchen or pantry backing it.</summary>
    public bool HasAttachedKitchen { get; set; }

    /// <summary>
    /// Hours blocked after a booking for setup/teardown before the next slot
    /// can sell. Zero means back-to-back sessions are allowed.
    /// </summary>
    public int TurnaroundHours { get; set; }

    /// <summary>Optional floor / seating layout image for the space dossier.</summary>
    public string? LayoutImageUrl { get; set; }

    public string Status { get; set; } = UnitStatuses.Available;

    public decimal BasePrice { get; set; }
    public decimal PricePerSqft { get; set; }
    public decimal FloorRisePremium { get; set; }
    public decimal PlcCharges { get; set; }
    public decimal TotalPrice { get; set; }

    /// <summary>
    /// Preferential location charge per sq ft, added to the rate before area is
    /// applied. Held per sq ft rather than as a lump sum because that is how it
    /// is priced — a corner unit carries it on every foot it has.
    /// </summary>
    public decimal PlcPerSqft { get; set; }

    /* ---------------- event pricing ---------------- */

    /// <summary>
    /// Per-head catering rate. The larger half of most event bills — a 500-guest
    /// wedding at ₹1,800 a plate dwarfs any hall rental — so it is priced on the
    /// space rather than buried in a catering package.
    /// </summary>
    public decimal PricePerPlate { get; set; }

    /// <summary>
    /// The plate count the venue bills for whether or not the guests turn up.
    ///
    /// The minimum guarantee is the venue's real floor price, and quoting a
    /// 200-guest event into a hall with a 400-plate minimum is the single most
    /// common way an events quote goes wrong.
    /// </summary>
    public int MinimumPlates { get; set; }

    /// <summary>
    /// Extra charged on a peak date, as a fraction of the rental — 0.25 for a
    /// quarter more. Peak dates are the saavan and November–February muhurat
    /// runs, when a hall prices itself differently.
    /// </summary>
    public decimal PeakDatePremium { get; set; }

    /// <summary>Refundable deposit held against damage and overrun.</summary>
    public decimal SecurityDeposit { get; set; }

    /// <summary>
    /// A soft hold — the safeguard against two reps selling the same unit.
    /// Expired holds are treated as released without needing a background job.
    /// </summary>
    public int? HeldByUserId { get; set; }
    public DateTime? HeldUntil { get; set; }
    public string? HoldReason { get; set; }

    public int? BookedByContactId { get; set; }
    public DateTime? BookedAt { get; set; }

    /// <summary>
    /// Who the unit is committed to, and who sold it. Denormalised onto the
    /// unit so the inventory board can show it without joining three tables
    /// per tile — a 290-unit board renders in one query.
    /// </summary>
    public int? BookedByLeadId { get; set; }
    public string? CustomerName { get; set; }
    public string? CustomerPhone { get; set; }
    public int? SalesPersonId { get; set; }
    public string? SalesPersonName { get; set; }

    /// <summary>The quotation the booking came from, when it came from one.</summary>
    public int? BookedQuotationId { get; set; }

    /// <summary>Why the unit is off the market, for Blocked and NotForSale.</summary>
    public string? BlockReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Project? Project { get; set; }
    public Tower? Tower { get; set; }
    public ICollection<UnitStatusHistory> History { get; set; } = new List<UnitStatusHistory>();

    /// <summary>Every date this space is spoken for. The real availability record.</summary>
    public ICollection<SpaceBooking> Bookings { get; set; } = new List<SpaceBooking>();
}

/// <summary>
/// One space, held or confirmed, for one slot on one date.
///
/// This is the table that makes the CRM an events system rather than a
/// property one. A flat is sold once; a banquet hall is sold three hundred
/// times a year, and the only question that matters — "can you do the 14th?" —
/// is a query against this table, not a status on the space.
///
/// A multi-day wedding writes one row per date per space, all sharing a
/// <see cref="GroupRef"/>, so the run can be released or moved as one thing
/// while each date still answers the availability check on its own.
/// </summary>
public class SpaceBooking : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int UnitId { get; set; }
    public int ProjectId { get; set; }

    /// <summary>The day itself, as a date with no time part.</summary>
    public DateOnly EventDate { get; set; }

    /// <summary>Which session — a value from <see cref="EventSlots"/>.</summary>
    public string Slot { get; set; } = EventSlots.Evening;

    /// <summary>
    /// Held or Booked, from <see cref="UnitStatuses"/>. A held row still blocks
    /// the date — that is the whole point of a hold — but expires on its own.
    /// </summary>
    public string Status { get; set; } = UnitStatuses.Held;

    /// <summary>Ties the dates of one multi-day booking together.</summary>
    public string? GroupRef { get; set; }

    /// <summary>When a Held row stops blocking the date. Null for a confirmed booking.</summary>
    public DateTime? HoldExpiresAt { get; set; }

    public int? LeadId { get; set; }
    public int? ContactId { get; set; }
    public int? QuotationId { get; set; }
    public int? BookingId { get; set; }

    /// <summary>
    /// Denormalised so the availability calendar can label a taken date without
    /// joining the lead table for every cell it draws.
    /// </summary>
    public string? ClientName { get; set; }
    public string? EventType { get; set; }
    public int? GuestCount { get; set; }

    public int? OwnerId { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Unit? Unit { get; set; }
    public Project? Project { get; set; }
    public Lead? Lead { get; set; }

    /// <summary>Whether this row still blocks its date, as of <paramref name="now"/>.</summary>
    public bool IsBlocking(DateTime now) =>
        Status is UnitStatuses.Booked or UnitStatuses.Sold or UnitStatuses.Blocked
            or UnitStatuses.Blackout
        || (Status == UnitStatuses.Held
            && (HoldExpiresAt is null || HoldExpiresAt > now));
}

/* ------------------------------------------------------------------ *
 * Venue packages and peak dates
 * ------------------------------------------------------------------ */

/// <summary>
/// A sellable bundle — e.g. lawn + banquet + rooms — with default commercial
/// heads. Quotations can load from a package instead of picking spaces one by one.
/// </summary>
public class VenuePackage : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Optional link to <see cref="PlanningPackages"/>.</summary>
    public string? PlanningPackage { get; set; }

    public decimal? IndicativeRental { get; set; }
    public decimal? IndicativePerPlate { get; set; }
    public int? DefaultMinimumPlates { get; set; }
    public int? DefaultGuestCount { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Project? Project { get; set; }
    public ICollection<VenuePackageSpace> Spaces { get; set; } = new List<VenuePackageSpace>();
}

public class VenuePackageSpace
{
    public int Id { get; set; }
    public int VenuePackageId { get; set; }
    public int UnitId { get; set; }
    public int SortOrder { get; set; }
    public string? DefaultSlot { get; set; }

    public VenuePackage? Package { get; set; }
    public Unit? Unit { get; set; }
}

/// <summary>
/// Peak / off-peak window for a venue. Drives PeakDatePremium on quotes.
/// </summary>
public class VenuePeakDate : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }

    public string Label { get; set; } = "Peak";

    /// <summary>Fraction added to rental — 0.25 = +25%.</summary>
    public decimal PremiumFraction { get; set; } = 0.25m;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Project? Project { get; set; }
}
