using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Shared plumbing for the list-heavy CRM objects.
///
/// Every one of them needs the same thing: take a filter tree from the client,
/// push it into SQL, return one page plus the facet counts the toolbar renders.
/// Doing it once here is what keeps the individual controllers to their own
/// domain logic.
/// </summary>
public abstract class CrmControllerBase(AppDbContext db) : ControllerBase
{
    protected AppDbContext Db { get; } = db;

    /// <summary>
    /// Who this request may see inside its company.
    ///
    /// Resolved from the request's services rather than taken as a constructor
    /// argument, so the twenty-odd controllers deriving from this did not all
    /// need their constructors rewritten to gain record-level scoping.
    /// </summary>
    protected AccessScope Scope =>
        HttpContext.RequestServices.GetRequiredService<AccessScope>();

    /// <summary>
    /// Narrows a query to the records this user may read, for objects that
    /// carry an owner. Applied by <see cref="RunQueryAsync"/> for every grid;
    /// call it directly when building a query outside that path.
    /// </summary>
    protected Task<IQueryable<T>> ScopedAsync<T>(
        IQueryable<T> source, CancellationToken ct = default)
        where T : class, IOwnedRecord
        => Scope.ApplyAsync(source, ct);

    /// <summary>
    /// Runs a query request end to end: search → filter → facets → sort → page.
    /// Facets are computed over the filtered set but *before* paging, so the
    /// counts describe the whole result rather than the visible rows.
    /// </summary>
    protected async Task<PagedResult<TDto>> RunQueryAsync<TEntity, TDto>(
        IQueryable<TEntity> source,
        QueryRequest request,
        FieldMap<TEntity> map,
        Func<TEntity, TDto> project,
        string defaultSortPath,
        bool defaultSortDescending = true,
        Func<IQueryable<TEntity>, Task<Dictionary<string, decimal>>>? aggregates = null,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        // Record-level sharing, applied before anything else looks at the set —
        // the facet counts and the total have to describe what this user can
        // see, not what exists. A grid that says "8,046 records" and then shows
        // eleven is worse than one that simply says eleven.
        // Passed as the concrete entity type, not cast to IOwnedRecord first: a
        // criteria sharing rule reflects over the entity's own fields, and an
        // interface has none of them to find.
        source = await Scope.ApplyAsync(source, cancellationToken);

        var filtered = QueryEngine.Apply(source, request, map);

        var total = await filtered.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(request.PageSize <= 0 ? 50 : request.PageSize, 1, 500);
        var page = Math.Max(1, request.Page);

        var ordered = QueryEngine.ApplySort(
            filtered, request.Sort, map, defaultSortPath, defaultSortDescending);

        var rows = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var result = new PagedResult<TDto>
        {
            Items = rows.Select(project).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };

        foreach (var facet in request.Facets ?? [])
        {
            if (map.Find(facet) is null) continue;

            result.Facets[facet] = await QueryEngine
                .Facet(filtered, map, facet)
                .OrderByDescending(b => b.Count)
                .Take(50)
                .ToListAsync(cancellationToken);
        }

        if (aggregates is not null)
        {
            result.Aggregates = await aggregates(filtered);
        }

        return result;
    }

    /* ------------------------------------------------------------------ *
     * Metadata
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Publishes the filterable field list so the client's filter builder is
    /// generated from the server's own whitelist — the two can never drift into
    /// offering a field the API would reject.
    /// </summary>
    protected static List<FilterFieldDto> DescribeFields<TEntity>(
        FieldMap<TEntity> map,
        IReadOnlyDictionary<string, IReadOnlyList<FilterOptionDto>>? options = null,
        IReadOnlyDictionary<string, string>? groups = null) =>
        map.All
            .Select(field => new FilterFieldDto(
                field.Id,
                field.Label,
                field.Kind switch
                {
                    FieldKind.Number => "number",
                    FieldKind.Date => "date",
                    FieldKind.Boolean => "boolean",
                    FieldKind.Select => "select",
                    _ => "text",
                },
                options?.GetValueOrDefault(field.Id),
                groups?.GetValueOrDefault(field.Id)))
            .ToList();

    protected static IReadOnlyList<FilterOptionDto> Options(params string[] values) =>
        values.Select(v => new FilterOptionDto(Humanise(v), v)).ToList();

    /// <summary>`ChannelPartner` reads as "Channel Partner" in a dropdown.</summary>
    public static string Humanise(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;

        var builder = new System.Text.StringBuilder(value.Length + 4);

        for (var i = 0; i < value.Length; i++)
        {
            if (i > 0 && char.IsUpper(value[i]) && !char.IsUpper(value[i - 1]))
            {
                builder.Append(' ');
            }
            builder.Append(value[i]);
        }

        return builder.ToString();
    }

    /* ------------------------------------------------------------------ *
     * Validation helpers
     * ------------------------------------------------------------------ */

