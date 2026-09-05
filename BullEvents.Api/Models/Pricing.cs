namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Rate cards
 * ------------------------------------------------------------------ */

/// <summary>
/// The basic sale price per square foot, valid from a date.
///
/// Modelled as dated rows rather than a column on the unit because the price
/// list moves on a schedule while the unit does not: a quotation raised in
/// August must keep quoting August's rate even after September's card opens,
/// and a card that has already priced a deal can never be edited away.
/// </summary>
public class RateCard : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ProjectId { get; set; }

    /// <summary>Null applies the card to every tower in the project.</summary>
    public int? TowerId { get; set; }

    /// <summary>Studio, 1BHK, … — null applies to every configuration.</summary>
    public string? UnitType { get; set; }

    /// <summary>Shown on the quotation ("Aug 2026"), so it survives a rename.</summary>
    public string Label { get; set; } = string.Empty;

    public DateTime EffectiveFrom { get; set; }

    /// <summary>Basic sale price per sq ft of saleable (super) area.</summary>
    public decimal RatePerSqft { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Project? Project { get; set; }
    public Tower? Tower { get; set; }
}

/* ------------------------------------------------------------------ *
 * Payment plans
 * ------------------------------------------------------------------ */

/// <summary>
/// How the price is broken into instalments, and what discount the plan buys.
///
/// The discount lives on the plan rather than on the quotation because it is
/// the plan that earns it — paying in full up front is worth 20%, spreading it
/// over a year is worth 10%, and construction-linked is worth nothing. A rep
/// quoting a bigger discount than the plan allows is the case the approval
/// workflow exists for.
/// </summary>
public class PaymentPlan : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>Null makes the plan available across every project.</summary>
    public int? ProjectId { get; set; }

    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The discount this plan grants as a fraction — 0.20 for 20%.</summary>
    public decimal StandardDiscount { get; set; }

    /// <summary>
    /// How far past the standard a rep may go before the quotation needs a
    /// manager's approval. Zero means any extra discount needs one.
    /// </summary>
    public decimal DiscountTolerance { get; set; }

    /// <summary>GST on the unit cost, as a fraction. 0.12 across these plans.</summary>
    public decimal TaxRate { get; set; } = 0.12m;

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    /* ---------------- investor annexure ---------------- */
    //
    // The up-front plans are sold as an investment, and the workbook prints a
    // return schedule beside the price to make that case. It is set per plan
    // because it is the plan that earns it: the assured return is the developer
    // paying for money received early, so a construction-linked plan carries
    // none of this and prints no annexure.

    /// <summary>Assured annual return as a fraction of BSP. 0.09 on the up-front plans.</summary>
    public decimal AssuredReturnPercent { get; set; }

    /// <summary>Years the assured return runs — to possession.</summary>
    public decimal AssuredReturnYears { get; set; }

    /// <summary>Buy-back as a fraction of BSP per year. 0.06.</summary>
    public decimal BuyBackPercentPerYear { get; set; }

    /// <summary>Years after full payment before buy-back may be exercised. 2.5.</summary>
    public decimal BuyBackEligibleAfterYears { get; set; }

    /// <summary>Indicative rent per sq ft per month, for the yield line.</summary>
    public decimal IndicativeRentPerSqftPerMonth { get; set; }

    /// <summary>The conditions the annexure is subject to, printed under it.</summary>
    public string? ReturnConditions { get; set; }

    /// <summary>True once the plan carries a return worth printing.</summary>
    public bool HasInvestorAnnexure =>
        AssuredReturnPercent > 0 || BuyBackPercentPerYear > 0 || IndicativeRentPerSqftPerMonth > 0;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Project? Project { get; set; }
    public ICollection<PaymentPlanMilestone> Milestones { get; set; } = new List<PaymentPlanMilestone>();
}

/// <summary>
/// One instalment in a plan. The <see cref="Basis"/> is what makes three very
/// different-looking schedules share one engine.
/// </summary>
public class PaymentPlanMilestone
{
    public int Id { get; set; }
    public int PaymentPlanId { get; set; }

    public int SortOrder { get; set; }
    public string Label { get; set; } = string.Empty;

    public string Basis { get; set; } = MilestoneBases.PercentOfTotal;

    /// <summary>Used by the percent bases. 0.05 for 5%.</summary>
    public decimal Percent { get; set; }

    /// <summary>Used by <see cref="MilestoneBases.Fixed"/> — a gross figure, tax inclusive.</summary>
    public decimal FixedAmount { get; set; }

