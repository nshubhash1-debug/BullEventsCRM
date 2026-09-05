namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Document templates
 * ------------------------------------------------------------------ */

public static class TemplateKinds
{
    /// <summary>Raised against one instalment. The commonest letter in this business.</summary>
    public const string DemandLetter = "DemandLetter";

    /// <summary>Chasing a demand that has gone past its date.</summary>
    public const string ReminderLetter = "ReminderLetter";

    public const string AllotmentLetter = "AllotmentLetter";
    public const string WelcomeLetter = "WelcomeLetter";
    public const string PaymentReceipt = "PaymentReceipt";
    public const string StatementOfAccount = "StatementOfAccount";
    public const string PossessionOffer = "PossessionOffer";
    public const string PossessionLetter = "PossessionLetter";
    public const string NocForLoan = "NocForLoan";
    public const string CancellationLetter = "CancellationLetter";
    public const string TransferLetter = "TransferLetter";
    public const string BrokerageInvoice = "BrokerageInvoice";

    public static readonly string[] All =
    [
        DemandLetter, ReminderLetter, AllotmentLetter, WelcomeLetter,
        PaymentReceipt, StatementOfAccount, PossessionOffer, PossessionLetter,
        NocForLoan, CancellationLetter, TransferLetter, BrokerageInvoice,
    ];

    /// <summary>
    /// Which of these need a demand in scope to render, and which only need the
    /// booking. A demand letter with no demand is a letter with blanks in it.
    /// </summary>
    public static readonly string[] NeedDemand = [DemandLetter, ReminderLetter];

    public static readonly string[] NeedReceipt = [PaymentReceipt];
}

