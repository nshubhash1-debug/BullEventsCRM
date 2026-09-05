namespace BullEvents.Api.Models;

/// <summary>
/// A priced offer. Totals are computed server-side from the lines on every
/// write — the client never gets to decide what a quotation is worth.
/// </summary>
public class Quotation : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    public string QuoteNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Version { get; set; } = 1;

    public int? ContactId { get; set; }
    public int? LeadId { get; set; }
    public int? OpportunityId { get; set; }
    public int? ProjectId { get; set; }
    public int? UnitId { get; set; }

    public string CustomerName { get; set; } = string.Empty;
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string? BillingAddress { get; set; }

    public string Status { get; set; } = QuotationStatuses.Draft;
    public DateTime IssueDate { get; set; } = DateTime.UtcNow;
    public DateTime ValidUntil { get; set; } = DateTime.UtcNow.AddDays(15);
    public DateTime? SentAt { get; set; }
    public DateTime? RespondedAt { get; set; }

    /* ---------------- the event ---------------- */
    //
    // Snapshotted onto the proposal for the same reason the rate is: the
    // enquiry's guest count moves as the family argues, and a proposal is an
    // offer made against one head count on one date. Re-reading the lead would
    // make an accepted proposal silently disagree with the price it quoted.

    public string? EventType { get; set; }

    /// <summary>The date the offer is for. Anchors the pre-event instalments.</summary>
    public DateTime? EventDate { get; set; }

    /// <summary>Last day, for a multi-day run.</summary>
    public DateTime? EventEndDate { get; set; }

    public string? EventSlot { get; set; }

    /// <summary>Guests the client expects.</summary>
    public int GuestCount { get; set; }

    /// <summary>
    /// The venue's minimum plate guarantee at the time of quoting, and the
    /// floor the per-head lines were billed on.
    ///
    /// Held beside the guest count rather than derived from the space, because
    /// the space's minimum can be renegotiated next season and the offer that
    /// went out must still explain its own arithmetic.
    /// </summary>
    public int MinimumPlates { get; set; }

    /// <summary>The head count the per-head lines were actually struck on.</summary>
    public int BilledHeads => Math.Max(GuestCount, MinimumPlates);

    /// <summary>Comma-separated functions the proposal covers.</summary>
    public string? Functions { get; set; }

    /* ---------------- pricing basis ---------------- */
    //
    // Snapshotted, not looked up on read: a quotation is an offer that was made
    // at a moment, and the rate card moves on the first of every month.

    public int? PaymentPlanId { get; set; }
    public string? PaymentPlanName { get; set; }

    /// <summary>Tower and unit as they read on the document.</summary>
    public string? TowerName { get; set; }
    public string? UnitNumber { get; set; }
    public string? UnitType { get; set; }

    /// <summary>Saleable (super) area the price was computed on.</summary>
    public decimal SaleableArea { get; set; }

    /// <summary>
    /// The usable and built-up areas, snapshotted alongside the saleable one.
    ///
    /// The cost sheet carries all three because the buyer is charged on the
    /// super area but lives in the usable one, and RERA requires the carpet area
    /// to be stated. Printing only the number the price was struck on is what
    /// makes a quotation look evasive.
    /// </summary>
    public decimal CarpetArea { get; set; }
    public decimal BuiltUpArea { get; set; }

    /// <summary>Rate card in force when the quotation was raised.</summary>
    public int? RateCardId { get; set; }
    public string? RateCardLabel { get; set; }
    public decimal RatePerSqft { get; set; }
    public decimal PlcPerSqft { get; set; }

    /// <summary>Rate after discount, with PLC added back. What the customer sees.</summary>
    public decimal EffectiveRatePerSqft { get; set; }

    /// <summary>The discount the chosen plan grants as standard, for comparison.</summary>
    public decimal StandardDiscountPercent { get; set; }

    public string Currency { get; set; } = "INR";

    /* ---------------- unit cost ---------------- */
    //
    // Subtotal / TaxAmount / Total stay the unit cost alone, which is what the
    // payment plan percentages have always been struck against. The other charge
    // heads total separately below, so adding parking to a quotation can never
    // silently move every instalment.

    public decimal Subtotal { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxPercent { get; set; } = 5m;
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }

    /* ---------------- other charge heads ---------------- */

    public decimal ChargesBasic { get; set; }
    public decimal ChargesTax { get; set; }
    public decimal ChargesTotal { get; set; }

    /// <summary>Of the charges, the part that is a refundable deposit.</summary>
    public decimal RefundableTotal { get; set; }

    /// <summary>Unit cost plus every other head — what the buyer actually pays.</summary>
    public decimal GrandTotal { get; set; }

    /// <summary>
    /// The part of the consideration the payment plan spreads: the unit cost
    /// plus whichever heads are scheduled. The rest falls due on its own terms.
    /// </summary>
    public decimal ScheduledTotal { get; set; }

    /// <summary>Rendered on the document, and on the PDF, in Indian numbering.</summary>
    public string? AmountInWords { get; set; }

    /// <summary>The grand total in words — the figure the buyer signs against.</summary>
    public string? GrandTotalInWords { get; set; }

    /* ---------------- commercial options ---------------- */
    //
    // What the desk chose to put on this offer, snapshotted alongside what it
    // produced. Each is a decision made per deal rather than a property of the
    // plan: the same Flexi plan is sold with an assured return to an investor
    // and without one to an end user, and the quotation has to remember which
    // conversation it came out of.

    /// <summary>
    /// False when the offer carries no discount at all.
    ///
    /// Distinct from a zero percentage: "no discount was offered" and "a
    /// discount of nothing was offered" print differently, and a rep who has
    /// deliberately held the list price should not have a discount line on the
    /// document saying they gave away nought percent.
    /// </summary>
    public bool DiscountApplied { get; set; } = true;

    /// <summary>What the discount is called on the document — "Launch offer".</summary>
    public string? DiscountLabel { get; set; }

    /// <summary>Whether the assured-return annexure is part of this offer.</summary>
    public bool ShowAssuredReturn { get; set; }
    public decimal AssuredReturnPercent { get; set; }
    public decimal AssuredReturnYears { get; set; }
    public decimal AssuredReturnAmount { get; set; }

    /// <summary>Whether a buy-back is being offered on this quotation.</summary>
    public bool ShowBuyBack { get; set; }
    public decimal BuyBackPercentPerYear { get; set; }
    public decimal BuyBackEligibleAfterYears { get; set; }
    public decimal BuyBackHorizonYears { get; set; }
    public decimal BuyBackAmount { get; set; }

    /// <summary>The buy-back price — BSP plus the accrued appreciation.</summary>
    public decimal BuyBackValue { get; set; }

    /// <summary>Whether the indicative rental yield is stated.</summary>
    public bool ShowRentalYield { get; set; }
    public decimal RentPerSqftPerMonth { get; set; }
    public decimal RentPerMonth { get; set; }
    public decimal GrossRentalYield { get; set; }

    /// <summary>Assured return plus buy-back appreciation over the horizon.</summary>
    public decimal ReturnsTotalEarned { get; set; }
    public decimal ReturnOnInvestment { get; set; }
    public decimal ReturnHorizonYears { get; set; }

    /// <summary>The conditions the return offer is subject to, printed under it.</summary>
    public string? ReturnConditions { get; set; }

    /* ---------------- approval ---------------- */

    /// <summary>
    /// NotRequired, Pending, Approved or Rejected. Kept on the quotation as
    /// well as on the approval row so the list can filter on it without a join,
    /// and so a quotation can never be sent while its exception is unresolved.
    /// </summary>
    public string ApprovalStatus { get; set; } = QuotationApprovalStatuses.NotRequired;
    public int? ApprovalId { get; set; }

    public string? PaymentTerms { get; set; }
    public string? Notes { get; set; }
    public string? TermsAndConditions { get; set; }
    public string? RejectionReason { get; set; }

    public int? OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    /* ---------------- follow-up tracking ---------------- */

    /// <summary>When the next follow-up with the customer is due.</summary>
    public DateTime? NextFollowUpAt { get; set; }
    public string? FollowUpNote { get; set; }
    public DateTime? LastFollowUpAt { get; set; }
    public int FollowUpCount { get; set; }

    /// <summary>The original validity date before any extensions.</summary>
    public DateTime? OriginalValidUntil { get; set; }

    public Branch? Branch { get; set; }
    public User? Owner { get; set; }
    public Project? Project { get; set; }
    public Unit? Unit { get; set; }
    public PaymentPlan? PaymentPlan { get; set; }
    public ICollection<QuotationLine> Lines { get; set; } = new List<QuotationLine>();
    public ICollection<QuotationMilestone> Milestones { get; set; } = new List<QuotationMilestone>();
    public ICollection<QuotationCharge> Charges { get; set; } = new List<QuotationCharge>();

    public ICollection<QuotationFollowUp> FollowUps { get; set; } = new List<QuotationFollowUp>();
    public ICollection<QuotationShareLink> ShareLinks { get; set; } = new List<QuotationShareLink>();
    public ICollection<QuotationNegotiation> Negotiations { get; set; } = new List<QuotationNegotiation>();
    public ICollection<QuotationActivity> Activities { get; set; } = new List<QuotationActivity>();
}

