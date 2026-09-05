namespace BullEvents.Api.Models;

/// <summary>
/// Every record that belongs to exactly one tenant. The DbContext applies a
/// global query filter over this interface, so a query can never accidentally
/// read across companies — this is what replaces Postgres row-level security
/// on MySQL.
/// </summary>
public interface ITenantScoped
{
    int CompanyId { get; set; }
}

/// <summary>
/// Records that are hidden rather than removed. The same global filter drops
/// soft-deleted rows, so callers opt in explicitly via IgnoreQueryFilters.
/// </summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    int? DeletedById { get; set; }
}

/// <summary>Standard create/update stamps, filled in by the audit interceptor.</summary>
public interface IAuditable
{
    DateTime CreatedAt { get; set; }
    DateTime UpdatedAt { get; set; }
    int? CreatedById { get; set; }
    int? UpdatedById { get; set; }
}

/// <summary>
/// Records that belong to somebody.
///
/// Ownership is what the sharing rules turn on: a Sales Executive sees the
/// records they own, an AGM sees their reporting line's, and a company-scoped
/// seat sees them all. Marking an entity with this is what opts it into that
/// filtering — an object without an owner is company-wide by nature.
/// </summary>
public interface IOwnedRecord
{
    int? OwnerId { get; set; }
}
