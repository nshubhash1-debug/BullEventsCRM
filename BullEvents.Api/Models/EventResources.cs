namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * The rest of what an event is assembled from: the suppliers who are
 * subcontracted, the people who work the floor, and the vehicles that move
 * everything there.
 *
 * All three follow the shape the props module established, because the
 * question is the same every time: *can this be promised for those dates?*
 *
 *   - a prop answers it by summing PropReservation over the window;
 *   - a crew member answers it by looking for a CrewAssignment that overlaps;
 *   - a vehicle answers it by looking for a VehicleTrip that overlaps;
 *   - a vendor answers it by counting the purchase orders already on the date
 *     against how many jobs they say they can run at once.
 *
 * Keeping the four answers the same shape is what lets one screen ask "what
 * can we field on the 14th?" without four different mental models.
 * ------------------------------------------------------------------ */

/* ================================================================== *
 * Vendors — the subcontracted half of every wedding
 * ================================================================== */

/// <summary>
/// How a supplier is currently regarded. Not their status on one job.
/// </summary>
public static class VendorStatuses
{
    public const string Active = "Active";

    /// <summary>Usable, but not offered first — a rate dispute, a bad show.</summary>
    public const string OnWatch = "OnWatch";

    /// <summary>Never to be booked again. Kept for the history on old events.</summary>
    public const string Blacklisted = "Blacklisted";

    /// <summary>Onboarding — documents or rates not yet in.</summary>
    public const string Pending = "Pending";

    public static readonly string[] All = [Active, OnWatch, Blacklisted, Pending];

    /// <summary>Statuses a new purchase order may be raised against.</summary>
    public static readonly string[] Bookable = [Active, OnWatch];
}

/// <summary>What the supplier is paid on.</summary>
public static class VendorRateBases
{
    public const string PerEvent = "PerEvent";
    public const string PerDay = "PerDay";
    public const string PerHour = "PerHour";

    /// <summary>Caterers, mostly. Multiplies by the guest count.</summary>
    public const string PerPlate = "PerPlate";

    public const string PerPiece = "PerPiece";
    public const string PerSqft = "PerSqft";

    /// <summary>A share of what the client is billed. Photographers and DJs.</summary>
    public const string Commission = "Commission";

    public static readonly string[] All =
    [
        PerEvent, PerDay, PerHour, PerPlate, PerPiece, PerSqft, Commission
    ];
}

/// <summary>Where a purchase order has reached.</summary>
public static class PurchaseOrderStatuses
{
    public const string Draft = "Draft";

    /// <summary>Sent to the supplier, awaiting their word.</summary>
    public const string Sent = "Sent";

    /// <summary>They have taken the date. This is what blocks their calendar.</summary>
    public const string Confirmed = "Confirmed";

    /// <summary>The job is done and the work was acceptable.</summary>
    public const string Delivered = "Delivered";

    /// <summary>Delivered and settled.</summary>
    public const string Closed = "Closed";

    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    [
        Draft, Sent, Confirmed, Delivered, Closed, Cancelled
    ];

    /// <summary>
    /// Statuses in which the order holds the supplier's date.
    ///
    /// Sent counts: a caterer who has been sent the order for the 14th and has
    /// not refused it is not free to be offered to another wedding, and treating
    /// them as free is how two events end up expecting the same kitchen.
    /// </summary>
    public static readonly string[] Blocking = [Sent, Confirmed, Delivered];
}