public class QuotationLine
{
    public int Id { get; set; }
    public int QuotationId { get; set; }

    public string Description { get; set; } = string.Empty;
    public string? Category { get; set; }
    public decimal Quantity { get; set; } = 1m;
    public string? Unit { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal LineTotal { get; set; }
    public int SortOrder { get; set; }

    public Quotation? Quotation { get; set; }
}

public static class QuotationApprovalStatuses
{
    /// <summary>Within the plan's standard terms — nobody has to look at it.</summary>
    public const string NotRequired = "NotRequired";

    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [NotRequired, Pending, Approved, Rejected];
}

/// <summary>
/// A follow-up interaction logged against a quotation.
///
/// Tracks every outreach to the customer and when the next one is due,
/// so the desk knows which quotes are being chased and which are going cold.
/// </summary>
public class QuotationFollowUp : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int QuotationId { get; set; }

    public string Channel { get; set; } = "Call";
    public string Note { get; set; } = string.Empty;
    public string? Outcome { get; set; }
    public DateTime? NextFollowUpAt { get; set; }

    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Quotation? Quotation { get; set; }
}

/// <summary>
/// One round of negotiation on a quotation — what the customer asked for,
/// what was offered, and where the price landed.
///
/// The sales desk looks at these when the deal closes to understand how
/// far the final price moved from the first offer, and why.
/// </summary>
public class QuotationNegotiation : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int QuotationId { get; set; }

    /// <summary>Sequential round number — 1, 2, 3.</summary>
    public int Round { get; set; }

    /// <summary>CounterOffer, Concession, FinalOffer.</summary>
    public string Type { get; set; } = NegotiationTypes.CounterOffer;

    /// <summary>The discount the customer asked for, as a fraction.</summary>
    public decimal? RequestedDiscount { get; set; }

    /// <summary>The discount offered in response, as a fraction.</summary>
    public decimal? OfferedDiscount { get; set; }

    /// <summary>What the customer said, in their words.</summary>
    public string? CustomerDemand { get; set; }

    /// <summary>How the rep responded.</summary>
    public string? OurResponse { get; set; }

    /// <summary>Price change from the previous round. Negative means a reduction was given.</summary>
    public decimal? DeltaAmount { get; set; }

    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Quotation? Quotation { get; set; }
}

