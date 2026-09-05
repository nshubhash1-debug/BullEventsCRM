namespace BullEvents.Api.Dtos;

/* ------------------------------------------------------------------ *
 * Vendors, crew and fleet.
 *
 * All three answer availability the same way, so all three carry the same
 * pair of nullable fields: a null AvailableOn means nobody asked about dates,
 * which is different from "not available".
 * ------------------------------------------------------------------ */

/* ================================================================== *
 * Vendors
 * ================================================================== */

public record VendorRateDto(
    int Id,
    int VendorId,
    string Service,
    string Name,
    string Basis,
    decimal Rate,
    decimal? SellRate,
    decimal? MarginPerUnit,
    int? MinimumQuantity,
    string? Notes,
    bool IsActive);

public record VendorRateInput(
    string Service,
    string Name,
    string Basis,
    decimal Rate,
    decimal? SellRate = null,
    int? MinimumQuantity = null,
    string? Notes = null,
    bool IsActive = true);

public record VendorDocumentDto(
    int Id,
    string DocumentType,
    string FileName,
    string? Url,
    DateOnly? IssueDate,
    DateOnly? ExpiryDate,
    bool IsExpired,
    string? Notes);

public record VendorDocumentInput(
    string DocumentType,
    string FileName,
    string? Url = null,
    DateOnly? IssueDate = null,
    DateOnly? ExpiryDate = null,
    string? Notes = null);

