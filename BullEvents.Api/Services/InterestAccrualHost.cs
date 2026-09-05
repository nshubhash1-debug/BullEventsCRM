using BullEvents.Api.Data;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// Brings every overdue booking's penal interest up to today.
///
/// <see cref="BookingLedger.RecomputeAsync"/> only runs when money moves, and
/// interest is the one figure that changes when nothing happens: a demand that
/// goes unpaid for six weeks accrues on all forty-two days, but between the day
/// it was raised and the day somebody finally pays there is no event to
/// recompute it. Without this pass the ageing report, the collections desk and
/// the customer's own statement all quietly understate what is owed, and the
/// error grows by a day every day.
///
/// It recomputes rather than accumulates, exactly as the event path does, so
/// running it twice in one day is harmless and a missed night simply lands the
/// same numbers the next morning.
/// </summary>
public class InterestAccrual(
    AppDbContext db,
    BookingLedger ledger,
    ILogger<InterestAccrual> logger)
{
    public async Task<int> RunAsync(CancellationToken ct = default)
    {
        var today = DateTime.UtcNow.Date;

        // Filters off: this walks every tenant, and outside a request there is
        // no ambient company to scope to.
        //
        // Only bookings carrying a demand that is past due with a balance and a
        // rate — everything else would recompute to the numbers already stored.
        var bookingIds = await db.Demands
            .IgnoreQueryFilters()
            .Where(d => d.Status != DemandStatuses.Cancelled
                && d.DueDate < today
                && d.InterestRatePercent > 0
                && d.Received < d.TotalAmount)
            .Select(d => d.BookingId)
            .Distinct()
            .ToListAsync(ct);

        if (bookingIds.Count == 0) return 0;

        var accrued = 0;

        foreach (var bookingId in bookingIds)
        {
            try
            {
                await ledger.RecomputeAsync(bookingId, today, ct);
                accrued++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception error)
            {
                // One booking with bad data must not stop the rest of the book
                // from accruing. The next pass tries it again.
                logger.LogError(error,
                    "Interest accrual failed for booking {BookingId}; continuing.",
                    bookingId);
            }
        }

        logger.LogInformation(
            "Accrued penal interest on {Count} of {Total} overdue bookings.",
            accrued, bookingIds.Count);

        return accrued;
    }
}

/// <summary>
/// Runs the accrual pass a few times a day.
///
/// More often than daily on purpose: the desk opens a booking expecting today's
/// interest, and an hourly-ish cadence costs one small query per pass when
/// nothing is overdue.
/// </summary>
public class InterestAccrualHost(IServiceProvider services, ILogger<InterestAccrualHost> logger)
    : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(4);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // A short delay so the first pass does not compete with migrations and
        // seeding for the same connection pool.
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = services.CreateScope();
                var accrual = scope.ServiceProvider.GetRequiredService<InterestAccrual>();
                await accrual.RunAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception error)
            {
                // A failed pass is not worth taking the host down for: the next
                // one recomputes the same numbers from scratch.
                logger.LogError(error, "Interest accrual pass failed; will retry.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
