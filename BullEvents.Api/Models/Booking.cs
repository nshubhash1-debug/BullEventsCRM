namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Booking
 * ------------------------------------------------------------------ */

/// <summary>
/// A unit that has been sold.
///
/// The point at which a deal stops being a pipeline record and becomes an
/// account with money running through it. Everything post-sales hangs off this:
/// the payment schedule, the demands raised against it, the receipts that
/// answer them, the agreement, the loan, and eventually the keys.
///
/// The commercial figures are <em>snapshotted</em> from the quotation rather
/// than read through it. A quotation is a live document that gets revised; a
/// booking is what was agreed on a date, and the agreement value on it is the
/// figure that goes on the registered document. If the price later changes,
/// that is a new event with its own trail — not a number that quietly moves.
/// </summary>
public class Booking : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    /// <summary>Human-facing and unique per company — the number a customer quotes on the phone.</summary>
    public string BookingNumber { get; set; } = string.Empty;

    /* ---------------- where it came from ---------------- */

    public int? QuotationId { get; set; }
    public int? LeadId { get; set; }
    public int? ContactId { get; set; }

    /* ---------------- what was sold ---------------- */

    public int UnitId { get; set; }
    public int? ProjectId { get; set; }

    /// <summary>
    /// Snapshotted alongside the id.
    ///
    /// A tower gets renamed, a unit gets renumbered during a re-plan, and the
    /// allotment letter already went out with the old label. What the customer
    /// holds has to keep resolving.
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;
    public string? TowerName { get; set; }
    public string UnitNumber { get; set; } = string.Empty;
    public string? Configuration { get; set; }
    public decimal SaleableArea { get; set; }
    public string AreaUnit { get; set; } = "sqft";

    /* ---------------- what the contract is for ---------------- */
    //
    // Snapshotted like the venue's name, and for a stronger reason: the event
    // date is the whole subject of the contract. If it moves, that is a
    // renegotiation — a new date, possibly a new price — not a field the
    // system should silently follow from the lead.

    public string? EventType { get; set; }

    /// <summary>The contracted date. Drives every pre-event payment milestone.</summary>
    public DateTime? EventDate { get; set; }

    /// <summary>Last day, for a multi-day run.</summary>
    public DateTime? EventEndDate { get; set; }

    public string? EventSlot { get; set; }

    /// <summary>Comma-separated functions this contract covers.</summary>
    public string? Functions { get; set; }

    /// <summary>Guests contracted for.</summary>
    public int GuestCount { get; set; }

    /// <summary>The plate floor the catering was priced on.</summary>
    public int MinimumPlates { get; set; }

    /// <summary>
    /// Guests who actually came, filled in after the event.
    ///
    /// The gap between this and <see cref="GuestCount"/> is what the final
    /// settlement invoice is struck on — an event bills on actuals above the
    /// contracted count, and never below the minimum guarantee.
    /// </summary>
    public int? FinalGuestCount { get; set; }

    /* ---------------- the money, as agreed ---------------- */

    /// <summary>
    /// The registrable consideration — basic plus PLC and floor rise.
    ///
    /// Kept apart from the other charges because this is the figure stamp duty
    /// and registration are computed on, and the one the sale deed carries.
    /// Lumping the club fee into it overpays stamp duty on every booking.
    /// </summary>
    public decimal AgreementValue { get; set; }

    /// <summary>Everything outside the agreement: parking, club, maintenance deposit, legal.</summary>
    public decimal OtherCharges { get; set; }

    public decimal TaxAmount { get; set; }

    /// <summary>Agreement value plus other charges plus tax. What the customer owes in total.</summary>
    public decimal GrandTotal { get; set; }

    public int? PaymentPlanId { get; set; }
    public string? PaymentPlanName { get; set; }

    /* ---------------- the money, as it stands ---------------- */
    //
    // Denormalised and recomputed by the ledger rather than trusted: these drive
    // every list and dashboard in post-sales, and recomputing them per row on a
    // thousand-booking project would make the collections screen unusable. The
    // ledger is the authority; these are its cache.

    /// <summary>Total of every demand raised so far.</summary>
    public decimal Demanded { get; set; }

    /// <summary>Total cleared receipts. Bounced and pending instruments do not count.</summary>
    public decimal Received { get; set; }

    /// <summary>Penal interest charged on late instalments.</summary>
    public decimal InterestCharged { get; set; }

    /// <summary>Interest written off, with a reason, by somebody who may.</summary>
    public decimal InterestWaived { get; set; }

    /// <summary>Demanded plus unwaived interest, less received. What is due today.</summary>
    public decimal Outstanding { get; set; }

    /// <summary>The oldest unpaid demand's age in days. Drives the ageing buckets.</summary>
    public int OverdueDays { get; set; }

    /* ---------------- lifecycle ---------------- */

    public string Status { get; set; } = BookingStatuses.Booked;

    public DateTime BookingDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// The relationship manager. Not the sales rep who closed it — post-sales is
    /// a different desk, and the person chasing a demand is rarely the person
    /// who negotiated the discount.
    /// </summary>
    public int? OwnerId { get; set; }

    public string? Notes { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Branch? Branch { get; set; }
    public Unit? Unit { get; set; }
    public User? Owner { get; set; }

    public ICollection<BookingApplicant> Applicants { get; set; } = new List<BookingApplicant>();
    public ICollection<BookingMilestone> Milestones { get; set; } = new List<BookingMilestone>();
    public ICollection<Demand> Demands { get; set; } = new List<Demand>();
    public ICollection<Receipt> Receipts { get; set; } = new List<Receipt>();
}

