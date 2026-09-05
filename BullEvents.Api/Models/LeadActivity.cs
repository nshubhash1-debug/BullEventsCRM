namespace BullEvents.Api.Models;

public class LeadActivity
{
    public int Id { get; set; }
    public int LeadId { get; set; }
    public string Type { get; set; } = LeadActivityTypes.Note;
    public string? Remarks { get; set; }
    public string? FromStage { get; set; }
    public string? ToStage { get; set; }
    public int ActorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Lead? Lead { get; set; }
}