    /// <summary>
    /// Days from whatever <see cref="DueAnchor"/> names, for the schedule's
    /// indicative due dates. Counted <i>backwards</i> for an event-anchored
    /// instalment, so 30 means thirty days before the event.
    /// </summary>
    public int? DueOffsetDays { get; set; }

    /// <summary>
    /// What <see cref="DueOffsetDays"/> counts from — a value from
    /// <see cref="MilestoneAnchors"/>.
    ///
    /// This is the difference between a property schedule and an event one. A
    /// flat is paid off a booking date because possession is years out and
    /// moves; an event is paid off the event date, which is fixed before the
    /// contract is signed and is the only date the client is thinking about.
    /// "Balance 15 days before the function" cannot be expressed as an offset
    /// from booking without recomputing it every time the enquiry converts.
    /// </summary>
    /// <remarks>
    /// The column defaults to <c>FromBooking</c> so milestones written before
    /// anchoring existed keep behaving exactly as they did.
    /// </remarks>
    public string DueAnchor { get; set; } = MilestoneAnchors.FromBooking;

    /// <summary>Delivery stage this instalment is tied to, where it is.</summary>
    public string? ConstructionStage { get; set; }

    public PaymentPlan? PaymentPlan { get; set; }
}

/// <summary>What a milestone's due-date offset is measured from.</summary>
public static class MilestoneAnchors
{
    /// <summary>Forwards from the booking — the advance and the confirmation.</summary>
    public const string FromBooking = "FromBooking";

    /// <summary>Backwards from the event date — the pre-event instalments.</summary>
    public const string BeforeEvent = "BeforeEvent";

    /// <summary>Forwards from the event date — the settlement of actuals.</summary>
    public const string AfterEvent = "AfterEvent";

    public static readonly string[] All = [FromBooking, BeforeEvent, AfterEvent];

    /// <summary>
    /// When an instalment falls due. Null when the anchor it needs is unknown —
    /// an event-anchored milestone on an enquiry with no date yet.
    /// </summary>
    public static DateTime? Resolve(string anchor, int? offsetDays, DateTime bookingDate, DateTime? eventDate)
    {
        if (offsetDays is null) return null;

        return anchor switch
        {
            BeforeEvent => eventDate?.AddDays(-offsetDays.Value),
            AfterEvent => eventDate?.AddDays(offsetDays.Value),
            _ => bookingDate.AddDays(offsetDays.Value),
        };
    }
}

public static class MilestoneBases
{
    /// <summary>A flat amount — the booking EOI, which is the same on every unit.</summary>
    public const string Fixed = "Fixed";

    /// <summary>A percentage of the whole consideration. The construction-linked slabs.</summary>
    public const string PercentOfTotal = "PercentOfTotal";

    /// <summary>
    /// A percentage of what is left after the fixed instalments. The one-time
    /// and flexi plans split the balance this way, so the EOI is not billed twice.
    /// </summary>
    public const string PercentOfNetOfFixed = "PercentOfNetOfFixed";

    /// <summary>
    /// Brings the running total up to a percentage of the whole, whatever has
    /// already been collected. This is the "10% within 30 days, less the 9 lakh
    /// already paid" line — it has to be computed, never typed.
    /// </summary>
    public const string BalanceToPercent = "BalanceToPercent";

    public static readonly string[] All =
    [
        Fixed, PercentOfTotal, PercentOfNetOfFixed, BalanceToPercent
    ];
}

/* ------------------------------------------------------------------ *
 * Charge heads
 * ------------------------------------------------------------------ */