public record VendorDto(
    int Id,
    string Name,
    string Code,
    string Status,
    IReadOnlyList<string> Services,
    string? ContactPerson,
    string? Phone,
    string? AltPhone,
    string? Email,
    string? Address,
    string? City,
    string? CoverageAreas,
    string? Website,
    string? GstNumber,
    string? PanNumber,
    string? BankAccountName,
    string? BankAccountNumber,
    string? BankIfsc,
    int PaymentTermDays,
    decimal? AdvanceFraction,
    int ConcurrentEventCapacity,
    decimal? Rating,
    int CompletedEvents,
    string? Notes,
    int? OwnerId,
    string? OwnerName,
    int RateCount,
    decimal? LowestRate,
    int ExpiringDocuments,
    /// <summary>Orders already on the asked-about window. Null when no dates were given.</summary>
    int? CommittedOnDates,
    /// <summary>Whether they can still take the job on those dates.</summary>
    bool? IsAvailable,
    IReadOnlyList<VendorRateDto> Rates,
    IReadOnlyList<VendorDocumentDto> Documents,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record VendorInput(
    string Name,
    IReadOnlyList<string> Services,
    string? Code = null,
    string Status = "Active",
    string? ContactPerson = null,
    string? Phone = null,
    string? AltPhone = null,
    string? Email = null,
    string? Address = null,
    string? City = null,
    string? CoverageAreas = null,
    string? Website = null,
    string? GstNumber = null,
    string? PanNumber = null,
    string? BankAccountName = null,
    string? BankAccountNumber = null,
    string? BankIfsc = null,
    int PaymentTermDays = 30,
    decimal? AdvanceFraction = null,
    int ConcurrentEventCapacity = 1,
    string? Notes = null,
    int? OwnerId = null);

public record VendorPoLineDto(
    int Id,
    int? VendorRateId,
    string Description,
    string Basis,
    decimal Quantity,
    decimal Rate,
    decimal? SellRate,
    decimal LineCost,
    decimal LineSell,
    string? Notes,
    int SortOrder);

public record VendorPoLineInput(
    string Description,
    decimal Quantity,
    decimal Rate,
    string Basis = "PerEvent",
    decimal? SellRate = null,
    int? VendorRateId = null,
    string? Notes = null);

public record VendorPaymentDto(
    int Id,
    decimal Amount,
    DateOnly PaidOn,
    string Mode,
    string? Reference,
    string Kind,
    decimal? TdsAmount,
    string? Notes);

public record VendorPaymentInput(
    decimal Amount,
    DateOnly PaidOn,
    string Mode = "NEFT",
    string Kind = "Milestone",
    string? Reference = null,
    decimal? TdsAmount = null,
    string? Notes = null);

public record PurchaseOrderDto(
    int Id,
    string Code,
    string Status,
    int VendorId,
    string VendorName,
    string? VendorPhone,
    string Service,
    int? LeadId,
    int? BookingId,
    int? QuotationId,
    int? ProjectId,
    string? EventName,
    string? EventType,
    string? ClientName,
    string? VenueName,
    string? VenueAddress,
    int? GuestCount,
    DateOnly ServiceDate,
    DateOnly ServiceEndDate,
    TimeSpan? ReportingTime,
    decimal TotalCost,
    decimal TotalSell,
    decimal Margin,
    decimal AmountPaid,
    decimal AmountDue,
    decimal? RetentionAmount,
    int? Rating,
    string? Terms,
    string? Notes,
    int? CoordinatorId,
    string? CoordinatorName,
    int LineCount,
    DateTime? SentAt,
    DateTime? ConfirmedAt,
    DateTime? DeliveredAt,
    DateTime CreatedAt,
    IReadOnlyList<VendorPoLineDto> Lines,
    IReadOnlyList<VendorPaymentDto> Payments);

public record PurchaseOrderInput(
    int VendorId,
    DateOnly ServiceDate,
    DateOnly? ServiceEndDate = null,
    string Service = "Decor",
    int? LeadId = null,
    int? BookingId = null,
    int? QuotationId = null,
    int? ProjectId = null,
    string? EventName = null,
    string? EventType = null,
    string? ClientName = null,
    string? VenueName = null,
    string? VenueAddress = null,
    int? GuestCount = null,
    TimeSpan? ReportingTime = null,
    decimal? RetentionAmount = null,
    string? Terms = null,
    string? Notes = null,
    int? CoordinatorId = null,
    int? OwnerId = null,
    IReadOnlyList<VendorPoLineInput>? Lines = null);

/// <summary>Who can take a job on these dates, for the vendor picker.</summary>
public record VendorAvailabilityRequest(
    DateOnly From,
    DateOnly To,
    string? Service = null,
    string? City = null,
    int? ExcludePurchaseOrderId = null);

/* ================================================================== *
 * Crew
 * ================================================================== */

public record CrewMemberDto(
    int Id,
    string Name,
    string Code,
    string EngagementType,
    string Status,
    int? EmployeeId,
    string? EmployeeName,
    int? SupplierVendorId,
    string? SupplierVendorName,
    string PrimaryRole,
    IReadOnlyList<string> SecondaryRoles,
    int? YearsExperience,
    string? Phone,
    string? AltPhone,
    string? Email,
    string? Address,
    string? City,
    string? PhotoUrl,
    bool WillTravel,
    decimal? DayRate,
    decimal? OvertimeHourlyRate,
    decimal? Rating,
    int EventsWorked,
    int NoShowCount,
    string? IdProofType,
    string? IdProofNumber,
    string? Notes,
    int? OwnerId,
    /// <summary>Assignments clashing with the asked-about window. Null when no dates were given.</summary>
    int? ClashingAssignments,
    bool? IsAvailable,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CrewMemberInput(
    string Name,
    string PrimaryRole,
    string? Code = null,
    string EngagementType = "Freelancer",
    string Status = "Active",
    int? EmployeeId = null,
    int? SupplierVendorId = null,
    IReadOnlyList<string>? SecondaryRoles = null,
    int? YearsExperience = null,
    string? Phone = null,
    string? AltPhone = null,
    string? Email = null,
    string? Address = null,
    string? City = null,
    string? PhotoUrl = null,
    bool WillTravel = false,
    decimal? DayRate = null,
    decimal? OvertimeHourlyRate = null,
    string? IdProofType = null,
    string? IdProofNumber = null,
    string? Notes = null,
    int? OwnerId = null);

public record CrewAssignmentDto(
    int Id,
    int CrewMemberId,
    string CrewMemberName,
    string CrewMemberCode,
    string? CrewMemberPhone,
    string EngagementType,
    string Status,
    string Role,
    int? LeadId,
    int? BookingId,
    int? ProjectId,
    int? PropIssueId,
    string? EventName,
    string? ClientName,
    string? VenueName,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeSpan? ReportingTime,
    TimeSpan? ClosingTime,
    decimal? DayRate,
    decimal OvertimeHours,
    decimal? OvertimeAmount,
    decimal? AllowanceAmount,
    decimal TotalCost,
    bool IsPaid,
    DateOnly? PaidOn,
    int? Rating,
    string? Notes,
    DateTime CreatedAt);

public record CrewAssignmentInput(
    int CrewMemberId,
    DateOnly FromDate,
    DateOnly? ToDate = null,
    string Role = "Helper",
    int? LeadId = null,
    int? BookingId = null,
    int? ProjectId = null,
    int? PropIssueId = null,
    string? EventName = null,
    string? ClientName = null,
    string? VenueName = null,
    TimeSpan? ReportingTime = null,
    TimeSpan? ClosingTime = null,
    decimal? DayRate = null,
    decimal? AllowanceAmount = null,
    string? Notes = null);

/// <summary>
/// Booking several people onto one event in one action — how a coordinator
/// actually staffs a wedding: four bearers, two loaders, a supervisor.
/// </summary>
public record CrewRequisitionInput(
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<int> CrewMemberIds,
    string Role = "Helper",
    int? LeadId = null,
    int? ProjectId = null,
    int? PropIssueId = null,
    string? EventName = null,
    string? ClientName = null,
    string? VenueName = null,
    TimeSpan? ReportingTime = null);

public record CrewAvailabilityRequest(
    DateOnly From,
    DateOnly To,
    string? Role = null,
    string? EngagementType = null,
    string? City = null,
    bool AvailableOnly = false,
    int? ExcludeAssignmentId = null);

/// <summary>How many of each trade are free over a window — the staffing summary.</summary>
public record CrewRoleCoverageDto(
    string Role,
    int OnRoster,
    int Available,
    int Committed,
    decimal? AverageDayRate);

/* ================================================================== *
 * Fleet
 * ================================================================== */

public record VehicleDto(
    int Id,
    string RegistrationNumber,
    string Name,
    string VehicleType,
    string Status,
    int? SupplierVendorId,
    string? SupplierVendorName,
    decimal? PayloadKg,
    decimal? CapacityCubicFeet,
    int? PassengerSeats,
    int? DefaultDriverCrewId,
    string? DefaultDriverName,
    decimal? DayRate,
    decimal? RatePerKm,
    DateOnly? InsuranceExpiry,
    DateOnly? PermitExpiry,
    DateOnly? PucExpiry,
    DateOnly? FitnessExpiry,
    bool HasLapsedPapers,
    DateOnly? LastServicedOn,
    int? OdometerKm,
    int? StoreId,
    string? StoreName,
    string? Notes,
    /// <summary>Trips clashing with the asked-about window. Null when no dates were given.</summary>
    int? ClashingTrips,
    bool? IsAvailable,
    DateTime CreatedAt);

public record VehicleInput(
    string RegistrationNumber,
    string Name,
    string VehicleType = "Tempo",
    string Status = "Active",
    int? SupplierVendorId = null,
    decimal? PayloadKg = null,
    decimal? CapacityCubicFeet = null,
    int? PassengerSeats = null,
    int? DefaultDriverCrewId = null,
    decimal? DayRate = null,
    decimal? RatePerKm = null,
    DateOnly? InsuranceExpiry = null,
    DateOnly? PermitExpiry = null,
    DateOnly? PucExpiry = null,
    DateOnly? FitnessExpiry = null,
    DateOnly? LastServicedOn = null,
    int? OdometerKm = null,
    int? StoreId = null,
    string? Notes = null);

public record TripLoadDto(
    int Id,
    int PropIssueId,
    string IssueCode,
    string? EventName,
    string? VenueName,
    int Pieces,
    decimal? WeightKg,
    int SortOrder);

public record VehicleTripDto(
    int Id,
    string Code,
    string Status,
    string Direction,
    int VehicleId,
    string VehicleRegistration,
    string VehicleName,
    decimal? PayloadKg,
    int? DriverCrewId,
    string? DriverName,
    string? DriverPhone,
    DateOnly FromDate,
    DateOnly ToDate,
    TimeSpan? DepartureTime,
    string? FromLocation,
    string? ToLocation,
    int? LeadId,
    int? ProjectId,
    string? EventName,
    int? StartOdometerKm,
    int? EndOdometerKm,
    int? DistanceKm,
    decimal? FuelCost,
    decimal? TollCost,
    decimal? OtherCost,
    decimal TotalRunningCost,
    decimal? LoadedWeightKg,
    /// <summary>Weight of the gate passes on board, summed from the item weights.</summary>
    decimal? PlannedWeightKg,
    /// <summary>Whether the planned load exceeds what the vehicle may legally carry.</summary>
    bool IsOverloaded,
    string? Notes,
    IReadOnlyList<TripLoadDto> Loads,
    DateTime CreatedAt);

public record VehicleTripInput(
    int VehicleId,
    DateOnly FromDate,
    DateOnly? ToDate = null,
    string Direction = "Outbound",
    int? DriverCrewId = null,
    TimeSpan? DepartureTime = null,
    string? FromLocation = null,
    string? ToLocation = null,
    int? LeadId = null,
    int? ProjectId = null,
    string? EventName = null,
    string? Notes = null,
    IReadOnlyList<int>? PropIssueIds = null);

public record TripCloseInput(
    int? EndOdometerKm = null,
    decimal? FuelCost = null,
    decimal? TollCost = null,
    decimal? OtherCost = null,
    string? Notes = null);

public record VehicleAvailabilityRequest(
    DateOnly From,
    DateOnly To,
    string? VehicleType = null,
    decimal? MinimumPayloadKg = null,
    bool AvailableOnly = false,
    int? ExcludeTripId = null);

/* ================================================================== *
 * Props: kits, pull sheets, utilisation
 * ================================================================== */

public record PropKitLineDto(
    int Id,
    int PropItemId,
    string ItemName,
    string ItemCode,
    string CategoryName,
    string Unit,
    string? PrimaryThumbnailUrl,
    int Quantity,
    bool IsOptional,
    string? Notes,
    int SortOrder,
    int GoodQuantity,
    /// <summary>Free over the asked-about window. Null when no dates were given.</summary>
    int? AvailableQuantity,
    /// <summary>Whether the godown is short on this line for those dates.</summary>
    bool? IsShort);

public record PropKitDto(
    int Id,
    string Name,
    string Code,
    string? Description,
    string? SetupType,
    string? EventType,
    string? CoverImageUrl,
    decimal? RentalRatePerDay,
    /// <summary>Summed from the lines, for a kit with no set price of its own.</summary>
    decimal LineRateTotal,
    decimal? SetupHours,
    int? CrewRequired,
    bool IsActive,
    int LineCount,
    int TotalPieces,
    /// <summary>Lines the godown cannot fully supply over the window, if one was given.</summary>
    int? ShortLines,
    bool? CanFulfil,
    IReadOnlyList<PropKitLineDto> Lines,
    DateTime CreatedAt);

public record PropKitInput(
    string Name,
    string? Code = null,
    string? Description = null,
    string? SetupType = null,
    string? EventType = null,
    string? CoverImageUrl = null,
    decimal? RentalRatePerDay = null,
    decimal? SetupHours = null,
    int? CrewRequired = null,
    bool IsActive = true);

public record PropKitLineInput(
    int PropItemId,
    int Quantity = 1,
    bool IsOptional = false,
    string? Notes = null);

/// <summary>Dropping a whole kit onto a gate pass.</summary>
public record ApplyKitInput(
    int PropKitId,
    /// <summary>Multiplies every line — two mandaps for a two-venue wedding.</summary>
    int Multiplier = 1,
    /// <summary>Leave out the lines marked optional.</summary>
    bool EssentialOnly = false);

/// <summary>What the loaders carry, grouped the way the godown is walked.</summary>
public record PullSheetLineDto(
    int PropItemId,
    string ItemName,
    string ItemCode,
    string Unit,
    string? Size,
    string? StorageLocation,
    int Quantity,
    int? PackingUnit,
    /// <summary>Crates to pull, from the packing unit. One when the item is not crated.</summary>
    int Crates,
    decimal? WeightKg,
    decimal? LineWeightKg,
    bool IsFragile,
    string? Notes);

public record PullSheetGroupDto(
    string Heading,
    int Pieces,
    decimal? WeightKg,
    IReadOnlyList<PullSheetLineDto> Lines);

public record PullSheetDto(
    int PropIssueId,
    string IssueCode,
    string Status,
    string? EventName,
    string? ClientName,
    string? VenueName,
    string? VenueAddress,
    DateOnly DispatchDate,
    DateOnly? EventDate,
    DateOnly ExpectedReturnDate,
    string? VehicleNumber,
    string? DriverName,
    string? SiteInChargeName,
    int TotalLines,
    int TotalPieces,
    int TotalCrates,
    decimal? TotalWeightKg,
    int FragileLines,
    IReadOnlyList<PullSheetGroupDto> Groups);

public record PropUtilisationRowDto(
    int PropItemId,
    string ItemName,
    string ItemCode,
    string CategoryName,
    string Unit,
    string? PrimaryThumbnailUrl,
    int GoodQuantity,
    /// <summary>Gate passes this item went out on, over the window.</summary>
    int TimesIssued,
    int PiecesIssued,
    int PiecesDamaged,
    int PiecesLost,
    /// <summary>Days at least one piece was out, over the window.</summary>
    int DaysOut,
    /// <summary>Days out as a fraction of the window. The number that ranks the list.</summary>
    decimal UtilisationRate,
    decimal EstimatedRevenue,
    decimal LossValue,
    DateOnly? LastIssuedOn);

public record PropUtilisationDto(
    DateOnly From,
    DateOnly To,
    int WindowDays,
    int ItemsTracked,
    int ItemsNeverIssued,
    decimal EstimatedRevenue,
    decimal LossValue,
    IReadOnlyList<PropUtilisationRowDto> Busiest,
    IReadOnlyList<PropUtilisationRowDto> Idle);

/* ================================================================== *
 * One event, everything committed to it
 * ================================================================== */

/// <summary>
/// Everything booked against one lead, across all four inventories.
///
/// The screen a coordinator actually wants the morning of a wedding: what is
/// going out, who is going with it, which suppliers are due, and what is
/// carrying it — in one call rather than four.
/// </summary>
public record EventResourceSheetDto(
    int LeadId,
    string? ClientName,
    string? EventType,
    IReadOnlyList<PropIssueDto> GatePasses,
    IReadOnlyList<CrewAssignmentDto> Crew,
    IReadOnlyList<PurchaseOrderDto> PurchaseOrders,
    IReadOnlyList<VehicleTripDto> Trips,
    int TotalPieces,
    int TotalCrew,
    decimal VendorCost,
    decimal CrewCost,
    decimal TransportCost,
    decimal TotalCommitted);
