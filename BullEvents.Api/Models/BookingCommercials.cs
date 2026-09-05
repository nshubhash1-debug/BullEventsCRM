namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Brokerage
 * ------------------------------------------------------------------ */

/// <summary>
/// What a channel partner is owed on a booking, and what has actually been paid.
///
/// Slabbed rather than paid in one go, because that is how the market works: a
/// broker earns on booking, again at agreement, and the balance at a collection
/// threshold. Paying it all at booking is how a developer ends up chasing a
/// broker for a refund after a cancellation — which is also why
/// <see cref="BookingCancellation.BrokerageRecovered"/> exists.
///
/// TDS is withheld here too: brokerage is a professional payment, so 5% under
/// section 194-H comes off the gross and the partner is credited with the
/// whole.
/// </summary>
public class Brokerage : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    /// <summary>The channel partner. A user row, since partners hold seats in this CRM.</summary>
    public int? PartnerUserId { get; set; }

    /// <summary>Kept alongside the id — a partner leaving must not blank the payout history.</summary>
    public string PartnerName { get; set; } = string.Empty;
    public string? PartnerFirm { get; set; }

    /// <summary>What the rate applies to. Almost always the agreement value, not the grand total.</summary>
    public decimal BaseAmount { get; set; }

    public decimal RatePercent { get; set; }

    /// <summary>Gross entitlement across every slab.</summary>
    public decimal GrossAmount { get; set; }

    /// <summary>Released so far, gross of TDS.</summary>
    public decimal PaidAmount { get; set; }

    public decimal TdsWithheld { get; set; }

    /// <summary>Clawed back after a cancellation or a transfer.</summary>
    public decimal RecoveredAmount { get; set; }

    public string Status { get; set; } = BrokerageStatuses.Accrued;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
    public ICollection<BrokerageSlab> Slabs { get; set; } = new List<BrokerageSlab>();

    public decimal PayableNow =>
        Math.Max(0, Slabs.Where(s => s.IsEarned).Sum(s => s.Amount) - PaidAmount);

    public decimal OutstandingEntitlement =>
        Math.Max(0, GrossAmount - PaidAmount - RecoveredAmount);
}

/// <summary>One tranche of a brokerage, and the condition that releases it.</summary>
public class BrokerageSlab : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BrokerageId { get; set; }

    public int SortOrder { get; set; }
    public string Label { get; set; } = string.Empty;

    public decimal SharePercent { get; set; }
    public decimal Amount { get; set; }

    /// <summary>A key from <see cref="BrokerageTriggers"/>.</summary>
    public string Trigger { get; set; } = BrokerageTriggers.OnBooking;

    /// <summary>For a collection trigger: the share of the total that must be in first.</summary>
    public decimal CollectionThresholdPercent { get; set; }

    /// <summary>Set by the payout service when the trigger is satisfied.</summary>
    public bool IsEarned { get; set; }
    public DateTime? EarnedOn { get; set; }

    public DateTime? PaidOn { get; set; }
    public string? PaymentReference { get; set; }

    public Brokerage? Brokerage { get; set; }
}

public static class BrokerageTriggers
{
    public const string OnBooking = "OnBooking";
    public const string OnAgreement = "OnAgreement";
    public const string OnRegistration = "OnRegistration";

    /// <summary>Released once collections cross a share of the total.</summary>
    public const string OnCollection = "OnCollection";

    public const string OnPossession = "OnPossession";

    public static readonly string[] All =
        [OnBooking, OnAgreement, OnRegistration, OnCollection, OnPossession];

    public static string Label(string trigger) => trigger switch
    {
        OnBooking => "On booking",
        OnAgreement => "On agreement",
        OnRegistration => "On registration",
        OnCollection => "On collection",
        OnPossession => "On possession",
        _ => trigger,
    };
}

public static class BrokerageStatuses
{
    /// <summary>Earned in principle; nothing released.</summary>
    public const string Accrued = "Accrued";

    public const string PartlyPaid = "PartlyPaid";
    public const string Paid = "Paid";

    /// <summary>The booking went away and the money is being clawed back.</summary>
    public const string Recovering = "Recovering";

    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Accrued, PartlyPaid, Paid, Recovering, Cancelled];
}

/* ------------------------------------------------------------------ *
 * Document control
 * ------------------------------------------------------------------ */

/// <summary>
/// A document a booking is supposed to have.
///
/// The checklist is generated per booking from a catalogue rather than left to
/// whoever is handling the file, because the answer to "can we register on
/// Tuesday" is a list of missing papers and nobody can hold it in their head
/// across two hundred bookings. Which documents are required depends on the
/// booking — a bank-funded purchase needs the sanction letter, an NRI buyer
/// needs different KYC — so requirement is a property of the row, not of the
/// catalogue.
/// </summary>
public class BookingDocument : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BookingId { get; set; }

    /// <summary>A key from <see cref="DocumentCatalog"/>.</summary>
    public string Key { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string Stage { get; set; } = DocumentStages.Booking;

    public bool IsRequired { get; set; } = true;

    /// <summary>Which applicant it belongs to, for the per-person KYC papers.</summary>
    public int? ApplicantId { get; set; }

    public string Status { get; set; } = DocumentStatuses.Pending;

    public DateTime? ReceivedOn { get; set; }
    public DateTime? VerifiedOn { get; set; }
    public int? VerifiedById { get; set; }

    /// <summary>
    /// Where the file lives.
    ///
    /// A reference rather than the bytes: a scanned agreement is fifteen
    /// megabytes and a database is the wrong place for two hundred of them.
    /// </summary>
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }

    /// <summary>For the papers that lapse — a sanction letter, an NOC.</summary>
    public DateTime? ExpiresOn { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
}

