namespace BullEvents.Api.Dtos;

/* ------------------------------------------------------------------ *
 * Rentable asset inventory — props, décor, furniture, fabric, florals.
 * ------------------------------------------------------------------ */

public record PropCategoryDto(
    int Id,
    string Name,
    string Code,
    int? ParentId,
    string DefaultItemType,
    int SortOrder,
    bool IsActive,
    int ItemCount,
    int TotalGoodQuantity);

public record PropCategoryInput(
    string Name,
    string Code,
    int? ParentId = null,
    string DefaultItemType = "Prop",
    int SortOrder = 0,
    bool IsActive = true);

public record PropStoreDto(
    int Id,
    string Name,
    string Code,
    string? City,
    string? Address,
    int? KeeperId,
    string? KeeperName,
    bool IsActive,
    int ItemCount);

public record PropStoreInput(
    string Name,
    string Code,
    string? City = null,
    string? Address = null,
    int? KeeperId = null,
    bool IsActive = true);

public record PropPhotoDto(
    int Id,
    string Url,
    string? ThumbnailUrl,
    string? Caption,
    int SortOrder);

/// <summary>
/// One catalogue line as the grid and the picker read it.
///
/// <see cref="AvailableQuantity"/> is null unless the caller asked about a date
/// window — an availability number with no dates behind it is the exact
/// confusion this module exists to prevent.
/// </summary>
public record PropItemDto(
    int Id,
    int CategoryId,
    string CategoryName,
    int? StoreId,
    string? StoreName,
    string Name,
    string Code,
    string ItemType,
    string Status,
    string Ownership,
    string? Size,
    string? Colour,
    string? Material,
    string Unit,
    string? Description,
    string? Tags,
    int GoodQuantity,
    int RepairableQuantity,
    int DamagedQuantity,
    int OnHandQuantity,
    int ReorderLevel,
    bool IsBelowReorderLevel,
    decimal? RentalRatePerDay,
    decimal? PurchaseCost,
    decimal? ReplacementValue,
    string? SupplierName,
    DateOnly? PurchaseDate,
    decimal? WeightKg,
    int? PackingUnit,
    bool IsFragile,
    bool IsSerialised,
    int TurnaroundDays,
    string? StorageLocation,
    int? OwnerId,
    string? OwnerName,
    string? PrimaryPhotoUrl,
    string? PrimaryThumbnailUrl,
    int PhotoCount,
    IReadOnlyList<PropPhotoDto> Photos,
    /// <summary>Held by other events across the requested window, if one was given.</summary>
    int? ReservedQuantity,
    /// <summary>Good stock minus those holds. Null when no window was requested.</summary>
    int? AvailableQuantity,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record PropItemInput(
    int CategoryId,
    string Name,
    string? Code = null,
    int? StoreId = null,
    string ItemType = "Prop",
    string Status = "Active",
    string Ownership = "Owned",
    string? Size = null,
    string? Colour = null,
    string? Material = null,
    string Unit = "PCS",
    string? Description = null,
    string? Tags = null,
    int GoodQuantity = 0,
    int RepairableQuantity = 0,
    int DamagedQuantity = 0,
    int ReorderLevel = 0,
    decimal? RentalRatePerDay = null,
    decimal? PurchaseCost = null,
    decimal? ReplacementValue = null,
    string? SupplierName = null,
    DateOnly? PurchaseDate = null,
    decimal? WeightKg = null,
    int? PackingUnit = null,
    bool IsFragile = false,
    bool IsSerialised = false,
    int TurnaroundDays = 0,
    string? StorageLocation = null,
    int? OwnerId = null);

public record PropPhotoInput(string Url, string? ThumbnailUrl = null, string? Caption = null);

/* ---------------- rates ---------------- */

/// <summary>
/// One item's commercial numbers.
///
/// Every field is nullable and only the ones present are written, so a screen
/// that only sets rental rates cannot blank out the replacement values somebody
/// else filled in last week.
/// </summary>
public record PropRateRow(
    int PropItemId,
    decimal? RentalRatePerDay = null,
    decimal? ReplacementValue = null,
    decimal? PurchaseCost = null,
    int? ReorderLevel = null,
    decimal? WeightKg = null,
    int? PackingUnit = null,
    string? StorageLocation = null);

/// <summary>
/// Setting rates across many items at once.
///
/// The catalogue arrived from a workbook with 734 lines and no rate column, so
/// pricing it one item at a time was never going to happen. This is the endpoint
/// that makes it an afternoon rather than a fortnight.
/// </summary>
public record PropRateBulkInput(IReadOnlyList<PropRateRow> Rows);

public record PropRateBulkResultDto(
    int Updated,
    int Skipped,
    /// <summary>Items still without a rental rate, after this write.</summary>
    int StillUnpriced,
    int TotalItems,
    decimal CatalogueValue,
    IReadOnlyList<string> Warnings);

/// <summary>
/// How much of the catalogue is priced, by category.
///
/// The screen opens on this because the useful first question is not "what is
/// this vase worth" but "which sections have I not touched yet".
/// </summary>
public record PropPricingCoverageDto(
    int CategoryId,
    string CategoryName,
    int ItemCount,
    int Priced,
    int Unpriced,
    decimal? AverageRate,
    decimal CategoryValue);

public record PropPricingSummaryDto(
    int TotalItems,
    int Priced,
    int Unpriced,
    decimal CatalogueValue,
    decimal? AverageRate,
    IReadOnlyList<PropPricingCoverageDto> ByCategory);

/* ---------------- stock ---------------- */

public record PropMovementDto(
    int Id,
    int PropItemId,
    string ItemName,
    string ItemCode,
    string MovementType,
    int Quantity,
    string? FromCondition,
    string? ToCondition,
    int BalanceAfter,
    int? PropIssueId,
    string? IssueCode,
    int? LeadId,
    decimal? Amount,
    string? Notes,
    string? HandledBy,
    DateTime MovedAt,
    string? RecordedBy);

/// <summary>
/// A hand-written stock change — a purchase, a breakage, a physical count.
///
/// <see cref="Quantity"/> is always given unsigned; the sign comes from the
/// movement type, so a user can never book a purchase that removes stock.
/// The one exception is Adjustment, where the caller supplies the direction
/// through <see cref="IsIncrease"/>.
/// </summary>
public record PropMovementInput(
    int PropItemId,
    string MovementType,
    int Quantity,
    string? FromCondition = null,
    string? ToCondition = null,
    decimal? Amount = null,
    string? Notes = null,
    string? HandledBy = null,
    bool IsIncrease = true,
    DateTime? MovedAt = null);

/* ---------------- gate pass ---------------- */

public record PropIssueLineDto(
    int Id,
    int PropItemId,
    string ItemName,
    string ItemCode,
    string CategoryName,
    string Unit,
    string? PrimaryThumbnailUrl,
    int ReservedQuantity,
    int IssuedQuantity,
    int ReturnedQuantity,
    int DamagedQuantity,
    int LostQuantity,
    int ConsumedQuantity,
    int PendingQuantity,
    decimal? RatePerDay,
    int ChargeableDays,
    decimal LineTotal,
    string? Notes,
    int SortOrder,
    /// <summary>What the godown can actually give for this pass's window.</summary>
    int? AvailableQuantity);

public record PropIssueDto(
    int Id,
    string Code,
    string Status,
    int? LeadId,
    int? BookingId,
    int? QuotationId,
    int? ProjectId,
    string? EventName,
    string? EventType,
    string? ClientName,
    string? VenueName,
    string? VenueAddress,
    DateOnly DispatchDate,
    DateOnly? EventDate,
    DateOnly ExpectedReturnDate,
    DateOnly? ActualReturnDate,
    int? StoreId,
    string? StoreName,
    string? VehicleNumber,
    string? DriverName,
    string? DriverPhone,
    int? SiteInChargeId,
    string? SiteInChargeName,
    string? Notes,
    decimal? DamageRecovery,
    int? OwnerId,
    string? OwnerName,
    int LineCount,
    int TotalReserved,
    int TotalIssued,
    int TotalReturned,
    int TotalPending,
    decimal EstimatedValue,
    bool IsOverdue,
    DateTime? DispatchedAt,
    DateTime? ClosedAt,
    DateTime CreatedAt,
    IReadOnlyList<PropIssueLineDto> Lines);

public record PropIssueLineInput(
    int PropItemId,
    int Quantity,
    decimal? RatePerDay = null,
    int ChargeableDays = 1,
    string? Notes = null);

public record PropIssueInput(
    DateOnly DispatchDate,
    DateOnly ExpectedReturnDate,
    DateOnly? EventDate = null,
    int? LeadId = null,
    int? BookingId = null,
    int? QuotationId = null,
    int? ProjectId = null,
    string? EventName = null,
    string? EventType = null,
    string? ClientName = null,
    string? VenueName = null,
    string? VenueAddress = null,
    int? StoreId = null,
    string? VehicleNumber = null,
    string? DriverName = null,
    string? DriverPhone = null,
    int? SiteInChargeId = null,
    string? Notes = null,
    int? OwnerId = null,
    IReadOnlyList<PropIssueLineInput>? Lines = null);

/// <summary>
/// The counts taken at the gate as the truck is loaded.
///
/// Sent per line rather than as a single "dispatch it all", because the loaded
/// quantity routinely differs from the reserved one and the difference has to
/// be recorded at the moment it happens, not reconstructed afterwards.
/// </summary>
public record PropDispatchLineInput(int PropIssueLineId, int IssuedQuantity);

public record PropDispatchInput(
    IReadOnlyList<PropDispatchLineInput> Lines,
    string? VehicleNumber = null,
    string? DriverName = null,
    string? DriverPhone = null,
    string? HandledBy = null,
    string? Notes = null);

/// <summary>What came back, per line, split by the condition it came back in.</summary>
public record PropReturnLineInput(
    int PropIssueLineId,
    int ReturnedQuantity,
    int DamagedQuantity = 0,
    int LostQuantity = 0,
    int ConsumedQuantity = 0,
    string? Notes = null);

public record PropReturnInput(
    IReadOnlyList<PropReturnLineInput> Lines,
    DateOnly? ReturnDate = null,
    string? HandledBy = null,
    string? Notes = null);

/* ---------------- availability ---------------- */

public record PropAvailabilityRequest(
    DateOnly From,
    DateOnly To,
    IReadOnlyList<int>? ItemIds = null,
    int? CategoryId = null,
    /// <summary>A pass to exclude from the holds — so editing it does not fight itself.</summary>
    int? ExcludePropIssueId = null);

public record PropAvailabilityHoldDto(
    int ReservationId,
    int? PropIssueId,
    string? IssueCode,
    int Quantity,
    DateOnly FromDate,
    DateOnly ToDate,
    string? EventName,
    string? ClientName,
    string Status);

public record PropAvailabilityDto(
    int PropItemId,
    string ItemName,
    string ItemCode,
    string CategoryName,
    string Unit,
    string? PrimaryThumbnailUrl,
    int GoodQuantity,
    int ReservedQuantity,
    int AvailableQuantity,
    IReadOnlyList<PropAvailabilityHoldDto> Holds);

/// <summary>One item's day-by-day availability, for the planner's calendar strip.</summary>
public record PropCalendarCellDto(DateOnly Date, int Reserved, int Available);

public record PropCalendarRowDto(
    int PropItemId,
    string ItemName,
    string ItemCode,
    string Unit,
    int GoodQuantity,
    IReadOnlyList<PropCalendarCellDto> Days);

/* ---------------- dashboard ---------------- */

public record PropCategorySummaryDto(
    int CategoryId,
    string CategoryName,
    int ItemCount,
    int GoodQuantity,
    int RepairableQuantity,
    int DamagedQuantity,
    decimal EstimatedValue);

public record PropDashboardDto(
    int TotalItems,
    int TotalPieces,
    int GoodPieces,
    int RepairablePieces,
    int DamagedPieces,
    decimal CatalogueValue,
    int ItemsBelowReorder,
    int OpenIssues,
    int DispatchedIssues,
    int OverdueIssues,
    int PiecesOutOnEvents,
    int DispatchesThisWeek,
    int ReturnsDueThisWeek,
    IReadOnlyList<PropCategorySummaryDto> Categories,
    IReadOnlyList<PropItemDto> LowStock,
    IReadOnlyList<PropIssueDto> Overdue);
