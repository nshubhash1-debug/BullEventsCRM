namespace BullEvents.Api.Dtos;

/* ------------------------------------------------------------------ *
 * Payment plans
 * ------------------------------------------------------------------ */

public record PaymentPlanDto(
    int Id,
    int? ProjectId,
    string Code,
    string Name,
    string? Description,
    decimal StandardDiscount,
    decimal DiscountTolerance,
    decimal TaxRate,
    bool IsActive,
    int SortOrder,
    decimal AssuredReturnPercent,
    decimal AssuredReturnYears,
    decimal BuyBackPercentPerYear,
    decimal BuyBackEligibleAfterYears,
    decimal IndicativeRentPerSqftPerMonth,
    string? ReturnConditions,
    IReadOnlyList<PaymentPlanMilestoneDto> Milestones);

public record PaymentPlanMilestoneDto(
    int Id,
    int SortOrder,
    string Label,
    string Basis,
    decimal Percent,
    decimal FixedAmount,
    int? DueOffsetDays,
    string? ConstructionStage);

/* ------------------------------------------------------------------ *
 * Charge heads
 * ------------------------------------------------------------------ */

public record ChargeHeadDto(
    int Id,
    int? ProjectId,
    string Group,
    string Code,
    string Name,
    string? Description,
    string Basis,
    decimal Rate,
    decimal TaxRate,
    decimal DefaultQuantity,
    bool IsMandatory,
    bool IsRefundable,
    bool IncludeInSchedule,
    string? DueLabel,
    int SortOrder);

/// <summary>
/// A head the rep has put on this quotation. Quantity only bites on the
/// per-quantity basis — two parking slots rather than one.
/// </summary>
public record ChargeSelectionDto(int ChargeHeadId, decimal? Quantity);

/// <summary>One priced charge row, as it appears on the sheet.</summary>
public record QuoteChargeDto(
    int? ChargeHeadId,
    int SortOrder,
    string Group,
    string Name,
    string Basis,
    decimal Quantity,
    string? QuantityUnit,
    decimal Rate,
    decimal BasicAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    bool IsRefundable,
    bool IncludeInSchedule,
    string? DueLabel);

/* ------------------------------------------------------------------ *
 * Commercial options
 * ------------------------------------------------------------------ */

/// <summary>
/// The optional commercial terms on one quotation — discount, assured return,
/// buy-back, rental yield.
///
/// Every field is nullable and null means "take the plan's default", so a
/// client that does not know about an option cannot switch it off by omission.
/// The rep turning one off is an explicit false, which is a different fact from
/// the plan not offering it, and the document reads differently for each.
/// </summary>
public record QuoteOptionsDto(
    /// <summary>False holds the list price — no discount line prints at all.</summary>
    bool? ApplyDiscount = null,
    /// <summary>What the discount is called on the document. "Launch offer".</summary>
    string? DiscountLabel = null,

    /// <summary>Null follows the plan: on where the plan configures a return.</summary>
    bool? IncludeAssuredReturn = null,
    /// <summary>A fraction per year. Null takes the plan's rate.</summary>
    decimal? AssuredReturnPercent = null,
    decimal? AssuredReturnYears = null,

    bool? IncludeBuyBack = null,
    decimal? BuyBackPercentPerYear = null,
    decimal? BuyBackEligibleAfterYears = null,
    /// <summary>Years the buy-back accrues over. Null follows the assured horizon.</summary>
    decimal? BuyBackHorizonYears = null,

    bool? IncludeRentalYield = null,
    decimal? RentPerSqftPerMonth = null,

    string? ReturnConditions = null);

/// <summary>One year of the return projection.</summary>
public record ReturnYearDto(
    int Year,
    DateTime PeriodEnd,
    decimal YearFraction,
    decimal AssuredReturn,
    decimal RentalIncome,
    decimal CumulativeReturn);

/// <summary>The indicative return, as it prints on the annexure.</summary>
public record InvestorAnnexureDto(
    decimal BasicSalePrice,

    bool HasAssuredReturn,
    bool HasBuyBack,
    bool HasRentalYield,

    decimal AssuredReturnPercent,
    decimal AssuredReturnYears,
    decimal AssuredReturnPerYear,
    decimal AssuredReturnPerMonth,
    decimal AssuredReturnAmount,

    decimal BuyBackPercentPerYear,
    decimal BuyBackEligibleAfterYears,
    decimal BuyBackHorizonYears,
    decimal BuyBackAmount,
    decimal BuyBackValue,

    decimal IndicativeRentPerSqftPerMonth,
    decimal IndicativeRentPerMonth,
    decimal IndicativeRentPerYear,
    decimal GrossRentalYield,

    decimal TotalEarned,
    decimal ReturnOnInvestment,
    decimal AnnualisedReturn,
    decimal HorizonYears,

    IReadOnlyList<ReturnYearDto> Schedule,
    string? Conditions);

