namespace BullEvents.Api.Dtos;

public record DiaryRequest(
    DateOnly From,
    DateOnly To,
    string? Slot = null);

public record InventoryDiaryDto(
    int ProjectId,
    string ProjectName,
    DateOnly From,
    DateOnly To,
    string Slot,
    IReadOnlyList<VenuePeakWindowDto> PeakWindows,
    IReadOnlyList<DiarySpaceRowDto> Spaces);

public record VenuePeakWindowDto(
    int Id,
    DateOnly StartDate,
    DateOnly EndDate,
    string Label,
    decimal PremiumFraction);

public record DiarySpaceRowDto(
    int UnitId,
    string Name,
    string SpaceType,
    int SeatingCapacity,
    int FloatingCapacity,
    bool IsOutdoor,
    bool IsAirConditioned,
    int MinimumPlates,
    decimal PricePerPlate,
    decimal BasePrice,
    int TurnaroundHours,
    string CatalogueStatus,
    IReadOnlyList<DiaryCellDto> Days);

public record DiaryCellDto(
    DateOnly Date,
    string Status,
    string? ClientName,
    string? EventType,
    string? Slot,
    int? GuestCount,
    DateTime? HoldExpiresAt,
    int? SpaceBookingId,
    int? LeadId,
    int? QuotationId,
    bool IsPeak,
    string? Notes);

public record DiaryMoveRequest(
    int UnitId,
    DateOnly EventDate,
    DateOnly? EventEndDate,
    string Slot,
    string Status,
    string? ClientName,
    string? EventType,
    int? GuestCount,
    int? LeadId,
    int? ContactId,
    int? QuotationId,
    string? Notes,
    int? HoldHours,
    /// <summary>When set, all unit ids share one GroupRef (multi-space event).</summary>
    IReadOnlyList<int>? AdditionalUnitIds);

public record SpaceConflictDto(
    int UnitId,
    string UnitName,
    DateOnly EventDate,
    string Slot,
    string Status,
    string? ClientName);

public record VenuePackageDto(
    int Id,
    int ProjectId,
    string ProjectName,
    string Name,
    string Code,
    string? Description,
    string? PlanningPackage,
    decimal? IndicativeRental,
    decimal? IndicativePerPlate,
    int? DefaultMinimumPlates,
    int? DefaultGuestCount,
    bool IsActive,
    IReadOnlyList<VenuePackageSpaceDto> Spaces);

public record VenuePackageSpaceDto(
    int UnitId,
    string UnitName,
    string SpaceType,
    int SeatingCapacity,
    int SortOrder,
    string? DefaultSlot);

public record VenuePackageInput(
    int ProjectId,
    string Name,
    string Code,
    string? Description,
    string? PlanningPackage,
    decimal? IndicativeRental,
    decimal? IndicativePerPlate,
    int? DefaultMinimumPlates,
    int? DefaultGuestCount,
    bool IsActive,
    IReadOnlyList<int> UnitIds);

public record VenuePeakDateInput(
    int ProjectId,
    DateOnly StartDate,
    DateOnly EndDate,
    string Label,
    decimal PremiumFraction,
    string? Notes);

public record SpaceDossierExtraDto(
    int SeatingCapacity,
    int FloatingCapacity,
    int? TheatreCapacity,
    bool IsOutdoor,
    bool IsAirConditioned,
    bool HasStage,
    bool HasAttachedKitchen,
    int MinimumPlates,
    decimal PricePerPlate,
    decimal PeakDatePremium,
    decimal SecurityDeposit,
    decimal BasePrice,
    int TurnaroundHours,
    string? LayoutImageUrl,
    bool AllowsOutsideCatering,
    bool AllowsAlcohol,
    bool AllowsOpenFlame,
    TimeSpan? NoiseCurfew,
    int? ParkingCapacity,
    int? GuestRooms,
    IReadOnlyList<DiaryCellDto> UpcomingBookings);