/// <summary>
/// One line of the cost sheet's "Charge Details" table — the pricing workbook's
/// Group / Revenue Head pair.
///
/// The unit cost is only ever one of these. A real cost sheet also carries
/// parking, club membership, the maintenance deposit, development charges and
/// the legal fee, and they do not behave alike: they are charged on different
/// bases, they attract different GST rates, some are refundable deposits rather
/// than revenue, and only some of them are spread across the payment plan. Each
/// of those differences is a column here rather than a special case in the
/// pricing code.
/// </summary>
public class ChargeHead : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>Null offers the head on every project.</summary>
    public int? ProjectId { get; set; }

    /// <summary>The banding the cost sheet groups rows under.</summary>
    public string Group { get; set; } = ChargeGroups.UnitCharge;

    public string Code { get; set; } = string.Empty;

    /// <summary>The revenue head as it prints — "Covered Car Parking".</summary>
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Basis { get; set; } = ChargeBases.Lumpsum;

    /// <summary>Per sq ft, per slot, a lump sum, or a fraction of the unit cost.</summary>
    public decimal Rate { get; set; }

    /// <summary>
    /// GST on this head. Not inherited from the plan: the unit cost and a club
    /// membership are taxed at different rates on the same document.
    /// </summary>
    public decimal TaxRate { get; set; } = 0.18m;

    /// <summary>Slots, KVA, or 1 for a lump sum.</summary>
    public decimal DefaultQuantity { get; set; } = 1m;

    /// <summary>Priced onto every quotation without the rep choosing it.</summary>
    public bool IsMandatory { get; set; }

    /// <summary>A deposit that comes back — shown apart from revenue on the sheet.</summary>
    public bool IsRefundable { get; set; }

    /// <summary>
    /// Whether the payment plan spreads this head across its instalments.
    ///
    /// The maintenance deposit and the registration fee fall due in one lump at
    /// possession however the unit itself is being paid for, so putting them
    /// through the milestone percentages would misstate every instalment.
    /// </summary>
    public bool IncludeInSchedule { get; set; } = true;

    /// <summary>When an unscheduled head falls due — "On possession".</summary>
    public string? DueLabel { get; set; }

    public int SortOrder { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Project? Project { get; set; }
}

/// <summary>
/// The bandings a proposal groups its lines under.
///
/// <c>UnitCharge</c> keeps its stored name — it is the group every seeded
/// charge head and every historical quotation line already carries — and reads
/// as the venue rental.
/// </summary>
public static class ChargeGroups
{
    /// <summary>The venue rental. Surfaced as <i>Venue</i>.</summary>
    public const string UnitCharge = "Unit Charge";

    public const string Catering = "Catering";
    public const string Decor = "Décor & Styling";
    public const string Photography = "Photography & Film";
    public const string Entertainment = "Entertainment";
    public const string Logistics = "Logistics & Staffing";
    public const string Statutory = "Statutory & Permits";
    public const string Deposits = "Deposits";

    /// <summary>Print order on the proposal — venue first, deposits last.</summary>
    public static readonly string[] All =
    [
        UnitCharge, Catering, Decor, Photography, Entertainment,
        Logistics, Statutory, Deposits
    ];

    public static string Label(string group) =>
        group == UnitCharge ? "Venue" : group;
}

public static class ChargeBases
{
    /// <summary>Rate x area. Rigging and draping are quoted this way.</summary>
    public const string PerSqft = "PerSqft";

    /// <summary>A flat amount, whatever the event.</summary>
    public const string Lumpsum = "Lumpsum";

    /// <summary>Rate x quantity — crew, cameras, shuttle vehicles.</summary>
    public const string PerQuantity = "PerQuantity";

    /// <summary>A fraction of the venue rental. The planning fee is often 10%.</summary>
    public const string PercentOfUnitCost = "PercentOfUnitCost";

    /// <summary>
    /// Rate x head count — the catering basis, and the largest line on almost
    /// every event bill.
    ///
    /// The head count it multiplies is <i>not</i> simply the guest count: a
    /// venue bills the higher of the guests expected and its minimum plate
    /// guarantee, and quoting a 200-guest event into a hall with a 400-plate
    /// minimum is the single commonest way an events proposal goes wrong. The
    /// flooring lives in the pricing engine so no caller can forget it.
    /// </summary>
    public const string PerGuest = "PerGuest";

    public static readonly string[] All =
    [
        PerSqft, Lumpsum, PerQuantity, PercentOfUnitCost, PerGuest
    ];
}

/// <summary>
/// A charge head as it was priced onto one quotation.
///
/// Copied for the same reason the milestones are: the head can be repriced next
/// quarter, and a quotation already with a customer must still say what it said.
/// </summary>
public class QuotationCharge
{
    public int Id { get; set; }
    public int QuotationId { get; set; }

    /// <summary>Where it came from, for reporting. Null once the head is deleted.</summary>
    public int? ChargeHeadId { get; set; }

    public int SortOrder { get; set; }
    public string Group { get; set; } = ChargeGroups.UnitCharge;
    public string Name { get; set; } = string.Empty;
    public string Basis { get; set; } = ChargeBases.Lumpsum;

    /// <summary>What the rate was multiplied by — area, slots, or 1.</summary>
    public decimal Quantity { get; set; } = 1m;
    public string? QuantityUnit { get; set; }
    public decimal Rate { get; set; }