public static class DocumentStages
{
    public const string Booking = "Booking";
    public const string Kyc = "KYC";
    public const string Loan = "Loan";
    public const string Agreement = "Agreement";
    public const string Registration = "Registration";
    public const string Possession = "Possession";

    public static readonly string[] All =
        [Booking, Kyc, Loan, Agreement, Registration, Possession];
}

public static class DocumentStatuses
{
    public const string Pending = "Pending";
    public const string Received = "Received";
    public const string Verified = "Verified";

    /// <summary>Received, looked at, and sent back — the wrong page, an unsigned copy.</summary>
    public const string Rejected = "Rejected";

    /// <summary>Genuinely not applicable to this booking, marked so rather than left pending forever.</summary>
    public const string NotApplicable = "NotApplicable";

    public static readonly string[] All = [Pending, Received, Verified, Rejected, NotApplicable];

    public static string Label(string status) =>
        status == NotApplicable ? "Not applicable" : status;
}

/// <summary>What a booking's file is expected to contain, by stage.</summary>
/// <summary>
/// One line on the checklist of papers to <em>collect</em> from a customer.
///
/// Named apart from <see cref="DocumentTemplate"/>, which is the opposite
/// thing: a letter this system <em>produces</em> and sends out.
/// </summary>
public record ChecklistDocument(
    string Key,
    string Name,
    string Stage,
    /// <summary>One per applicant rather than one per booking.</summary>
    bool PerApplicant = false,
    /// <summary>Only when the booking is bank-funded.</summary>
    bool LoanOnly = false);

public static class DocumentCatalog
{
    public static readonly ChecklistDocument[] All =
    [
        new("application-form", "Application form", DocumentStages.Booking),
        new("booking-receipt", "Booking amount receipt", DocumentStages.Booking),
        new("cost-sheet", "Signed cost sheet", DocumentStages.Booking),
        new("allotment-letter", "Allotment letter", DocumentStages.Booking),

        new("pan", "PAN card", DocumentStages.Kyc, PerApplicant: true),
        new("aadhaar", "Aadhaar", DocumentStages.Kyc, PerApplicant: true),
        new("photo", "Passport photograph", DocumentStages.Kyc, PerApplicant: true),
        new("address-proof", "Address proof", DocumentStages.Kyc, PerApplicant: true),
        new("bank-details", "Cancelled cheque", DocumentStages.Kyc),

        new("loan-sanction", "Loan sanction letter", DocumentStages.Loan, LoanOnly: true),
        new("tripartite", "Tripartite agreement", DocumentStages.Loan, LoanOnly: true),
        new("noc-bank", "NOC to the bank", DocumentStages.Loan, LoanOnly: true),

        new("agreement-draft", "Agreement draft", DocumentStages.Agreement),
        new("agreement-executed", "Executed agreement", DocumentStages.Agreement),
        new("stamp-receipt", "Franking / stamp duty receipt", DocumentStages.Agreement),

        new("registered-deed", "Registered agreement", DocumentStages.Registration),
        new("index-ii", "Index II", DocumentStages.Registration),
        new("tds-challan", "TDS challan (Form 26QB)", DocumentStages.Registration),

        new("possession-letter", "Possession letter", DocumentStages.Possession),
        new("snag-signoff", "Snag list sign-off", DocumentStages.Possession),
        new("maintenance-receipt", "Maintenance advance receipt", DocumentStages.Possession),
        new("handover-checklist", "Handover checklist", DocumentStages.Possession),
    ];
}

/* ------------------------------------------------------------------ *
 * RERA escrow
 * ------------------------------------------------------------------ */

/// <summary>
/// Where a receipt went, for the seventy-per-cent rule.
///
/// RERA obliges a developer to keep 70% of what buyers pay in a designated
/// account, spendable only on that project's construction and land cost. The
/// obligation is per project and per receipt, so the split has to be recorded
/// as the money arrives — reconstructing it at audit from bank statements is
/// the work this row exists to avoid.
///
/// This tracks the developer's own position. It is a compliance record, not an
/// instruction to a bank.
/// </summary>
public class EscrowEntry : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? ProjectId { get; set; }
    public int BookingId { get; set; }
    public int ReceiptId { get; set; }

    public DateTime On { get; set; } = DateTime.UtcNow;

    public decimal ReceiptAmount { get; set; }

    /// <summary>The share that must sit in the designated account. 70% by default.</summary>
    public decimal DesignatedPercent { get; set; } = 70m;

    public decimal DesignatedAmount { get; set; }

    /// <summary>The rest, free for any purpose.</summary>
    public decimal FreeAmount { get; set; }

    /// <summary>True once the developer's own bank transfer has been confirmed.</summary>
    public bool Transferred { get; set; }
    public DateTime? TransferredOn { get; set; }
    public string? TransferReference { get; set; }

    public Receipt? Receipt { get; set; }
}
