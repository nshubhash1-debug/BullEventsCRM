using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>One line of the customer's running account.</summary>
public record LedgerLine(
    DateTime On,
    string Kind,
    string Reference,
    string Description,
    /// <summary>What was billed — a demand, or interest charged.</summary>
    decimal Debit,
    /// <summary>What was paid or written off.</summary>
    decimal Credit,
    decimal Balance);

/// <summary>Where a booking's money stands, recomputed from the rows beneath it.</summary>
public record LedgerSummary(
    decimal GrandTotal,
    decimal Demanded,
    decimal Received,
    decimal InterestCharged,
    decimal InterestWaived,
    decimal Outstanding,
    /// <summary>Billed but not yet demanded — the rest of the plan.</summary>
    decimal NotYetDemanded,
    int OverdueDays,
    decimal OverdueAmount,
    IReadOnlyList<LedgerLine> Lines);

/// <summary>
/// The arithmetic behind every post-sales number.
///
/// One place, deliberately. Collections, the booking list, the ageing report and
/// the customer's own statement all have to agree, and the fastest way to make
/// them disagree is to let each compute its own total. Everything here is
/// derived from demands, receipts and allocations; the cached figures on the
/// booking are written by <see cref="RecomputeAsync"/> and read by everything
/// else.
///
/// Interest is <em>recomputed</em> rather than accrued, so a receipt entered
/// late — which is most of them — corrects the charge instead of leaving the
/// customer billed for days their money was already in the bank.
/// </summary>
public class BookingLedger(AppDbContext db)
{
    /// <summary>
    /// Recomputes one booking's position and writes the cached totals back.
    ///
    /// Called after anything that moves money: a demand raised, a receipt taken,
    /// a cheque bounced, interest waived. Cheap — one booking's demands and
    /// receipts are tens of rows, not thousands.
    /// </summary>
    public async Task<LedgerSummary> RecomputeAsync(
        int bookingId, DateTime? asOf = null, CancellationToken ct = default)
    {
        var today = (asOf ?? DateTime.UtcNow).Date;

        var booking = await db.Bookings
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw new InvalidOperationException($"Booking {bookingId} does not exist.");

        var demands = await db.Demands
            .IgnoreQueryFilters()
            .Where(d => d.BookingId == bookingId && d.Status != DemandStatuses.Cancelled)
            .OrderBy(d => d.RaisedOn).ThenBy(d => d.Id)
            .ToListAsync(ct);

        var receipts = await db.Receipts
            .IgnoreQueryFilters()
            .Where(r => r.BookingId == bookingId && r.Status == ReceiptStatuses.Cleared)
            .OrderBy(r => r.ReceivedOn).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var demandIds = demands.Select(d => d.Id).ToList();

        var allocations = await db.ReceiptAllocations
            .IgnoreQueryFilters()
            .Where(a => demandIds.Contains(a.DemandId))
            .ToListAsync(ct);

        var clearedReceiptIds = receipts.Select(r => r.Id).ToHashSet();

        /* ---------------- per demand ---------------- */

        foreach (var demand in demands)
        {
            var applied = allocations
                .Where(a => a.DemandId == demand.Id && clearedReceiptIds.Contains(a.ReceiptId))
                .ToList();

            demand.Received = applied.Sum(a => a.Amount);

            var interestPaid = applied.Sum(a => a.TowardsInterest);

            demand.InterestAccrued = InterestOn(demand, applied, receipts, today);

            // Interest that has been paid is not also owed — but it is not a
            // waiver either. Kept in its own column so "we collected it" and
            // "we forgave it" stay distinguishable; folding the first into the
            // second reported interest income as a concession and quietly
            // subtracted the same rupees from the balance twice.
            demand.InterestReceived = interestPaid;

            // A waiver cannot forgive more than is left after payment, and it
            // never grows on its own: it is a decision somebody recorded, so a
            // recompute clamps it rather than inventing more of it.
            demand.InterestWaived = Math.Min(
                demand.InterestWaived,
                Math.Max(0, demand.InterestAccrued - interestPaid));

            demand.Status =
                demand.Received >= demand.TotalAmount - 0.5m ? DemandStatuses.Paid
                : demand.DueDate.Date < today ? DemandStatuses.Overdue
                : demand.Received > 0 ? DemandStatuses.PartlyPaid
                : DemandStatuses.Raised;
        }

        /* ---------------- per milestone ---------------- */

        var milestones = await db.BookingMilestones
            .IgnoreQueryFilters()
            .Where(m => m.BookingId == bookingId)
            .ToListAsync(ct);

        foreach (var milestone in milestones)
        {
            if (milestone.Status == MilestoneStatuses.Waived) continue;

            var demand = demands.FirstOrDefault(d => d.BookingMilestoneId == milestone.Id);

            milestone.Status = demand is null
                ? MilestoneStatuses.Pending
                : demand.Status switch
                {
                    DemandStatuses.Paid => MilestoneStatuses.Paid,
                    DemandStatuses.PartlyPaid => MilestoneStatuses.PartlyPaid,
                    _ => MilestoneStatuses.Demanded,
                };

            milestone.DemandId = demand?.Id;
        }

        /* ---------------- per receipt ---------------- */

        foreach (var receipt in receipts)
        {
            var applied = allocations
                .Where(a => a.ReceiptId == receipt.Id)
                .Sum(a => a.Amount + a.TowardsInterest);

            receipt.Unallocated = Math.Max(0, receipt.CreditedAmount - applied);
        }

        /* ---------------- the booking's position ---------------- */

        var demanded = demands.Sum(d => d.TotalAmount);
        var received = receipts.Sum(r => r.CreditedAmount);
        var interestCharged = demands.Sum(d => d.InterestAccrued);
        var interestWaived = demands.Sum(d => d.InterestWaived);

        var overdue = demands
            .Where(d => d.Status == DemandStatuses.Overdue)
            .ToList();

        booking.Demanded = demanded;
        booking.Received = received;
        booking.InterestCharged = interestCharged;
        booking.InterestWaived = interestWaived;

        // Interest that has been waived is not owed; interest that has not been
        // is. Netting the two before subtracting receipts is what makes this
        // number match the letter the customer gets.
        booking.Outstanding = Math.Max(
            0, demanded + (interestCharged - interestWaived) - received);

        booking.OverdueDays = overdue.Count == 0
            ? 0
            : (int)(today - overdue.Min(d => d.DueDate).Date).TotalDays;

        await db.SaveChangesAsync(ct);

        return new LedgerSummary(
            booking.GrandTotal,
            demanded,
            received,
            interestCharged,
            interestWaived,
            booking.Outstanding,
            Math.Max(0, booking.GrandTotal - demanded),
            booking.OverdueDays,
            overdue.Sum(d => d.Outstanding + d.InterestDue),
            BuildLines(demands, receipts, allocations));
    }