/* ------------------------------------------------------------------ *
 * Pricing preview
 * ------------------------------------------------------------------ */

/// <summary>
/// Prices a unit against a plan without writing anything.
///
/// The builder screen calls this on every keystroke of the discount box, so it
/// has to be side-effect free — and it is the same engine the saved quotation
/// uses, which is what stops the preview and the document disagreeing.
/// </summary>
public record QuotePreviewRequest(
    int UnitId,
    int PaymentPlanId,
    /// <summary>Null takes the plan's standard discount. A fraction, not a percent.</summary>
    decimal? Discount,
    /// <summary>Overrides the rate card. Needs approval, like an over-discount.</summary>
    decimal? RateOverride,
    DateTime? BookingDate,
    /// <summary>Optional heads the rep has added. Mandatory ones apply regardless.</summary>
    IReadOnlyList<ChargeSelectionDto>? Charges = null,
    /// <summary>Discount, return and buy-back switches. Null takes the plan's terms.</summary>
    QuoteOptionsDto? Options = null,

    /* ---------------- the event ---------------- */

    /// <summary>The date being quoted for. Anchors the pre-event instalments.</summary>
    DateTime? EventDate = null,

    /// <summary>Guests expected. What the per-head lines multiply.</summary>
    int? GuestCount = null,

    /// <summary>
    /// A negotiated plate guarantee. Null takes the space's own minimum — this
    /// is here for the venue that drops its floor to win a date.
    /// </summary>
    int? MinimumPlates = null);

public record QuotePreviewDto(
    int UnitId,
    string UnitNumber,
    string? TowerName,
    string ProjectName,
    string UnitType,
    int Floor,
    string UnitStatus,

    decimal SaleableArea,
    decimal BuiltUpArea,
    decimal CarpetArea,
    decimal PlcPerSqft,
    string? Facing,
    string? ViewType,

    /* ---------------- head count ---------------- */

    /// <summary>Guests the client expects.</summary>
    int GuestCount,

    /// <summary>The venue's plate guarantee this was priced against.</summary>
    int MinimumPlates,

    /// <summary>
    /// The count the per-head lines were struck on — the higher of the two
    /// above. Surfaced rather than left implicit so the proposal can say why a
    /// 200-guest wedding is a 400-plate bill instead of being argued about.
    /// </summary>
    int BilledHeads,

    /// <summary>True when the guarantee, not the guest count, set the bill.</summary>
    bool MinimumApplied,

    int? RateCardId,
    string? RateCardLabel,
    decimal RatePerSqft,
    bool IsRateOverridden,

    int PaymentPlanId,
    string PaymentPlanName,
    decimal StandardDiscount,
    decimal Discount,
    decimal EffectiveRatePerSqft,

    decimal BasicAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal TotalAmount,
    string AmountInWords,

    IReadOnlyList<QuoteChargeDto> Charges,
    decimal ChargesBasic,
    decimal ChargesTax,
    decimal ChargesTotal,
    decimal RefundableTotal,
    decimal GrandTotal,
    decimal ScheduledTotal,
    decimal UnscheduledTotal,
    string GrandTotalInWords,

    /// <summary>False when the offer holds the list price rather than discounting it.</summary>
    bool DiscountApplied,
    string? DiscountLabel,
    /// <summary>Rate x area x discount — the money the buyer is being told they save.</summary>
    decimal DiscountAmount,
    /// <summary>Null when nothing was switched on; the annexure is then not printed.</summary>
    InvestorAnnexureDto? Annexure,

    IReadOnlyList<QuoteMilestoneDto> Milestones,
    decimal ScheduleVariance,

    /// <summary>True when saving this quotation will stop for a manager.</summary>
    bool RequiresApproval,
    string? ApprovalReason);

public record QuoteMilestoneDto(
    int SortOrder,
    string Label,
    decimal Percent,
    decimal BasicAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    DateTime? DueDate);

/* ------------------------------------------------------------------ *
 * Creating from a unit
 * ------------------------------------------------------------------ */