/// <summary>
/// A supplier the company subcontracts to — a caterer, a DJ, a florist, a
/// mandap decorator, a bus operator.
///
/// The directory is deliberately one table rather than one per trade.
/// <see cref="ServiceCategories"/> already names the trades, a supplier
/// routinely covers more than one, and splitting them would mean the same
/// photographer existed twice the day they started supplying video too.
/// </summary>
public class Vendor : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Quotable reference — <c>VN-0042</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public string Status { get; set; } = VendorStatuses.Active;

    /// <summary>
    /// Comma-separated values from <see cref="ServiceCategories"/>.
    ///
    /// A set rather than one choice, and stored the same way a lead stores what
    /// it needs, so "who can do décor on the 14th?" is one query against one
    /// column on both sides.
    /// </summary>
    public string Services { get; set; } = string.Empty;

    /* ---------------- reaching them ---------------- */

    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? AltPhone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }

    /// <summary>Cities they will travel to, comma separated. Blank means local only.</summary>
    public string? CoverageAreas { get; set; }

    public string? Website { get; set; }

    /* ---------------- paperwork ---------------- */

    public string? GstNumber { get; set; }
    public string? PanNumber { get; set; }
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIfsc { get; set; }

    /// <summary>Days from invoice to payment, as agreed.</summary>
    public int PaymentTermDays { get; set; } = 30;

    /// <summary>Fraction taken up front — 0.5 is half in advance.</summary>
    public decimal? AdvanceFraction { get; set; }

    /* ---------------- capacity and standing ---------------- */

    /// <summary>
    /// How many events they can run on one date.
    ///
    /// The whole reason a vendor needs an availability check at all: a
    /// single-crew photographer is one, a large caterer might genuinely be
    /// three, and booking the first as though they were the third is the
    /// mistake this number prevents.
    /// </summary>
    public int ConcurrentEventCapacity { get; set; } = 1;

    /// <summary>Rolling average of the ratings left on delivered orders, 1–5.</summary>
    public decimal? Rating { get; set; }

    public int CompletedEvents { get; set; }

    public string? Notes { get; set; }

    public int? OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public User? Owner { get; set; }
    public ICollection<VendorRate> Rates { get; set; } = new List<VendorRate>();
    public ICollection<VendorDocument> Documents { get; set; } = new List<VendorDocument>();

    /// <summary>Whether a new order may be raised against this supplier.</summary>
    public bool IsBookable => VendorStatuses.Bookable.Contains(Status);

    /// <summary>Whether they cover a trade, tested against the stored set.</summary>
    public bool Covers(string service) =>
        !string.IsNullOrWhiteSpace(Services)
        && Services.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(service, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// One line of a supplier's rate card — what they charge for one thing, on one
/// basis.
///
/// Separate rows rather than a single rate on the vendor because the same
/// caterer quotes a veg per-plate, a non-veg per-plate and a live-counter
/// surcharge, and a proposal needs all three to cost a wedding honestly.
/// </summary>
public class VendorRate : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int VendorId { get; set; }

    /// <summary>Which trade this line belongs to, from <see cref="ServiceCategories"/>.</summary>
    public string Service { get; set; } = ServiceCategories.Decor;

    /// <summary>What is being charged for — "Veg thali", "Sangeet DJ, 4 hours".</summary>
    public string Name { get; set; } = string.Empty;

    public string Basis { get; set; } = VendorRateBases.PerEvent;

    /// <summary>What the supplier charges us.</summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// What we charge the client for it.
    ///
    /// Held here rather than computed from a blanket margin, because the markup
    /// on a mandap and the markup on a bus are not the same number and never
    /// have been.
    /// </summary>
    public decimal? SellRate { get; set; }

    /// <summary>Minimum billable quantity — the caterer's floor on plates.</summary>
    public int? MinimumQuantity { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Vendor? Vendor { get; set; }

    /// <summary>What one unit earns us. Negative means the line loses money.</summary>
    public decimal? MarginPerUnit => SellRate is null ? null : SellRate - Rate;
}

/// <summary>
/// A supplier's paperwork — trade licence, FSSAI certificate, insurance, the
/// signed rate agreement.
///
/// <see cref="ExpiryDate"/> is the point: a caterer whose FSSAI licence lapsed
/// last month is a liability nobody notices until an inspector does.
/// </summary>
public class VendorDocument : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int VendorId { get; set; }

    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? Url { get; set; }

    public DateOnly? IssueDate { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Vendor? Vendor { get; set; }

    public bool IsExpired(DateOnly today) => ExpiryDate is DateOnly e && e < today;
}

/// <summary>
/// What one supplier has been engaged to do for one event.
///
/// This is the vendor-side twin of a gate pass: it holds the supplier's date,
/// carries the priced lines, and is what a payment is settled against. A
/// wedding with a caterer, a DJ, a florist and a bus operator is four of these,
/// all pointing at the same lead.
/// </summary>
public class VendorPurchaseOrder : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>Quotable reference — <c>PO-2609-K3P9QF</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public int VendorId { get; set; }

    public string Status { get; set; } = PurchaseOrderStatuses.Draft;

    /// <summary>Which trade this order covers.</summary>
    public string Service { get; set; } = ServiceCategories.Decor;

    /* ---------------- the event ---------------- */

    public int? LeadId { get; set; }
    public int? BookingId { get; set; }
    public int? QuotationId { get; set; }
    public int? ProjectId { get; set; }

    public string? EventName { get; set; }
    public string? EventType { get; set; }
    public string? ClientName { get; set; }
    public string? VenueName { get; set; }
    public string? VenueAddress { get; set; }

    /// <summary>Guests the supplier is catering or seating for.</summary>
    public int? GuestCount { get; set; }

    /* ---------------- the dates ---------------- */

    /// <summary>First day the supplier is committed — setup, not the function.</summary>
    public DateOnly ServiceDate { get; set; }

    /// <summary>Last day. Same as the start for a single-day engagement.</summary>
    public DateOnly ServiceEndDate { get; set; }

    /// <summary>When they are due on site.</summary>
    public TimeSpan? ReportingTime { get; set; }

    /* ---------------- money ---------------- */

    /// <summary>What we owe them, from the lines.</summary>
    public decimal TotalCost { get; set; }

    /// <summary>What the client is billed for it, from the lines.</summary>
    public decimal TotalSell { get; set; }

    /// <summary>Paid so far, from the payments.</summary>
    public decimal AmountPaid { get; set; }

    /// <summary>Held back until the job is signed off.</summary>
    public decimal? RetentionAmount { get; set; }

    /* ---------------- closing out ---------------- */

    /// <summary>1–5, recorded on delivery. Feeds the vendor's rolling rating.</summary>
    public int? Rating { get; set; }

    public string? Terms { get; set; }
    public string? Notes { get; set; }

    /// <summary>Our person accountable for this supplier on the day.</summary>
    public int? CoordinatorId { get; set; }

    public int? OwnerId { get; set; }

    public DateTime? SentAt { get; set; }
    public DateTime? ConfirmedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Vendor? Vendor { get; set; }
    public Lead? Lead { get; set; }
    public Project? Project { get; set; }
    public User? Coordinator { get; set; }
    public ICollection<VendorPoLine> Lines { get; set; } = new List<VendorPoLine>();
    public ICollection<VendorPayment> Payments { get; set; } = new List<VendorPayment>();

    /// <summary>Whether this order still holds the supplier's dates.</summary>
    public bool IsBlocking => PurchaseOrderStatuses.Blocking.Contains(Status);

    /// <summary>Whether the order's dates touch the window being asked about.</summary>
    public bool Overlaps(DateOnly from, DateOnly to) =>
        ServiceDate <= to && ServiceEndDate >= from;

    /// <summary>Still owed to the supplier.</summary>
    public decimal AmountDue => Math.Max(0, TotalCost - AmountPaid);

    /// <summary>What the company keeps. Negative means the job is being run at a loss.</summary>
    public decimal Margin => TotalSell - TotalCost;
}