public static class NegotiationTypes
{
    public const string CounterOffer = "CounterOffer";
    public const string Concession = "Concession";
    public const string FinalOffer = "FinalOffer";

    public static readonly string[] All = [CounterOffer, Concession, FinalOffer];
}

/// <summary>
/// An event in the life of a quotation — created, issued, revised, declined.
///
/// The timeline answers the sales manager's question: what happened to this
/// quotation, and in what order. It is append-only — events are never edited,
/// so the audit trail stays clean.
/// </summary>
public class QuotationActivity
{
    public int Id { get; set; }
    public int QuotationId { get; set; }

    /// <summary>Created, Edited, Issued, Accepted, Declined, Deleted, FollowUp,
    /// ValidityExtended, ApprovalRequested, ApprovalGranted, ApprovalRejected,
    /// NegotiationRound, Revised.</summary>
    public string Type { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    /// <summary>Additional structured data — e.g. old/new discount, extended date.</summary>
    public string? Metadata { get; set; }

    public int? ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Quotation? Quotation { get; set; }
}

public static class QuotationActivityTypes
{
    public const string Created = "Created";
    public const string Edited = "Edited";
    public const string Issued = "Issued";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";
    public const string Deleted = "Deleted";
    public const string FollowUp = "FollowUp";
    public const string ValidityExtended = "ValidityExtended";
    public const string ApprovalRequested = "ApprovalRequested";
    public const string ApprovalGranted = "ApprovalGranted";
    public const string ApprovalRejected = "ApprovalRejected";
    public const string NegotiationRound = "NegotiationRound";
    public const string Revised = "Revised";
    public const string Expired = "Expired";
}

/* ------------------------------------------------------------------ *
 * Shareable links
 * ------------------------------------------------------------------ */

/// <summary>
/// A time-limited link that lets a customer view their quotation online
/// without authenticating.
///
/// Every quotation may have multiple links — one per send — so a revoked
/// link does not block a fresh one. The token is a GUID carried in the URL
/// rather than an id, so it cannot be guessed by incrementing.
/// </summary>
public class QuotationShareLink
{
    public int Id { get; set; }
    public int QuotationId { get; set; }

