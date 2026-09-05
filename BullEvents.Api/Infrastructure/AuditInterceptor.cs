using System.Text.Json;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace BullEvents.Api.Infrastructure;

/// <summary>
/// Stamps audit fields and writes the change trail on every SaveChanges.
///
/// Doing this in an interceptor rather than in controllers means the trail is
/// complete by construction: there is no code path that writes an entity and
/// forgets to log it.
/// </summary>
public class AuditInterceptor(TenantContext tenant) : SaveChangesInterceptor
{
    /// <summary>
    /// Rows inserted by the save currently in flight, held until it completes.
    ///
    /// An inserted row has no key yet while SavingChanges runs — EF is still
    /// carrying a temporary negative placeholder, and writing that into the
    /// trail produced entries pointing at <c>Lead#-2147482647</c>. The real key
    /// only exists once the database has assigned it, so the create entries are
    /// built here and written in a second pass from SavedChanges.
    ///
    /// Scoped alongside the DbContext, so this list belongs to one request.
    /// </summary>
    private readonly List<EntityEntry> _inserted = [];

    /// <summary>Guards the second pass from auditing itself into a loop.</summary>
    private bool _writingTrail;

    /// <summary>
    /// Never copied into the change log — secrets, and machine-written fields.
    ///
    /// The cached model scores matter here: they are rewritten for every lead on
    /// each retrain, and logging that would bury real human edits under
    /// thousands of rows nobody asked for. A record whose only modified
    /// properties are on this list produces no audit entry at all.
    /// </summary>
    private static readonly HashSet<string> Redacted =
    [
        // Credentials. The trail is read by anybody who can administer users,
        // which is a wider audience than the one that may hold a webhook secret
        // or an API key — and a create entry was copying both in full.
        "PasswordHash", "Secret", "SecretHash", "CodeHash", "Token",

        "UpdatedAt", "CreatedAt", "UpdatedById", "CreatedById",
        "CachedScore", "CachedBand", "ScoredAt", "SentimentScore", "SentimentLabel",
        // Touch stamps the system maintains on the side of real edits. Logging
        // them doubles every history entry with a line nobody asked about.
        "LastActivityAt", "FirstResponseAt", "StageEnteredAt",

        // The same problem one layer down: a session's last-seen and a key's
        // call counter are written by the act of using them, so auditing them
        // makes the trail a record of traffic rather than of decisions. Both
        // arrived with the session and integration work and buried real edits
        // within minutes.
        "LastSeenAt", "LastUsedAt", "LastUsedIp", "CallCount"
    ];

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null) Apply(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        if (eventData.Context is not null) Apply(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData,
        int result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            var logs = DrainInserts();

            if (logs.Count > 0)
            {
                _writingTrail = true;
                try
                {
                    eventData.Context.Set<AuditLog>().AddRange(logs);
                    await eventData.Context.SaveChangesAsync(cancellationToken);
                }
                finally
                {
                    _writingTrail = false;
                }
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is not null)
        {
            var logs = DrainInserts();

            if (logs.Count > 0)
            {
                _writingTrail = true;
                try
                {
                    eventData.Context.Set<AuditLog>().AddRange(logs);
                    eventData.Context.SaveChanges();
                }
                finally
                {
                    _writingTrail = false;
                }
            }
        }

        return base.SavedChanges(eventData, result);
    }

    /// <summary>
    /// Turns the remembered inserts into trail entries, now that their keys are
    /// real, and clears the list.
    /// </summary>
    private List<AuditLog> DrainInserts()
    {
        if (_inserted.Count == 0) return [];

        var logs = _inserted
            .Select(entry => Log(entry, AuditActions.Create, Snapshot(entry)))
            .ToList();

        _inserted.Clear();
        return logs;
    }

    private void Apply(DbContext context)
    {
        // The second pass writes only AuditLog rows. Auditing those would append
        // a trail entry for every trail entry, forever.
        if (_writingTrail) return;

        var now = DateTime.UtcNow;
        var userId = tenant.UserId > 0 ? tenant.UserId : (int?)null;
        var logs = new List<AuditLog>();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            if (entry.Entity is AuditLog) continue;

            // A soft delete is an update that flips the flag, so the trail
            // records it as a Delete rather than as a field change.
            var softDeleted = entry is { State: EntityState.Modified, Entity: ISoftDeletable }
                && entry.Property(nameof(ISoftDeletable.IsDeleted)).IsModified
                && entry.CurrentValues[nameof(ISoftDeletable.IsDeleted)] is true;

            switch (entry.State)
            {
                case EntityState.Added:
                    Stamp(entry, now, userId, created: true);

                    // Not logged here: the key is still a placeholder. Held for
                    // the second pass, which runs once the insert has returned a
                    // real one.
                    if (tenant.IsResolved) _inserted.Add(entry);
                    break;

                case EntityState.Modified:
                    Stamp(entry, now, userId, created: false);
                    if (tenant.IsResolved)
                    {
                        var changes = Diff(entry);
                        if (softDeleted)
                        {
                            logs.Add(Log(entry, AuditActions.Delete, null));
                        }
                        else if (changes.Count > 0)
                        {
                            logs.Add(Log(entry, AuditActions.Update, changes));
                        }
                    }
                    break;

                case EntityState.Deleted:
                    if (tenant.IsResolved) logs.Add(Log(entry, AuditActions.Delete, null));
                    break;
            }
        }

        if (logs.Count > 0) context.Set<AuditLog>().AddRange(logs);
    }

    private void Stamp(EntityEntry entry, DateTime now, int? userId, bool created)
    {
        if (entry.Entity is IAuditable auditable)
        {
            auditable.UpdatedAt = now;
            auditable.UpdatedById = userId;

            if (created)
            {
                auditable.CreatedAt = auditable.CreatedAt == default ? now : auditable.CreatedAt;
                auditable.CreatedById ??= userId;
            }
        }

        // A tenant-scoped row created without an explicit CompanyId inherits the
        // caller's — a new record can never land in another company by omission.
        if (created && entry.Entity is ITenantScoped scoped && scoped.CompanyId == 0 && tenant.IsResolved)
        {
            scoped.CompanyId = tenant.CompanyId;
        }
    }

    private static Dictionary<string, object?> Diff(EntityEntry entry)
    {
        var changes = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            if (!property.IsModified) continue;

            var name = property.Metadata.Name;
            if (Redacted.Contains(name)) continue;

            var from = property.OriginalValue;
            var to = property.CurrentValue;
            if (Equals(from, to)) continue;

            changes[name] = new { from, to };
        }

        return changes;
    }

    /// <summary>
    /// The values a newly created row was given.
    ///
    /// Only what was actually set — a create that logged every default would
    /// bury the three fields somebody typed under forty they did not. Redacted
    /// names never appear, and collections and navigations are skipped, so this
    /// stays the size of a form rather than the size of an object graph.
    /// </summary>
    private static Dictionary<string, object?> Snapshot(EntityEntry entry)
    {
        var values = new Dictionary<string, object?>();

        foreach (var property in entry.Properties)
        {
            var name = property.Metadata.Name;

            if (Redacted.Contains(name)) continue;
            if (property.Metadata.IsPrimaryKey()) continue;

            var value = property.CurrentValue;
            if (value is null) continue;

            // Defaults are what the record would have had anyway; they say
            // nothing about the decision that created it.
            if (value is bool and false) continue;
            if (value is int and 0) continue;
            if (value is decimal and 0) continue;
            if (value is string text && text.Length == 0) continue;

            // A long free-text field is a paragraph, not a diff line.
            values[name] = value is string s && s.Length > 200 ? s[..200] + "…" : value;

            if (values.Count >= 40) break;
        }

        return values;
    }

    private AuditLog Log(EntityEntry entry, string action, Dictionary<string, object?>? changes)
    {
        var key = entry.Metadata.FindPrimaryKey()?.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString())
            .FirstOrDefault();

        var companyId = entry.Entity is ITenantScoped scoped && scoped.CompanyId > 0
            ? scoped.CompanyId
            : tenant.CompanyId;

        return new AuditLog
        {
            CompanyId = companyId,
            UserId = tenant.UserId > 0 ? tenant.UserId : null,
            UserName = tenant.UserName,
            Entity = entry.Metadata.ClrType.Name,
            EntityId = key ?? "?",
            Action = action,
            Changes = changes is null ? null : JsonSerializer.Serialize(changes),
            IpAddress = tenant.IpAddress,
            At = DateTime.UtcNow,
        };
    }
}
