namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Agreement and registration
 * ------------------------------------------------------------------ */

/// <summary>
/// The paperwork that turns a booking into a legally sold flat.
///
/// One row per booking rather than a set of dates on the booking itself,
/// because this is a process with its own money — stamp duty and registration
/// fee are real amounts somebody collects and pays — and because the sequence
/// is what the customer keeps asking about. A dozen nullable date columns on
/// the booking would answer "when was it registered" and nothing else.
/// </summary>
public class BookingAgreement : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    /* ---------------- the ladder ---------------- */

    public DateTime? AllotmentLetterOn { get; set; }
    public DateTime? DraftSharedOn { get; set; }

    /// <summary>Stamping. In this market it is usually franked rather than papered.</summary>
    public DateTime? FrankedOn { get; set; }

    /// <summary>Signed by both sides.</summary>
    public DateTime? ExecutedOn { get; set; }

    /// <summary>Lodged and registered at the sub-registrar.</summary>
    public DateTime? RegisteredOn { get; set; }

    /* ---------------- what it cost ---------------- */

    /// <summary>
    /// The consideration the duty was computed on.
    ///
    /// Copied from the booking at drafting rather than read live: a later change
    /// to the booking must not silently restate what a registered document says.
    /// </summary>
    public decimal ConsiderationValue { get; set; }

    public decimal StampDuty { get; set; }
    public decimal RegistrationFee { get; set; }

    /// <summary>Paid by the customer directly, in most arrangements here.</summary>
    public bool PaidByCustomer { get; set; } = true;

    public string? RegistrationNumber { get; set; }
    public string? SubRegistrarOffice { get; set; }

    public string Status { get; set; } = AgreementStatuses.NotStarted;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
}

public static class AgreementStatuses
{
    public const string NotStarted = "NotStarted";
    public const string Drafted = "Drafted";
    public const string WithCustomer = "WithCustomer";
    public const string Franked = "Franked";
    public const string Executed = "Executed";
    public const string Registered = "Registered";

    public static readonly string[] All =
        [NotStarted, Drafted, WithCustomer, Franked, Executed, Registered];

    /// <summary>
    /// The event-contract wording. Stored values are unchanged — they are on
    /// every booking already, and two of them read differently for an event
    /// than for a sale deed: nothing is franked or registered, but a contract
    /// is countersigned and then locked.
    /// </summary>
    public static string Label(string status) => status switch
    {
        NotStarted => "Not started",
        Drafted => "Drafted",
        WithCustomer => "With client",
        Franked => "Signed by client",
        Executed => "Countersigned",
        Registered => "Locked",
        _ => status,
    };
}

/* ------------------------------------------------------------------ *
 * Home loan
 * ------------------------------------------------------------------ */

/// <summary>
/// A bank paying part of the price on the customer's behalf.
///
/// Worth tracking separately from receipts because the developer's collection
/// risk moves: once a loan is sanctioned and the tripartite is signed, the
/// remaining instalments arrive from a bank on construction progress rather
/// than from a person on a due date. A collections desk that cannot see which
/// bookings are bank-funded chases the wrong customers.
/// </summary>
public class HomeLoan : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    public string BankName { get; set; } = string.Empty;
    public string? BranchName { get; set; }
    public string? ApplicationNumber { get; set; }

    public DateTime? AppliedOn { get; set; }

    public decimal RequestedAmount { get; set; }
    public decimal SanctionedAmount { get; set; }
    public DateTime? SanctionedOn { get; set; }

    /// <summary>Sanction letters lapse. A stale one is a collection problem nobody has noticed yet.</summary>
    public DateTime? SanctionValidUntil { get; set; }

    /// <summary>
    /// The developer–bank–buyer agreement. Disbursement does not start without
    /// it, so its absence explains a stalled instalment more often than the
    /// customer does.
    /// </summary>
    public DateTime? TripartiteSignedOn { get; set; }

    /// <summary>Total released so far. Reconciled against the receipts marked as disbursements.</summary>
    public decimal DisbursedAmount { get; set; }

    public string Status { get; set; } = LoanStatuses.Applied;

    public string? RejectionReason { get; set; }
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }

    public decimal UndisbursedAmount => Math.Max(0, SanctionedAmount - DisbursedAmount);
}