    /// <summary>The unguessable slug in the URL — /q/{token}.</summary>
    public string Token { get; set; } = Guid.NewGuid().ToString("N");

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(30);
    public bool IsActive { get; set; } = true;

    public int ViewCount { get; set; }
    public DateTime? LastViewedAt { get; set; }

    /// <summary>Set when the customer responds through the link.</summary>
    public DateTime? RespondedAt { get; set; }
    public string? ResponseStatus { get; set; }
    public string? CustomerComment { get; set; }

    public int? CreatedById { get; set; }
    public string CreatedByName { get; set; } = string.Empty;

    public Quotation? Quotation { get; set; }
}

/* ------------------------------------------------------------------ *
 * Quotation templates
 * ------------------------------------------------------------------ */

/// <summary>
/// A reusable configuration that pre-fills the quote builder — payment plan,
/// discount, charge heads, and standard notes.
///
/// Saves the rep from ticking the same five heads on every 2BHK quotation
/// they build for the same project.
/// </summary>
public class QuotationTemplate : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>Scopes the template to one project. Null makes it global.</summary>
    public int? ProjectId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    public int? PaymentPlanId { get; set; }
    public decimal? DefaultDiscount { get; set; }
    public string? DefaultNotes { get; set; }
    public string? DefaultTermsAndConditions { get; set; }
    public int? ValidDays { get; set; }

    /* ---------------- commercial options ---------------- */
    //
    // Null on each means the template has no opinion and the plan decides. That
    // is what separates an "Investor package" template, which deliberately turns
    // the assured return on, from an ordinary one that simply never mentioned it.

    public bool? ApplyDiscount { get; set; }
    public bool? IncludeAssuredReturn { get; set; }
    public bool? IncludeBuyBack { get; set; }
    public bool? IncludeRentalYield { get; set; }

    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Project? Project { get; set; }
    public PaymentPlan? PaymentPlan { get; set; }
    public ICollection<QuotationTemplateCharge> Charges { get; set; } = new List<QuotationTemplateCharge>();
}

/// <summary>
/// One charge head selection saved in a template.
/// </summary>
public class QuotationTemplateCharge
{
    public int Id { get; set; }
    public int TemplateId { get; set; }
    public int ChargeHeadId { get; set; }
    public decimal Quantity { get; set; } = 1m;

    public QuotationTemplate? Template { get; set; }
}
