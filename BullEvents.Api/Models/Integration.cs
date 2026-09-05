namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * API keys
 * ------------------------------------------------------------------ */

/// <summary>
/// A credential belonging to an integration rather than to a person.
///
/// Before this, anything calling the API had to impersonate a user: a real
/// account, with a password somebody had to store, whose sessions and role
/// changes broke the integration whenever HR touched them. A key is scoped to
/// the tenant that issued it, carries only the permissions it was granted, and
/// can be revoked without disturbing anybody's seat.
///
/// The secret is stored as a hash. A key that can be read back out of the
/// console is a key that leaks through the console.
/// </summary>
public class ApiKey : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>What this key is for — "99acres feed", "website form".</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The first characters of the key, kept in clear.
    ///
    /// Enough to recognise a key in a log line or a list without being enough to
    /// use one. It is also what the lookup narrows on, so verifying a key is one
    /// indexed read plus one hash comparison rather than a hash comparison
    /// against every key in the table.
    /// </summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>SHA-256 of the full key. Compared in constant time.</summary>
    public string SecretHash { get; set; } = string.Empty;

    /// <summary>
    /// What this key may do, as <c>object:verb</c> pairs — "Lead:View",
    /// "Lead:Create". Deliberately narrower than a role: an inbound lead feed
    /// needs exactly one of these, and giving it a role would give it the rest.
    /// </summary>
    public string ScopesCsv { get; set; } = string.Empty;

    public int? CreatedById { get; set; }
    public string? CreatedByName { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null for a key with no end date, which most integrations want.</summary>
    public DateTime? ExpiresAt { get; set; }

    public DateTime? LastUsedAt { get; set; }
    public string? LastUsedIp { get; set; }

    /// <summary>How many requests this key has made. Cheap, and the first thing asked when a feed goes quiet.</summary>
    public long CallCount { get; set; }

    public DateTime? RevokedAt { get; set; }

    public string[] Scopes =>
        ScopesCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool IsLive(DateTime now) =>
        RevokedAt is null && (ExpiresAt is null || ExpiresAt > now);
}

/// <summary>The permissions a key can be granted, and what each one lets through.</summary>
public static class ApiScopes
{
    public const string LeadRead = "Lead:View";
    public const string LeadWrite = "Lead:Create";
    public const string ContactRead = "Contact:View";
    public const string ContactWrite = "Contact:Create";
    public const string InventoryRead = "Unit:View";
    public const string QuotationRead = "Quotation:View";

    public static readonly (string Scope, string Label, string Description)[] All =
    [
        (LeadRead, "Read leads", "List and open leads, including their custom fields."),
        (LeadWrite, "Create leads", "Push enquiries in — what a portal or website form needs."),
        (ContactRead, "Read contacts", "List and open the people behind the deals."),
        (ContactWrite, "Create contacts", "Add a person without going through a lead."),
        (InventoryRead, "Read inventory", "Projects, towers and units with their availability."),
        (QuotationRead, "Read quotations", "Priced offers and their status."),
    ];

    public static bool Exists(string scope) =>
        All.Any(s => string.Equals(s.Scope, scope, StringComparison.OrdinalIgnoreCase));
}

/* ------------------------------------------------------------------ *
 * Webhooks
 * ------------------------------------------------------------------ */

/// <summary>The things a company can be told about.</summary>
public static class WebhookEvents
{
    public const string LeadCreated = "lead.created";
    public const string LeadStageChanged = "lead.stage_changed";
    public const string LeadAssigned = "lead.assigned";
    public const string QuotationIssued = "quotation.issued";
    public const string QuotationAccepted = "quotation.accepted";
    public const string SiteVisitScheduled = "site_visit.scheduled";

    public static readonly (string Event, string Description)[] All =
    [
        (LeadCreated, "A new enquiry was captured, however it arrived."),
        (LeadStageChanged, "A lead moved between pipeline stages."),
        (LeadAssigned, "A lead changed owner."),
        (QuotationIssued, "A priced offer was sent to a customer."),
        (QuotationAccepted, "A customer accepted a quotation."),
        (SiteVisitScheduled, "A site visit was booked."),
    ];

    public static bool Exists(string name) =>
        All.Any(e => string.Equals(e.Event, name, StringComparison.OrdinalIgnoreCase));
}

/// <summary>Somewhere one company wants its events posted.</summary>
public class WebhookEndpoint : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;

    /// <summary>
    /// Shared with the receiver so it can verify the signature on each delivery.
    ///
    /// Held in clear rather than hashed, unlike an API key, because this end has
    /// to compute the same HMAC the receiver checks — there is no version of
    /// this that works one-way.
    /// </summary>
    public string Secret { get; set; } = string.Empty;

    public string EventsCsv { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Switched off automatically after enough consecutive failures, so a dead
    /// endpoint stops costing a retry budget forever. Cleared by any success.
    /// </summary>
    public int ConsecutiveFailures { get; set; }
    public DateTime? DisabledAt { get; set; }
    public string? DisabledReason { get; set; }

    public string[] Events =>
        EventsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    public bool Wants(string name) =>
        Events.Contains(name, StringComparer.OrdinalIgnoreCase);
}

public enum DeliveryStatus
{
    Pending = 0,
    Delivered = 1,
    /// <summary>Failed, and out of attempts. Visible in the log so somebody can replay it.</summary>
    Failed = 2,
}

/// <summary>
/// One attempt to tell one endpoint about one event.
///
/// Stored rather than fired and forgotten, because "did you send it" is the
/// first question every integration argument starts with, and the honest answer
/// needs the payload, the response code and the timing.
/// </summary>
public class WebhookDelivery : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EndpointId { get; set; }

    public string Event { get; set; } = string.Empty;

    /// <summary>The exact body that was signed and posted.</summary>
    public string Payload { get; set; } = string.Empty;

    public DeliveryStatus Status { get; set; } = DeliveryStatus.Pending;

    public int Attempts { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? NextAttemptAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastAttemptAt { get; set; }
    public DateTime? DeliveredAt { get; set; }

    public int? ResponseCode { get; set; }

    /// <summary>Trimmed hard: this is a diagnostic, not a copy of somebody's error page.</summary>
    public string? Error { get; set; }

    public WebhookEndpoint? Endpoint { get; set; }
}