/// <summary>One priced line on a purchase order.</summary>
public class VendorPoLine
{
    public int Id { get; set; }
    public int VendorPurchaseOrderId { get; set; }

    /// <summary>The rate-card line this came from, where it came from one.</summary>
    public int? VendorRateId { get; set; }

    public string Description { get; set; } = string.Empty;
    public string Basis { get; set; } = VendorRateBases.PerEvent;

    public decimal Quantity { get; set; } = 1;

    /// <summary>Per unit, what the supplier charges.</summary>
    public decimal Rate { get; set; }

    /// <summary>Per unit, what the client is billed.</summary>
    public decimal? SellRate { get; set; }

    public string? Notes { get; set; }
    public int SortOrder { get; set; }

    public VendorPurchaseOrder? Order { get; set; }
    public VendorRate? RateCard { get; set; }

    public decimal LineCost => Quantity * Rate;
    public decimal LineSell => Quantity * (SellRate ?? Rate);
}

/// <summary>Money actually paid out against an order.</summary>
public class VendorPayment : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int VendorPurchaseOrderId { get; set; }

    public decimal Amount { get; set; }
    public DateOnly PaidOn { get; set; }

    /// <summary>NEFT, cheque, cash, UPI.</summary>
    public string Mode { get; set; } = "NEFT";

    public string? Reference { get; set; }

    /// <summary>Advance, milestone, final, retention release.</summary>
    public string Kind { get; set; } = "Milestone";

    public decimal? TdsAmount { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public VendorPurchaseOrder? Order { get; set; }
}

