namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * GST
 * ------------------------------------------------------------------ */

/// <summary>
/// How a charge is taxed.
///
/// Under-construction residential in India is taxed at 5% without input credit,
/// or 1% for an affordable unit. Land is excluded — the statutory abatement is
/// one third of the consideration — and several of the charges a developer
/// bills alongside the flat are taxed at 18% instead. Getting this wrong is not
/// a rounding error: it is an under-collection the developer eats, or an
/// over-collection a customer can recover with interest.
/// </summary>
public static class GstTreatments
{
    /// <summary>5% on two-thirds of the value, no input credit. The normal flat.</summary>
    public const string ResidentialUnderConstruction = "ResidentialUnderConstruction";

    /// <summary>1% on two-thirds. Affordable housing as the notification defines it.</summary>
    public const string Affordable = "Affordable";

    /// <summary>18% on the whole value — club, parking hire, most deposits and fees.</summary>
    public const string Standard = "Standard";

    /// <summary>
    /// Outside GST. A completed unit sold after the occupancy certificate is a
    /// sale of immovable property, and the statutory deposits pass through.
    /// </summary>
    public const string Exempt = "Exempt";

    public static readonly string[] All =
        [ResidentialUnderConstruction, Affordable, Standard, Exempt];

    /// <summary>The rate, as a percentage of the taxable value.</summary>
    public static decimal Rate(string treatment) => treatment switch
    {
        ResidentialUnderConstruction => 5m,
        Affordable => 1m,
        Standard => 18m,
        _ => 0m,
    };

    /// <summary>
    /// The share of the consideration that is taxable.
    ///
    /// One third is deemed to be the value of the land and is not taxed, so the
    /// 5% is charged on two thirds — an effective 3.33% of the whole. Writing it
    /// as an abatement rather than baking 3.33% into a rate keeps the invoice
    /// showing the figures the law actually names.
    /// </summary>
    public static decimal TaxableFraction(string treatment) => treatment switch
    {
        ResidentialUnderConstruction or Affordable => 2m / 3m,
        Standard => 1m,
        _ => 0m,
    };

    public static string Label(string treatment) => treatment switch
    {
        ResidentialUnderConstruction => "Residential, under construction — 5%",
        Affordable => "Affordable housing — 1%",
        Standard => "Standard — 18%",
        Exempt => "Outside GST",
        _ => treatment,
    };
}

/* ------------------------------------------------------------------ *
 * Tax invoice
 * ------------------------------------------------------------------ */

public static class InvoiceStatuses
{
    public const string Draft = "Draft";
    public const string Issued = "Issued";

    /// <summary>Reversed by a credit note rather than deleted. An issued invoice never vanishes.</summary>
    public const string Credited = "Credited";

    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Draft, Issued, Credited, Cancelled];
}

/// <summary>
/// The tax invoice raised against a demand.
///
/// Separate from the demand on purpose. A demand is a request for money under
/// the agreement; an invoice is a statutory document with its own unbroken
/// number series, its own date, and a tax breakup the customer will claim
/// against. They usually go out together and are still not the same thing —
/// a demand can be revised, a raised invoice can only be credited.
///
/// The place of supply is always the project's state, because immovable
/// property is supplied where it stands. That is why the split is CGST and SGST
/// for every buyer, wherever they live, and IGST essentially never arises.
/// </summary>
public class TaxInvoice : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int BookingId { get; set; }

    /// <summary>The demand this bills. One invoice per demand.</summary>
    public int DemandId { get; set; }

    /// <summary>Unbroken per company and financial year — the law requires it.</summary>
    public string InvoiceNumber { get; set; } = string.Empty;

    public DateTime InvoiceDate { get; set; } = DateTime.UtcNow.Date;

    public string Treatment { get; set; } = GstTreatments.ResidentialUnderConstruction;

    /// <summary>Construction services. 9954 for the flat itself.</summary>
    public string SacCode { get; set; } = "9954";

    /* ---------------- the figures ---------------- */

    /// <summary>What is being billed, before any abatement.</summary>
    public decimal GrossValue { get; set; }

    /// <summary>The deemed land value, excluded from tax.</summary>
    public decimal LandAbatement { get; set; }

    public decimal TaxableValue { get; set; }
    public decimal GstRate { get; set; }

    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }

    /// <summary>Only for an inter-state supply, which for immovable property does not arise.</summary>
    public decimal IgstAmount { get; set; }

    public decimal TotalTax => CgstAmount + SgstAmount + IgstAmount;
    public decimal InvoiceTotal => GrossValue + TotalTax;

    /* ---------------- the parties ---------------- */

    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerGstin { get; set; }
    public string? CustomerAddress { get; set; }
    public string? CustomerPan { get; set; }

    /// <summary>Where the property stands, which is the place of supply.</summary>
    public string? PlaceOfSupply { get; set; }

    public string Status { get; set; } = InvoiceStatuses.Draft;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
    public Demand? Demand { get; set; }
}

