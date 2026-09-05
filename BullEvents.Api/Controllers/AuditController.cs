using System.Text.Json;
using System.Text.Json.Nodes;
using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/* ------------------------------------------------------------------ *
 * DTOs
 * ------------------------------------------------------------------ */

/// <summary>One field that changed, as a person would read it.</summary>
public record AuditChangeDto(string Field, string? From, string? To);

public record AuditEntryDto(
    long Id,
    int? UserId,
    string UserName,
    string Entity,
    string EntityId,
    string Action,
    IReadOnlyList<AuditChangeDto> Changes,
    string? IpAddress,
    DateTime At);

public record AuditPageDto(
    IReadOnlyList<AuditEntryDto> Items,
    int Total,
    int Page,
    int PageSize,
    /// <summary>The entity names present in this company's trail, for the filter.</summary>
    IReadOnlyList<string> Entities,
    IReadOnlyList<string> Actors);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// Reads the change trail.
///
/// The trail has been written on every save since the audit interceptor went
/// in, and nothing has ever read it — "who changed this lead's owner" was a SQL
/// query, which is not an answer a compliance reviewer or an argument between
/// two reps can use.
///
/// Read-only by construction: there is no write path here, and the interceptor
/// is the only thing that appends. An audit trail somebody can edit is not one.
/// </summary>
[ApiController]
[Route("api/admin/audit")]
[Authorize]
[RequirePermission(SecuredObjects.User, ObjectAction.ModifyAll)]
public class AuditController(AppDbContext db, TenantContext tenant) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<AuditPageDto>> Trail(
        [FromQuery] string? entity = null,
        [FromQuery] string? entityId = null,
        [FromQuery] string? actor = null,
        [FromQuery] string? action = null,
        [FromQuery] DateTime? since = null,
        [FromQuery] DateTime? until = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        // AuditLog is not ITenantScoped — it is written outside the filter so a
        // system action still lands — so the tenant clause is applied by hand
        // here, and it is the first thing in the query rather than the last.
        var query = db.AuditLogs
            .AsNoTracking()
            .Where(a => a.CompanyId == tenant.CompanyId);

        if (!string.IsNullOrWhiteSpace(entity)) query = query.Where(a => a.Entity == entity);
        if (!string.IsNullOrWhiteSpace(entityId)) query = query.Where(a => a.EntityId == entityId);
        if (!string.IsNullOrWhiteSpace(actor)) query = query.Where(a => a.UserName == actor);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(a => a.Action == action);
        if (since is not null) query = query.Where(a => a.At >= since);
        if (until is not null) query = query.Where(a => a.At <= until);

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(a => a.At)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        // The filter options come from the trail itself rather than from a fixed
        // list, so they only ever offer values that will actually match.
        var entities = await db.AuditLogs
            .Where(a => a.CompanyId == tenant.CompanyId)
            .Select(a => a.Entity)
            .Distinct()
            .OrderBy(e => e)
            .ToListAsync(ct);

        var actors = await db.AuditLogs
            .Where(a => a.CompanyId == tenant.CompanyId)
            .Select(a => a.UserName)
            .Distinct()
            .OrderBy(u => u)
            .Take(200)
            .ToListAsync(ct);

        return Ok(new AuditPageDto(
            rows.Select(ToDto).ToList(), total, page, pageSize, entities, actors));
    }

    /// <summary>Everything that ever happened to one record, oldest first.</summary>
    [HttpGet("{entity}/{entityId}")]
    public async Task<ActionResult<IReadOnlyList<AuditEntryDto>>> History(
        string entity, string entityId, CancellationToken ct)
    {
        var rows = await db.AuditLogs
            .AsNoTracking()
            .Where(a => a.CompanyId == tenant.CompanyId
                        && a.Entity == entity
                        && a.EntityId == entityId)
            .OrderBy(a => a.At)
            .Take(500)
            .ToListAsync(ct);

        return Ok(rows.Select(ToDto).ToList());
    }

    private static AuditEntryDto ToDto(AuditLog log) => new(
        log.Id, log.UserId, log.UserName, log.Entity, log.EntityId, log.Action,
        Flatten(log.Changes), log.IpAddress, log.At);

    /// <summary>
    /// Turns the stored <c>{ field: { from, to } }</c> object into a list a
    /// table can render.
    ///
    /// Done here rather than in the client because the shape is the interceptor's
    /// business, and a screen that had to understand it would break the next time
    /// the interceptor changed.
    /// </summary>
    private static IReadOnlyList<AuditChangeDto> Flatten(string? changes)
    {
        if (string.IsNullOrWhiteSpace(changes)) return [];

        try
        {
            if (JsonNode.Parse(changes) is not JsonObject root) return [];

            var result = new List<AuditChangeDto>();

            foreach (var (field, value) in root)
            {
                if (value is JsonObject pair)
                {
                    result.Add(new AuditChangeDto(
                        field,
                        Text(pair["from"]),
                        Text(pair["to"])));
                }
                else
                {
                    // A create writes the new values flat rather than as pairs.
                    result.Add(new AuditChangeDto(field, null, Text(value)));
                }
            }

            return result;
        }
        catch (JsonException)
        {
            // A malformed entry is a bug upstream, but the rest of the trail
            // still has to be readable.
            return [];
        }
    }

    private static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue value => value.ToString(),
        _ => node.ToJsonString(),
    };
}
