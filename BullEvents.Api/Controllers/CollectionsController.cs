using System.ComponentModel.DataAnnotations;
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

public record DemandRowDto(
    int Id,
    string DemandNumber,
    int BookingId,
    string BookingNumber,
    string CustomerName,
    string? CustomerPhone,
    string ProjectName,
    string? TowerName,
    string UnitNumber,
    string Label,
    DateTime RaisedOn,
    DateTime DueDate,
    decimal TotalAmount,
    decimal Received,
    decimal Outstanding,
    decimal InterestDue,
    decimal InterestRatePercent,
    string Status,
    /// <summary>Days past due. Negative while it is still in hand.</summary>
    int AgeDays,
    /// <summary>Which ageing column it falls in: Current, 1-30, 31-60, 61-90, 90+.</summary>
    string Bucket);

public record ReceiptRowDto(
    int Id,
    string ReceiptNumber,
    int BookingId,
    string BookingNumber,
    string CustomerName,
    string UnitNumber,
    DateTime ReceivedOn,
    decimal Amount,
    decimal TdsAmount,
    decimal CreditedAmount,
    string Mode,
    string? Instrument,
    string? BankName,
    string Status,
    decimal Unallocated,
    DateTime? BouncedOn,
    string? BounceReason,
    IReadOnlyList<AllocationDto> Allocations);

public record AllocationDto(
    int DemandId, string DemandNumber, string Label, decimal Amount, decimal TowardsInterest);

public record LedgerLineDto(
    DateTime On, string Kind, string Reference, string Description,
    decimal Debit, decimal Credit, decimal Balance);

public record LedgerDto(
    int BookingId,
    string BookingNumber,
    string CustomerName,
    string UnitLabel,
    decimal GrandTotal,
    decimal Demanded,
    decimal Received,
    decimal InterestCharged,
    decimal InterestWaived,
    decimal Outstanding,
    decimal NotYetDemanded,
    int OverdueDays,
    decimal OverdueAmount,
    IReadOnlyList<LedgerLineDto> Lines);

public record RaiseDemandRequest(
    [Required] int MilestoneId, DateTime? DueDate, decimal? InterestRatePercent);

public record BulkRaiseRequest(
    [Required] string ConstructionStage,
    int? ProjectId,
    string? Tower,
    DateTime? DueDate,
    decimal? InterestRatePercent);

public record CreateReceiptRequest(
    [Required] int BookingId,
    DateTime? ReceivedOn,
    [Range(1, double.MaxValue)] decimal Amount,
    decimal TdsAmount,
    [Required] string Mode,
    string? Instrument,
    string? BankName,
    DateTime? InstrumentDate,
    string? Status,
    string? Notes);

public record BounceRequest([Required, MinLength(3)] string Reason);
public record ClearRequest(DateTime? ClearedOn);
public record WaiveInterestRequest(decimal? Amount, [Required, MinLength(3)] string Reason);

/// <summary>One ageing column, across whatever was filtered.</summary>
public record AgeingBucketDto(string Bucket, int Count, decimal Amount);

public record CollectionsSummaryDto(
    decimal TotalSold,
    decimal TotalDemanded,
    decimal TotalReceived,
    decimal TotalOutstanding,
    decimal InterestDue,
    /// <summary>Received over demanded. The number a collections head is measured on.</summary>
    double CollectionEfficiency,
    int LiveBookings,
    int OverdueBookings,
    decimal DueThisMonth,
    decimal ReceivedThisMonth,
    decimal BouncedThisMonth,
    IReadOnlyList<AgeingBucketDto> Ageing,
    /// <summary>What the plan bills over the next six months, by month.</summary>
    IReadOnlyList<ForecastPointDto> Forecast);

public record ForecastPointDto(string Month, decimal Scheduled, decimal Demanded);

public record EscrowSummaryDto(
    int? ProjectId,
    string ProjectName,
    decimal Collected,
    decimal Designated,
    decimal Free,
    decimal Transferred,
    decimal PendingTransfer);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The money desk: what has been billed, what has come in, and what is late.