public static class LoanStatuses
{
    public const string Applied = "Applied";
    public const string UnderProcess = "UnderProcess";
    public const string Sanctioned = "Sanctioned";
    public const string Disbursing = "Disbursing";
    public const string FullyDisbursed = "FullyDisbursed";
    public const string Rejected = "Rejected";
    public const string Withdrawn = "Withdrawn";

    public static readonly string[] All =
        [Applied, UnderProcess, Sanctioned, Disbursing, FullyDisbursed, Rejected, Withdrawn];

    public static string Label(string status) => status switch
    {
        UnderProcess => "Under process",
        FullyDisbursed => "Fully disbursed",
        _ => status,
    };
}

/* ------------------------------------------------------------------ *
 * Possession and handover
 * ------------------------------------------------------------------ */

/// <summary>
/// Giving the customer their keys.
///
/// The snag list is the part that decides whether a handover is remembered
/// well: a possession offered before the unit is ready produces a customer who
/// takes the keys and complains for a year. Counting raised against closed is
/// the smallest honest measure of readiness.
/// </summary>
public class Possession : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    /// <summary>What the agreement committed to. The date RERA holds the developer to.</summary>
    public DateTime? CommittedOn { get; set; }

    public DateTime? OfferedOn { get; set; }

    /// <summary>The customer walked the unit and listed what is wrong.</summary>
    public DateTime? InspectedOn { get; set; }

    public int SnagsRaised { get; set; }
    public int SnagsClosed { get; set; }

    public DateTime? FitOutFrom { get; set; }
    public DateTime? FitOutTo { get; set; }

    /* ---------------- what has to be settled first ---------------- */

    /// <summary>Advance maintenance, usually a fixed number of months.</summary>
    public int MaintenanceAdvanceMonths { get; set; }
    public decimal MaintenanceAmount { get; set; }
    public bool MaintenanceCollected { get; set; }

    /// <summary>The refundable deposit that transfers to the society on formation.</summary>
    public decimal CorpusDeposit { get; set; }
    public bool CorpusCollected { get; set; }

    public bool DuesCleared { get; set; }
    public bool DocumentsHandedOver { get; set; }

    public DateTime? HandedOverOn { get; set; }

    public string Status { get; set; } = PossessionStatuses.NotDue;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }

    public int SnagsOpen => Math.Max(0, SnagsRaised - SnagsClosed);

    /// <summary>Every gate a handover waits on, so the screen can say which one is shut.</summary>
    public bool ReadyToHandOver =>
        DuesCleared && MaintenanceCollected && SnagsOpen == 0;
}

/// <summary>
/// How far along delivery of the event itself is. Surfaced as <b>Delivery</b>.
///
/// Stored values are unchanged — they are on every booking already — and read
/// as the run-up an events desk actually works: the plan goes to the client,
/// the venue is walked, the open points are closed, the crew sets up, and the
/// event happens.
/// </summary>
public static class PossessionStatuses
{
    /// <summary>Too far out to plan. Surfaced as <i>Not started</i>.</summary>
    public const string NotDue = "NotDue";

    /// <summary>Run sheet sent to the client. Surfaced as <i>Plan shared</i>.</summary>
    public const string Offered = "Offered";

    /// <summary>Technical recce done. Surfaced as <i>Recce done</i>.</summary>
    public const string Inspected = "Inspected";

    /// <summary>Points still open against the plan. Surfaced as <i>Open points</i>.</summary>
    public const string SnagsOpen = "SnagsOpen";

