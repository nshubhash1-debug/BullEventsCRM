using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The follow-up queue — every dated commitment across leads, contacts and
/// opportunities in one list, which is how a rep actually plans a day.
/// </summary>
[ApiController]
[Route("api/follow-ups")]
[Authorize]
[SecuredBy(SecuredObjects.FollowUp)]
public class FollowUpsController(AppDbContext db) : CrmControllerBase(db)
{
    internal static readonly FieldMap<FollowUp> Fields = new FieldMap<FollowUp>()
        .Text("subject", "Subject", searchable: true)
        .Text("description", "Description")
        .Text("relatedName", "Related to", searchable: true)
        .Select("relatedType", "Related object")
        .Number("relatedId", "Related ID")
        .Select("channel", "Channel")
        .Select("status", "Status")
        .Select("priority", "Priority")
        .Date("dueAt", "Due")
        .Date("reminderAt", "Reminder")
        .Date("completedAt", "Completed")
        .Text("outcome", "Outcome", searchable: true)
        .Number("slaMinutes", "SLA (minutes)")
        .Select("ownerName", "Owner", "Owner.Name")
        .Select("branchName", "Branch", "Branch.Name")
        .Number("ownerId", "Owner ID")
        .Number("branchId", "Branch ID")
        .Date("createdAt", "Created")
        .Date("updatedAt", "Last modified");

    private IQueryable<FollowUp> Base() => Db.FollowUps
        .Include(f => f.Branch)
        .Include(f => f.Owner)
        .AsNoTracking();

    private static FollowUpDto ToDto(FollowUp f)
    {
        var now = DateTime.UtcNow;
        var isOpen = f.Status is FollowUpStatuses.Open or FollowUpStatuses.InProgress;

        return new FollowUpDto(
            f.Id, f.Subject, f.Description,
            f.RelatedType, f.RelatedId, f.RelatedName,
            f.Channel, f.Status, f.Priority,
            f.DueAt, f.ReminderAt, f.CompletedAt, f.Outcome, f.SlaMinutes,
            isOpen && f.DueAt < now,
            (int)Math.Round((f.DueAt - now).TotalMinutes),
            f.BranchId, f.Branch?.Name ?? "—", f.OwnerId, f.Owner?.Name,
            f.CreatedAt, f.UpdatedAt);
    }

    /* ------------------------------------------------------------------ *
     * Reads
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<FilterFieldDto>>> GetFields(CancellationToken ct)
    {
        var owners = await Db.Users
            .Where(u => u.CompanyId == Db.Tenant.CompanyId && u.IsActive)
            .Select(u => u.Name).OrderBy(n => n).ToListAsync(ct);

        var branches = await Db.Branches
            .Where(b => b.CompanyId == Db.Tenant.CompanyId)
            .Select(b => b.Name).OrderBy(n => n).ToListAsync(ct);

        return Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["relatedType"] = Options(RelatedTypes.All),
            ["channel"] = Options(FollowUpChannels.All),
            ["status"] = Options(FollowUpStatuses.All),
            ["priority"] = Options(LeadPriorities.All),
            ["ownerName"] = Options([.. owners]),
            ["branchName"] = Options([.. branches]),
        }, new Dictionary<string, string>
        {
            ["subject"] = "Task",
            ["description"] = "Task",
            ["channel"] = "Task",
            ["outcome"] = "Task",
            ["relatedName"] = "Related",
            ["relatedType"] = "Related",
            ["status"] = "Status",
            ["priority"] = "Status",
            ["dueAt"] = "Timing",
            ["reminderAt"] = "Timing",
            ["completedAt"] = "Timing",
            ["slaMinutes"] = "Timing",
        }));
    }

    [PermissionAction(ObjectAction.View)]
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<FollowUpDto>>> Query(QueryRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        return Ok(await RunQueryAsync(
            Base(),
            request,
            Fields,
            ToDto,
            defaultSortPath: "DueAt",
            defaultSortDescending: false,
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["open"] = await filtered.CountAsync(f => f.Status == FollowUpStatuses.Open, ct),
                ["overdue"] = await filtered.CountAsync(f =>
                    f.DueAt < now &&
                    (f.Status == FollowUpStatuses.Open || f.Status == FollowUpStatuses.InProgress), ct),
                ["dueToday"] = await filtered.CountAsync(f =>
                    f.DueAt >= today && f.DueAt < today.AddDays(1), ct),
                ["completed"] = await filtered.CountAsync(f => f.Status == FollowUpStatuses.Completed, ct),
            },
            cancellationToken: ct));
    }

    /// <summary>
    /// The agenda strip above the list: overdue, today, tomorrow, this week and
    /// later — the five buckets a rep triages against.
    /// </summary>
    [HttpGet("agenda")]
    public async Task<ActionResult<AgendaDto>> Agenda([FromQuery] int? ownerId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var today = now.Date;

        var query = Db.FollowUps.AsNoTracking()
            .Where(f => f.Status == FollowUpStatuses.Open || f.Status == FollowUpStatuses.InProgress);

        if (ownerId is not null) query = query.Where(f => f.OwnerId == ownerId);

        var open = await query
            .Select(f => new { f.DueAt, f.Priority, f.Channel })
            .ToListAsync(ct);

        int Count(Func<DateTime, bool> predicate) => open.Count(f => predicate(f.DueAt));

        var byChannel = open
            .GroupBy(f => f.Channel)
            .Select(g => new AgendaBucketDto(g.Key, Humanise(g.Key), g.Count()))
            .OrderByDescending(b => b.Count)
            .ToList();

        var byPriority = open
            .GroupBy(f => f.Priority)
            .Select(g => new AgendaBucketDto(g.Key, g.Key, g.Count()))
            .ToList();

        return Ok(new AgendaDto(
            Count(due => due < now),
            Count(due => due >= today && due < today.AddDays(1)),
            Count(due => due >= today.AddDays(1) && due < today.AddDays(2)),
            Count(due => due >= today.AddDays(2) && due < today.AddDays(7)),
            Count(due => due >= today.AddDays(7)),
            byChannel,
            byPriority));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<FollowUpDto>> GetOne(int id, CancellationToken ct)
    {
        var followUp = await Base().FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw ApiException.NotFound("Follow-up");

        return Ok(ToDto(followUp));
    }

    /* ------------------------------------------------------------------ *
     * Writes
     * ------------------------------------------------------------------ */