/* ================================================================== *
 * Crew — the people on the floor
 * ================================================================== */

/// <summary>
/// How somebody on the roster is engaged.
///
/// The distinction that matters operationally is whether payroll already knows
/// about them. An employee's day is an HR attendance record; a freelancer's day
/// is a line on a payment run that exists nowhere else.
/// </summary>
public static class CrewEngagementTypes
{
    /// <summary>On the payroll. Mirrors an <see cref="HrEmployee"/>.</summary>
    public const string Employee = "Employee";

    /// <summary>Paid by the day, event to event.</summary>
    public const string Freelancer = "Freelancer";

    /// <summary>Supplied in a gang by a labour contractor.</summary>
    public const string Contractor = "Contractor";

    public static readonly string[] All = [Employee, Freelancer, Contractor];
}

/// <summary>
/// What somebody does on site.
///
/// Free-form would have been easier and useless: the whole point of the roster
/// is answering "find me four bearers and a light technician for Friday", and
/// that is a query against a known set.
/// </summary>
public static class CrewRoles
{
    public const string EventManager = "EventManager";
    public const string Coordinator = "Coordinator";
    public const string Supervisor = "Supervisor";
    public const string Decorator = "Decorator";
    public const string Florist = "Florist";
    public const string Carpenter = "Carpenter";
    public const string Electrician = "Electrician";
    public const string LightTechnician = "LightTechnician";
    public const string SoundTechnician = "SoundTechnician";
    public const string Bearer = "Bearer";
    public const string Helper = "Helper";
    public const string Loader = "Loader";
    public const string Driver = "Driver";
    public const string Security = "Security";
    public const string Housekeeping = "Housekeeping";
    public const string Usher = "Usher";
    public const string Photographer = "Photographer";
    public const string Videographer = "Videographer";
    public const string Anchor = "Anchor";
    public const string Chef = "Chef";

    public static readonly string[] All =
    [
        EventManager, Coordinator, Supervisor, Decorator, Florist, Carpenter,
        Electrician, LightTechnician, SoundTechnician, Bearer, Helper, Loader,
        Driver, Security, Housekeeping, Usher, Photographer, Videographer,
        Anchor, Chef
    ];
}

public static class CrewStatuses
{
    public const string Active = "Active";

    /// <summary>On the roster but not to be called at the moment.</summary>
    public const string Inactive = "Inactive";

    /// <summary>Never to be called again.</summary>
    public const string Blacklisted = "Blacklisted";

    public static readonly string[] All = [Active, Inactive, Blacklisted];
}

/// <summary>Where one person's assignment to one event stands.</summary>
public static class CrewAssignmentStatuses
{
    /// <summary>Pencilled in. Holds the date, so a plan cannot double-book.</summary>
    public const string Planned = "Planned";

    /// <summary>They have said yes.</summary>
    public const string Confirmed = "Confirmed";

    /// <summary>They worked it.</summary>
    public const string Completed = "Completed";

    /// <summary>They did not turn up. Counts against their reliability.</summary>
    public const string NoShow = "NoShow";

    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    [
        Planned, Confirmed, Completed, NoShow, Cancelled
    ];

