namespace BullEvents.Api.Models;

/// <summary>
/// One telephony interaction. Written by hand today; the shape is the one a
/// CTI/dialler webhook would post, so wiring a provider later is a mapping job
/// rather than a schema change.
/// </summary>
public class CallLog : ITenantScoped, ISoftDeletable, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int BranchId { get; set; }

    public string RelatedType { get; set; } = RelatedTypes.Lead;
    public int RelatedId { get; set; }
    public string RelatedName { get; set; } = string.Empty;

    public string Direction { get; set; } = CallDirections.Outbound;
    public string Outcome { get; set; } = CallOutcomes.Connected;
    public string? Disposition { get; set; }

    public string? PhoneNumber { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public int DurationSeconds { get; set; }
    public int? WaitSeconds { get; set; }

    public int? AgentId { get; set; }
    public string AgentName { get; set; } = string.Empty;

    public string? Notes { get; set; }
    public string? RecordingUrl { get; set; }

    /// <summary>-1..1 from the local ML.NET sentiment pass over <see cref="Notes"/>.</summary>
    public double? SentimentScore { get; set; }
    public string? SentimentLabel { get; set; }

    public DateTime? FollowUpAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Branch? Branch { get; set; }
}