    [HttpPost]
    public async Task<ActionResult<FollowUpDto>> Create(FollowUpInput input, CancellationToken ct)
    {
        var branch = await RequireBranchAsync(input.BranchId, ct);
        await RequireOwnerAsync(input.OwnerId, ct);

        var followUp = new FollowUp
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            OwnerId = input.OwnerId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null),
        };

        Apply(followUp, input);

        Db.FollowUps.Add(followUp);
        await Db.SaveChangesAsync(ct);

        followUp.Branch = branch;
        return CreatedAtAction(nameof(GetOne), new { id = followUp.Id }, ToDto(followUp));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<FollowUpDto>> Update(int id, FollowUpInput input, CancellationToken ct)
    {
        var followUp = await Db.FollowUps.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw ApiException.NotFound("Follow-up");

        var branch = await RequireBranchAsync(input.BranchId, ct);
        await RequireOwnerAsync(input.OwnerId, ct);

        followUp.BranchId = branch.Id;
        followUp.OwnerId = input.OwnerId;
        Apply(followUp, input);

        await Db.SaveChangesAsync(ct);

        followUp.Branch = branch;
        return Ok(ToDto(followUp));
    }

    /// <summary>Tick-off from the list, with the outcome captured in the same move.</summary>
    [HttpPatch("{id:int}/complete")]
    public async Task<ActionResult<FollowUpDto>> Complete(int id, CompleteFollowUpRequest request, CancellationToken ct)
    {
        var followUp = await Db.FollowUps
            .Include(f => f.Branch).Include(f => f.Owner)
            .FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw ApiException.NotFound("Follow-up");

        followUp.Status = FollowUpStatuses.Completed;
        followUp.CompletedAt = DateTime.UtcNow;
        followUp.Outcome = request.Outcome;

        // Closing one commitment usually creates the next; doing it here keeps
        // the queue unbroken rather than relying on the rep to remember.
        if (request.NextDueAt is DateTime nextDue)
        {
            Db.FollowUps.Add(new FollowUp
            {
                CompanyId = followUp.CompanyId,
                BranchId = followUp.BranchId,
                Subject = request.NextSubject ?? $"Follow up: {followUp.Subject}",
                RelatedType = followUp.RelatedType,
                RelatedId = followUp.RelatedId,
                RelatedName = followUp.RelatedName,
                Channel = request.NextChannel ?? followUp.Channel,
                Priority = followUp.Priority,
                DueAt = nextDue,
                SlaMinutes = followUp.SlaMinutes,
                OwnerId = followUp.OwnerId,
            });
        }

        await LogLeadActivityAsync(
            followUp.RelatedType == RelatedTypes.Lead ? followUp.RelatedId : null,
            followUp.Channel == FollowUpChannels.Task
                ? LeadActivityTypes.Task
                : LeadActivityTypes.Note,
            $"{(followUp.Channel == FollowUpChannels.Task ? "Task" : "Follow-up")} “{followUp.Subject}” completed"
                + (string.IsNullOrWhiteSpace(request.Outcome) ? "." : $" — {request.Outcome}"),
            ct);

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(followUp));
    }

    /// <summary>
    /// Move a follow-up or task to any status from wherever it is listed.
    ///
    /// A follow-up has no slot, so "reschedule" is simply a new due date — and
    /// it is allowed alongside any open status, because pushing a date is the
    /// most common edit of all and should not need a second call.
    /// </summary>
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<FollowUpDto>> SetStatus(
        int id,
        FollowUpStatusRequest request,
        CancellationToken ct)
    {
        var followUp = await Db.FollowUps
            .Include(f => f.Branch).Include(f => f.Owner)
            .FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw ApiException.NotFound("Follow-up");

        var status = Require(request.Status, FollowUpStatuses.All, "status");
        var previous = followUp.Status;
        var moved = request.DueAt is DateTime due && due != followUp.DueAt;

        if (status == previous && !moved) return Ok(ToDto(followUp));

        if (request.DueAt is DateTime newDue) followUp.DueAt = newDue;

        followUp.Status = status;
        followUp.Outcome = request.Outcome ?? followUp.Outcome;

        // Only a completion carries a completion stamp; reopening one has to
        // clear it, or the queue shows an open item that closed last Tuesday.
        followUp.CompletedAt = status == FollowUpStatuses.Completed
            ? followUp.CompletedAt ?? DateTime.UtcNow
            : null;

        var label = followUp.Channel == FollowUpChannels.Task ? "Task" : "Follow-up";
        var line = status == previous && moved
            ? $"{label} “{followUp.Subject}” moved to {followUp.DueAt:dd MMM yyyy HH:mm}"
            : $"{label} “{followUp.Subject}”: {previous} → {status}"
              + (moved ? $", now due {followUp.DueAt:dd MMM yyyy HH:mm}" : "");

        await LogLeadActivityAsync(
            followUp.RelatedType == RelatedTypes.Lead ? followUp.RelatedId : null,
            followUp.Channel == FollowUpChannels.Task
                ? LeadActivityTypes.Task
                : LeadActivityTypes.Note,
            string.IsNullOrWhiteSpace(request.Outcome) ? line + "." : $"{line} — {request.Outcome}",
            ct);

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(followUp));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var followUp = await Db.FollowUps.FirstOrDefaultAsync(f => f.Id == id, ct)
            ?? throw ApiException.NotFound("Follow-up");

        SoftDelete(followUp);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static void Apply(FollowUp followUp, FollowUpInput input)
    {
        followUp.Subject = input.Subject.Trim();
        followUp.Description = input.Description;
        followUp.RelatedType = Require(input.RelatedType, RelatedTypes.All, "related object");
        followUp.RelatedId = input.RelatedId;
        followUp.RelatedName = input.RelatedName;
        followUp.Channel = Require(input.Channel, FollowUpChannels.All, "channel");
        followUp.Status = Require(input.Status, FollowUpStatuses.All, "status");
        followUp.Priority = Require(input.Priority, LeadPriorities.All, "priority");
        followUp.DueAt = input.DueAt;
        followUp.ReminderAt = input.ReminderAt;
        followUp.Outcome = input.Outcome;
        followUp.SlaMinutes = Math.Clamp(input.SlaMinutes, 15, 60 * 24 * 30);

        followUp.CompletedAt = followUp.Status == FollowUpStatuses.Completed
            ? followUp.CompletedAt ?? DateTime.UtcNow
            : null;
    }
}

public record CompleteFollowUpRequest(
    string? Outcome,
    DateTime? NextDueAt,
    string? NextSubject,
    string? NextChannel
);

public record AgendaBucketDto(string Key, string Label, int Count);

public record AgendaDto(
    int Overdue,
    int Today,
    int Tomorrow,
    int ThisWeek,
    int Later,
    IReadOnlyList<AgendaBucketDto> ByChannel,
    IReadOnlyList<AgendaBucketDto> ByPriority
);