public record CreateUnitQuotationRequest(
    int UnitId,
    int PaymentPlanId,
    decimal? Discount,
    decimal? RateOverride,
    DateTime? BookingDate,
    IReadOnlyList<ChargeSelectionDto>? Charges,
    QuoteOptionsDto? Options,

    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? BillingAddress,

    int? LeadId,
    int? ContactId,
    int? OpportunityId,
    int? OwnerId,
    int? BranchId,

    string? Notes,
    string? TermsAndConditions,
    /// <summary>Why the exception is warranted, when one is being asked for.</summary>
    string? ApprovalReason,
    int? ValidDays,

    /* ---------------- the event ---------------- */

    string? EventType = null,
    /// <summary>The date being quoted for. Anchors the pre-event instalments.</summary>
    DateTime? EventDate = null,
    DateTime? EventEndDate = null,
    string? EventSlot = null,
    /// <summary>Comma-separated functions this proposal covers.</summary>
    string? Functions = null,
    int? GuestCount = null,
    /// <summary>A negotiated plate guarantee. Null takes the space's own minimum.</summary>
    int? MinimumPlates = null);

/// <summary>
/// Re-prices a quotation that already exists.
///
/// Separate from the generic PUT because a quotation is not a bag of editable
/// fields — its price is the output of the plan, the discount and the heads,
/// and the only safe way to change it is to run the same engine again. Anything
/// left null keeps what the quotation already carries.
/// </summary>
public record RepriceQuotationRequest(
    int? PaymentPlanId,
    decimal? Discount,
    decimal? RateOverride,
    DateTime? BookingDate,
    IReadOnlyList<ChargeSelectionDto>? Charges,
    QuoteOptionsDto? Options,

    string? CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? BillingAddress,

    string? Notes,
    string? TermsAndConditions,
    string? ApprovalReason,
    int? ValidDays,
    int? OwnerId,

    /// <summary>
    /// Re-points the quotation at a lead, contact or deal. Zero unlinks —
    /// null cannot, because null already means "leave it alone" on every other
    /// field here and one field cannot mean both.
    /// </summary>
    int? LeadId = null,
    int? ContactId = null,
    int? OpportunityId = null,

    /* ---------------- the event ---------------- */
    //
    // Null keeps what the proposal already carries, like every other field
    // here. Moving the date or the head count is a deliberate re-offer.

    DateTime? EventDate = null,
    int? GuestCount = null,
    int? MinimumPlates = null);

public record SubmitApprovalRequest(string? Reason);

public record QuotationDecisionRequest(string? Reason);

/// <summary>
/// The saved quotation plus whatever the save triggered — an approval request,
/// most often.
/// </summary>
public record QuotationResultDto(
    QuotationDetailDto Quotation,
    ApprovalDto? Approval,
    string Message);

