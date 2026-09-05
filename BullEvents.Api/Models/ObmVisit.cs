namespace BullEvents.Api.Models;

/// <summary>
/// Outdoor Business Meeting — a rep going out to a channel partner, builder,
/// bank or corporate. Distinct from a site visit because the prospect isn't
/// travelling to us: these carry check-in geo, travel and expense, and are
/// measured on leads generated rather than on interest level.
/// </summary>
public class ObmVisit : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    public string VisitCode { get; set; } = string.Empty;

    /// <summary>
    /// The lead this meeting was held for, when it was held for one. Optional
    /// because most OBMs are partner-facing and belong to no single lead —
    /// mirrors how <see cref="SiteVisit.LeadId"/> is optional.
    /// </summary>
    public int? LeadId { get; set; }

    public string PartnerName { get; set; } = string.Empty;
    public string PartnerType { get; set; } = PartnerTypes.ChannelPartner;
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }

    public string Status { get; set; } = VisitStatuses.Scheduled;
    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow.AddDays(1);

    /// <summary>
    /// How long the meeting blocks the agent's calendar. Longer than a site
    /// visit by default because the agent is travelling to the partner.
    /// </summary>
    public int DurationMinutes { get; set; } = 90;

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }

    // ---- field capture ----
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationLabel { get; set; }
    public string? City { get; set; }
    public decimal? DistanceKm { get; set; }
    public decimal? ExpenseAmount { get; set; }

    public string? Purpose { get; set; }
    public string? Outcome { get; set; }
    public string? MeetingNotes { get; set; }
    public int LeadsGenerated { get; set; }
    public decimal? BusinessValue { get; set; }
    public DateTime? NextMeetingAt { get; set; }

    public int? AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Branch? Branch { get; set; }
}
