using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Decides what needs a second signature, and records the request.
///
/// The rule is deliberately narrow: routine work must not queue. A rep holding
/// a unit for a day, or quoting the discount the plan already grants, needs
/// nobody's permission. Taking a unit off the market, committing it to a buyer,
/// or discounting past what the plan allows are the three moves that cost real
/// money if they are wrong, and those are the three that stop for approval.
///
/// Managers approve their own moves implicitly — asking a branch manager to
/// raise a request for themselves and then grant it teaches everyone to click
/// through approvals without reading them.
/// </summary>
public class ApprovalService(AppDbContext db)
{
    private static readonly string[] Approvers =
    [
        Roles.SuperAdmin, Roles.CompanyAdmin, Roles.BranchManager
    ];

    public static bool CanDecide(string role) => Approvers.Contains(role);

    /// <summary>Whether the current user's move applies straight away.</summary>
    public bool IsSelfApproving => CanDecide(db.Tenant.Role);

    /// <summary>
    /// Raises a request. Does not save — the caller commits it together with
    /// whatever it did to the record, so an approval can never exist for a
    /// change that was rolled back.
    /// </summary>
    public Approval Request(
        string entityType,
        int entityId,
        string entityLabel,
        string kind,
        string summary,
        int branchId,
        string? reason = null,
        decimal? amount = null)
    {
        var approval = new Approval
        {
            CompanyId = db.Tenant.CompanyId,
            BranchId = branchId,
            EntityType = entityType,
            EntityId = entityId,
            EntityLabel = entityLabel,
            Kind = kind,
            Status = ApprovalStatuses.Pending,
            Summary = summary,
            Reason = reason,
            Amount = amount,
            RequestedById = db.Tenant.UserId,
            RequestedByName = db.Tenant.UserName,
            RequestedAt = DateTime.UtcNow,
        };

        db.Approvals.Add(approval);
        return approval;
    }

    /// <summary>
    /// The open request against a record, if there is one. Used to badge the
    /// unit or quotation without the caller having to know the shape of the
    /// approvals table.
    /// </summary>
    public Task<Approval?> PendingForAsync(
        string entityType, int entityId, CancellationToken ct = default) =>
        db.Approvals.FirstOrDefaultAsync(
            a => a.EntityType == entityType
                && a.EntityId == entityId
                && a.Status == ApprovalStatuses.Pending,
            ct);

    /// <summary>
    /// Closes any open request against a record without a decision — used when
    /// the underlying change is withdrawn or superseded, so the approver's queue
    /// does not fill with questions nobody needs answered any more.
    /// </summary>
    public async Task CancelOpenAsync(
        string entityType, int entityId, string note, CancellationToken ct = default)
    {
        var open = await db.Approvals
            .Where(a => a.EntityType == entityType
                && a.EntityId == entityId
                && a.Status == ApprovalStatuses.Pending)
            .ToListAsync(ct);

        foreach (var approval in open)
        {
            approval.Status = ApprovalStatuses.Cancelled;
            approval.DecidedAt = DateTime.UtcNow;
            approval.DecisionNote = note;
        }
    }
}