public static class BookingStatuses
{
    /// <summary>Token taken, paperwork not started.</summary>
    public const string Booked = "Booked";

    /// <summary>Allotment letter issued.</summary>
    public const string Allotted = "Allotted";

    /// <summary>Agreement drafted and with the customer.</summary>
    public const string AgreementPending = "AgreementPending";

    public const string AgreementExecuted = "AgreementExecuted";
    public const string Registered = "Registered";

    /// <summary>Possession offered; the customer has not taken it yet.</summary>
    public const string PossessionOffered = "PossessionOffered";

    public const string HandedOver = "HandedOver";
    public const string Cancelled = "Cancelled";

    /// <summary>Sold on to somebody else before possession.</summary>
    public const string Transferred = "Transferred";

    public static readonly string[] All =
    [
        Booked, Allotted, AgreementPending, AgreementExecuted, Registered,
        PossessionOffered, HandedOver, Cancelled, Transferred,
    ];

    /// <summary>The ones where money is still expected.</summary>
    public static readonly string[] Live =
    [
        Booked, Allotted, AgreementPending, AgreementExecuted, Registered, PossessionOffered,
    ];

    public static string Label(string status) => status switch
    {
        AgreementPending => "Agreement pending",
        AgreementExecuted => "Agreement executed",
        PossessionOffered => "Possession offered",
        HandedOver => "Handed over",
        _ => status,
    };
}

/* ------------------------------------------------------------------ *
 * Applicants
 * ------------------------------------------------------------------ */

/// <summary>
/// A person on the booking.
///
/// One row per applicant rather than columns on the booking, because a joint
/// purchase is the norm rather than the exception here and a second set of
/// name/PAN/address columns runs out at the third buyer. The nominee sits in
/// the same table for the same reason — the paperwork treats them alike.
/// </summary>
public class BookingApplicant : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    public string Role { get; set; } = ApplicantRoles.Primary;
    public int SortOrder { get; set; }

    public string? Salutation { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>Son of, wife of — as the agreement writes it.</summary>
    public string? Relation { get; set; }

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Required over ₹50 lakh for the 1% TDS, and required by the registrar
    /// either way. Held in clear because it is printed on the agreement.
    /// </summary>
    public string? Pan { get; set; }

    /// <summary>
    /// Stored as the last four digits only.
    ///
    /// A full Aadhaar number in a CRM is a liability with no matching use: the
    /// registrar sees the physical document, and everything this system needs it
    /// for — telling two applicants apart on a list — the last four do.
    /// </summary>
    public string? AadhaarLast4 { get; set; }

    public string KycStatus { get; set; } = KycStatuses.Pending;
    public DateTime? KycVerifiedOn { get; set; }

    public Booking? Booking { get; set; }
}

public static class ApplicantRoles
{
    public const string Primary = "Primary";
    public const string CoApplicant = "CoApplicant";
    public const string Nominee = "Nominee";
    public const string PowerOfAttorney = "PowerOfAttorney";

