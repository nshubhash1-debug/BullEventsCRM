namespace BullEvents.Api.Dtos;

/* ------------------------------------------------------------------ *
 * The resource plan behind a proposal, and what it makes of the margin.
 * ------------------------------------------------------------------ */

public record QuotationResourceDto(
    int Id,
    int QuotationId,
    string ResourceKind,
    string State,
    int? PropItemId,
    int? PropKitId,
    int? CrewMemberId,
    string? CrewMemberName,
    string? CrewRole,
    int? VendorId,
    string? VendorName,
    int? VendorRateId,
    string Description,
    string ChargeGroup,
    bool IsInternalOnly,
    int SortOrder,
    string? Notes,
    decimal Quantity,
    string? QuantityUnit,
    int Days,
    decimal UnitCost,
    decimal UnitSell,
    decimal TaxRate,
    decimal LineCost,
    decimal LineSell,
    decimal LineMargin,
    decimal? MarginFraction,
    DateOnly? FromDate,
    DateOnly? ToDate,
    string? PrimaryThumbnailUrl,
    /// <summary>Free over this line's window, for stock-backed lines. Null otherwise.</summary>
    int? AvailableQuantity,
    /// <summary>Whether the godown cannot currently supply what this line promises.</summary>
    bool? IsShort,
    int? PropReservationId,
    int? PropIssueId,
    int? CrewAssignmentId,
    int? VendorPurchaseOrderId,
    DateTime CreatedAt);

/// <summary>
/// Adding one line to the plan.
///
/// Which identifier is set decides the kind, so a caller cannot pick "Vendor"
/// and then hand over a prop id. Rates default from the underlying record when
/// they are not given, which is what makes dropping a kit onto a proposal a
/// single click rather than forty priced decisions.
/// </summary>
public record QuotationResourceInput(
    string ResourceKind,
    int? PropItemId = null,
    int? PropKitId = null,
    int? CrewMemberId = null,
    string? CrewRole = null,
    int? VendorId = null,
    int? VendorRateId = null,
    string? Description = null,
    string? ChargeGroup = null,
    decimal Quantity = 1,
    string? QuantityUnit = null,
    int? Days = null,
    decimal? UnitCost = null,
    decimal? UnitSell = null,
    decimal? TaxRate = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    bool IsInternalOnly = false,
    string? Notes = null);

/// <summary>Editing the numbers on a line already on the plan.</summary>
public record QuotationResourceUpdate(
    decimal? Quantity = null,
    int? Days = null,
    decimal? UnitCost = null,
    decimal? UnitSell = null,
    string? Description = null,
    string? ChargeGroup = null,
    bool? IsInternalOnly = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    string? Notes = null);

/// <summary>
/// Dropping a whole kit onto a proposal.
///
/// Expands to one line per kit item rather than a single opaque line, because
/// a client asks what is in the mandap and a coordinator needs the availability
/// answered piece by piece.
/// </summary>
public record QuotationKitInput(
    int PropKitId,
    int Multiplier = 1,
    bool EssentialOnly = false,
    /// <summary>Price the whole set at the kit's own rate instead of summing its items.</summary>
    bool UseKitRate = false);

/// <summary>Quoting a headcount of a trade — "four bearers, three days".</summary>
public record QuotationCrewInput(
    string CrewRole,
    int Headcount = 1,
    int? Days = null,
    decimal? UnitCost = null,
    decimal? UnitSell = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null);

/* ---------------- margin ---------------- */

public record MarginByGroupDto(
    string Group,
    string Label,
    decimal Sell,
    decimal Cost,
    decimal Margin,
    decimal? MarginFraction,
    int LineCount);

/// <summary>
/// What a proposal earns once delivering it is paid for.
///
/// Two costs are reported, not one. <see cref="PlannedCost"/> is what the
/// resource plan says it will take; <see cref="CommittedCost"/> is what has
/// actually been booked against the event so far. They diverge the moment a
/// caterer is confirmed at a different number from the one quoted, and that
/// divergence is the thing worth watching.
/// </summary>
public record QuotationMarginDto(
    int QuotationId,
    string QuoteNumber,
    string Status,
    string CustomerName,
    string? EventType,
    DateTime? EventDate,
    int GuestCount,

    /// <summary>What the client is billed, from the quotation's own totals.</summary>
    decimal Revenue,
    decimal RevenueExTax,

    decimal PlannedCost,
    decimal PlannedMargin,
    decimal? PlannedMarginFraction,

    decimal CommittedCost,
    decimal CommittedMargin,
    decimal? CommittedMarginFraction,

    /// <summary>Committed minus planned. Positive means delivery is running over.</summary>
    decimal CostVariance,

    int ResourceCount,
    int HeldLines,
    int ConvertedLines,
    int ShortLines,

    IReadOnlyList<MarginByGroupDto> ByGroup,
    IReadOnlyList<QuotationResourceDto> Resources);

/* ---------------- holds and conversion ---------------- */

public record HoldResourcesInput(
    /// <summary>Null holds until the quotation's own validity date.</summary>
    DateTime? ExpiresAt = null);

public record HoldResultDto(
    int Held,
    int Skipped,
    IReadOnlyList<string> Warnings,
    QuotationMarginDto Plan);

/// <summary>
/// What an accepted proposal turned into.
///
/// Reported rather than merely done, because the conversion crosses four
/// modules and a coordinator needs to know which references to go and look at.
/// </summary>
public record ConversionResultDto(
    int QuotationId,
    int? PropIssueId,
    string? PropIssueCode,
    int PropLines,
    int PropPieces,
    IReadOnlyList<int> CrewAssignmentIds,
    int CrewBooked,
    IReadOnlyList<int> PurchaseOrderIds,
    IReadOnlyList<string> PurchaseOrderCodes,
    int VendorLines,
    decimal CommittedCost,
    IReadOnlyList<string> Warnings);

public record ConvertQuotationInput(
    /// <summary>Leave the plan's dates alone unless the event moved.</summary>
    DateOnly? DispatchDate = null,
    DateOnly? ReturnDate = null,
    int? StoreId = null,
    bool IncludeProps = true,
    bool IncludeCrew = true,
    bool IncludeVendors = true);

/* ---------------- pickers ---------------- */

/// <summary>
/// What can be put on this proposal for its own dates.
///
/// One call rather than three, because the planner picking décor for a wedding
/// is choosing between a kit, a few loose props and a florist in one sitting.
/// </summary>
public record QuotationResourceOptionsDto(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<PropKitDto> Kits,
    IReadOnlyList<PropAvailabilityDto> Props,
    IReadOnlyList<CrewRoleCoverageDto> CrewCoverage,
    IReadOnlyList<VendorDto> Vendors);