    /// <summary>
    /// Penal interest on one demand, day by day against what was still unpaid.
    ///
    /// Computed over the balance as it changed rather than on the original
    /// amount, because a customer who paid 80% on time and the rest a month late
    /// owes interest on the 20%. Charging the whole instalment for the whole
    /// delay is the arithmetic that produces the angriest phone call in this
    /// business.
    /// </summary>
    private static decimal InterestOn(
        Demand demand,
        IReadOnlyList<ReceiptAllocation> applied,
        IReadOnlyList<Receipt> receipts,
        DateTime today)
    {
        if (demand.InterestRatePercent <= 0) return 0;

        var from = demand.DueDate.Date;
        if (from >= today) return 0;

        // When each payment landed against this demand, oldest first.
        var payments = applied
            .Select(a => new
            {
                On = receipts.FirstOrDefault(r => r.Id == a.ReceiptId)?.ReceivedOn.Date ?? today,
                a.Amount,
            })
            .OrderBy(p => p.On)
            .ToList();

        var balance = demand.TotalAmount;
        var cursor = from;
        var interest = 0m;
        var daily = demand.InterestRatePercent / 100m / 365m;

        foreach (var payment in payments)
        {
            var until = payment.On < from ? from : payment.On;
            if (until > today) break;

            var days = (until - cursor).Days;
            if (days > 0 && balance > 0) interest += balance * daily * days;

            balance = Math.Max(0, balance - payment.Amount);
            cursor = until;
        }

        var tail = (today - cursor).Days;
        if (tail > 0 && balance > 0) interest += balance * daily * tail;

        return Math.Round(interest, 2);
    }

