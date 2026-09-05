using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BullEvents.Api.Dtos;

/// <summary>
/// The full lead record.
///
/// Written with init-only properties rather than a positional record: at this
/// width a positional constructor is a sixty-argument call that nobody can read
/// or safely reorder, and every caller would break on an inserted field.
/// </summary>
public record LeadDto
{
    public int Id { get; init; }

    // ---- personal ----
    public string? Salutation { get; init; }
    public string Name { get; init; } = string.Empty;
    public string? CompanyName { get; init; }
    public string? Phone { get; init; }
    public string? Phone2 { get; init; }
    public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Pincode { get; init; }
    public string? Country { get; init; }
    public string? Zone { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public DateTime? AnniversaryDate { get; init; }
    public string? MaritalStatus { get; init; }
    public string? FatherOrSpouseName { get; init; }
    public string? Occupation { get; init; }
    public string? Designation { get; init; }
    public string? Nationality { get; init; }

    /// <summary>The other half of the couple, or the guest of honour.</summary>
    public string? PartnerName { get; init; }
    public string? PartnerPhone { get; init; }
    public string? PartnerEmail { get; init; }
    public string? InquirerRole { get; init; }

    // ---- lead ----
    public string Source { get; init; } = string.Empty;
    public string Stage { get; init; } = string.Empty;
    public string? SubStatus { get; init; }
    public string Priority { get; init; } = string.Empty;
    public int BranchId { get; init; }
    public string BranchName { get; init; } = string.Empty;
    public int? OwnerId { get; init; }
    public string? OwnerName { get; init; }
    public int? SupportingManagerId { get; init; }
    public string? SupportingManagerName { get; init; }
    public string? Notes { get; init; }

    // ---- the event ----
    public decimal? BudgetMin { get; init; }
    public decimal? BudgetMax { get; init; }
    public string? EventType { get; init; }
    public string? EventCategory { get; init; }
    public DateTime? EventDate { get; init; }
    public DateTime? EventEndDate { get; init; }
    public string? EventSlot { get; init; }
    public bool IsDateFlexible { get; init; }
    public int? GuestCount { get; init; }
    public string? Functions { get; init; }
    public string? ServicesNeeded { get; init; }
    public string? MealPreference { get; init; }
    public string? PreferredLocality { get; init; }
    public string? PaymentMode { get; init; }

    /// <summary>The venue named on the enquiry, when the client named one.</summary>
    public int? InterestedProjectId { get; init; }
    public string? InterestedProjectName { get; init; }
    public string? VenueStatus { get; init; }
    public string? PlanningPackage { get; init; }
    public int? CeremonyGuestCount { get; init; }
    public int? ReceptionGuestCount { get; init; }
    public string? PortalName { get; init; }
    public string? CeremonyStyle { get; init; }
    public DateTime? ConsultAt { get; init; }
    public string? QuestionnaireStatus { get; init; }
    public DateTime? QuestionnaireSentAt { get; init; }
    public DateTime? QuestionnaireCompletedAt { get; init; }
    public string? QuestionnaireToken { get; init; }
    public DateTime? AutoAckAt { get; init; }

    /// <summary>
    /// Whole days until the event; negative once it has passed, null with no
    /// date. Computed on read — the list sorts by it, and a stored copy would
    /// be wrong by morning.
    /// </summary>
    public int? DaysToEvent { get; init; }

    // ---- attribution ----
    public string? Campaign { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmMedium { get; init; }
    public string? ReferredBy { get; init; }
    public string? Tags { get; init; }

    // ---- intelligence ----
    public int? Score { get; init; }
    public string? Band { get; init; }
    public DateTime? ScoredAt { get; init; }

    // ---- this company's own fields ----

    /// <summary>
    /// Values for the fields this company added, keyed by the definition's key.
    /// Empty for a company that has added none, which is most of them.
    /// </summary>
    public IReadOnlyDictionary<string, JsonNode?> CustomFields { get; init; } =
        new Dictionary<string, JsonNode?>();

    // ---- engagement ----
    //
    // Derived on read from the visit tables rather than stored on the lead:
    // a denormalised copy would go stale the moment a visit is rescheduled.

    /// <summary>Status of this lead's most recent site visit; null if it has none.</summary>
    public string? SiteVisitStatus { get; init; }
    public DateTime? SiteVisitAt { get; init; }
    public int SiteVisitCount { get; init; }

    /// <summary>Status of this lead's most recent OBM visit; null if it has none.</summary>
    public string? ObmStatus { get; init; }
    public DateTime? ObmVisitAt { get; init; }
    public int ObmVisitCount { get; init; }

    // ---- lifecycle ----

    /// <summary>Type of the newest timeline entry — "Call", "Note", "StageChange".</summary>
    public string? LastActivityType { get; init; }

    /// <summary>The newest entry's remarks, so the grid can show what actually happened.</summary>
    public string? LastActivitySummary { get; init; }

    public DateTime? LastActivityAt { get; init; }
    public DateTime? SlaDueAt { get; init; }
    public DateTime? FirstResponseAt { get; init; }

    /// <summary>OnTrack, AtRisk, Breached, Met or None — computed, never stored.</summary>
    public string SlaState { get; init; } = "None";

    public bool IsConverted { get; init; }
    public DateTime? ConvertedAt { get; init; }
    public int? ConvertedContactId { get; init; }
    public int? ConvertedOpportunityId { get; init; }
    public int? ConvertedBookingId { get; init; }
    public string? LossReason { get; init; }
    public int ActivityCount { get; init; }

    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Everything a lead can carry on capture. <see cref="UpdateLeadRequest"/>
/// extends it with the fields that only exist once a lead is in play, so both
/// paths share one mapping function.
/// </summary>
public record CreateLeadRequest
{
    /// <summary>
    /// Values for this company's own fields. Taken as raw JSON because the type
    /// each one should be is a row in a table, not something the compiler knows
    /// — <c>CustomFieldService</c> coerces and refuses them against the
    /// definitions before anything is stored.
    /// </summary>
    public Dictionary<string, JsonElement>? CustomFields { get; init; }

    [Required, MinLength(2)]
    public string Name { get; init; } = string.Empty;

    [Required] public string Source { get; init; } = string.Empty;
    [Required] public int BranchId { get; init; }

    public string? Salutation { get; init; }
    public string? CompanyName { get; init; }
    public string? Phone { get; init; }
    public string? Phone2 { get; init; }
    [EmailAddress] public string? Email { get; init; }
    public string? Address { get; init; }
    public string? City { get; init; }
    public string? State { get; init; }
    public string? Pincode { get; init; }
    public string? Country { get; init; }
    public string? Zone { get; init; }
    public DateTime? DateOfBirth { get; init; }
    public DateTime? AnniversaryDate { get; init; }
    public string? MaritalStatus { get; init; }
    public string? FatherOrSpouseName { get; init; }
    public string? Occupation { get; init; }
    public string? Designation { get; init; }
    public string? Nationality { get; init; }
    public string? PartnerName { get; init; }
    public string? PartnerPhone { get; init; }
    public string? PartnerEmail { get; init; }
    public string? InquirerRole { get; init; }

    public string? Priority { get; init; }
    public string? SubStatus { get; init; }
    public int? OwnerId { get; init; }
    public int? SupportingManagerId { get; init; }
    public string? Notes { get; init; }

    public decimal? BudgetMin { get; init; }
    public decimal? BudgetMax { get; init; }
    public string? EventType { get; init; }

    /// <summary>Optional — derived from <see cref="EventType"/> when left out.</summary>
    public string? EventCategory { get; init; }

    public DateTime? EventDate { get; init; }
    public DateTime? EventEndDate { get; init; }
    public string? EventSlot { get; init; }
    public bool? IsDateFlexible { get; init; }
    public int? GuestCount { get; init; }
    public string? Functions { get; init; }
    public string? ServicesNeeded { get; init; }
    public string? MealPreference { get; init; }
    public string? PreferredLocality { get; init; }
    public string? PaymentMode { get; init; }
    public int? InterestedProjectId { get; init; }
    public string? VenueStatus { get; init; }
    public string? PlanningPackage { get; init; }
    public int? CeremonyGuestCount { get; init; }
    public int? ReceptionGuestCount { get; init; }
    public string? PortalName { get; init; }
    public string? CeremonyStyle { get; init; }
    public DateTime? ConsultAt { get; init; }

    public string? Campaign { get; init; }
    public string? UtmSource { get; init; }
    public string? UtmMedium { get; init; }
    public string? ReferredBy { get; init; }
    public string? Tags { get; init; }
}

public record UpdateLeadRequest : CreateLeadRequest
{
    [Required] public string Stage { get; init; } = string.Empty;
    public string? LossReason { get; init; }
}

/* ------------------------------------------------------------------ *
 * Inline editing
 * ------------------------------------------------------------------ */

/// <summary>
/// A single-field write from the record page.
///
/// Kept separate from the full update so a hover-to-edit on one field does not
/// have to round-trip — and cannot silently clobber — the other fifty.
/// </summary>
public record PatchLeadFieldRequest
{
    [Required] public string Field { get; init; } = string.Empty;

    /// <summary>Serialised as text; the server parses it against the field's real type.</summary>
    public string? Value { get; init; }
}

/* ------------------------------------------------------------------ *
 * Activity
 * ------------------------------------------------------------------ */

public record LeadActivityDto(
    int Id,
    string Type,
    string? Remarks,
    string? FromStage,
    string? ToStage,
    string ActorName,
    DateTime CreatedAt
);

public record CreateLeadActivityRequest(
    [Required] string Type,
    string? Remarks
);

/* ------------------------------------------------------------------ *
 * Related records and history
 * ------------------------------------------------------------------ */

public record RelatedRecordDto(
    string Kind,
    int Id,
    string Title,
    string? Subtitle,
    string? Status,
    decimal? Amount,
    DateTime At
);

public record LeadRelatedDto(
    IReadOnlyList<RelatedRecordDto> Calls,
    IReadOnlyList<RelatedRecordDto> SiteVisits,
    IReadOnlyList<RelatedRecordDto> ObmVisits,
    IReadOnlyList<RelatedRecordDto> FollowUps,
    IReadOnlyList<RelatedRecordDto> Quotations,
    IReadOnlyList<RelatedRecordDto> Opportunities,
    int TotalCalls,
    int TotalSiteVisits,
    int CompletedSiteVisits,
    int TotalObmVisits,
    int TotalFollowUps,
    int OpenFollowUps,
    int TotalQuotations,
    decimal QuotedValue
);

public record FieldChangeDto(
    DateTime At,
    string UserName,
    string Field,
    string? From,
    string? To,
    string Action
);

public record LeadTransferRequest(
    [Required] int OwnerId,
    string? Reason
);