    /// <summary>Crew and vendors confirmed. Surfaced as <i>Ready to set up</i>.</summary>
    public const string ReadyToHandOver = "ReadyToHandOver";

    /// <summary>The event happened. Surfaced as <i>Delivered</i>.</summary>
    public const string HandedOver = "HandedOver";

    public static readonly string[] All =
        [NotDue, Offered, Inspected, SnagsOpen, ReadyToHandOver, HandedOver];

    public static string Label(string status) => status switch
    {
        NotDue => "Not started",
        Offered => "Plan shared",
        Inspected => "Recce done",
        SnagsOpen => "Open points",
        ReadyToHandOver => "Ready to set up",
        HandedOver => "Delivered",
        _ => status,
    };
}

/* ------------------------------------------------------------------ *
 * Cancellation
 * ------------------------------------------------------------------ */

/// <summary>
/// A booking being unwound.
///
/// The deduction is the whole substance of it, and it is computed and recorded
/// rather than typed as a lump: a customer disputing a refund is disputing the
/// arithmetic, and "we kept 10% of the agreement value plus the brokerage
/// already paid" is an answer, while "we refunded ₹8,40,000" is not.
///
/// The unit is only released back to inventory when the cancellation is
/// approved, not when it is requested — a flat marked available on a request
/// nobody has agreed to is how two people get sold one home.
/// </summary>
public class BookingCancellation : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    public DateTime RequestedOn { get; set; } = DateTime.UtcNow;
    public string Reason { get; set; } = string.Empty;

    /// <summary>Everything the customer had paid in, gross of TDS.</summary>
    public decimal AmountReceived { get; set; }

    /// <summary>The forfeiture, as the agreement's cancellation clause words it.</summary>
    public decimal DeductionPercent { get; set; }
    public decimal DeductionAmount { get; set; }

    /// <summary>Brokerage already paid out, which the developer does not get back.</summary>
    public decimal BrokerageRecovered { get; set; }

    /// <summary>Interest and other charges retained.</summary>
    public decimal OtherDeductions { get; set; }

    public decimal RefundAmount { get; set; }

    public DateTime? ApprovedOn { get; set; }
    public int? ApprovedById { get; set; }

    public DateTime? RefundedOn { get; set; }
    public string? RefundReference { get; set; }

    public string Status { get; set; } = CancellationStatuses.Requested;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
}

public static class CancellationStatuses
{
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Refunded = "Refunded";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Requested, Approved, Refunded, Rejected];
}

/* ------------------------------------------------------------------ *
 * Transfer
 * ------------------------------------------------------------------ */

/// <summary>
/// The booking sold on to somebody else before possession.
///
/// Common enough in this market to need its own record: an investor exits, the
/// developer charges a transfer fee, and the new buyer inherits the schedule
/// and everything paid against it. Recorded rather than done by editing the
/// applicant, because the old buyer's payments have to stay attributable to
/// them for the rest of the file's life.
/// </summary>
public class BookingTransfer : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    public DateTime RequestedOn { get; set; } = DateTime.UtcNow;

    public string FromName { get; set; } = string.Empty;

    public string ToName { get; set; } = string.Empty;
    public string? ToPhone { get; set; }
    public string? ToEmail { get; set; }
    public string? ToPan { get; set; }
    public string? ToAddress { get; set; }

    /// <summary>Usually a percentage of the agreement value, sometimes a rate per square foot.</summary>
    public decimal TransferChargePercent { get; set; }
    public decimal TransferChargeAmount { get; set; }
    public decimal TransferChargeReceived { get; set; }

    public DateTime? ApprovedOn { get; set; }
    public int? ApprovedById { get; set; }
    public DateTime? CompletedOn { get; set; }

    public string Status { get; set; } = TransferStatuses.Requested;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
}

public static class TransferStatuses
{
    public const string Requested = "Requested";
    public const string Approved = "Approved";
    public const string Completed = "Completed";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Requested, Approved, Completed, Rejected];
}
