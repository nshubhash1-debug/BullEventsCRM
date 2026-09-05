using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The approval queue — one list for every exception waiting on a manager.
///
/// Deciding is the interesting part: an approval is not a note, it is the thing
/// that makes the change happen. Approving a booking books the unit; approving
/// a discount releases the quotation to be sent. Both effects live here so a
/// decision can never be recorded without the change it authorises.
/// </summary>
[ApiController]
[Route("api/approvals")]
[Authorize]
[SecuredBy(SecuredObjects.Quotation)]
public class ApprovalsController(AppDbContext db) : CrmControllerBase(db)
{
    private IQueryable<Approval> Base() => Db.Approvals.Include(a => a.Branch);

    private static ApprovalDto ToDto(Approval a) =>
        InventoryBoardController.ToApprovalDto(a, a.Branch?.Name ?? "—");

    /* ------------------------------------------------------------------ *
     * Reads
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The queue. Defaults to what is still open, oldest first — an approval
    /// list sorted newest-first buries exactly the requests that have waited
    /// longest.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ApprovalDto>>> List(
        [FromQuery] string? status,
        [FromQuery] string? entityType,
        [FromQuery] bool mine = false,
        CancellationToken ct = default)
    {
        var query = Base().AsNoTracking();

        query = string.IsNullOrWhiteSpace(status)
            ? query.Where(a => a.Status == ApprovalStatuses.Pending)
            : status.Equals("All", StringComparison.OrdinalIgnoreCase)
                ? query
                : query.Where(a => a.Status == Require(status, ApprovalStatuses.All, "status"));

        if (!string.IsNullOrWhiteSpace(entityType))
        {
            var kind = Require(entityType, ApprovalEntities.All, "entity type");
            query = query.Where(a => a.EntityType == kind);
        }

        if (mine) query = query.Where(a => a.RequestedById == Db.Tenant.UserId);

        var items = await query
            .OrderBy(a => a.Status == ApprovalStatuses.Pending ? 0 : 1)
            .ThenBy(a => a.RequestedAt)
            .Take(300)
            .ToListAsync(ct);

        return Ok(items.Select(ToDto).ToList());
    }

    /// <summary>Counts for the sidebar badge, in one round trip.</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<ApprovalSummaryDto>> Summary(CancellationToken ct)
    {
        var pending = Db.Approvals.Where(a => a.Status == ApprovalStatuses.Pending);
        var cutoff = DateTime.UtcNow.AddHours(-24);

        return Ok(new ApprovalSummaryDto(
            await pending.CountAsync(ct),
            await pending.CountAsync(a => a.EntityType == ApprovalEntities.Quotation, ct),
            await pending.CountAsync(a => a.EntityType == ApprovalEntities.Unit, ct),
            await pending.CountAsync(a => a.RequestedAt < cutoff, ct),
            await pending.CountAsync(a => a.RequestedById == Db.Tenant.UserId, ct),
            ApprovalService.CanDecide(Db.Tenant.Role)));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<ApprovalDto>> GetOne(int id, CancellationToken ct)
    {
        var approval = await Base().AsNoTracking().FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Approval");

        return Ok(ToDto(approval));
    }

    /* ------------------------------------------------------------------ *
     * Decisions
     * ------------------------------------------------------------------ */

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/approve")]
    public Task<ActionResult<ApprovalDto>> Approve(
        int id, ApprovalDecisionRequest request, CancellationToken ct) =>
        DecideAsync(id, ApprovalStatuses.Approved, request.Note, ct);

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/reject")]
    public Task<ActionResult<ApprovalDto>> Reject(
        int id, ApprovalDecisionRequest request, CancellationToken ct) =>
        DecideAsync(id, ApprovalStatuses.Rejected, request.Note, ct);