    /// <summary>
    /// Someone who held this booking before it was transferred.
    ///
    /// Kept on the record rather than overwritten. Their payments have to stay
    /// attributable to them for the rest of the file's life — a TDS certificate
    /// issued three years ago names a person, and that person has to still be
    /// findable here.
    /// </summary>
    public const string PreviousOwner = "PreviousOwner";

    public static readonly string[] All =
        [Primary, CoApplicant, Nominee, PowerOfAttorney, PreviousOwner];
}

public static class KycStatuses
{
    public const string Pending = "Pending";
    public const string Submitted = "Submitted";
    public const string Verified = "Verified";

    public static readonly string[] All = [Pending, Submitted, Verified];
}

/* ------------------------------------------------------------------ *
 * The payment schedule
 * ------------------------------------------------------------------ */

/// <summary>
/// One instalment on this booking's plan.
///
/// Copied from the quotation at booking rather than referenced, for the same
/// reason the totals are: the plan on the quotation can be re-priced, and a
/// schedule that moved underneath a customer who has already paid two
/// instalments against it is the single fastest way to lose their trust.
/// </summary>
public class BookingMilestone : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    public int SortOrder { get; set; }
    public string Label { get; set; } = string.Empty;

    public decimal Percent { get; set; }
    public decimal BasicAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// The construction stage this instalment waits for, on a linked plan.
    ///
    /// A construction-linked instalment must not be demanded because a date
    /// passed — it is demanded because the slab was cast. The date on it is
    /// indicative until then.
    /// </summary>
    public string? ConstructionStage { get; set; }

    public DateTime? DueDate { get; set; }

    public string Status { get; set; } = MilestoneStatuses.Pending;

    /// <summary>The demand raised for it, once one has been.</summary>
    public int? DemandId { get; set; }

    public Booking? Booking { get; set; }
}

public static class MilestoneStatuses
{
    /// <summary>Waiting for its date or its construction stage.</summary>
    public const string Pending = "Pending";

    public const string Demanded = "Demanded";
    public const string PartlyPaid = "PartlyPaid";
    public const string Paid = "Paid";

    /// <summary>Written off — a negotiated waiver, recorded rather than deleted.</summary>
    public const string Waived = "Waived";

    public static readonly string[] All = [Pending, Demanded, PartlyPaid, Paid, Waived];
}

/* ------------------------------------------------------------------ *
 * Demands
 * ------------------------------------------------------------------ */

/// <summary>
/// A bill raised against the customer.
///
/// Separate from the milestone it came from because the two answer different
/// questions. The milestone is what the plan says; the demand is what was
/// actually asked for, on a date, for an amount, with a due date the penal
/// interest clock runs from. A milestone can be demanded late, demanded in
/// part, or re-demanded after a cancellation, and the schedule should not have
/// to carry any of that.
/// </summary>
public class Demand : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }
    public int? BookingMilestoneId { get; set; }

    public string DemandNumber { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public DateTime RaisedOn { get; set; } = DateTime.UtcNow;

    /// <summary>When it turns overdue and interest starts running.</summary>
    public DateTime DueDate { get; set; }

    public decimal BasicAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    /// <summary>Cleared receipts allocated to this demand.</summary>
    public decimal Received { get; set; }

    /// <summary>
    /// Penal interest accrued on the unpaid balance since the due date.
    ///
    /// Recomputed rather than accumulated, so a back-dated receipt corrects it
    /// instead of leaving an interest charge for days the money was already in.
    /// </summary>
    public decimal InterestAccrued { get; set; }

    /// <summary>Penal interest written off. A decision, not an outcome.</summary>
    public decimal InterestWaived { get; set; }

    /// <summary>
    /// Penal interest the customer actually paid, taken from the allocations.
    ///
    /// Separate from <see cref="InterestWaived"/> because the two are opposite
    /// facts: one is money collected, the other is money forgiven. Folding the
    /// first into the second — as this did — inflates the write-off a finance
    /// team reconciles and reports interest income as a concession.
    /// </summary>
    public decimal InterestReceived { get; set; }

    /// <summary>Annual penal rate. Typically 12–18% in this market.</summary>
    public decimal InterestRatePercent { get; set; } = 12m;

    public string Status { get; set; } = DemandStatuses.Raised;

    public DateTime? CancelledOn { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }

    /// <summary>Principal and tax still owed, ignoring interest.</summary>
    public decimal Outstanding => Math.Max(0, TotalAmount - Received);

    /// <summary>Interest still owed after anything paid or written off.</summary>
    public decimal InterestDue =>
        Math.Max(0, InterestAccrued - InterestWaived - InterestReceived);
}

