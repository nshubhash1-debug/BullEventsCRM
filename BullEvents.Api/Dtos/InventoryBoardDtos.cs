namespace BullEvents.Api.Dtos;

/* ------------------------------------------------------------------ *
 * Board
 * ------------------------------------------------------------------ */

/// <summary>
/// Everything the inventory board needs, in one response.
///
/// The three view modes — tiles, list and building elevation — are three
/// renderings of the same stock, so they are served by one payload rather than
/// three endpoints. A tower is a few hundred units; splitting it would cost
/// three round trips and let the views disagree with each other mid-refresh.
/// </summary>
public record InventoryBoardDto(
    int ProjectId,
    string ProjectName,
    string? Developer,
    string? Address,
    string? ReraNumber,
    int? TowerId,
    string? TowerName,
    int FloorCount,
    int UnitsPerFloor,
    /// <summary>
    /// The headline card, for the projects that price on one. Where a project
    /// prices by configuration there is no single active card, so read
    /// <see cref="ActiveRateCards"/> instead — this one is only the first of
    /// them and stating it alone would misprice every other configuration.
    /// </summary>
    RateCardDto? ActiveRateCard,

    /// <summary>Every card in force today, most specific first.</summary>
    IReadOnlyList<RateCardDto> ActiveRateCards,

    InventoryStatsDto Stats,
    IReadOnlyList<InventoryFloorDto> Floors,
    IReadOnlyList<TowerDto> Towers);

public record InventoryStatsDto(
    int Total,
    int Available,
    int Held,
    int Blocked,
    int Booked,
    int Sold,
    int NotForSale,
    decimal AvailableValue,
    decimal SoldValue,
    decimal TotalValue,
    /// <summary>Share of the tower already committed — booked and sold.</summary>
    decimal SoldPercent);

/// <summary>Floors run top-down, the way a building is drawn.</summary>
public record InventoryFloorDto(
    int Floor,
    int Available,
    int Total,
    IReadOnlyList<BoardUnitDto> Units);

public record BoardUnitDto(
    int Id,
    string UnitNumber,
    int Floor,
    /// <summary>Position in the floor plan, 1 upward. Drives the building stacks.</summary>
    int Position,
    string Configuration,
    decimal CarpetArea,
    decimal? BuiltUpArea,
    decimal? SuperArea,
    decimal PlcPerSqft,
    /// <summary>Rate the live card gives this unit today.</summary>
    decimal RatePerSqft,
    /// <summary>Rate plus PLC — what a no-discount quotation would charge.</summary>
    decimal EffectiveRate,
    decimal TotalPrice,
    string Status,
    bool IsCornerUnit,
    string? Facing,
    int? HeldByUserId,
    string? HeldByName,
    DateTime? HeldUntil,
    string? HoldReason,
    int? BookedByContactId,
    int? BookedByLeadId,
    string? CustomerName,
    string? CustomerPhone,
    int? SalesPersonId,
    string? SalesPersonName,
    DateTime? BookedAt,
    int? BookedQuotationId,
    string? BlockReason,
    /// <summary>Set while an approval on this unit is still open.</summary>
    int? PendingApprovalId);

public record RateCardDto(
    int Id,
    int ProjectId,
    int? TowerId,
    string? UnitType,
    string Label,
    DateTime EffectiveFrom,
    decimal RatePerSqft,
    /// <summary>True for the card in force today.</summary>
    bool IsActive);

/* ------------------------------------------------------------------ *
 * Status moves
 * ------------------------------------------------------------------ */

/// <summary>
/// A move on the inventory board — hold, block, book, sell or release.
///
/// The customer fields are what turn a status into a record of who took the
/// unit; they are required for a booking and ignored for a release.
/// </summary>
public record UnitStatusRequest(
    string Status,
    string? Reason,
    int? LeadId,
    int? ContactId,
    string? CustomerName,
    string? CustomerPhone,
    int? SalesPersonId,
    int? QuotationId,
    /// <summary>Hold length. Only read when moving to Held.</summary>
    int? HoldHours,
    /// <summary>Event date the hold/booking applies to. Required for Booked.</summary>
    DateOnly? EventDate = null,
    /// <summary>Session on that date — Morning / Evening / FullDay, etc.</summary>
    string? EventSlot = null);

/// <summary>
/// What actually happened. A move that needed sign-off returns
/// <see cref="Approval"/> and the unit is parked on hold until it is decided.
/// </summary>
public record UnitStatusResultDto(
    UnitDto Unit,
    bool Applied,
    ApprovalDto? Approval,
    string Message);

public record UnitHistoryDto(
    int Id,
    string FromStatus,
    string ToStatus,
    string? Reason,
    int? LeadId,
    int? ContactId,
    string? PartyName,
    string ActorName,
    DateTime CreatedAt);

/* ------------------------------------------------------------------ *
 * Approvals
 * ------------------------------------------------------------------ */

public record ApprovalDto(
    int Id,
    string EntityType,
    int EntityId,
    string EntityLabel,
    string Kind,
    string Status,
    string Summary,
    string? Reason,
    decimal? Amount,
    int RequestedById,
    string RequestedByName,
    DateTime RequestedAt,
    int? DecidedById,
    string? DecidedByName,
    DateTime? DecidedAt,
    string? DecisionNote,
    int BranchId,
    string BranchName,
    /// <summary>How long it has been waiting, in hours. Drives the ageing column.</summary>
    double AgeHours);

public record ApprovalDecisionRequest(string? Note);

/* ------------------------------------------------------------------ *
 * Unit dossier
 * ------------------------------------------------------------------ */

/// <summary>
/// The whole story of one unit, in one response.
///
/// The detail window asks four questions at once — what is it, who has it, what
/// has been quoted, and how did it get here. Four endpoints would mean four
/// spinners and four chances for the panel to render half-answered.
/// </summary>
public record UnitDossierDto(
    BoardUnitDto Unit,
    string ProjectName,
    string? TowerName,
    /// <summary>Every unit on the same floor, for the floor plate.</summary>
    IReadOnlyList<BoardUnitDto> FloorUnits,
    IReadOnlyList<UnitQuotationDto> Quotations,
    IReadOnlyList<UnitHistoryDto> History,
    UnitPartyDto? Customer,
    UnitPartyDto? SalesPerson,
    ApprovalDto? PendingApproval);

/// <summary>A person attached to the unit, with enough to reach them.</summary>
public record UnitPartyDto(
    int? Id,
    string Name,
    string? Phone,
    string? Email,
    string? Role,
    /// <summary>Set when the party is a lead or contact that can be opened.</summary>
    int? LeadId,
    int? ContactId);

public record UnitQuotationDto(
    int Id,
    string QuoteNumber,
    int Version,
    string Status,
    string ApprovalStatus,
    string CustomerName,
    string? PaymentPlanName,
    decimal DiscountPercent,
    decimal Total,
    DateTime IssueDate,
    DateTime ValidUntil,
    DateTime? SentAt,
    string? OwnerName);
