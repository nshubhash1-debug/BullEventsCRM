namespace BullEvents.Api.Models;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string PlanTier { get; set; } = "Starter";
    public string Status { get; set; } = "Active";
    public string? BrandColor { get; set; }
    public string? LogoUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /* ---------------- subscription ---------------- */

    /// <summary>
    /// When the trial stops. A scheduled job suspends the company on this date
    /// and nothing else reads it, so leaving it null means "not on trial" rather
    /// than "trial with no end".
    /// </summary>
    public DateTime? TrialEndsAt { get; set; }

    /// <summary>When the current paid term runs out. Informational for now.</summary>
    public DateTime? RenewsAt { get; set; }

    /// <summary>
    /// Seats this company gets regardless of its plan, when a contract said
    /// something the price list does not.
    ///
    /// An override rather than a bespoke plan, because a plan is a thing the
    /// product is sold in and a negotiated seat count is a line in one
    /// agreement. Null means the plan decides.
    /// </summary>
    public int? SeatLimitOverride { get; set; }

    public int? LeadLimitOverride { get; set; }
    public int? BranchLimitOverride { get; set; }

    /// <summary>Why the overrides exist, so the next person does not have to guess.</summary>
    public string? EntitlementNote { get; set; }

    /* ---------------- legal identity ---------------- */
    //
    // A company row that knows only its display name cannot print a letterhead,
    // raise an invoice, or file anything. Every one of these appears on a
    // document this system already produces, and until now they were rendered
    // as a dash.

    /// <summary>
    /// The name on the incorporation certificate.
    ///
    /// Kept apart from <see cref="Name"/> because they differ in almost every
    /// real company: the app header says "Bull Events", the contract has to say
    /// "Bull Events Private Limited". Null falls back to the display name,
    /// which is right for a sole proprietor.
    /// </summary>
    public string? LegalName { get; set; }

    /// <summary>Corporate Identity Number. Twenty-one characters, on every letterhead by law.</summary>
    public string? Cin { get; set; }

    public string? Pan { get; set; }

    /// <summary>Deduction account number — needed to file the TDS the company itself deducts.</summary>
    public string? Tan { get; set; }

    /// <summary>
    /// The promoter's RERA registration.
    ///
    /// Per project registrations live on the project; this is the promoter
    /// number that goes on advertising, which is a different one.
    /// </summary>
    public string? ReraNumber { get; set; }

    /* ---------------- registered office ---------------- */

    public string? AddressLine1 { get; set; }
    public string? AddressLine2 { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Country { get; set; } = "India";

    /// <summary>
    /// The address as one block, for letterheads and merge fields.
    ///
    /// Composed rather than stored, so editing the city cannot leave a stale
    /// copy on the letterhead — and null when there is nothing to say, so the
    /// caller decides what a missing address looks like rather than getting a
    /// string of stray commas.
    /// </summary>
    public string? PostalAddress
    {
        get
        {
            var parts = new[]
            {
                AddressLine1, AddressLine2, City, State, Pincode, Country,
            }.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();

            return parts.Length == 0 ? null : string.Join(", ", parts);
        }
    }

    /* ---------------- how to reach it ---------------- */

    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? SupportEmail { get; set; }
    public string? Website { get; set; }

    /* ---------------- conventions ---------------- */

    /// <summary>
    /// The month the financial year opens on. April in India.
    ///
    /// Configurable because the number series on invoices and receipts are cut
    /// per financial year, and a company running an April–March year that had
    /// its documents numbered on the calendar year would break its own series
    /// halfway through.
    /// </summary>
    public int FinancialYearStartMonth { get; set; } = 4;

    public string CurrencyCode { get; set; } = "INR";
    public string TimeZoneId { get; set; } = "India Standard Time";

    /// <summary>Printed under the signature block on outgoing letters.</summary>
    public string? LetterheadFooter { get; set; }

    public ICollection<Branch> Branches { get; set; } = new List<Branch>();
    public ICollection<User> Users { get; set; } = new List<User>();
}