/* ------------------------------------------------------------------ *
 * Credit note
 * ------------------------------------------------------------------ */

public static class CreditReasons
{
    public const string Cancellation = "Cancellation";
    public const string AreaReduction = "AreaReduction";
    public const string RateRevision = "RateRevision";
    public const string InterestWaiver = "InterestWaiver";
    public const string BillingError = "BillingError";
    public const string Rebate = "Rebate";

    public static readonly string[] All =
        [Cancellation, AreaReduction, RateRevision, InterestWaiver, BillingError, Rebate];

    public static string Label(string reason) => reason switch
    {
        AreaReduction => "Area reduced on re-measurement",
        RateRevision => "Rate revised",
        InterestWaiver => "Interest waived",
        BillingError => "Billing error",
        _ => reason,
    };
}

/// <summary>
/// Money taken back off an issued invoice.
///
/// An issued tax invoice is never edited or deleted — the number has been
/// reported and the customer may already have claimed against it. A reduction
/// is a new document pointing at the old one, which is both what the law
/// requires and what makes the history readable three years later.
/// </summary>
public class CreditNote : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int BookingId { get; set; }

    /// <summary>The invoice being reduced. Null for a credit against no single invoice.</summary>
    public int? TaxInvoiceId { get; set; }

    public string CreditNoteNumber { get; set; } = string.Empty;
    public DateTime IssuedOn { get; set; } = DateTime.UtcNow.Date;

    public string Reason { get; set; } = CreditReasons.BillingError;
    public string? Narrative { get; set; }

    public decimal GrossValue { get; set; }
    public decimal TaxableValue { get; set; }
    public decimal GstRate { get; set; }
    public decimal CgstAmount { get; set; }
    public decimal SgstAmount { get; set; }

    public decimal TotalTax => CgstAmount + SgstAmount;
    public decimal CreditTotal => GrossValue + TotalTax;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
    public TaxInvoice? TaxInvoice { get; set; }
}

/* ------------------------------------------------------------------ *
 * Post-dated cheques
 * ------------------------------------------------------------------ */

public static class PdcStatuses
{
    /// <summary>In the drawer, not yet due.</summary>
    public const string Held = "Held";

    /// <summary>Sent to the bank.</summary>
    public const string Deposited = "Deposited";

    public const string Cleared = "Cleared";
    public const string Bounced = "Bounced";

    /// <summary>Handed back to the customer, usually because they paid another way.</summary>
    public const string Returned = "Returned";

    public static readonly string[] All = [Held, Deposited, Cleared, Bounced, Returned];
}

/// <summary>
/// A cheque dated for the future, sitting in a drawer.
///
/// Worth its own register because it is money the developer has been promised
/// but cannot see: a cheque banked a week late is a week of cash flow lost, and
/// a cheque nobody banked at all is a collection everybody believed had
/// happened. The register answers "what is due for banking this week", which is
/// a question no ledger built on receipts can answer — the receipt does not
/// exist until the cheque clears.
/// </summary>
public class PostDatedCheque : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int BookingId { get; set; }

    /// <summary>What it is meant to answer, when it is earmarked.</summary>
    public int? DemandId { get; set; }

    public string ChequeNumber { get; set; } = string.Empty;
    public string BankName { get; set; } = string.Empty;
    public string? BranchName { get; set; }

    public decimal Amount { get; set; }

    /// <summary>The date on the face of it. Not bankable before this.</summary>
    public DateTime ChequeDate { get; set; }

    public DateTime ReceivedOn { get; set; } = DateTime.UtcNow.Date;

    public DateTime? DepositedOn { get; set; }
    public DateTime? ClearedOn { get; set; }
    public DateTime? BouncedOn { get; set; }
    public string? BounceReason { get; set; }

    /// <summary>The receipt raised when it cleared, so the two are traceable to each other.</summary>
    public int? ReceiptId { get; set; }

    public string Status { get; set; } = PdcStatuses.Held;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }

    /// <summary>Bankable today, and nobody has banked it.</summary>
    public bool DueForBanking(DateTime today) =>
        Status == PdcStatuses.Held && ChequeDate.Date <= today.Date;
}

