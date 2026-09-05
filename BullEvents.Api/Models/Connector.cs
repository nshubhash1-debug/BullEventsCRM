namespace BullEvents.Api.Models;

/// <summary>
/// One company's connection to one outside system.
///
/// A company may hold several of the same provider — a 99acres feed per project
/// is the common case, each landing in a different branch — so this is an
/// instance rather than a per-provider singleton.
///
/// Credentials live in a JSON bag rather than as columns because every provider
/// wants different ones, and a column per provider per field would be a
/// migration every time a partner changes their auth. What each provider needs
/// is declared in <see cref="ConnectorCatalog"/>; what the client may read back
/// is decided there too.
/// </summary>
public class Connector : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>A key from <see cref="ConnectorCatalog"/>.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>What this one is for — "WedMeGood banquet feed".</summary>
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// The provider's credentials, keyed by the catalogue's field keys.
    ///
    /// Never returned whole. A field the catalogue marks secret comes back as a
    /// masked hint or not at all — a screen that reprints an API key turns every
    /// shoulder into a leak, and support sessions are shared screens.
    /// </summary>
    public string? CredentialsJson { get; set; }

    /// <summary>Per-provider options that are not credentials.</summary>
    public string? SettingsJson { get; set; }

    /* ---------------- what arrives, and where it lands ---------------- */

    /// <summary>
    /// The path segment the provider posts to.
    ///
    /// A per-connector secret rather than a shared one: rotating a leaked
    /// endpoint should cost one feed, not every feed. It is a URL rather than a
    /// header because most portal dashboards only offer a URL field.
    /// </summary>
    public Guid InboundToken { get; set; } = Guid.NewGuid();

    /// <summary>Which office the leads file under. Falls back to the company's first.</summary>
    public int? DefaultBranchId { get; set; }

    /// <summary>Who owns what arrives. Null leaves it unassigned for the routing rules.</summary>
    public int? DefaultOwnerId { get; set; }

    /// <summary>
    /// What to record as the lead's source.
    ///
    /// Set per connector rather than taken from the payload, because a portal
    /// sends whatever it sends and the source report fills with "99acres",
    /// "99Acres" and "99 acres" as three different things.
    /// </summary>
    public string? SourceLabel { get; set; }

    /* ---------------- health ---------------- */

    public string Status { get; set; } = ConnectorStatuses.NotConfigured;

    /// <summary>The last thing that went wrong, so the card can say what rather than that.</summary>
    public string? LastError { get; set; }

    public DateTime? LastEventAt { get; set; }
    public long EventCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public Branch? DefaultBranch { get; set; }
    public User? DefaultOwner { get; set; }
}

/// <summary>
/// One thing that arrived from, or was sent to, a connector.
///
/// Every delivery is recorded with its raw payload. The first question of every
/// integration argument is "did it reach you", and the only answer worth having
/// is the body as it landed — reconstructing it from the lead that resulted
/// cannot show the enquiry that was refused.
/// </summary>
public class ConnectorEvent : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ConnectorId { get; set; }

    /// <summary>lead, call, message or test.</summary>
    public string Kind { get; set; } = "lead";

    /// <summary>A value from <see cref="ConnectorOutcomes"/>.</summary>
    public string Outcome { get; set; } = ConnectorOutcomes.Created;

    /// <summary>Why, in one line, when the outcome is not Created.</summary>
    public string? Detail { get; set; }

    /// <summary>The record this produced, when it produced one.</summary>
    public int? LeadId { get; set; }

    /// <summary>The body exactly as it arrived.</summary>
    public string Payload { get; set; } = "{}";

    public string? IpAddress { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;

    public Connector? Connector { get; set; }
}