    /// <summary>Statuses in which the person's dates are taken.</summary>
    public static readonly string[] Blocking = [Planned, Confirmed, Completed];
}

/// <summary>
/// One person available to work events — employee, freelancer or contractor
/// gang.
///
/// Deliberately separate from <see cref="HrEmployee"/> rather than an extension
/// of it. Most of the people who work a wedding are not on the payroll and
/// never will be; forcing them through the HR module would mean inventing an
/// employee record, a joining date and a salary structure for a bearer engaged
/// for one night. Staff who <i>are</i> employees carry
/// <see cref="EmployeeId"/>, so the two records stay one person.
/// </summary>
public class CrewMember : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Quotable reference — <c>CR-0117</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public string EngagementType { get; set; } = CrewEngagementTypes.Freelancer;
    public string Status { get; set; } = CrewStatuses.Active;

    /// <summary>The payroll record, when this person is on staff.</summary>
    public int? EmployeeId { get; set; }

    /// <summary>The labour contractor who supplies them, for a gang hand.</summary>
    public int? SupplierVendorId { get; set; }

    /* ---------------- what they do ---------------- */

    /// <summary>Their main trade — a value from <see cref="CrewRoles"/>.</summary>
    public string PrimaryRole { get; set; } = CrewRoles.Helper;

    /// <summary>Anything else they can cover, comma separated.</summary>
    public string? SecondaryRoles { get; set; }

    /// <summary>Years doing it. Sorts the roster when a job needs a safe pair of hands.</summary>
    public int? YearsExperience { get; set; }

    /* ---------------- reaching them ---------------- */

    public string? Phone { get; set; }
    public string? AltPhone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? PhotoUrl { get; set; }

    /// <summary>Whether they will travel out of the city for a destination wedding.</summary>
    public bool WillTravel { get; set; }

    /* ---------------- money ---------------- */

    /// <summary>Standard day rate. Zero for staff, whose day is payroll's problem.</summary>
    public decimal? DayRate { get; set; }

    /// <summary>Rate for hours past the shift.</summary>
    public decimal? OvertimeHourlyRate { get; set; }

    /* ---------------- standing ---------------- */

    /// <summary>1–5, averaged over completed assignments.</summary>
    public decimal? Rating { get; set; }

    public int EventsWorked { get; set; }

    /// <summary>
    /// Times booked and not turned up. The single most useful number on the
    /// record when a coordinator is picking a crew for a Saturday.
    /// </summary>
    public int NoShowCount { get; set; }

    public string? IdProofType { get; set; }
    public string? IdProofNumber { get; set; }
    public string? Notes { get; set; }

    public int? OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public HrEmployee? Employee { get; set; }
    public Vendor? SupplierVendor { get; set; }
    public User? Owner { get; set; }

    public bool IsBookable => Status == CrewStatuses.Active;

    /// <summary>Whether they can work a role, primary or secondary.</summary>
    public bool CanWork(string role) =>
        string.Equals(PrimaryRole, role, StringComparison.OrdinalIgnoreCase)
        || (SecondaryRoles ?? string.Empty)
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Contains(role, StringComparer.OrdinalIgnoreCase);
}

