namespace BullEvents.Api.Models;

public class Lead : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }


    /// <summary>
    /// The originating system's id for this enquiry, when it came from one.
    ///
    /// Unique per company, and the only thing that can tell a retried webhook
    /// delivery from a genuine second enquiry from the same person — which is
    /// the difference between one lead and two.
    /// </summary>
    public string? ExternalId { get; set; }

    /// <summary>
    /// Values for the fields this company added, keyed by the definition's key.
    ///
    /// A jsonb column rather than a row-per-value table: the values are read
    /// with the record every time and almost never on their own, so a join per
    /// field would cost more than it saves. Postgres can index inside it if a
    /// filter ever needs to.
    /// </summary>
    public string? CustomFields { get; set; }

    /* ---------------- personal ---------------- */

    public string? Salutation { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Pincode { get; set; }
    public string? Country { get; set; }

    /// <summary>Sales territory — coarser than city, used for routing and reporting.</summary>
    public string? Zone { get; set; }

    public DateTime? DateOfBirth { get; set; }
    public DateTime? AnniversaryDate { get; set; }
    public string? MaritalStatus { get; set; }
    public string? FatherOrSpouseName { get; set; }
    public string? Occupation { get; set; }
    public string? Designation { get; set; }
    public string? Nationality { get; set; }

    /// <summary>
    /// The other half of the couple, or the guest of honour.
    ///
    /// The enquiry almost never comes from the person the event is for — it
    /// comes from a parent, a sibling or an office administrator — so the name
    /// on the record and the name on the invitation are different fields.
    /// </summary>
    public string? PartnerName { get; set; }
    public string? PartnerPhone { get; set; }
    public string? PartnerEmail { get; set; }

    /// <summary>Who filled the form — couple, parent, office admin.</summary>
    public string? InquirerRole { get; set; }

    /* ---------------- lead ---------------- */

    public string Source { get; set; } = LeadSources.Other;
    public string Stage { get; set; } = LeadStages.New;

    /// <summary>
    /// Finer-grained position inside the stage — "Awaiting callback", "Budget
    /// review". The stage drives the pipeline; this drives the daily queue.
    /// </summary>
    public string? SubStatus { get; set; }

    public string Priority { get; set; } = LeadPriorities.Medium;
    public int? OwnerId { get; set; }

    /// <summary>
    /// A second user who backs the owner on this lead — the manager who joins
    /// the negotiation or covers while the owner is out. Separate from
    /// <see cref="OwnerId"/> because accountability stays with the owner.
    /// </summary>
    public int? SupportingManagerId { get; set; }
    public string? Notes { get; set; }

    /* ---------------- the event ---------------- */

    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }

    /// <summary>The occasion — a value from <see cref="EventTypes"/>.</summary>
    public string? EventType { get; set; }

    /// <summary>Wedding / Social / Corporate — from <see cref="EventCategories"/>.</summary>
    public string? EventCategory { get; set; }

    /// <summary>
    /// The date the event is on.
    ///
    /// The single most decisive field on an event lead: it is what makes two
    /// otherwise identical enquiries worth wildly different effort, it gates
    /// every availability check, and a lead without it cannot be qualified.
    /// Nullable only because an enquiry may genuinely arrive before the family
    /// has fixed a muhurat.
    /// </summary>
    public DateTime? EventDate { get; set; }

    /// <summary>
    /// Last day, for a multi-day booking. Null for a single-day event.
    ///
    /// A wedding is a run of functions — mehendi, haldi, sangeet, the wedding,
    /// the reception — held over three or four days, and the whole run is one
    /// commercial decision. Holding only a start date would make the venue
    /// availability check wrong for the majority of the pipeline.
    /// </summary>
    public DateTime? EventEndDate { get; set; }

    /// <summary>Which session of the day — from <see cref="EventSlots"/>.</summary>
    public string? EventSlot { get; set; }

    /// <summary>
    /// Whether the client can move the date.
    ///
    /// Worth a column of its own because it changes the sale entirely: a
    /// flexible client can be steered to an open date, a fixed one either gets
    /// the date or is lost.
    /// </summary>
    public bool IsDateFlexible { get; set; }

    /// <summary>Expected footfall. Drives venue shortlisting and the per-plate maths.</summary>
    public int? GuestCount { get; set; }

    /// <summary>
    /// Which functions the enquiry covers, comma separated — "Mehendi,Sangeet,Wedding".
    ///
    /// A set rather than one value because a wedding enquiry is always several
    /// functions, and which ones decides how many days and crews are quoted.
    /// </summary>
    public string? Functions { get; set; }

    /// <summary>
    /// Which services the planner is being asked to handle, comma separated —
    /// values from <see cref="ServiceCategories"/>.
    /// </summary>
    public string? ServicesNeeded { get; set; }

    /// <summary>Veg / non-veg / Jain — from <see cref="MealPreferences"/>.</summary>
    public string? MealPreference { get; set; }

    /// <summary>Where the client wants it held — a locality, a city, or "destination".</summary>
    public string? PreferredLocality { get; set; }

    /// <summary>How the client intends to pay — from <see cref="PaymentPreferences"/>.</summary>
    public string? PaymentMode { get; set; }

    /// <summary>The specific venue the lead enquired about, when they named one.</summary>
    public int? InterestedProjectId { get; set; }

    /// <summary>Searching / shortlisted / already booked — from <see cref="EnquiryVenueStatuses"/>.</summary>
    public string? VenueStatus { get; set; }

    /// <summary>Full planning / day-of / partial — from <see cref="PlanningPackages"/>.</summary>
    public string? PlanningPackage { get; set; }

    public int? CeremonyGuestCount { get; set; }
    public int? ReceptionGuestCount { get; set; }

    /// <summary>WedMeGood, WeddingWire, etc. when <see cref="Source"/> is EventPortal.</summary>
    public string? PortalName { get; set; }

    public string? CeremonyStyle { get; set; }
    public DateTime? ConsultAt { get; set; }

    public string? QuestionnaireStatus { get; set; }
    public DateTime? QuestionnaireSentAt { get; set; }
    public DateTime? QuestionnaireCompletedAt { get; set; }
    public string? QuestionnaireToken { get; set; }
    public DateTime? AutoAckAt { get; set; }

    /* ---------------- attribution ---------------- */

    public string? Campaign { get; set; }
    public string? UtmSource { get; set; }
    public string? UtmMedium { get; set; }
    public string? ReferredBy { get; set; }
    public string? Tags { get; set; }

    /* ---------------- lifecycle ---------------- */

    /// <summary>Last time anything at all happened — kept denormalised so list views stay cheap.</summary>
    public DateTime? LastActivityAt { get; set; }

    /// <summary>When first-response is due. Drives the SLA column on the list.</summary>
    public DateTime? SlaDueAt { get; set; }
    public DateTime? FirstResponseAt { get; set; }

    public bool IsConverted { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public int? ConvertedContactId { get; set; }
    public int? ConvertedOpportunityId { get; set; }
    public int? ConvertedBookingId { get; set; }
    public string? LossReason { get; set; }

    /// <summary>Last score written by the local model — cached so lists sort without re-inferring.</summary>
    public int? CachedScore { get; set; }
    public string? CachedBand { get; set; }
    public DateTime? ScoredAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Company? Company { get; set; }
    public Branch? Branch { get; set; }
    public User? Owner { get; set; }
    public User? SupportingManager { get; set; }
    /// <summary>The venue named on the enquiry — <c>Project</c> is the venue entity.</summary>
    public Project? InterestedProject { get; set; }
    public ICollection<LeadActivity> Activities { get; set; } = new List<LeadActivity>();
}