    public decimal BasicAmount { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public bool IsRefundable { get; set; }
    public bool IncludeInSchedule { get; set; } = true;
    public string? DueLabel { get; set; }

    public Quotation? Quotation { get; set; }
}

/* ------------------------------------------------------------------ *
 * Quotation schedule
 * ------------------------------------------------------------------ */

/// <summary>
/// A milestone as it was priced onto one quotation.
///
/// Copied rather than referenced: the plan can be re-tuned next quarter, and a
/// quotation already in a customer's inbox must still say what it said.
/// </summary>
public class QuotationMilestone
{
    public int Id { get; set; }
    public int QuotationId { get; set; }

    public int SortOrder { get; set; }
    public string Label { get; set; } = string.Empty;

    /// <summary>The share of the consideration this instalment represents.</summary>
    public decimal Percent { get; set; }

    public decimal BasicAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal TotalAmount { get; set; }

    public DateTime? DueDate { get; set; }

    public Quotation? Quotation { get; set; }
}

/* ------------------------------------------------------------------ *
 * Approvals
 * ------------------------------------------------------------------ */

/// <summary>
/// A request for someone senior to sign off a move the requester cannot make
/// alone — an over-standard discount, or taking a unit out of sale.
///
/// One table for every kind rather than a flag per object: the approver wants a
/// single queue, and the rule about who may decide belongs in one place.
/// </summary>
public class Approval : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    public string EntityType { get; set; } = ApprovalEntities.Quotation;
    public int EntityId { get; set; }

    /// <summary>Human reference to the target — quote number or unit number.</summary>
    public string EntityLabel { get; set; } = string.Empty;

    public string Kind { get; set; } = ApprovalKinds.Discount;
    public string Status { get; set; } = ApprovalStatuses.Pending;

    /// <summary>Why the requester says the exception is warranted.</summary>
    public string? Reason { get; set; }

    /// <summary>
    /// What is actually being asked for, in words the approver can act on
    /// without opening the record — "Discount 25% vs standard 20%".
    /// </summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>The money at stake, for sorting the queue by exposure.</summary>
    public decimal? Amount { get; set; }

    public int RequestedById { get; set; }
    public string RequestedByName { get; set; } = string.Empty;
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public int? DecidedById { get; set; }
    public string? DecidedByName { get; set; }
    public DateTime? DecidedAt { get; set; }
    public string? DecisionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Branch? Branch { get; set; }
}

public static class ApprovalEntities
{
    public const string Quotation = "Quotation";
    public const string Unit = "Unit";

    public static readonly string[] All = [Quotation, Unit];
}

public static class ApprovalKinds
{
    /// <summary>Discount beyond what the payment plan grants.</summary>
    public const string Discount = "Discount";

    /// <summary>A rate typed over the one the rate card produced.</summary>
    public const string PriceOverride = "PriceOverride";

    /// <summary>Taking a space off the calendar without a booking behind it.</summary>
    public const string UnitBlock = "UnitBlock";

    /// <summary>Confirming a space booking for a client date.</summary>
    public const string UnitBooking = "UnitBooking";

    public static readonly string[] All = [Discount, PriceOverride, UnitBlock, UnitBooking];

    /// <summary>Manager-facing label — stored kind stays stable for the wire.</summary>
    public static string Label(string kind) => kind switch
    {
        Discount => "Discount exception",
        PriceOverride => "Rate override",
        UnitBlock => "Space off calendar",
        UnitBooking => "Confirm space booking",
        _ => kind,
    };
}

public static class ApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    /// <summary>Withdrawn by the requester, or made moot by a later edit.</summary>
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Pending, Approved, Rejected, Cancelled];
}

/* ------------------------------------------------------------------ *
 * Unit history
 * ------------------------------------------------------------------ */

/// <summary>
/// Every status a unit has been through, and who moved it.
///
/// Inventory disputes are always about a moment in time — "it was available
/// when I promised it" — so the trail is a first-class record rather than
/// something to be reconstructed from the audit log.
/// </summary>
public class UnitStatusHistory
{
    public int Id { get; set; }
    public int UnitId { get; set; }

    public string FromStatus { get; set; } = string.Empty;
    public string ToStatus { get; set; } = string.Empty;
    public string? Reason { get; set; }

    /// <summary>Who the unit was moved for, when it was moved for someone.</summary>
    public int? LeadId { get; set; }
    public int? ContactId { get; set; }
    public string? PartyName { get; set; }

    public int? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Unit? Unit { get; set; }
}