    protected static string Require(string? value, string[] allowed, string fieldName)
    {
        if (value is not null && allowed.Contains(value)) return value;

        throw ApiException.BadRequest(
            $"'{value ?? "(none)"}' is not a valid {fieldName}. Expected one of: {string.Join(", ", allowed)}.");
    }

    protected async Task<Branch> RequireBranchAsync(int branchId, CancellationToken cancellationToken = default)
    {
        var branch = await Db.Branches.FirstOrDefaultAsync(
            b => b.Id == branchId && b.CompanyId == Db.Tenant.CompanyId, cancellationToken);

        return branch ?? throw ApiException.BadRequest("Select a valid branch.");
    }

    protected async Task RequireOwnerAsync(int? ownerId, CancellationToken cancellationToken = default)
    {
        if (ownerId is null) return;

        var exists = await Db.Users.AnyAsync(
            u => u.Id == ownerId && u.CompanyId == Db.Tenant.CompanyId, cancellationToken);

        if (!exists) throw ApiException.BadRequest("Select a valid owner.");
    }

    /// <summary>
    /// Writes a linked record's status change onto the lead's timeline and
    /// bumps its last-activity stamp.
    ///
    /// This is what makes a visit closed from the visits list show up on the
    /// lead — the lead is the record people actually watch, and a status that
    /// only lived on the child object would be invisible from there.
    ///
    /// Does not save: the caller commits it with the change that caused it, so
    /// the two can never diverge.
    /// </summary>
    protected async Task<bool> LogLeadActivityAsync(
        int? leadId,
        string type,
        string remarks,
        CancellationToken cancellationToken = default)
    {
        if (leadId is not int id) return false;

        var lead = await Db.Leads.FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
        if (lead is null) return false;

        lead.LastActivityAt = DateTime.UtcNow;

        Db.LeadActivities.Add(new LeadActivity
        {
            LeadId = id,
            Type = type,
            Remarks = remarks,
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        return true;
    }

    /// <summary>
    /// The one sentence a status change leaves on the lead's timeline.
    ///
    /// Phrased for someone reading the lead months later, so it names the
    /// record, where it moved from and to, and the slot it now sits in.
    /// </summary>
    protected static string VisitRemark(
        string label,
        string code,
        string from,
        string to,
        DateTime scheduledAt,
        string? reason)
    {
        var line = $"{label} {code}: {from} → {to}";

        if (to is VisitStatuses.Scheduled or VisitStatuses.Confirmed or VisitStatuses.Rescheduled)
        {
            line += $" for {scheduledAt:dd MMM yyyy HH:mm}";
        }

        return string.IsNullOrWhiteSpace(reason) ? line + "." : $"{line} — {reason}";
    }

    /// <summary>Soft-deletes rather than removing, so the audit trail keeps its target.</summary>
    protected void SoftDelete<T>(T entity) where T : ISoftDeletable
    {
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedById = Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null;
    }

    /// <summary>
    /// A short, human-quotable reference (`SV-2608-K3P9QF`).
    ///
    /// The random tail matters: deriving the suffix from the row id would mean
    /// inserting a blank code first and filling it in on a second save, and two
    /// concurrent creates would collide on the unique index in between. This is
    /// unique at construction time, so the row is only ever written once.
    /// </summary>
    protected static string Code(string prefix) => ReferenceCode(prefix, DateTime.UtcNow);

    public static string ReferenceCode(string prefix, DateTime issuedAt)
    {
        // Crockford base32 — no I, L, O or U, so a code read over the phone
        // cannot be misheard as a digit.
        const string alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ";
        const int length = 8;

        // A Guid needs all sixteen bytes written; the first eight are enough
        // entropy for the tail.
        Span<byte> bytes = stackalloc byte[16];
        Guid.NewGuid().TryWriteBytes(bytes);
        var value = BitConverter.ToUInt64(bytes[..8]);

        Span<char> tail = stackalloc char[length];
        for (var i = length - 1; i >= 0; i--)
        {
            tail[i] = alphabet[(int)(value % 32)];
            value /= 32;
        }

        return $"{prefix}-{issuedAt:yyMM}-{new string(tail)}";
    }
}
