namespace BullEvents.Api.Models;

/// <summary>
/// The customer database. One row per human the company deals with — a lead
/// becomes a Contact on conversion, and stays here for the rest of the
/// relationship regardless of how many opportunities they run through.
/// </summary>
public class Contact : ITenantScoped, ISoftDeletable, IAuditable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>
    /// Values for the fields this company added, keyed by the definition's key.
    ///
    /// A jsonb column rather than a row-per-value table: the values are read
    /// with the record every time and almost never on their own, so a join per
    /// field would cost more than it saves. Postgres can index inside it if a
    /// filter ever needs to.
    /// </summary>
    public string? CustomFields { get; set; }
    public int BranchId { get; set; }

    // ---- identity ----
    public string? Salutation { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string? LastName { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string? Designation { get; set; }
    public string? AccountName { get; set; }

    // ---- reach ----
    public string? Phone { get; set; }
    public string? Phone2 { get; set; }
    public string? Email { get; set; }
    public string? AltEmail { get; set; }
    public string? WhatsAppNumber { get; set; }

    // ---- location ----
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? State { get; set; }
    public string? Country { get; set; }
    public string? Pincode { get; set; }

    // ---- classification ----
    public string Type { get; set; } = ContactTypes.Contact;
    public string LifecycleStage { get; set; } = LifecycleStages.Prospect;
    public string Source { get; set; } = LeadSources.Other;
    public string? Tags { get; set; }

    /// <summary>Cluster label assigned by the local KMeans segmentation model.</summary>
    public string? Segment { get; set; }

    // ---- commercial footprint ----
    public decimal LifetimeValue { get; set; }
    public int DealCount { get; set; }
    public decimal? BudgetMin { get; set; }
    public decimal? BudgetMax { get; set; }
    public string? PreferredConfiguration { get; set; }
    public string? PreferredLocality { get; set; }

    // ---- compliance / preferences ----
    public bool DoNotCall { get; set; }
    public bool DoNotEmail { get; set; }
    public bool WhatsAppOptIn { get; set; } = true;
    public string? PanNumber { get; set; }
    public string? Gstin { get; set; }
    public string? PreferredLanguage { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public DateTime? AnniversaryDate { get; set; }

    // ---- relationships ----
    public int? OwnerId { get; set; }
    public int? ConvertedFromLeadId { get; set; }
    public DateTime? LastActivityAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }

    public Branch? Branch { get; set; }
    public User? Owner { get; set; }
    public ICollection<Opportunity> Opportunities { get; set; } = new List<Opportunity>();
}