/// <summary>
/// One person, on one event, over one date window.
///
/// The crew-side equivalent of <c>PropReservation</c>: a blocking row is what
/// makes a person unavailable, and availability is the absence of an
/// overlapping one. Unlike a prop there is no quantity — a person is one, and
/// the overlap test is the whole answer.
/// </summary>
public class CrewAssignment : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int CrewMemberId { get; set; }

    public string Status { get; set; } = CrewAssignmentStatuses.Planned;

    /// <summary>What they are doing on this job, which may not be their main trade.</summary>
    public string Role { get; set; } = CrewRoles.Helper;

    /* ---------------- the event ---------------- */

    public int? LeadId { get; set; }
    public int? BookingId { get; set; }
    public int? ProjectId { get; set; }

    /// <summary>The gate pass they are accompanying, for a loading crew.</summary>
    public int? PropIssueId { get; set; }

    public string? EventName { get; set; }
    public string? ClientName { get; set; }
    public string? VenueName { get; set; }

    /* ---------------- the window ---------------- */

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public TimeSpan? ReportingTime { get; set; }
    public TimeSpan? ClosingTime { get; set; }

    /* ---------------- money ---------------- */

    /// <summary>Agreed for this job. Defaults from the person's standard rate.</summary>
    public decimal? DayRate { get; set; }

    public decimal OvertimeHours { get; set; }
    public decimal? OvertimeAmount { get; set; }

    /// <summary>Travel, food, whatever was reimbursed on the day.</summary>
    public decimal? AllowanceAmount { get; set; }

    public bool IsPaid { get; set; }
    public DateOnly? PaidOn { get; set; }

    /// <summary>1–5, left when the assignment is completed.</summary>
    public int? Rating { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public CrewMember? CrewMember { get; set; }
    public Lead? Lead { get; set; }
    public Project? Project { get; set; }

    public bool IsBlocking => CrewAssignmentStatuses.Blocking.Contains(Status);

    public bool Overlaps(DateOnly from, DateOnly to) => FromDate <= to && ToDate >= from;

    /// <summary>
    /// The whole cost of this person on this job — days, overtime and
    /// allowances. Days are inclusive of both ends, because a crew member
    /// working the 12th to the 14th is paid for three days, not two.
    /// </summary>
    public decimal TotalCost =>
        (DayRate ?? 0) * (ToDate.DayNumber - FromDate.DayNumber + 1)
        + (OvertimeAmount ?? 0)
        + (AllowanceAmount ?? 0);
}

/* ================================================================== *
 * Fleet — getting it all there
 * ================================================================== */

public static class VehicleTypes
{
    public const string Tempo = "Tempo";
    public const string Truck = "Truck";
    public const string Container = "Container";
    public const string Pickup = "Pickup";
    public const string Van = "Van";
    public const string Car = "Car";
    public const string Bus = "Bus";
    public const string Tractor = "Tractor";

    public static readonly string[] All =
    [
        Tempo, Truck, Container, Pickup, Van, Car, Bus, Tractor
    ];
}

public static class VehicleStatuses
{
    public const string Active = "Active";

    /// <summary>Off the road — servicing, repair, papers expired.</summary>
    public const string InService = "InService";

    public const string Retired = "Retired";

    public static readonly string[] All = [Active, InService, Retired];
}

public static class TripStatuses
{
    public const string Planned = "Planned";
    public const string Loading = "Loading";
    public const string InTransit = "InTransit";
    public const string Delivered = "Delivered";

    /// <summary>Bringing the load home.</summary>
    public const string Returning = "Returning";

    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
    [
        Planned, Loading, InTransit, Delivered, Returning, Completed, Cancelled
    ];

    /// <summary>Statuses in which the vehicle is committed and cannot be sent elsewhere.</summary>
    public static readonly string[] Blocking =
    [
        Planned, Loading, InTransit, Delivered, Returning
    ];
}

