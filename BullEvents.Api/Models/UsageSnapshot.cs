namespace BullEvents.Api.Models;

/// <summary>
/// What one tenant was using on one day.
///
/// Written as it happens rather than computed at billing time, because the
/// counts are of live rows and a lead deleted in March is invisible to a query
/// run in April. A subscription argued over three months later needs the number
/// as it stood, not as it can be reconstructed.
///
/// One row per company per day. Cheap — a hundred tenants is thirty-six
/// thousand rows a year — and it is what the usage chart on the subscription
/// screen reads.
/// </summary>
public class UsageSnapshot : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>The UTC date this counts, stored at midnight.</summary>
    public DateTime Day { get; set; }

    public string PlanTier { get; set; } = string.Empty;

    public int ActiveUsers { get; set; }
    public int Leads { get; set; }
    public int Branches { get; set; }
    public int Quotations { get; set; }
    public int Units { get; set; }

    /// <summary>Leads created on this day, which is the number that reads as activity.</summary>
    public int LeadsCreated { get; set; }

    /// <summary>Distinct people who completed a sign-in on this day.</summary>
    public int SignIns { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
