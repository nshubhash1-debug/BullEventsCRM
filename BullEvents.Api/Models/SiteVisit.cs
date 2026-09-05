namespace BullEvents.Api.Models;

/// <summary>
/// A prospect walking a property. The single strongest conversion signal in
/// residential sales, so it gets its own object rather than living as a
/// generic activity.
/// </summary>
public class SiteVisit : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    public string VisitCode { get; set; } = string.Empty;

    public int? LeadId { get; set; }
    public int? ContactId { get; set; }
    public int? OpportunityId { get; set; }
    public int? ProjectId { get; set; }
    public int? UnitId { get; set; }

    public string VisitorName { get; set; } = string.Empty;
    public string? VisitorPhone { get; set; }
    public int PartySize { get; set; } = 1;

    public string VisitType { get; set; } = VisitTypes.FirstVisit;
    public string Status { get; set; } = VisitStatuses.Scheduled;

    public DateTime ScheduledAt { get; set; } = DateTime.UtcNow.AddDays(1);

    /// <summary>How long the slot is held for. Drives the availability grid.</summary>
    public int DurationMinutes { get; set; } = 60;

    public DateTime? CheckInAt { get; set; }
    public DateTime? CheckOutAt { get; set; }

    public int? HostId { get; set; }
    public string? HostName { get; set; }
    public string? TransportMode { get; set; }
    public string? PickupLocation { get; set; }

    public string? Feedback { get; set; }
    public string? InterestLevel { get; set; }
    public int? Rating { get; set; }
    public decimal? BudgetDiscussed { get; set; }
    public string? NextAction { get; set; }
    public string? CancellationReason { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Branch? Branch { get; set; }
    public Project? Project { get; set; }
    public Unit? Unit { get; set; }
}