/// <summary>
/// A vehicle available to move props and crew — owned or on contract.
///
/// <see cref="PayloadKg"/> and <see cref="CapacityCubicFeet"/> are the point of
/// keeping the fleet in the system at all: the props already carry a weight and
/// a packing unit, so a load can be checked against the truck before the truck
/// is at the godown door.
/// </summary>
public class Vehicle : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>The number on the plate. What the gate register records.</summary>
    public string RegistrationNumber { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string VehicleType { get; set; } = VehicleTypes.Tempo;
    public string Status { get; set; } = VehicleStatuses.Active;

    /// <summary>Hired in rather than owned. Set when it belongs to a transporter.</summary>
    public int? SupplierVendorId { get; set; }

    /* ---------------- what it can carry ---------------- */

    public decimal? PayloadKg { get; set; }
    public decimal? CapacityCubicFeet { get; set; }

    /// <summary>People it can seat, for a crew or a baraat bus.</summary>
    public int? PassengerSeats { get; set; }

    /* ---------------- running it ---------------- */

    public int? DefaultDriverCrewId { get; set; }

    /// <summary>What a day on the road costs, before fuel.</summary>
    public decimal? DayRate { get; set; }

    public decimal? RatePerKm { get; set; }

    /// <summary>Papers, so a vehicle with a lapsed permit is not dispatched.</summary>
    public DateOnly? InsuranceExpiry { get; set; }
    public DateOnly? PermitExpiry { get; set; }
    public DateOnly? PucExpiry { get; set; }
    public DateOnly? FitnessExpiry { get; set; }

    public DateOnly? LastServicedOn { get; set; }
    public int? OdometerKm { get; set; }

    public int? StoreId { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Vendor? SupplierVendor { get; set; }
    public CrewMember? DefaultDriver { get; set; }
    public PropStore? Store { get; set; }

    public bool IsBookable => Status == VehicleStatuses.Active;

    /// <summary>Whether any statutory paper has lapsed as of <paramref name="today"/>.</summary>
    public bool HasLapsedPapers(DateOnly today) =>
        (InsuranceExpiry is DateOnly i && i < today)
        || (PermitExpiry is DateOnly p && p < today)
        || (PucExpiry is DateOnly c && c < today)
        || (FitnessExpiry is DateOnly f && f < today);
}

/// <summary>
/// One run of one vehicle over a date window.
///
/// A trip can carry several gate passes — the same tempo drops décor at two
/// venues on the same morning — which is why the link to
/// <see cref="PropIssue"/> is a collection rather than a column. Availability
/// works exactly as it does for crew: an overlapping blocking trip means the
/// vehicle is spoken for.
/// </summary>
public class VehicleTrip : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>Quotable reference — <c>TR-2609-K3P9QF</c>.</summary>
    public string Code { get; set; } = string.Empty;

    public int VehicleId { get; set; }

    public string Status { get; set; } = TripStatuses.Planned;

    /// <summary>Outbound to the venue, or the return leg.</summary>
    public string Direction { get; set; } = "Outbound";

    public int? DriverCrewId { get; set; }

    /* ---------------- the window ---------------- */

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public TimeSpan? DepartureTime { get; set; }

    /* ---------------- the route ---------------- */

    public string? FromLocation { get; set; }
    public string? ToLocation { get; set; }

    public int? LeadId { get; set; }
    public int? ProjectId { get; set; }
    public string? EventName { get; set; }

    /* ---------------- the run ---------------- */

    public int? StartOdometerKm { get; set; }
    public int? EndOdometerKm { get; set; }

    public decimal? FuelCost { get; set; }
    public decimal? TollCost { get; set; }
    public decimal? OtherCost { get; set; }

    /// <summary>What was actually loaded, in kg. Checked against the vehicle's payload.</summary>
    public decimal? LoadedWeightKg { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Vehicle? Vehicle { get; set; }
    public CrewMember? Driver { get; set; }
    public Lead? Lead { get; set; }
    public Project? Project { get; set; }
    public ICollection<VehicleTripLoad> Loads { get; set; } = new List<VehicleTripLoad>();

    public bool IsBlocking => TripStatuses.Blocking.Contains(Status);

    public bool Overlaps(DateOnly from, DateOnly to) => FromDate <= to && ToDate >= from;

    public int? DistanceKm =>
        StartOdometerKm is int s && EndOdometerKm is int e && e >= s ? e - s : null;

    public decimal TotalRunningCost =>
        (FuelCost ?? 0) + (TollCost ?? 0) + (OtherCost ?? 0);
}

/// <summary>One gate pass riding on one trip.</summary>
public class VehicleTripLoad
{
    public int Id { get; set; }
    public int VehicleTripId { get; set; }
    public int PropIssueId { get; set; }

    /// <summary>Drop order along the route.</summary>
    public int SortOrder { get; set; }

    public VehicleTrip? Trip { get; set; }
    public PropIssue? Issue { get; set; }
}
