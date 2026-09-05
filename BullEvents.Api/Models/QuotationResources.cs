namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * The resource plan behind a proposal.
 *
 * A quotation already knows what the client pays. What it has never known is
 * what delivering it costs — the props off the shelf, the crew on the floor,
 * the caterer and the DJ. Those live in four modules that each answer their own
 * question well, and a wedding's margin was consequently four screens rather
 * than one number.
 *
 * A QuotationResource is one line of that plan. It does three jobs the priced
 * QuotationCharge cannot:
 *
 *   1. it remembers *which* prop, person or supplier the line stands for, so an
 *      accepted proposal can become a gate pass, a crew booking and a purchase
 *      order without anybody retyping it;
 *   2. it carries a cost beside the sell price, so the margin is arithmetic
 *      rather than a guess;
 *   3. it can hold the stock while the client decides — softly, with an expiry,
 *      so a deal that goes quiet hands the crates back on its own.
 * ------------------------------------------------------------------ */

/// <summary>
/// What a resource line stands for.
///
/// One table with a discriminator rather than three, because a planner reads
/// the resource plan as one list — the mandap, the four bearers and the
/// caterer are one decision — and three tables would have made that list a
/// three-way union on every read.
/// </summary>
public static class QuotationResourceKinds
{
    /// <summary>A single catalogue line off the shelf.</summary>
    public const string Prop = "Prop";

    /// <summary>A whole set — a mandap, an entrance arch. Expands to its items.</summary>
    public const string PropKit = "PropKit";

    /// <summary>A person from the roster, or a headcount of a trade.</summary>
    public const string Crew = "Crew";

    /// <summary>A subcontracted service, usually off a supplier's rate card.</summary>
    public const string Vendor = "Vendor";

    public static readonly string[] All = [Prop, PropKit, Crew, Vendor];

    /// <summary>Kinds that draw on godown stock and can therefore be held.</summary>
    public static readonly string[] StockBacked = [Prop, PropKit];

    /// <summary>
    /// Which banding on the proposal a kind prints under by default.
    ///
    /// Overridable per line, because a floral installation is décor to one
    /// planner and its own head to another, but the default has to be sensible
    /// or every line arrives needing a decision.
    /// </summary>
    public static string DefaultGroup(string kind) => kind switch
    {
        Prop or PropKit => ChargeGroups.Decor,
        Crew => ChargeGroups.Logistics,
        _ => ChargeGroups.Catering,
    };
}

/// <summary>
/// Where a resource plan has got to against the godown and the diary.
///
/// Deliberately separate from the quotation's own status: a proposal can be
/// sent with nothing held, or held while still in negotiation, and the two
/// facts move independently.
/// </summary>
public static class QuotationResourceStates
{
    /// <summary>Priced onto the proposal, holding nothing.</summary>
    public const string Planned = "Planned";

    /// <summary>Stock is softly held, expiring with the quotation's validity.</summary>
    public const string Held = "Held";

    /// <summary>Turned into a gate pass, a crew booking or a purchase order.</summary>
    public const string Converted = "Converted";

    /// <summary>The hold was handed back — the deal died, or the line was dropped.</summary>
    public const string Released = "Released";

    public static readonly string[] All = [Planned, Held, Converted, Released];
}