public record QuotationDetailDto(
    int Id,
    string QuoteNumber,
    string Title,
    int Version,
    string Status,
    string ApprovalStatus,

    int? LeadId,
    int? ContactId,
    int? OpportunityId,
    int? ProjectId,
    string? ProjectName,
    int? UnitId,
    string? UnitNumber,
    string? TowerName,
    string? UnitType,

    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? BillingAddress,

    int? PaymentPlanId,
    string? PaymentPlanName,
    decimal SaleableArea,
    decimal BuiltUpArea,
    decimal CarpetArea,

    /* ---------------- the event this offer is for ---------------- */

    string? EventType,
    DateTime? EventDate,
    DateTime? EventEndDate,
    string? EventSlot,
    string? Functions,
    int GuestCount,
    /// <summary>The plate guarantee the per-head lines were floored at.</summary>
    int MinimumPlates,

    string? RateCardLabel,
    decimal RatePerSqft,
    decimal PlcPerSqft,
    decimal EffectiveRatePerSqft,
    decimal StandardDiscountPercent,
    decimal DiscountPercent,
    bool DiscountApplied,
    string? DiscountLabel,
    decimal DiscountAmount,

    decimal Subtotal,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal Total,
    string? AmountInWords,

    decimal ChargesBasic,
    decimal ChargesTax,
    decimal ChargesTotal,
    decimal RefundableTotal,
    decimal GrandTotal,
    decimal ScheduledTotal,
    string? GrandTotalInWords,
    IReadOnlyList<QuoteChargeDto> Charges,

    /// <summary>The return offer as it was struck, not as the plan reads today.</summary>
    InvestorAnnexureDto? Annexure,

    DateTime IssueDate,
    DateTime ValidUntil,
    DateTime? SentAt,
    int? OwnerId,
    string? OwnerName,
    int BranchId,
    string BranchName,
    string? Notes,
    string? TermsAndConditions,
    string? RejectionReason,

    IReadOnlyList<QuoteMilestoneDto> Milestones,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/* ------------------------------------------------------------------ *
 * Timeline & Activity
 * ------------------------------------------------------------------ */

public record QuotationActivityDto(
    int Id,
    string Type,
    string Description,
    string? Metadata,
    int? ActorId,
    string ActorName,
    DateTime CreatedAt);

/* ------------------------------------------------------------------ *
 * Follow-ups
 * ------------------------------------------------------------------ */

public record CreateFollowUpRequest(
    string Note,
    string? Channel,
    string? Outcome,
    DateTime? NextFollowUpAt);

public record QuotationFollowUpDto(
    int Id,
    int QuotationId,
    string Channel,
    string Note,
    string? Outcome,
    DateTime? NextFollowUpAt,
    string CreatedByName,
    DateTime CreatedAt);

public record ExtendValidityRequest(
    DateTime NewValidUntil,
    string? Reason);

/* ------------------------------------------------------------------ *
 * Negotiation
 * ------------------------------------------------------------------ */

public record CreateNegotiationRequest(
    string Type,
    decimal? RequestedDiscount,
    decimal? OfferedDiscount,
    string? CustomerDemand,
    string? OurResponse,
    decimal? DeltaAmount);

public record QuotationNegotiationDto(
    int Id,
    int QuotationId,
    int Round,
    string Type,
    decimal? RequestedDiscount,
    decimal? OfferedDiscount,
    string? CustomerDemand,
    string? OurResponse,
    decimal? DeltaAmount,
    string CreatedByName,
    DateTime CreatedAt);

/* ------------------------------------------------------------------ *
 * Analytics
 * ------------------------------------------------------------------ */

public record QuotationAnalyticsDto(
    QuotationFunnelDto Funnel,
    decimal ConversionRate,
    decimal AvgDealSize,
    double AvgDaysToClose,
    double AvgDaysToExpiry,
    QuotationWinLossDto WinLoss,
    IReadOnlyList<MonthlyTrendDto> MonthlyTrend,
    IReadOnlyList<RepPerformanceDto> RepPerformance,
    IReadOnlyList<ProjectBreakdownDto> ProjectBreakdown);

public record QuotationFunnelDto(
    int Draft,
    int Sent,
    int UnderReview,
    int Negotiation,
    int Accepted,
    int Rejected,
    int Expired);

public record QuotationWinLossDto(
    int Won,
    int Lost,
    IReadOnlyList<LossReasonDto> TopLossReasons);

public record LossReasonDto(string Reason, int Count);

public record MonthlyTrendDto(
    string Month,
    decimal Quoted,
    decimal Accepted,
    int Count);

public record RepPerformanceDto(
    int? OwnerId,
    string RepName,
    int Quoted,
    int Accepted,
    decimal Value,
    decimal AcceptedValue);

public record ProjectBreakdownDto(
    int? ProjectId,
    string Project,
    int Quoted,
    int Accepted,
    decimal AvgDiscount,
    decimal TotalValue);

public record FollowUpsDueDto(
    int QuotationId,
    string QuoteNumber,
    string CustomerName,
    string Status,
    decimal GrandTotal,
    DateTime? NextFollowUpAt,
    string? FollowUpNote,
    string? OwnerName,
    bool IsOverdue);

/* ------------------------------------------------------------------ *
 * Shareable links
 * ------------------------------------------------------------------ */

public record CreateShareLinkRequest(int? ExpiryDays);

public record ShareLinkDto(
    int Id,
    int QuotationId,
    string Token,
    string Url,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    bool IsActive,
    int ViewCount,
    DateTime? LastViewedAt,
    DateTime? RespondedAt,
    string? ResponseStatus,
    string? CustomerComment,
    string CreatedByName);

/// <summary>The publicly visible quotation — no auth, no secrets.</summary>
public record PublicQuotationDto(
    string QuoteNumber,
    string Title,
    int Version,
    string Status,
    string CustomerName,
    string? ProjectName,
    string? TowerName,
    string? UnitNumber,
    string? UnitType,
    decimal SaleableArea,
    decimal CarpetArea,
    decimal BuiltUpArea,
    string? RateCardLabel,
    decimal RatePerSqft,
    decimal PlcPerSqft,
    decimal EffectiveRatePerSqft,
    decimal DiscountPercent,
    decimal Subtotal,
    decimal TaxPercent,
    decimal TaxAmount,
    decimal Total,
    decimal ChargesTotal,
    decimal GrandTotal,
    string? GrandTotalInWords,
    string? PaymentPlanName,
    IReadOnlyList<QuoteChargeDto> Charges,
    IReadOnlyList<QuoteMilestoneDto> Milestones,
    InvestorAnnexureDto? Annexure,
    DateTime IssueDate,
    DateTime ValidUntil,
    string? Notes,
    bool IsExpired,
    bool CanRespond,
    /* ---------------- whose quotation this is ---------------- */
    //
    // The public link is the one place the CRM shows itself to somebody outside
    // the company, and until now it showed them the platform's own styling. A
    // buyer should see the developer they are buying from.
    string SellerName,
    string? SellerBrandColor,
    string? SellerLogoUrl);

public record PublicQuotationResponseRequest(string Status, string? Comment);

/* ------------------------------------------------------------------ *
 * Templates
 * ------------------------------------------------------------------ */

public record QuotationTemplateDto(
    int Id,
    int? ProjectId,
    string? ProjectName,
    string Name,
    string? Description,
    int? PaymentPlanId,
    string? PaymentPlanName,
    decimal? DefaultDiscount,
    string? DefaultNotes,
    string? DefaultTermsAndConditions,
    int? ValidDays,
    bool IsActive,
    int SortOrder,
    bool? ApplyDiscount,
    bool? IncludeAssuredReturn,
    bool? IncludeBuyBack,
    bool? IncludeRentalYield,
    IReadOnlyList<TemplateChargeDto> Charges,
    DateTime CreatedAt);

public record TemplateChargeDto(int ChargeHeadId, decimal Quantity);

public record CreateTemplateRequest(
    int? ProjectId,
    string Name,
    string? Description,
    int? PaymentPlanId,
    decimal? DefaultDiscount,
    string? DefaultNotes,
    string? DefaultTermsAndConditions,
    int? ValidDays,
    IReadOnlyList<TemplateChargeDto>? Charges,
    bool? ApplyDiscount = null,
    bool? IncludeAssuredReturn = null,
    bool? IncludeBuyBack = null,
    bool? IncludeRentalYield = null);

public record UpdateTemplateRequest(
    string? Name,
    string? Description,
    int? PaymentPlanId,
    decimal? DefaultDiscount,
    string? DefaultNotes,
    string? DefaultTermsAndConditions,
    int? ValidDays,
    bool? IsActive,
    IReadOnlyList<TemplateChargeDto>? Charges,
    bool? ApplyDiscount = null,
    bool? IncludeAssuredReturn = null,
    bool? IncludeBuyBack = null,
    bool? IncludeRentalYield = null);

/* ------------------------------------------------------------------ *
 * Delivery (Email & WhatsApp)
 * ------------------------------------------------------------------ */

public record SendQuotationEmailRequest(
    string To,
    string? Cc,
    string Subject,
    string Body,
    bool AttachPdf = true);

public record SendQuotationWhatsAppRequest(
    string Phone,
    string? Message);

public record DeliveryResultDto(
    bool Success,
    string Message,
    DateTime SentAt);

/* ------------------------------------------------------------------ *
 * Multi-Unit / Combo Quotation
 * ------------------------------------------------------------------ */

public record MultiUnitPreviewRequest(
    IReadOnlyList<int> UnitIds,
    int PaymentPlanId,
    decimal? Discount,
    decimal? RateOverride,
    DateTime? BookingDate,
    IReadOnlyList<ChargeSelectionDto>? Charges,
    QuoteOptionsDto? Options = null);

public record MultiUnitItemPreviewDto(
    int UnitId,
    string UnitNumber,
    string? TowerName,
    string UnitType,
    int Floor,
    decimal SaleableArea,
    decimal RatePerSqft,
    decimal EffectiveRatePerSqft,
    decimal BasicAmount,
    decimal TotalAmount);

public record MultiUnitPreviewDto(
    IReadOnlyList<MultiUnitItemPreviewDto> Units,
    int PaymentPlanId,
    string PaymentPlanName,
    decimal StandardDiscount,
    decimal Discount,
    decimal CombinedSaleableArea,
    decimal CombinedBasicAmount,
    decimal CombinedTaxAmount,
    decimal CombinedTotalAmount,
    IReadOnlyList<QuoteChargeDto> Charges,
    decimal CombinedChargesTotal,
    decimal CombinedGrandTotal,
    string GrandTotalInWords,
    IReadOnlyList<QuoteMilestoneDto> Milestones,
    bool RequiresApproval,
    string? ApprovalReason);

public record CreateMultiUnitQuotationRequest(
    IReadOnlyList<int> UnitIds,
    int PaymentPlanId,
    decimal? Discount,
    decimal? RateOverride,
    DateTime? BookingDate,
    IReadOnlyList<ChargeSelectionDto>? Charges,
    QuoteOptionsDto? Options,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? BillingAddress,
    int? LeadId,
    int? ContactId,
    int? OpportunityId,
    int? OwnerId,
    int? BranchId,
    string? Notes,
    string? TermsAndConditions,
    string? ApprovalReason,
    int? ValidDays);