    /// <summary>Withdrawn by whoever raised it, before anyone has ruled on it.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<ApprovalDto>> Cancel(
        int id, ApprovalDecisionRequest request, CancellationToken ct)
    {
        var approval = await Base().FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Approval");

        if (approval.RequestedById != Db.Tenant.UserId && !ApprovalService.CanDecide(Db.Tenant.Role))
        {
            throw ApiException.Forbidden("Only the requester can withdraw this.");
        }

        if (approval.Status != ApprovalStatuses.Pending)
        {
            throw ApiException.Conflict($"This request was already {approval.Status.ToLowerInvariant()}.");
        }

        approval.Status = ApprovalStatuses.Cancelled;
        approval.DecidedById = Db.Tenant.UserId;
        approval.DecidedByName = Db.Tenant.UserName;
        approval.DecidedAt = DateTime.UtcNow;
        approval.DecisionNote = request.Note;

        await ReleaseParkedUnitAsync(approval, "Request withdrawn.", ct);
        await Db.SaveChangesAsync(ct);

        return Ok(ToDto(approval));
    }

    private async Task<ActionResult<ApprovalDto>> DecideAsync(
        int id, string decision, string? note, CancellationToken ct)
    {
        if (!ApprovalService.CanDecide(Db.Tenant.Role))
        {
            throw ApiException.Forbidden("Approvals are decided by a branch manager or above.");
        }

        var approval = await Base().FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Approval");

        if (approval.Status != ApprovalStatuses.Pending)
        {
            throw ApiException.Conflict(
                $"This request was already {approval.Status.ToLowerInvariant()} "
                + $"by {approval.DecidedByName ?? "someone"}.");
        }

        // Nobody rules on their own exception. Without this the workflow is
        // decoration: a manager could raise and grant their own discount.
        if (approval.RequestedById == Db.Tenant.UserId)
        {
            throw ApiException.Forbidden("An approval has to be decided by someone other than its requester.");
        }

        approval.Status = decision;
        approval.DecidedById = Db.Tenant.UserId;
        approval.DecidedByName = Db.Tenant.UserName;
        approval.DecidedAt = DateTime.UtcNow;
        approval.DecisionNote = note;

        if (approval.EntityType == ApprovalEntities.Unit)
        {
            await ApplyUnitDecisionAsync(approval, decision, note, ct);
        }
        else
        {
            await ApplyQuotationDecisionAsync(approval, decision, note, ct);
        }

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(approval));
    }

    /* ------------------------------------------------------------------ *
     * Effects
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Approving moves the unit to what was asked for; rejecting hands it back
    /// to the market. Either way the parking hold goes, because a decided
    /// request must not leave stock quietly locked.
    /// </summary>
    private async Task ApplyUnitDecisionAsync(
        Approval approval, string decision, string? note, CancellationToken ct)
    {
        var unit = await Db.Units.FirstOrDefaultAsync(u => u.Id == approval.EntityId, ct);
        if (unit is null) return;

        var from = unit.Status;

        if (decision == ApprovalStatuses.Approved)
        {
            unit.Status = approval.Kind == ApprovalKinds.UnitBlock
                ? UnitStatuses.Blocked
                : UnitStatuses.Booked;

            unit.HeldByUserId = null;
            unit.HeldUntil = null;
            unit.HoldReason = null;

            if (unit.Status == UnitStatuses.Booked) unit.BookedAt ??= DateTime.UtcNow;
            else unit.BlockReason = approval.Reason;
        }
        else
        {
            unit.Status = UnitStatuses.Available;
            unit.HeldByUserId = null;
            unit.HeldUntil = null;
            unit.HoldReason = null;
            unit.BookedByContactId = null;
            unit.BookedByLeadId = null;
            unit.BookedQuotationId = null;
            unit.BookedAt = null;
            unit.CustomerName = null;
            unit.CustomerPhone = null;
        }

        Db.UnitStatusHistories.Add(new UnitStatusHistory
        {
            UnitId = unit.Id,
            FromStatus = from,
            ToStatus = unit.Status,
            Reason = $"{decision} by {Db.Tenant.UserName}"
                + (string.IsNullOrWhiteSpace(note) ? "." : $" — {note}"),
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });
    }

    /// <summary>
    /// A quotation whose discount is approved becomes sendable; a rejected one
    /// stays a draft with the reason attached, so the rep can requote rather
    /// than guess.
    /// </summary>
    private async Task ApplyQuotationDecisionAsync(
        Approval approval, string decision, string? note, CancellationToken ct)
    {
        var quotation = await Db.Quotations.FirstOrDefaultAsync(q => q.Id == approval.EntityId, ct);
        if (quotation is null) return;

        if (decision == ApprovalStatuses.Approved)
        {
            quotation.ApprovalStatus = QuotationApprovalStatuses.Approved;
        }
        else
        {
            quotation.ApprovalStatus = QuotationApprovalStatuses.Rejected;
            quotation.Status = QuotationStatuses.Draft;
            quotation.RejectionReason = note ?? "Discount not approved.";
        }
    }

    /// <summary>Frees a unit parked behind a request that was withdrawn.</summary>
    private async Task ReleaseParkedUnitAsync(Approval approval, string reason, CancellationToken ct)
    {
        if (approval.EntityType != ApprovalEntities.Unit) return;

        var unit = await Db.Units.FirstOrDefaultAsync(u => u.Id == approval.EntityId, ct);
        if (unit is null || unit.Status != UnitStatuses.Held) return;

        var from = unit.Status;

        unit.Status = UnitStatuses.Available;
        unit.HeldByUserId = null;
        unit.HeldUntil = null;
        unit.HoldReason = null;

        Db.UnitStatusHistories.Add(new UnitStatusHistory
        {
            UnitId = unit.Id,
            FromStatus = from,
            ToStatus = unit.Status,
            Reason = reason,
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });
    }
}

public record ApprovalSummaryDto(
    int Pending,
    int PendingQuotations,
    int PendingUnits,
    /// <summary>Open for more than a day — the queue's ageing problem.</summary>
    int Overdue,
    int RaisedByMe,
    bool CanDecide);
