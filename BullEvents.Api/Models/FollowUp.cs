namespace BullEvents.Api.Models;

/// <summary>
/// A dated commitment to do something about a record. Follow-ups are the
/// backbone of the SLA view: anything Open with a DueAt in the past is overdue,
/// and that is computed rather than stored so it can never drift.
/// </summary>
public class FollowUp : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    public string Subject { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Polymorphic parent — Lead, Contact or Opportunity.</summary>
    public string RelatedType { get; set; } = RelatedTypes.Lead;
    public int RelatedId { get; set; }
    public string RelatedName { get; set; } = string.Empty;

    public string Channel { get; set; } = FollowUpChannels.Call;
    public string Status { get; set; } = FollowUpStatuses.Open;
    public string Priority { get; set; } = LeadPriorities.Medium;

    public DateTime DueAt { get; set; } = DateTime.UtcNow.AddDays(1);
    public DateTime? ReminderAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Outcome { get; set; }

    /// <summary>Minutes allowed from creation to completion before the SLA is breached.</summary>
    public int SlaMinutes { get; set; } = 1440;

    public int? OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Branch? Branch { get; set; }
    public User? Owner { get; set; }
}
