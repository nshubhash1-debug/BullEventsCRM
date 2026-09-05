namespace BullEvents.Api.Models;

/// <summary>
/// An append-only record of who changed what. Written by the SaveChanges
/// interceptor, never by controller code, so nothing can be mutated without
/// leaving a trail.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public int CompanyId { get; set; }

    public int? UserId { get; set; }
    public string UserName { get; set; } = "System";

    public string Entity { get; set; } = string.Empty;
    public string EntityId { get; set; } = string.Empty;
    public string Action { get; set; } = AuditActions.Update;

    /// <summary>JSON object of `{ field: { from, to } }` for updates.</summary>
    public string? Changes { get; set; }

    public string? IpAddress { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
}