///
/// Everything here reads through <see cref="BookingLedger"/> rather than
/// computing its own totals. The collections list, the ageing report, a
/// customer's statement and the booking row all have to agree, and letting each
/// do its own arithmetic is how they stop agreeing.
/// </summary>
[ApiController]
[Route("api/collections")]
[Authorize]
[SecuredBy(SecuredObjects.Booking)]
[RequireModule(Modules.PostSales)]
public class CollectionsController(
    AppDbContext db,
    BookingService bookings,
    BookingPlanService plans,
    BookingLedger ledger) : CrmControllerBase(db)
{
    /* ------------------------------------------------------------------ *
     * Demands
     * ------------------------------------------------------------------ */

    [HttpGet("demands")]
    public async Task<ActionResult<IReadOnlyList<DemandRowDto>>> Demands(
        [FromQuery] string? status = null,
        [FromQuery] string? bucket = null,
        [FromQuery] int? projectId = null,
        [FromQuery] string? tower = null,
        [FromQuery] int? bookingId = null,
        CancellationToken ct = default)
    {
        var query = Db.Demands
            .Include(d => d.Booking!).ThenInclude(b => b.Applicants)
            .Where(d => d.Status != DemandStatuses.Cancelled);

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(d => d.Status == status);
        if (bookingId is int id) query = query.Where(d => d.BookingId == id);
        if (projectId is int project) query = query.Where(d => d.Booking!.ProjectId == project);
        if (!string.IsNullOrWhiteSpace(tower)) query = query.Where(d => d.Booking!.TowerName == tower);

        var rows = await query
            .OrderBy(d => d.DueDate)
            .Take(1000)
            .ToListAsync(ct);

        var today = DateTime.UtcNow.Date;

        var mapped = rows.Select(d => ToDto(d, today)).ToList();

        if (!string.IsNullOrWhiteSpace(bucket))
        {
            mapped = mapped.Where(d => d.Bucket == bucket).ToList();
        }

        return Ok(mapped);
    }

    /// <summary>Raises the demand for one instalment.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("bookings/{bookingId:int}/demands")]
    public async Task<ActionResult<DemandRowDto>> Raise(
        int bookingId, RaiseDemandRequest request, CancellationToken ct)
    {
        var demand = await bookings.RaiseAsync(
            bookingId, request.MilestoneId, request.DueDate, request.InterestRatePercent, ct);

        var loaded = await Db.Demands
            .Include(d => d.Booking!).ThenInclude(b => b.Applicants)
            .FirstAsync(d => d.Id == demand.Id, ct);

        return Ok(ToDto(loaded, DateTime.UtcNow.Date));
    }

    /// <summary>
    /// Raises every instalment tied to a construction stage, across a tower or a
    /// whole project.
    ///
    /// The operation post-sales actually runs. A slab is cast and forty demands
    /// fall due the same morning; doing that one booking at a time is an
    /// afternoon of clicking and a guarantee two get missed.
    /// </summary>
    [PermissionAction(ObjectAction.ModifyAll)]
    [HttpPost("demands/bulk")]
    public async Task<ActionResult<BulkDemandResult>> BulkRaise(
        BulkRaiseRequest request, CancellationToken ct)
    {
        var result = await bookings.RaiseByStageAsync(
            request.ConstructionStage, request.ProjectId, request.Tower,
            request.DueDate, request.InterestRatePercent, ct);

        return Ok(result);
    }

    /// <summary>Writes penal interest off on one demand, with the reason recorded.</summary>
    [PermissionAction(ObjectAction.ModifyAll)]
    [HttpPost("demands/{demandId:int}/waive-interest")]
    public async Task<ActionResult<DemandRowDto>> WaiveInterest(
        int demandId, WaiveInterestRequest request, CancellationToken ct)
    {
        await plans.WaiveInterestAsync(demandId, request.Amount, request.Reason, ct);

        var loaded = await Db.Demands
            .Include(d => d.Booking!).ThenInclude(b => b.Applicants)
            .FirstAsync(d => d.Id == demandId, ct);

        return Ok(ToDto(loaded, DateTime.UtcNow.Date));
    }

    /* ------------------------------------------------------------------ *
     * Receipts
     * ------------------------------------------------------------------ */

    [HttpGet("receipts")]
    public async Task<ActionResult<IReadOnlyList<ReceiptRowDto>>> Receipts(
        [FromQuery] int? bookingId = null,
        [FromQuery] string? status = null,
        [FromQuery] DateTime? since = null,
        CancellationToken ct = default)
    {
        var query = Db.Receipts
            .Include(r => r.Booking!).ThenInclude(b => b.Applicants)
            .Include(r => r.Allocations)
            .AsQueryable();

        if (bookingId is int id) query = query.Where(r => r.BookingId == id);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);
        if (since is not null) query = query.Where(r => r.ReceivedOn >= since);

        var rows = await query
            .OrderByDescending(r => r.ReceivedOn).ThenByDescending(r => r.Id)
            .Take(1000)
            .ToListAsync(ct);

        return Ok(await ProjectAsync(rows, ct));
    }

    /// <summary>
    /// Turns receipt rows into the shape the client reads, resolving what each
    /// allocation answered.
    ///
    /// Shared rather than called through the action: reading <c>.Value</c> off
    /// an <c>ActionResult</c> that <c>Ok()</c> produced returns null every time,
    /// because the payload lives in <c>.Result</c>. Taking a receipt used to
    /// throw on exactly that.
    /// </summary>
    private async Task<List<ReceiptRowDto>> ProjectAsync(
        IReadOnlyList<Receipt> rows, CancellationToken ct)
    {
        var demandIds = rows.SelectMany(r => r.Allocations).Select(a => a.DemandId).Distinct().ToList();

        var demands = await Db.Demands
            .Where(d => demandIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => new { d.DemandNumber, d.Label }, ct);

        return rows.Select(r => new ReceiptRowDto(
            r.Id, r.ReceiptNumber, r.BookingId,
            r.Booking?.BookingNumber ?? "—",
            Primary(r.Booking),
            r.Booking?.UnitNumber ?? "—",
            r.ReceivedOn, r.Amount, r.TdsAmount, r.CreditedAmount,
            r.Mode, r.Instrument, r.BankName, r.Status, r.Unallocated,
            r.BouncedOn, r.BounceReason,
            r.Allocations.Select(a => new AllocationDto(
                a.DemandId,
                demands.GetValueOrDefault(a.DemandId)?.DemandNumber ?? "—",
                demands.GetValueOrDefault(a.DemandId)?.Label ?? "—",
                a.Amount, a.TowardsInterest)).ToList()))
            .ToList();
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("receipts")]
    public async Task<ActionResult<ReceiptRowDto>> Receive(
        CreateReceiptRequest request, CancellationToken ct)
    {
        var booking = await Db.Bookings.FirstOrDefaultAsync(b => b.Id == request.BookingId, ct)
            ?? throw ApiException.NotFound("Booking");

        if (booking.Status == BookingStatuses.Cancelled)
        {
            throw ApiException.Conflict("This booking is cancelled. Record a refund instead.");
        }

        var receipt = new Receipt
        {
            BookingId = booking.Id,
            ReceivedOn = (request.ReceivedOn ?? DateTime.UtcNow).Date,
            Amount = request.Amount,
            TdsAmount = Math.Max(0, request.TdsAmount),
            Mode = Require(request.Mode, PaymentModes.All, "payment mode"),
            Instrument = request.Instrument,
            BankName = request.BankName,
            InstrumentDate = request.InstrumentDate,
            Notes = request.Notes,

            // A cheque is pending until it clears; anything electronic is money
            // already in. Defaulting a cheque to cleared is how an ageing report
            // starts lying a week before anybody notices.
            Status = request.Status is not null
                ? Require(request.Status, ReceiptStatuses.All, "status")
                : request.Mode is PaymentModes.Cheque or PaymentModes.DemandDraft
                    ? ReceiptStatuses.Pending
                    : ReceiptStatuses.Cleared,
        };

        await bookings.ReceiveAsync(receipt, ct);

        var loaded = await Db.Receipts
            .Include(r => r.Booking!).ThenInclude(b => b.Applicants)
            .Include(r => r.Allocations)
            .AsNoTracking()
            .FirstAsync(r => r.Id == receipt.Id, ct);

        return Ok((await ProjectAsync([loaded], ct))[0]);
    }

    /// <summary>Marks a pending instrument cleared and applies it to the oldest demands.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("receipts/{receiptId:int}/clear")]
    public async Task<IActionResult> Clear(int receiptId, ClearRequest request, CancellationToken ct)
    {
        await bookings.ClearAsync(receiptId, (request.ClearedOn ?? DateTime.UtcNow).Date, ct);
        return NoContent();
    }

    /// <summary>
    /// Marks a cheque returned.
    ///
    /// Its allocations go with it, so the demands it had settled become due
    /// again — and the interest the ledger recomputes covers the whole period,
    /// because the money was never there.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("receipts/{receiptId:int}/bounce")]
    public async Task<IActionResult> Bounce(int receiptId, BounceRequest request, CancellationToken ct)
    {
        await bookings.BounceAsync(receiptId, request.Reason, ct);
        return NoContent();
    }

    /* ------------------------------------------------------------------ *
     * Ledger
     * ------------------------------------------------------------------ */

    /// <summary>The customer's running account — what was billed, what was paid, and the balance after each.</summary>
    [HttpGet("bookings/{bookingId:int}/ledger")]
    public async Task<ActionResult<LedgerDto>> Ledger(int bookingId, CancellationToken ct)
    {
        var booking = await Db.Bookings
            .Include(b => b.Applicants)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw ApiException.NotFound("Booking");

        var summary = await ledger.RecomputeAsync(bookingId, ct: ct);

        return Ok(new LedgerDto(
            booking.Id, booking.BookingNumber, Primary(booking),
            $"{booking.ProjectName} · {booking.TowerName} {booking.UnitNumber}".Replace("  ", " "),
            summary.GrandTotal, summary.Demanded, summary.Received,
            summary.InterestCharged, summary.InterestWaived, summary.Outstanding,
            summary.NotYetDemanded, summary.OverdueDays, summary.OverdueAmount,
            summary.Lines
                .Select(l => new LedgerLineDto(
                    l.On, l.Kind, l.Reference, l.Description, l.Debit, l.Credit, l.Balance))
                .ToList()));
    }

    /* ------------------------------------------------------------------ *
     * The desk's own dashboard
     * ------------------------------------------------------------------ */

    [PermissionAction(ObjectAction.View)]
    [HttpGet("summary")]
    public async Task<ActionResult<CollectionsSummaryDto>> Summary(
        [FromQuery] int? projectId = null, CancellationToken ct = default)
    {
        var query = Db.Bookings.Where(b => b.Status != BookingStatuses.Cancelled);
        if (projectId is int project) query = query.Where(b => b.ProjectId == project);

        var live = await query.ToListAsync(ct);
        var ids = live.Select(b => b.Id).ToList();

        var today = DateTime.UtcNow.Date;
        var monthStart = new DateTime(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var demands = await Db.Demands
            .Where(d => ids.Contains(d.BookingId) && d.Status != DemandStatuses.Cancelled)
            .ToListAsync(ct);

        var receipts = await Db.Receipts
            .Where(r => ids.Contains(r.BookingId))
            .ToListAsync(ct);

        var cleared = receipts.Where(r => r.Status == ReceiptStatuses.Cleared).ToList();

        /* ---------------- ageing ---------------- */

        var open = demands
            .Where(d => d.Outstanding > 0.5m || d.InterestDue > 0.5m)
            .Select(d => new
            {
                Bucket = BucketFor((int)(today - d.DueDate.Date).TotalDays),
                Amount = d.Outstanding + d.InterestDue,
            })
            .ToList();

        var ageing = AgeingBuckets
            .Select(bucket => new AgeingBucketDto(
                bucket,
                open.Count(o => o.Bucket == bucket),
                open.Where(o => o.Bucket == bucket).Sum(o => o.Amount)))
            .ToList();

        /* ---------------- what the plan bills next ---------------- */

        var milestones = await Db.BookingMilestones
            .Where(m => ids.Contains(m.BookingId))
            .Where(m => m.Status == MilestoneStatuses.Pending && m.DueDate != null)
            .ToListAsync(ct);

        var forecast = Enumerable.Range(0, 6)
            .Select(offset =>
            {
                var from = monthStart.AddMonths(offset);
                var to = from.AddMonths(1);

                return new ForecastPointDto(
                    from.ToString("MMM yyyy"),
                    milestones
                        .Where(m => m.DueDate >= from && m.DueDate < to)
                        .Sum(m => m.TotalAmount),
                    demands
                        .Where(d => d.DueDate >= from && d.DueDate < to)
                        .Sum(d => d.Outstanding));
            })
            .ToList();

        var demanded = demands.Sum(d => d.TotalAmount);
        var received = cleared.Sum(r => r.CreditedAmount);

        return Ok(new CollectionsSummaryDto(
            live.Sum(b => b.GrandTotal),
            demanded,
            received,
            live.Sum(b => b.Outstanding),
            demands.Sum(d => d.InterestDue),

            // Received over demanded, not over the sale value: a project three
            // months old has billed 20% and collected all of it, and calling
            // that 20% efficiency would be measuring construction progress.
            demanded <= 0 ? 1 : (double)(received / demanded),

            live.Count,
            live.Count(b => b.OverdueDays > 0),
            demands.Where(d => d.DueDate >= monthStart && d.DueDate < monthEnd).Sum(d => d.Outstanding),
            cleared.Where(r => r.ReceivedOn >= monthStart && r.ReceivedOn < monthEnd).Sum(r => r.CreditedAmount),
            receipts
                .Where(r => r.Status == ReceiptStatuses.Bounced
                            && r.BouncedOn >= monthStart && r.BouncedOn < monthEnd)
                .Sum(r => r.Amount),
            ageing,
            forecast));
    }

    /// <summary>
    /// The seventy-per-cent position, per project.
    ///
    /// What RERA obliges a developer to hold back and what has actually been
    /// moved. Recorded as receipts arrive, so this is a read rather than a
    /// reconstruction from a year of bank statements.
    /// </summary>
    [PermissionAction(ObjectAction.ViewAll)]
    [HttpGet("escrow")]
    public async Task<ActionResult<IReadOnlyList<EscrowSummaryDto>>> Escrow(
        [FromQuery] DateTime? since = null, CancellationToken ct = default)
    {
        var query = Db.EscrowEntries.AsQueryable();
        if (since is not null) query = query.Where(e => e.On >= since);

        var grouped = await query
            .GroupBy(e => e.ProjectId)
            .Select(g => new
            {
                ProjectId = g.Key,
                Collected = g.Sum(e => e.ReceiptAmount),
                Designated = g.Sum(e => e.DesignatedAmount),
                Free = g.Sum(e => e.FreeAmount),
                Transferred = g.Where(e => e.Transferred).Sum(e => e.DesignatedAmount),
            })
            .ToListAsync(ct);

        var names = await Db.Projects
            .Select(p => new { p.Id, p.Name })
            .ToDictionaryAsync(p => p.Id, p => p.Name, ct);

        return Ok(grouped
            .Select(g => new EscrowSummaryDto(
                g.ProjectId,
                g.ProjectId is int id ? names.GetValueOrDefault(id, "Unknown project") : "Unassigned",
                g.Collected, g.Designated, g.Free, g.Transferred,
                Math.Max(0, g.Designated - g.Transferred)))
            .OrderByDescending(g => g.Collected)
            .ToList());
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The ageing columns, in the order every collections report in this
    /// industry prints them.
    /// </summary>
    private static readonly string[] AgeingBuckets =
        ["Current", "1-30", "31-60", "61-90", "90+"];

    private static string BucketFor(int daysPastDue) => daysPastDue switch
    {
        <= 0 => "Current",
        <= 30 => "1-30",
        <= 60 => "31-60",
        <= 90 => "61-90",
        _ => "90+",
    };

    private static DemandRowDto ToDto(Demand d, DateTime today)
    {
        var age = (int)(today - d.DueDate.Date).TotalDays;

        return new DemandRowDto(
            d.Id, d.DemandNumber, d.BookingId,
            d.Booking?.BookingNumber ?? "—",
            Primary(d.Booking),
            d.Booking?.Applicants
                .OrderBy(a => a.Role == ApplicantRoles.Primary ? 0 : 1)
                .FirstOrDefault()?.Phone,
            d.Booking?.ProjectName ?? "—",
            d.Booking?.TowerName,
            d.Booking?.UnitNumber ?? "—",
            d.Label, d.RaisedOn, d.DueDate,
            d.TotalAmount, d.Received, d.Outstanding, d.InterestDue, d.InterestRatePercent,
            d.Status, age,
            d.Outstanding <= 0.5m && d.InterestDue <= 0.5m ? "Current" : BucketFor(age));
    }

    private static string Primary(Booking? booking) =>
        booking?.Applicants
            .OrderBy(a => a.Role == ApplicantRoles.Primary ? 0 : 1)
            .ThenBy(a => a.SortOrder)
            .FirstOrDefault()?.Name ?? "—";

    private static string Require(string value, string[] allowed, string what) =>
        allowed.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase))
        ?? throw ApiException.BadRequest($"'{value}' is not a valid {what}.");
}