/* ------------------------------------------------------------------ *
 * TDS certificates
 * ------------------------------------------------------------------ */

/// <summary>
/// The Form 16B a buyer owes the developer.
///
/// Under Section 194-IA the buyer deducts 1% and pays it to the government, and
/// the developer credits them with the gross amount on trust. That trust is
/// only settled when the certificate arrives — until it does, the developer has
/// given credit for money it cannot prove was paid, and cannot claim it against
/// its own liability.
///
/// A developer with two hundred bookings and no register of these is carrying a
/// number it will discover at assessment.
/// </summary>
public class TdsCertificate : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int BookingId { get; set; }

    /// <summary>The receipt whose TDS this covers.</summary>
    public int ReceiptId { get; set; }

    /// <summary>Deductor is the buyer here, which surprises people every time.</summary>
    public string? DeductorPan { get; set; }
    public string? DeductorName { get; set; }

    public decimal AmountPaid { get; set; }
    public decimal TdsAmount { get; set; }

    /// <summary>The assessment quarter — "Q2 2026-27".</summary>
    public string? Quarter { get; set; }

    public string? CertificateNumber { get; set; }
    public DateTime? CertificateDate { get; set; }

    /// <summary>The challan the buyer filed. Form 26QB's acknowledgement.</summary>
    public string? ChallanNumber { get; set; }

    public DateTime? ReceivedOn { get; set; }
    public string? FileUrl { get; set; }

    public string Status { get; set; } = TdsStatuses.Awaited;
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
    public Receipt? Receipt { get; set; }
}

public static class TdsStatuses
{
    /// <summary>Deducted, nothing received. The default and the risky one.</summary>
    public const string Awaited = "Awaited";

    public const string Received = "Received";
    public const string Verified = "Verified";

    /// <summary>Deducted but never deposited by the buyer. The developer's problem to chase.</summary>
    public const string Mismatched = "Mismatched";

    public static readonly string[] All = [Awaited, Received, Verified, Mismatched];
}

/* ------------------------------------------------------------------ *
 * GST profile
 * ------------------------------------------------------------------ */

/// <summary>
/// The developer's own tax identity for a project, and the defaults its
/// invoices are raised on.
///
/// Held per project rather than per company because that is how the registration
/// actually works: a developer building in two states holds two GSTINs, and the
/// place of supply for immovable property is the state the building stands in,
/// never the state the buyer lives in or the head office is registered in.
/// A single company-wide GSTIN would put the wrong number on half the invoices.
/// </summary>
public class GstProfile : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>Null is the company-wide fallback, used when a project has no profile of its own.</summary>
    public int? ProjectId { get; set; }

    public string LegalName { get; set; } = string.Empty;
    public string? TradeName { get; set; }

    public string Gstin { get; set; } = string.Empty;
    public string? Pan { get; set; }

    /// <summary>Where the project stands. This is the place of supply on every invoice it raises.</summary>
    public string StateName { get; set; } = string.Empty;

    /// <summary>The two-digit state code that opens the GSTIN.</summary>
    public string StateCode { get; set; } = string.Empty;

    public string? RegisteredAddress { get; set; }

    /// <summary>What a flat in this project is taxed as, unless the booking overrides it.</summary>
    public string DefaultTreatment { get; set; } = GstTreatments.ResidentialUnderConstruction;

    /// <summary>SAC for construction services.</summary>
    public string DefaultSacCode { get; set; } = "9954";

    /// <summary>
    /// Once the occupancy certificate is issued the supply stops being a service
    /// and no further invoice carries GST. Set this and the engine stops charging.
    /// </summary>
    public DateTime? OccupancyCertificateOn { get; set; }

    /// <summary>The prefix on this project's invoice series — "BRG/GST".</summary>
    public string InvoicePrefix { get; set; } = "INV";
    public string CreditNotePrefix { get; set; } = "CN";

    /// <summary>Escrow account, printed on the demand so money reaches the RERA account.</summary>
    public string? BankAccountName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIfsc { get; set; }
    public string? BankBranch { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Project? Project { get; set; }
}
