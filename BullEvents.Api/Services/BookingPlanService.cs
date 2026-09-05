using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// The payment plan after the sale.
///
/// A plan is a pre-sales artefact only until the customer signs. From booking
/// onwards it is a post-sales object with a life of its own: instalments get
/// rescheduled when a slab slips, waived when a negotiation lands, added when
/// the customer buys a second parking space, and occasionally the whole plan is
/// replaced because a buyer switched from construction-linked to down-payment
/// for a better price.
///
/// Every one of those is a recorded event rather than an edit. A schedule that
/// silently changed underneath a customer who has already paid against it is
/// how a developer loses an arbitration.
/// </summary>
public class BookingPlanService(AppDbContext db, BookingLedger ledger, TenantContext tenant)
{
    /* ------------------------------------------------------------------ *
     * One instalment
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Moves an instalment's due date.
    ///
    /// Allowed before it is demanded, and on a raised demand too — a slab that
    /// slipped three weeks moves the whole tower's due dates, and refusing to
    /// move a demand already sent means either cancelling it or charging penal
    /// interest for a delay that was the developer's.
    /// </summary>
    public async Task<BookingMilestone> RescheduleAsync(
        int bookingId, int milestoneId, DateTime dueDate, string reason, CancellationToken ct = default)
    {
        var milestone = await Load(bookingId, milestoneId, ct);

        if (milestone.Status is MilestoneStatuses.Paid or MilestoneStatuses.Waived)
        {
            throw ApiException.Conflict(
                $"{milestone.Label} is already {milestone.Status.ToLowerInvariant()} and cannot be moved.");
        }

        var was = milestone.DueDate;
        milestone.DueDate = dueDate.Date;

        var demand = await db.Demands.FirstOrDefaultAsync(
            d => d.BookingMilestoneId == milestoneId && d.Status != DemandStatuses.Cancelled, ct);

        if (demand is not null)
        {
            // The interest clock runs from the demand's due date, so moving the
            // instalment without moving the demand would keep charging for the
            // extension that was just granted.
            demand.DueDate = dueDate.Date;
            demand.Notes = Append(demand.Notes,
                $"Rescheduled from {was:d MMM yyyy} to {dueDate:d MMM yyyy}. {reason}");
        }

        await db.SaveChangesAsync(ct);
        await ledger.RecomputeAsync(bookingId, ct: ct);

        return milestone;
    }

    /// <summary>
    /// Writes an instalment off.
    ///
    /// Any demand already raised against it is cancelled rather than deleted, so
    /// the customer's statement still shows that it was billed and then dropped
    /// — which is the version both sides can agree on later.
    /// </summary>
    public async Task<BookingMilestone> WaiveAsync(
        int bookingId, int milestoneId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw ApiException.BadRequest("A waiver needs a reason on the record.");
        }

        var milestone = await Load(bookingId, milestoneId, ct);

        if (milestone.Status == MilestoneStatuses.Paid)
        {
            throw ApiException.Conflict(
                $"{milestone.Label} has been paid. Refund it rather than waiving it.");
        }

        milestone.Status = MilestoneStatuses.Waived;

        var demand = await db.Demands.FirstOrDefaultAsync(
            d => d.BookingMilestoneId == milestoneId && d.Status != DemandStatuses.Cancelled, ct);

        if (demand is not null)
        {
            if (demand.Received > 0.5m)
            {
                throw ApiException.Conflict(
                    $"{demand.DemandNumber} has already been part-paid. Adjust it rather than waiving it.");
            }

            demand.Status = DemandStatuses.Cancelled;
            demand.CancelledOn = DateTime.UtcNow;
            demand.Notes = Append(demand.Notes, $"Waived by {tenant.UserName}. {reason}");
        }

        await db.SaveChangesAsync(ct);
        await ledger.RecomputeAsync(bookingId, ct: ct);

        return milestone;
    }

    /// <summary>
    /// Adds an instalment the original plan did not carry.
    ///
    /// The everyday case is a customer buying something after booking — a second
    /// parking space, a bigger deck — which has to be billable without rewriting
    /// the plan they agreed to. The booking's total moves with it, because it is
    /// genuinely a larger sale now.
    /// </summary>
    public async Task<BookingMilestone> AddInstalmentAsync(
        int bookingId,
        string label,
        decimal basicAmount,
        decimal taxAmount,
        DateTime? dueDate,
        CancellationToken ct = default)
    {
        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw ApiException.NotFound("Booking");

        if (basicAmount <= 0) throw ApiException.BadRequest("An instalment needs an amount.");

        var last = await db.BookingMilestones
            .Where(m => m.BookingId == bookingId)
            .MaxAsync(m => (int?)m.SortOrder, ct) ?? 0;

        var total = basicAmount + taxAmount;

        var milestone = new BookingMilestone
        {
            CompanyId = tenant.CompanyId,
            BookingId = bookingId,
            SortOrder = last + 1,
            Label = label.Trim(),
            BasicAmount = basicAmount,
            TaxAmount = taxAmount,
            TotalAmount = total,
            DueDate = dueDate?.Date,
            Status = MilestoneStatuses.Pending,

            // Percent is left at zero on purpose: this is not a share of the
            // agreed consideration, and showing it as one would make the plan's
            // percentages stop summing to a hundred.
            Percent = 0,
        };

        db.BookingMilestones.Add(milestone);

        booking.OtherCharges += basicAmount;
        booking.TaxAmount += taxAmount;
        booking.GrandTotal += total;

        await db.SaveChangesAsync(ct);
        await ledger.RecomputeAsync(bookingId, ct: ct);

        return milestone;
    }

    /* ------------------------------------------------------------------ *
     * The whole plan
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Replaces the remaining schedule with a different plan.
    ///
    /// A buyer who has paid the booking amount on a construction-linked plan and
    /// then decides to pay down and take the discount is a normal Tuesday. The
    /// rule that makes it safe: <em>paid and part-paid instalments are never
    /// touched</em>. The new plan is applied to what is left, and what is left
    /// is the grand total less everything already demanded — so a customer
    /// cannot be billed twice for the same money by switching plans.
    /// </summary>
    public async Task<IReadOnlyList<BookingMilestone>> RevisePlanAsync(
        int bookingId, int paymentPlanId, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw ApiException.BadRequest("Revising a plan needs a reason on the record.");
        }

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw ApiException.NotFound("Booking");

        var plan = await db.PaymentPlans
            .Include(p => p.Milestones)
            .FirstOrDefaultAsync(p => p.Id == paymentPlanId, ct)
            ?? throw ApiException.NotFound("Payment plan");

        var existing = await db.BookingMilestones
            .Where(m => m.BookingId == bookingId)
            .OrderBy(m => m.SortOrder)
            .ToListAsync(ct);

        var settled = existing
            .Where(m => m.Status is MilestoneStatuses.Paid
                        or MilestoneStatuses.PartlyPaid
                        or MilestoneStatuses.Demanded
                        or MilestoneStatuses.Waived)
            .ToList();

        var replaceable = existing.Except(settled).ToList();

        if (replaceable.Count == 0)
        {
            throw ApiException.Conflict(
                "Every instalment on this plan has already been demanded. There is nothing left to revise.");
        }

        // What the new plan has to raise: the whole consideration, less what the
        // untouched instalments were going to cover.
        var alreadyCommitted = settled.Sum(m => m.TotalAmount);
        var toSchedule = Math.Max(0, booking.GrandTotal - alreadyCommitted);

        db.BookingMilestones.RemoveRange(replaceable);

        var order = settled.Count == 0 ? 0 : settled.Max(m => m.SortOrder) + 1;
        var fixedTotal = plan.Milestones
            .Where(m => m.Basis == MilestoneBases.Fixed)
            .Sum(m => m.FixedAmount);

        var added = new List<BookingMilestone>();
        var running = alreadyCommitted;

        foreach (var template in plan.Milestones.OrderBy(m => m.SortOrder))
        {
            var amount = template.Basis switch
            {
                MilestoneBases.Fixed => template.FixedAmount,
                MilestoneBases.PercentOfTotal => booking.GrandTotal * template.Percent,
                MilestoneBases.PercentOfNetOfFixed => (toSchedule - fixedTotal) * template.Percent,
                MilestoneBases.BalanceToPercent =>
                    Math.Max(0, booking.GrandTotal * template.Percent - running),
                _ => 0,
            };

            amount = Math.Round(amount, 2);
            if (amount <= 0) continue;

            running += amount;

            var milestone = new BookingMilestone
            {
                CompanyId = tenant.CompanyId,
                BookingId = bookingId,
                SortOrder = order++,
                Label = template.Label,
                Percent = template.Percent,

                // The plan's figures are gross. Splitting basic from tax here
                // would need the charge mix, which the plan does not carry —
                // so the instalment is stored gross and the demand letter says
                // "inclusive of taxes", which is what these letters say anyway.
                BasicAmount = amount,
                TaxAmount = 0,
                TotalAmount = amount,

                ConstructionStage = template.ConstructionStage,
                DueDate = template.DueOffsetDays is int days
                    ? booking.BookingDate.Date.AddDays(days)
                    : null,
                Status = MilestoneStatuses.Pending,
            };

            db.BookingMilestones.Add(milestone);
            added.Add(milestone);
        }

        // Rounding across a dozen percentage lines will not land exactly on the
        // total. The last instalment absorbs the difference, which is what a
        // cost sheet does and what a customer checking the arithmetic expects.
        var drift = Math.Round(booking.GrandTotal - running, 2);

        if (Math.Abs(drift) >= 0.01m && added.Count > 0)
        {
            var last = added[^1];
            last.BasicAmount += drift;
            last.TotalAmount += drift;
        }

        booking.PaymentPlanId = plan.Id;
        booking.PaymentPlanName = plan.Name;
        booking.Notes = Append(booking.Notes,
            $"Plan revised to {plan.Name} by {tenant.UserName} on {DateTime.UtcNow:d MMM yyyy}. {reason}");

        await db.SaveChangesAsync(ct);
        await ledger.RecomputeAsync(bookingId, ct: ct);

        return added;
    }

    /* ------------------------------------------------------------------ *
     * Interest
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Writes off penal interest on a demand.
    ///
    /// The commonest concession in this business and the one most often given
    /// verbally and never recorded. Capped at what has actually accrued, with a
    /// reason, so the collections report and the customer's statement agree
    /// about why a balance dropped.
    /// </summary>
    public async Task<Demand> WaiveInterestAsync(
        int demandId, decimal? amount, string reason, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw ApiException.BadRequest("An interest waiver needs a reason on the record.");
        }

        var demand = await db.Demands.FirstOrDefaultAsync(d => d.Id == demandId, ct)
            ?? throw ApiException.NotFound("Demand");

        var waivable = demand.InterestDue;

        if (waivable <= 0.5m)
        {
            throw ApiException.Conflict("There is no interest outstanding on this demand.");
        }

        var waive = Math.Min(amount ?? waivable, waivable);

        demand.InterestWaived += Math.Round(waive, 2);
        demand.Notes = Append(demand.Notes,
            $"Interest of {waive:N0} waived by {tenant.UserName}. {reason}");

        await db.SaveChangesAsync(ct);
        await ledger.RecomputeAsync(demand.BookingId, ct: ct);

        return demand;
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    private async Task<BookingMilestone> Load(int bookingId, int milestoneId, CancellationToken ct) =>
        await db.BookingMilestones
            .FirstOrDefaultAsync(m => m.Id == milestoneId && m.BookingId == bookingId, ct)
        ?? throw ApiException.NotFound("Instalment");

    /// <summary>
    /// Appends to a note rather than replacing it. These fields are the audit
    /// trail people actually read, and overwriting one loses the reason the
    /// previous change was made.
    /// </summary>
    private static string Append(string? existing, string line) =>
        string.IsNullOrWhiteSpace(existing) ? line : $"{existing}\n{line}";
}