public static class DemandStatuses
{
    public const string Raised = "Raised";
    public const string PartlyPaid = "PartlyPaid";
    public const string Paid = "Paid";

    /// <summary>Past its due date with a balance. Set by the ledger, not by hand.</summary>
    public const string Overdue = "Overdue";

    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Raised, PartlyPaid, Paid, Overdue, Cancelled];
}

/* ------------------------------------------------------------------ *
 * Receipts
 * ------------------------------------------------------------------ */

/// <summary>
/// Money in.
///
/// A receipt is recorded against the booking rather than against a demand,
/// because that is how it arrives: a customer transfers a round figure that
/// clears two instalments and part of a third. Which demands it answers is the
/// allocation, and it is stored separately so it can be redone when a cheque
/// bounces without losing the record that the cheque was taken.
/// </summary>
public class Receipt : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    public string ReceiptNumber { get; set; } = string.Empty;

    public DateTime ReceivedOn { get; set; } = DateTime.UtcNow;

    /// <summary>The gross figure on the instrument, before TDS.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Tax deducted at source by the buyer — 1% under section 194-IA on a
    /// consideration over ₹50 lakh.
    ///
    /// Tracked because the developer receives the net but must credit the buyer
    /// with the gross: a ledger that only knew the net would show every such
    /// buyer permanently 1% short.
    /// </summary>
    public decimal TdsAmount { get; set; }

    public string Mode { get; set; } = PaymentModes.Neft;

    /// <summary>Cheque number, UTR, or the transaction reference.</summary>
    public string? Instrument { get; set; }

    public string? BankName { get; set; }

    /// <summary>The instrument's own date, which is not always the day it was handed over.</summary>
    public DateTime? InstrumentDate { get; set; }

    public string Status { get; set; } = ReceiptStatuses.Cleared;

    public DateTime? ClearedOn { get; set; }
    public DateTime? BouncedOn { get; set; }
    public string? BounceReason { get; set; }

    /// <summary>Not yet applied to a demand — an advance, or money taken before the next bill.</summary>
    public decimal Unallocated { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
    public ICollection<ReceiptAllocation> Allocations { get; set; } = new List<ReceiptAllocation>();

    /// <summary>The gross credited to the customer: what they paid plus what they withheld as TDS.</summary>
    public decimal CreditedAmount => Amount + TdsAmount;
}

public static class PaymentModes
{
    public const string Neft = "NEFT";
    public const string Rtgs = "RTGS";
    public const string Imps = "IMPS";
    public const string Upi = "UPI";
    public const string Cheque = "Cheque";
    public const string DemandDraft = "DD";
    public const string Cash = "Cash";

    /// <summary>Paid by the bank on the customer's behalf, against a sanctioned loan.</summary>
    public const string LoanDisbursement = "LoanDisbursement";

    public static readonly string[] All =
        [Neft, Rtgs, Imps, Upi, Cheque, DemandDraft, Cash, LoanDisbursement];
}

public static class ReceiptStatuses
{
    /// <summary>Taken but not yet in the bank — an uncleared cheque.</summary>
    public const string Pending = "Pending";

    public const string Cleared = "Cleared";
    public const string Bounced = "Bounced";

    public static readonly string[] All = [Pending, Cleared, Bounced];
}

/// <summary>
/// How much of one receipt answered one demand.
///
/// The join is what makes a customer ledger reconstructable: without it, a
/// booking knows what came in and what was billed but not which paid which, and
/// the first dispute about a part-payment cannot be settled.
/// </summary>
public class ReceiptAllocation : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ReceiptId { get; set; }
    public int DemandId { get; set; }

    /// <summary>Applied to the demand's principal and tax.</summary>
    public decimal Amount { get; set; }

    /// <summary>Applied to penal interest on that demand.</summary>
    public decimal TowardsInterest { get; set; }

    public Receipt? Receipt { get; set; }
    public Demand? Demand { get; set; }
}