/// <summary>
/// A letter the system writes for you.
///
/// Kept as a body with merge fields rather than as code, because the wording of
/// a demand letter is a commercial and legal decision that changes without a
/// deployment — a developer should never be the bottleneck on "add the GST
/// number to the footer".
///
/// The body is HTML. That is what a browser prints, what an email client
/// renders, and what survives a copy into Word; storing it as anything else
/// means converting it at every one of those points.
/// </summary>
public class DocumentTemplate : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>A key from <see cref="TemplateKinds"/>.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Rendered into the subject line when this goes out by email.</summary>
    public string? Subject { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Only one template per kind is used when nothing is chosen explicitly.
    ///
    /// Without this a bulk run has to guess between three demand-letter drafts,
    /// and the guess is silent.
    /// </summary>
    public bool IsDefault { get; set; }

    /// <summary>Shipped with the product. Editable, not deletable.</summary>
    public bool IsSystem { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/* ------------------------------------------------------------------ *
 * Generated documents
 * ------------------------------------------------------------------ */

public static class GeneratedStatuses
{
    /// <summary>Rendered and stored, not yet sent to anyone.</summary>
    public const string Draft = "Draft";

    public const string Issued = "Issued";
    public const string Sent = "Sent";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Draft, Issued, Sent, Cancelled];
}

/// <summary>
/// One letter, as it was actually sent.
///
/// The rendered body is stored rather than re-rendered on demand. A demand
/// letter is a commercial document: if the template is edited next month, the
/// copy the customer holds must still be the copy this system can show. Every
/// dispute in this business turns on exactly that.
/// </summary>
public class GeneratedDocument : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int BookingId { get; set; }

    /// <summary>The demand or receipt this was raised against, when there is one.</summary>
    public int? DemandId { get; set; }
    public int? ReceiptId { get; set; }

    public int? DocumentTemplateId { get; set; }

    public string Kind { get; set; } = string.Empty;

    /// <summary>Sequential per company and kind — DEM/2026/0001.</summary>
    public string Number { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Subject { get; set; }

    /// <summary>The rendered HTML, frozen at the moment it was produced.</summary>
    public string Body { get; set; } = string.Empty;

    public string Status { get; set; } = GeneratedStatuses.Draft;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? IssuedAt { get; set; }
    public DateTime? SentAt { get; set; }

    /// <summary>Where it went, masked the way the rest of this system masks contacts.</summary>
    public string? SentTo { get; set; }
    public string? SentVia { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Booking? Booking { get; set; }
    public Demand? Demand { get; set; }
}

/* ------------------------------------------------------------------ *
 * Merge fields
 * ------------------------------------------------------------------ */

/// <summary>One placeholder a template may use, and what it stands for.</summary>
public record MergeField(string Token, string Label, string Group, string Example);

/// <summary>
/// The tokens a template can contain.
///
/// Published as a list rather than left to be discovered, because the person
/// writing the letter is not the person who wrote the code, and a token that
/// silently renders as nothing is indistinguishable from a customer with no
/// middle name.
/// </summary>
public static class MergeFields
{
    public static readonly MergeField[] All =
    [
        new("{{company.name}}", "Company name", "Company", "Jeet Homes Solution"),
        new("{{company.address}}", "Company address", "Company", "Varanasi"),

        new("{{booking.number}}", "Booking number", "Booking", "BRG/BKG/2026/0001"),
        new("{{booking.date}}", "Booking date", "Booking", "25 Aug 2026"),
        new("{{booking.status}}", "Booking status", "Booking", "Booked"),
        new("{{booking.agreementValue}}", "Agreement value", "Booking", "₹1,72,00,000"),
        new("{{booking.grandTotal}}", "Grand total", "Booking", "₹2,03,62,005"),
        new("{{booking.received}}", "Received to date", "Booking", "₹2,00,000"),
        new("{{booking.outstanding}}", "Outstanding", "Booking", "₹80,16,013"),

        new("{{customer.name}}", "Primary applicant", "Customer", "Rakesh Deshpande"),
        new("{{customer.salutation}}", "Salutation", "Customer", "Mr."),
        new("{{customer.address}}", "Address", "Customer", "…"),
        new("{{customer.phone}}", "Phone", "Customer", "+919820055001"),
        new("{{customer.email}}", "Email", "Customer", "…"),
        new("{{customer.pan}}", "PAN", "Customer", "ABCDE1234F"),
        new("{{customer.coApplicants}}", "Co-applicants", "Customer", "Sunita Deshpande"),

        new("{{unit.number}}", "Space", "Space", "Emerald Hall"),
        new("{{unit.tower}}", "Block", "Space", "Block A"),
        new("{{unit.project}}", "Venue", "Space", "The Grand Palladium"),
        new("{{unit.configuration}}", "Space type", "Space", "Banquet Hall"),
        new("{{unit.area}}", "Area", "Space", "3,200 sq.ft."),

        new("{{demand.number}}", "Demand number", "Demand", "BRG/DEM/2026/0001"),
        new("{{demand.label}}", "What is being demanded", "Demand", "Second instalment — 50%"),
        new("{{demand.amount}}", "Amount demanded", "Demand", "₹38,54,941"),
        new("{{demand.dueDate}}", "Due date", "Demand", "9 Sept 2026"),
        new("{{demand.outstanding}}", "Still outstanding", "Demand", "₹38,54,941"),
        new("{{demand.interest}}", "Interest accrued", "Demand", "₹6,131"),
        new("{{demand.interestRate}}", "Penal rate", "Demand", "12% p.a."),
        new("{{demand.daysLate}}", "Days past due", "Demand", "45"),

        new("{{receipt.number}}", "Receipt number", "Receipt", "BRG/RCP/2026/0001"),
        new("{{receipt.amount}}", "Amount received", "Receipt", "₹2,00,000"),
        new("{{receipt.date}}", "Received on", "Receipt", "5 Aug 2026"),
        new("{{receipt.mode}}", "Mode", "Receipt", "NEFT"),
        new("{{receipt.tds}}", "TDS withheld", "Receipt", "₹1,000"),

        new("{{today}}", "Today's date", "General", "26 Aug 2026"),
        new("{{document.number}}", "This document's number", "General", "DEM/2026/0001"),

        // Rendered as a table rather than a value. Called out in its own group so
        // nobody drops it into the middle of a sentence.
        new("{{table.schedule}}", "The payment schedule, as a table", "Tables", "…"),
        new("{{table.ledger}}", "The customer ledger, as a table", "Tables", "…"),
        new("{{table.outstanding}}", "Outstanding demands, as a table", "Tables", "…"),
    ];
}