/// <summary>
/// One line of what it takes to deliver a proposal.
///
/// <see cref="UnitCost"/> and <see cref="UnitSell"/> are both snapshotted at
/// the moment the line is added, for the same reason the charge heads are: the
/// rate card moves on the first of every month, and a proposal already with a
/// client has to keep explaining its own arithmetic.
/// </summary>
public class QuotationResource : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int QuotationId { get; set; }

    /// <summary>A value from <see cref="QuotationResourceKinds"/>.</summary>
    public string ResourceKind { get; set; } = QuotationResourceKinds.Prop;

    public string State { get; set; } = QuotationResourceStates.Planned;

    /* ---------------- what it points at ---------------- */

    public int? PropItemId { get; set; }
    public int? PropKitId { get; set; }

    /// <summary>
    /// The named person, when one has been picked.
    ///
    /// Null is meaningful and common: a proposal says "four bearers" long
    /// before anybody decides which four, and forcing a name at quoting time
    /// would either block the proposal or book people against a deal that has
    /// not closed.
    /// </summary>
    public int? CrewMemberId { get; set; }

    /// <summary>The trade being quoted, when the line is a headcount.</summary>
    public string? CrewRole { get; set; }

    public int? VendorId { get; set; }
    public int? VendorRateId { get; set; }

    /* ---------------- how it prints ---------------- */

    /// <summary>The wording on the proposal. Snapshotted, so a rename cannot rewrite history.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Which banding it prints under — a value from <see cref="ChargeGroups"/>.</summary>
    public string ChargeGroup { get; set; } = ChargeGroups.Decor;

    /// <summary>Suppressed on the client's copy but still counted in the cost.</summary>
    public bool IsInternalOnly { get; set; }

    public int SortOrder { get; set; }
    public string? Notes { get; set; }

    /* ---------------- the numbers ---------------- */

    /// <summary>Pieces, people, or plates.</summary>
    public decimal Quantity { get; set; } = 1m;

    public string? QuantityUnit { get; set; }

    /// <summary>
    /// Days billed. One for a lump sum; the dispatch-to-return window for props
    /// and the shift count for crew.
    /// </summary>
    public int Days { get; set; } = 1;

    /// <summary>
    /// What one unit costs us for one day.
    ///
    /// Zero for owned props, which is the honest number: the piece is already
    /// bought, and the only cost of sending it out is wear, which belongs in
    /// depreciation rather than on a proposal line.
    /// </summary>
    public decimal UnitCost { get; set; }

    /// <summary>What one unit is billed at for one day.</summary>
    public decimal UnitSell { get; set; }

    /// <summary>GST on this line. Follows the charge head convention.</summary>
    public decimal TaxRate { get; set; } = 0.18m;

    /* ---------------- the dates it occupies ---------------- */

    /// <summary>
    /// First day the resource is committed — the dispatch, not the function.
    /// Defaults from the quotation's event date when the line is added.
    /// </summary>
    public DateOnly? FromDate { get; set; }

    public DateOnly? ToDate { get; set; }

    /* ---------------- what it became ---------------- */

    /// <summary>The soft hold, while this line is holding stock.</summary>
    public int? PropReservationId { get; set; }

    /// <summary>Set once the plan was converted into real bookings.</summary>
    public int? PropIssueId { get; set; }
    public int? CrewAssignmentId { get; set; }
    public int? VendorPurchaseOrderId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Quotation? Quotation { get; set; }
    public PropItem? PropItem { get; set; }
    public PropKit? PropKit { get; set; }
    public CrewMember? CrewMember { get; set; }
    public Vendor? Vendor { get; set; }
    public VendorRate? VendorRate { get; set; }

    /* ---------------- arithmetic ---------------- */

    /// <summary>What this line costs us across its whole window.</summary>
    public decimal LineCost => UnitCost * Quantity * Math.Max(1, Days);

    /// <summary>What the client is billed for it, before tax.</summary>
    public decimal LineSell => UnitSell * Quantity * Math.Max(1, Days);

    /// <summary>What the company keeps. Negative means the line loses money.</summary>
    public decimal LineMargin => LineSell - LineCost;

    /// <summary>
    /// Margin as a fraction of the sell price.
    ///
    /// Null rather than zero when nothing is being charged: a line quoted at
    /// nought has no margin percentage, and reporting one as 0% would put it
    /// alongside lines genuinely sold at cost.
    /// </summary>
    public decimal? MarginFraction =>
        LineSell == 0 ? null : Math.Round(LineMargin / LineSell, 4);

    /// <summary>Whether this line draws on godown stock and can be held.</summary>
    public bool IsStockBacked => QuotationResourceKinds.StockBacked.Contains(ResourceKind);

    /// <summary>Whether it is currently holding anything.</summary>
    public bool IsHolding => State == QuotationResourceStates.Held;
}
