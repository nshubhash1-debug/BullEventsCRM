namespace BullEvents.Api.Dtos;

/* ------------------------------------------------------------------ *
 * Contacts / customer database
 * ------------------------------------------------------------------ */

public record ContactDto(
    int Id,
    string? Salutation,
    string FirstName,
    string? LastName,
    string FullName,
    string? Designation,
    string? AccountName,
    string? Phone,
    string? Phone2,
    string? Email,
    string? WhatsAppNumber,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? Pincode,
    string Type,
    string LifecycleStage,
    string Source,
    string? Tags,
    string? Segment,
    decimal LifetimeValue,
    int DealCount,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? PreferredConfiguration,
    string? PreferredLocality,
    bool DoNotCall,
    bool DoNotEmail,
    bool WhatsAppOptIn,
    string? PanNumber,
    string? Gstin,
    DateTime? DateOfBirth,
    int BranchId,
    string BranchName,
    int? OwnerId,
    string? OwnerName,
    int? ConvertedFromLeadId,
    int OpenOpportunities,
    DateTime? LastActivityAt,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ContactInput(
    string? Salutation,
    string FirstName,
    string? LastName,
    string? Designation,
    string? AccountName,
    string? Phone,
    string? Phone2,
    string? Email,
    string? WhatsAppNumber,
    string? Address,
    string? City,
    string? State,
    string? Country,
    string? Pincode,
    string Type,
    string LifecycleStage,
    string Source,
    string? Tags,
    decimal? BudgetMin,
    decimal? BudgetMax,
    string? PreferredConfiguration,
    string? PreferredLocality,
    bool DoNotCall,
    bool DoNotEmail,
    bool WhatsAppOptIn,
    string? PanNumber,
    string? Gstin,
    DateTime? DateOfBirth,
    int BranchId,
    int? OwnerId
);

/* ------------------------------------------------------------------ *
 * Opportunities
 * ------------------------------------------------------------------ */

public record OpportunityDto(
    int Id,
    string Name,
    int? ContactId,
    string? ContactName,
    int? LeadId,
    int? ProjectId,
    string? ProjectName,
    int? UnitId,
    string? UnitNumber,
    string Stage,
    string Type,
    string Source,
    string ForecastCategory,
    decimal Amount,
    decimal? ExpectedCommission,
    string Currency,
    int Probability,
    int? AiProbability,
    string? AiBand,
    DateTime ExpectedCloseDate,
    DateTime? ActualCloseDate,
    int DaysInStage,
    int BranchId,
    string BranchName,
    int? OwnerId,
    string? OwnerName,
    string? NextStep,
    DateTime? NextStepDueAt,
    string? LossReason,
    string? CompetitorName,
    string? Description,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record OpportunityInput(
    string Name,
    int? ContactId,
    int? LeadId,
    int? ProjectId,
    int? UnitId,
    string Stage,
    string Type,
    string Source,
    decimal Amount,
    decimal? ExpectedCommission,
    int Probability,
    DateTime ExpectedCloseDate,
    int BranchId,
    int? OwnerId,
    string? NextStep,
    DateTime? NextStepDueAt,
    string? LossReason,
    string? CompetitorName,
    string? Description
);

/* ------------------------------------------------------------------ *
 * Follow-ups
 * ------------------------------------------------------------------ */

public record FollowUpDto(
    int Id,
    string Subject,
    string? Description,
    string RelatedType,
    int RelatedId,
    string RelatedName,
    string Channel,
    string Status,
    string Priority,
    DateTime DueAt,
    DateTime? ReminderAt,
    DateTime? CompletedAt,
    string? Outcome,
    int SlaMinutes,
    bool IsOverdue,
    int MinutesToDue,
    int BranchId,
    string BranchName,
    int? OwnerId,
    string? OwnerName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record FollowUpInput(
    string Subject,
    string? Description,
    string RelatedType,
    int RelatedId,
    string RelatedName,
    string Channel,
    string Status,
    string Priority,
    DateTime DueAt,
    DateTime? ReminderAt,
    string? Outcome,
    int SlaMinutes,
    int BranchId,
    int? OwnerId
);

/// <summary>
/// A status change on a follow-up or task, from any list it appears on.
/// Rescheduling here means a new <paramref name="DueAt"/> — a follow-up has a
/// due date, not a slot. <paramref name="Outcome"/> is what shows on the
/// lead's timeline, so it is worth asking for on a cancellation.
/// </summary>
public record FollowUpStatusRequest(string Status, DateTime? DueAt, string? Outcome);

/// <summary>
/// The same move for a site or OBM visit. <paramref name="ScheduledAt"/> is
/// required when moving to Rescheduled and ignored otherwise.
/// </summary>
public record VisitStatusRequest(string Status, DateTime? ScheduledAt, string? Reason);

/* ------------------------------------------------------------------ *
 * Calls
 * ------------------------------------------------------------------ */

public record CallLogDto(
    int Id,
    string RelatedType,
    int RelatedId,
    string RelatedName,
    string Direction,
    string Outcome,
    string? Disposition,
    string? PhoneNumber,
    DateTime StartedAt,
    int DurationSeconds,
    int? WaitSeconds,
    int? AgentId,
    string AgentName,
    string? Notes,
    string? RecordingUrl,
    double? SentimentScore,
    string? SentimentLabel,
    DateTime? FollowUpAt,
    int BranchId,
    string BranchName,
    DateTime CreatedAt
);

public record CallLogInput(
    string RelatedType,
    int RelatedId,
    string RelatedName,
    string Direction,
    string Outcome,
    string? Disposition,
    string? PhoneNumber,
    DateTime StartedAt,
    int DurationSeconds,
    int? WaitSeconds,
    int? AgentId,
    string? Notes,
    DateTime? FollowUpAt,
    int BranchId
);

/* ------------------------------------------------------------------ *
 * Site visits
 * ------------------------------------------------------------------ */

public record SiteVisitDto(
    int Id,
    string VisitCode,
    int? LeadId,
    int? ContactId,
    int? OpportunityId,
    int? ProjectId,
    string? ProjectName,
    int? UnitId,
    string? UnitNumber,
    string VisitorName,
    string? VisitorPhone,
    int PartySize,
    string VisitType,
    string Status,
    DateTime ScheduledAt,
    /// <summary>How long the calendar slot is held for.</summary>
    int SlotMinutes,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    /// <summary>Measured time on site, once the visit has been checked out.</summary>
    int? DurationMinutes,
    int? HostId,
    string? HostName,
    string? TransportMode,
    string? PickupLocation,
    string? Feedback,
    string? InterestLevel,
    int? Rating,
    decimal? BudgetDiscussed,
    string? NextAction,
    string? CancellationReason,
    int BranchId,
    string BranchName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record SiteVisitInput(
    int? LeadId,
    int? ContactId,
    int? OpportunityId,
    int? ProjectId,
    int? UnitId,
    string VisitorName,
    string? VisitorPhone,
    int PartySize,
    string VisitType,
    string Status,
    DateTime ScheduledAt,
    int DurationMinutes,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    int? HostId,
    string? TransportMode,
    string? PickupLocation,
    string? Feedback,
    string? InterestLevel,
    int? Rating,
    decimal? BudgetDiscussed,
    string? NextAction,
    string? CancellationReason,
    int BranchId,
    /// <summary>Book on top of an existing meeting. Off unless the rep insists.</summary>
    bool AllowOverlap = false
);

/* ------------------------------------------------------------------ *
 * OBM visits
 * ------------------------------------------------------------------ */

public record ObmVisitDto(
    int Id,
    string VisitCode,
    /// <summary>The lead this meeting was held for, when it was held for one.</summary>
    int? LeadId,
    /// <summary>Resolved on read — the grid needs a name, not an id.</summary>
    string? LeadName,
    string PartnerName,
    string PartnerType,
    string? ContactPerson,
    string? ContactPhone,
    string Status,
    DateTime ScheduledAt,
    /// <summary>How long the calendar slot is held for.</summary>
    int SlotMinutes,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    /// <summary>Measured meeting length, once checked out.</summary>
    int? DurationMinutes,
    double? Latitude,
    double? Longitude,
    string? LocationLabel,
    string? City,
    decimal? DistanceKm,
    decimal? ExpenseAmount,
    string? Purpose,
    string? Outcome,
    string? MeetingNotes,
    int LeadsGenerated,
    decimal? BusinessValue,
    DateTime? NextMeetingAt,
    int? AgentId,
    string AgentName,
    int BranchId,
    string BranchName,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record ObmVisitInput(
    int? LeadId,
    string PartnerName,
    string PartnerType,
    string? ContactPerson,
    string? ContactPhone,
    string Status,
    DateTime ScheduledAt,
    int DurationMinutes,
    DateTime? CheckInAt,
    DateTime? CheckOutAt,
    double? Latitude,
    double? Longitude,
    string? LocationLabel,
    string? City,
    decimal? DistanceKm,
    decimal? ExpenseAmount,
    string? Purpose,
    string? Outcome,
    string? MeetingNotes,
    int LeadsGenerated,
    decimal? BusinessValue,
    DateTime? NextMeetingAt,
    int? AgentId,
    int BranchId,
    /// <summary>Book anyway on top of an existing meeting. Off by default.</summary>
    bool AllowOverlap = false
);

/* ------------------------------------------------------------------ *
 * Quotations
 * ------------------------------------------------------------------ */

public record QuotationLineDto(
    int Id,
    string Description,
    string? Category,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    decimal DiscountPercent,
    decimal LineTotal,
    int SortOrder
);

public record QuotationDto(
    int Id,
    string QuoteNumber,
    string Title,
    int Version,
    int? ContactId,
    int? LeadId,
    int? OpportunityId,
    int? ProjectId,
    string? ProjectName,
    int? UnitId,
    string? UnitNumber,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? BillingAddress,
    string Status,
    DateTime IssueDate,
    DateTime ValidUntil,
    DateTime? SentAt,
    DateTime? RespondedAt,
    bool IsExpired,
    string Currency,
    decimal Subtotal,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxPercent,
    decimal TaxAmount,
    /// <summary>The unit cost. The payment plan percentages are struck on this.</summary>
    decimal Total,
    decimal ChargesTotal,
    /// <summary>Unit cost plus every other head — what the buyer actually pays.</summary>
    decimal GrandTotal,
    /// <summary>NotRequired, Pending, Approved or Rejected.</summary>
    string ApprovalStatus,
    string? PaymentTerms,
    string? Notes,
    string? TermsAndConditions,
    string? RejectionReason,
    int BranchId,
    string BranchName,
    int? OwnerId,
    string? OwnerName,
    IReadOnlyList<QuotationLineDto> Lines,
    DateTime CreatedAt,
    DateTime UpdatedAt
);

public record QuotationLineInput(
    string Description,
    string? Category,
    decimal Quantity,
    string? Unit,
    decimal UnitPrice,
    decimal DiscountPercent
);

public record QuotationInput(
    string Title,
    int? ContactId,
    int? LeadId,
    int? OpportunityId,
    int? ProjectId,
    int? UnitId,
    string CustomerName,
    string? CustomerEmail,
    string? CustomerPhone,
    string? BillingAddress,
    string Status,
    DateTime IssueDate,
    DateTime ValidUntil,
    decimal DiscountPercent,
    decimal TaxPercent,
    string? PaymentTerms,
    string? Notes,
    string? TermsAndConditions,
    string? RejectionReason,
    int BranchId,
    int? OwnerId,
    IReadOnlyList<QuotationLineInput> Lines
);

/* ------------------------------------------------------------------ *
 * Inventory
 * ------------------------------------------------------------------ */

public record ProjectDto(
    int Id,
    string Name,
    string Code,
    string? Developer,
    string Type,
    string Status,
    string? City,
    string? Locality,
    string? Address,
    string? ReraNumber,
    DateTime? LaunchDate,
    DateTime? PossessionDate,
    decimal? PriceMin,
    decimal? PriceMax,
    string? Amenities,
    string? Description,
    int TotalUnits,
    int AvailableUnits,
    int BookedUnits,
    decimal InventoryValue,
    DateTime CreatedAt
);

public record TowerDto(int Id, string Name, int FloorCount, int UnitsPerFloor, string Status, int UnitCount);

public record UnitDto(
    int Id,
    int ProjectId,
    string ProjectName,
    int? TowerId,
    string? TowerName,
    string UnitNumber,
    int Floor,
    string Configuration,
    decimal CarpetArea,
    decimal? BuiltUpArea,
    decimal? SuperArea,
    string AreaUnit,
    string? Facing,
    string? ViewType,
    int Bathrooms,
    int Balconies,
    int ParkingSlots,
    bool IsCornerUnit,
    bool VastuCompliant,

    /* ---- capacity: what decides whether a space fits the party ---- */

    int SeatingCapacity,
    int FloatingCapacity,
    int? TheatreCapacity,
    bool IsAirConditioned,
    bool IsOutdoor,
    bool HasStage,
    bool HasAttachedKitchen,

    string Status,
    decimal BasePrice,
    decimal PricePerSqft,
    decimal FloorRisePremium,
    decimal PlcCharges,
    decimal TotalPrice,

    /* ---- event pricing ---- */

    decimal PricePerPlate,
    int MinimumPlates,
    decimal PeakDatePremium,
    decimal SecurityDeposit,

    int? HeldByUserId,
    string? HeldByName,
    DateTime? HeldUntil,
    string? HoldReason,
    int? BookedByContactId,
    DateTime? BookedAt,
    DateTime CreatedAt
);

public record UnitInput(
    int ProjectId,
    int? TowerId,
    string UnitNumber,
    int Floor,
    string Configuration,
    decimal CarpetArea,
    decimal? BuiltUpArea,
    decimal? SuperArea,
    string? Facing,
    string? ViewType,
    int Bathrooms,
    int Balconies,
    int ParkingSlots,
    bool IsCornerUnit,
    bool VastuCompliant,
    string Status,
    decimal BasePrice,
    decimal PricePerSqft,
    decimal FloorRisePremium,
    decimal PlcCharges,

    /* ---------------- capacity and event pricing ---------------- */
    //
    // Defaulted so the shape stays compatible with callers written against the
    // property build, but a space created without a seating capacity cannot be
    // shortlisted for anything — the venue screens should always send these.

    int SeatingCapacity = 0,
    int FloatingCapacity = 0,
    int? TheatreCapacity = null,
    bool IsAirConditioned = true,
    bool IsOutdoor = false,
    bool HasStage = false,
    bool HasAttachedKitchen = false,

    decimal PricePerPlate = 0m,
    /// <summary>The plate floor this space bills at, whatever the guest count.</summary>
    int MinimumPlates = 0,
    decimal PeakDatePremium = 0m,
    decimal SecurityDeposit = 0m,
    int TurnaroundHours = 0,
    string? LayoutImageUrl = null
);

public record HoldUnitRequest(
    int Hours,
    string? Reason,
    DateOnly? EventDate = null,
    string? EventSlot = null,
    string? ClientName = null);

/* ------------------------------------------------------------------ *
 * Shared metadata — drives the client's filter builder
 * ------------------------------------------------------------------ */

public record FilterOptionDto(string Label, string Value);

public record FilterFieldDto(
    string Id,
    string Label,
    string Type,
    IReadOnlyList<FilterOptionDto>? Options,
    string? Group
);