    /// <summary>
    /// The statement, as a customer reads it: what was billed, what was paid,
    /// and the running balance after each.
    /// </summary>
    private static List<LedgerLine> BuildLines(
        IReadOnlyList<Demand> demands,
        IReadOnlyList<Receipt> receipts,
        IReadOnlyList<ReceiptAllocation> allocations)
    {
        var entries = new List<(DateTime On, int Order, string Kind, string Reference, string Description, decimal Debit, decimal Credit)>();

        foreach (var demand in demands)
        {
            entries.Add((demand.RaisedOn.Date, 0, "Demand", demand.DemandNumber,
                demand.Label, demand.TotalAmount, 0));

            // The gross accrual, not what is left of it. The waiver below is
            // credited in full, so debiting the net here would relieve the
            // customer of the same interest twice and walk the running balance
            // below what they actually owe.
            var accrued = demand.InterestAccrued;
            if (accrued > 0.5m)
            {
                entries.Add((DateTime.UtcNow.Date, 2, "Interest", demand.DemandNumber,
                    $"Interest on {demand.Label} at {demand.InterestRatePercent:0.##}%", accrued, 0));
            }

            var waived = demand.InterestWaived;
            if (waived > 0.5m)
            {
                entries.Add((DateTime.UtcNow.Date, 3, "Waiver", demand.DemandNumber,
                    $"Interest waived on {demand.Label}", 0, waived));
            }
        }

        foreach (var receipt in receipts)
        {
            var against = allocations
                .Where(a => a.ReceiptId == receipt.Id)
                .Select(a => demands.FirstOrDefault(d => d.Id == a.DemandId)?.Label)
                .Where(label => label is not null)
                .Distinct()
                .ToList();

            var description = against.Count > 0
                ? $"{receipt.Mode} against {string.Join(", ", against)}"
                : $"{receipt.Mode} — on account";

            if (receipt.TdsAmount > 0)
            {
                description += $" (incl. TDS {receipt.TdsAmount:N0})";
            }

            entries.Add((receipt.ReceivedOn.Date, 1, "Receipt",
                receipt.ReceiptNumber, description, 0, receipt.CreditedAmount));
        }

        var balance = 0m;

        return entries
            .OrderBy(e => e.On).ThenBy(e => e.Order)
            .Select(e =>
            {
                balance += e.Debit - e.Credit;
                return new LedgerLine(e.On, e.Kind, e.Reference, e.Description,
                    e.Debit, e.Credit, balance);
            })
            .ToList();
    }

    /// <summary>
    /// Applies a cleared receipt to the demands it answers, oldest first.
    ///
    /// Interest before principal on each demand, which is both the convention
    /// and the only order that terminates: crediting principal first would leave
    /// an interest balance that keeps accruing on a demand the customer believes
    /// they have settled.
    /// </summary>
    public async Task AllocateAsync(int receiptId, CancellationToken ct = default)
    {
        var receipt = await db.Receipts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(r => r.Id == receiptId, ct)
            ?? throw new InvalidOperationException($"Receipt {receiptId} does not exist.");

        if (receipt.Status != ReceiptStatuses.Cleared) return;

        var existing = await db.ReceiptAllocations
            .IgnoreQueryFilters()
            .Where(a => a.ReceiptId == receiptId)
            .ToListAsync(ct);

        db.ReceiptAllocations.RemoveRange(existing);

        var demands = await db.Demands
            .IgnoreQueryFilters()
            .Where(d => d.BookingId == receipt.BookingId && d.Status != DemandStatuses.Cancelled)
            .OrderBy(d => d.DueDate).ThenBy(d => d.Id)
            .ToListAsync(ct);

        // What every other receipt has already taken, so two receipts cannot
        // both claim the same instalment.
        var others = await db.ReceiptAllocations
            .IgnoreQueryFilters()
            .Where(a => a.ReceiptId != receiptId)
            .Where(a => demands.Select(d => d.Id).Contains(a.DemandId))
            .ToListAsync(ct);

        var remaining = receipt.CreditedAmount;

        foreach (var demand in demands)
        {
            if (remaining <= 0.5m) break;

            var taken = others.Where(a => a.DemandId == demand.Id).ToList();
            var principalLeft = Math.Max(0, demand.TotalAmount - taken.Sum(a => a.Amount));
            var interestLeft = Math.Max(0, demand.InterestDue - taken.Sum(a => a.TowardsInterest));

            if (principalLeft <= 0.5m && interestLeft <= 0.5m) continue;

            var toInterest = Math.Min(remaining, interestLeft);
            remaining -= toInterest;

            var toPrincipal = Math.Min(remaining, principalLeft);
            remaining -= toPrincipal;

            if (toInterest <= 0.005m && toPrincipal <= 0.005m) continue;

            db.ReceiptAllocations.Add(new ReceiptAllocation
            {
                CompanyId = receipt.CompanyId,
                ReceiptId = receipt.Id,
                DemandId = demand.Id,
                Amount = Math.Round(toPrincipal, 2),
                TowardsInterest = Math.Round(toInterest, 2),
            });
        }

        receipt.Unallocated = Math.Round(Math.Max(0, remaining), 2);

        await db.SaveChangesAsync(ct);
        await RecomputeAsync(receipt.BookingId, ct: ct);
    }
}
