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

public record AgreementDto(
    int BookingId,
    string Status,
    string StatusLabel,
    DateTime? AllotmentLetterOn,
    DateTime? DraftSharedOn,
    DateTime? FrankedOn,
    DateTime? ExecutedOn,
    DateTime? RegisteredOn,
    decimal ConsiderationValue,
    decimal StampDuty,
    decimal RegistrationFee,
    string? RegistrationNumber,
    string? SubRegistrarOffice,
    string? Notes,
    /// <summary>The rung this can move to next, or null when it is registered.</summary>
    string? NextStatus);

public record AdvanceAgreementRequest(
    [Required] string ToStatus,
    DateTime? On,
    string? RegistrationNumber,
    string? SubRegistrarOffice,
    decimal? StampDuty,
    decimal? RegistrationFee);

public record HomeLoanDto(
    int BookingId,
    string BankName,
    string? BranchName,
    string? ApplicationNumber,
    DateTime? AppliedOn,
    decimal RequestedAmount,
    decimal SanctionedAmount,
    DateTime? SanctionedOn,
    DateTime? SanctionValidUntil,
    DateTime? TripartiteSignedOn,
    decimal DisbursedAmount,
    decimal UndisbursedAmount,
    string Status,
    string StatusLabel,
    /// <summary>A sanction letter past its date stalls every further release.</summary>
    bool SanctionLapsed);

public record SaveLoanRequest(
    [Required] string BankName,
    string? BranchName,
    string? ApplicationNumber,
    decimal RequestedAmount);

public record SanctionRequest(decimal SanctionedAmount, DateTime? SanctionedOn, DateTime? ValidUntil);
public record DisburseRequest(decimal Amount, DateTime? On, string? Reference);

public record PossessionDto(
    int BookingId,
    DateTime? CommittedOn,
    DateTime? OfferedOn,
    DateTime? InspectedOn,
    int SnagsRaised,
    int SnagsClosed,
    int SnagsOpen,
    int MaintenanceAdvanceMonths,
    decimal MaintenanceAmount,
    bool MaintenanceCollected,
    decimal CorpusDeposit,
    bool CorpusCollected,
    bool DuesCleared,
    bool DocumentsHandedOver,
    DateTime? HandedOverOn,
    string Status,
    string StatusLabel,
    bool ReadyToHandOver,
    /// <summary>Everything still standing between here and the keys.</summary>
    IReadOnlyList<string> Blockers);

public record OfferPossessionRequest(
    DateTime? On, decimal MaintenanceAmount, int MaintenanceMonths, decimal CorpusDeposit);

public record InspectRequest(DateTime? On, int SnagsRaised);
public record CloseSnagsRequest(int Closed);
public record CollectRequest(bool Maintenance, bool Corpus);
public record HandOverRequest(DateTime? On, bool DocumentsHandedOver);

public record CancellationDto(
    int Id,
    int BookingId,
    DateTime RequestedOn,
    string Reason,
    decimal AmountReceived,
    decimal DeductionPercent,
    decimal DeductionAmount,
    decimal BrokerageRecovered,
    decimal OtherDeductions,
    decimal RefundAmount,
    DateTime? ApprovedOn,
    DateTime? RefundedOn,
    string? RefundReference,
    string Status);

public record RequestCancellationRequest(
    [Required, MinLength(3)] string Reason,
    decimal DeductionPercent,
    decimal OtherDeductions);

public record RefundRequest(DateTime? On, [Required] string Reference);

public record TransferDto(
    int Id,
    int BookingId,
    DateTime RequestedOn,
    string FromName,
    string ToName,
    string? ToPhone,
    string? ToEmail,
    string? ToPan,
    decimal TransferChargePercent,
    decimal TransferChargeAmount,
    decimal TransferChargeReceived,
    DateTime? ApprovedOn,
    DateTime? CompletedOn,
    string Status);

public record RequestTransferRequest(
    [Required, MinLength(2)] string ToName,
    string? ToPhone,
    string? ToEmail,
    string? ToPan,
    decimal TransferChargePercent);

public record TransferPaymentRequest(decimal Amount);

/// <summary>Everything hanging off one booking, for the record page's tabs.</summary>
public record LifecycleDto(
    AgreementDto? Agreement,
    HomeLoanDto? Loan,
    PossessionDto? Possession,
    IReadOnlyList<CancellationDto> Cancellations,
    IReadOnlyList<TransferDto> Transfers);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The file that follows a unit after it is sold: the agreement, the bank, the
/// keys, and the two ways a booking can end early.
///
/// Every endpoint here is a transition with a rule behind it rather than a
/// field to edit. "Registered on" is not a date somebody types — it is what
/// happens after franking, and an API that let the two be set in any order
/// would record a history that never occurred.
/// </summary>
[ApiController]
[Route("api/bookings/{bookingId:int}")]
[Authorize]
[SecuredBy(SecuredObjects.Booking)]
[RequireModule(Modules.PostSales)]
public class LifecycleController(
    AppDbContext db,
    BookingLifecycleService lifecycle,
    BookingService bookings) : CrmControllerBase(db)
{
    /* ---------------- the whole file ---------------- */

    [HttpGet("lifecycle")]
    public async Task<ActionResult<LifecycleDto>> Lifecycle(int bookingId, CancellationToken ct)
    {
        var booking = await Db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw ApiException.NotFound("Booking");

        var agreement = await Db.BookingAgreements
            .FirstOrDefaultAsync(a => a.BookingId == bookingId, ct);

        var loan = await Db.HomeLoans.FirstOrDefaultAsync(l => l.BookingId == bookingId, ct);
        var possession = await Db.Possessions.FirstOrDefaultAsync(p => p.BookingId == bookingId, ct);

        var cancellations = await Db.BookingCancellations
            .Where(c => c.BookingId == bookingId)
            .OrderByDescending(c => c.RequestedOn)
            .ToListAsync(ct);

        var transfers = await Db.BookingTransfers
            .Where(t => t.BookingId == bookingId)
            .OrderByDescending(t => t.RequestedOn)
            .ToListAsync(ct);

        return Ok(new LifecycleDto(
            agreement is null ? null : ToDto(agreement),
            loan is null ? null : ToDto(loan),
            possession is null ? null : ToDto(possession, booking),
            cancellations.Select(ToDto).ToList(),
            transfers.Select(ToDto).ToList()));
    }

    /* ---------------- agreement ---------------- */

    [HttpPost("agreement/advance")]
    public async Task<ActionResult<AgreementDto>> AdvanceAgreement(
        int bookingId, AdvanceAgreementRequest input, CancellationToken ct)
        => Ok(ToDto(await lifecycle.AdvanceAgreementAsync(
            bookingId, input.ToStatus, input.On,
            input.RegistrationNumber, input.SubRegistrarOffice,
            input.StampDuty, input.RegistrationFee, ct)));

    /* ---------------- home loan ---------------- */

    [HttpPut("loan")]
    public async Task<ActionResult<HomeLoanDto>> SaveLoan(
        int bookingId, SaveLoanRequest input, CancellationToken ct)
        => Ok(ToDto(await lifecycle.SaveLoanAsync(
            bookingId, input.BankName, input.BranchName,
            input.ApplicationNumber, input.RequestedAmount, ct)));

    [HttpPost("loan/sanction")]
    public async Task<ActionResult<HomeLoanDto>> Sanction(
        int bookingId, SanctionRequest input, CancellationToken ct)
        => Ok(ToDto(await lifecycle.SanctionAsync(
            bookingId, input.SanctionedAmount, input.SanctionedOn, input.ValidUntil, ct)));

    [HttpPost("loan/tripartite")]
    public async Task<ActionResult<HomeLoanDto>> Tripartite(
        int bookingId, [FromBody] DateTime? on, CancellationToken ct)
        => Ok(ToDto(await lifecycle.TripartiteAsync(bookingId, on, ct)));

    [HttpPost("loan/disburse")]
    public async Task<ActionResult<HomeLoanDto>> Disburse(
        int bookingId, DisburseRequest input, CancellationToken ct)
        => Ok(ToDto(await lifecycle.DisburseAsync(
            bookingId, input.Amount, input.On, input.Reference, bookings, ct)));

    /* ---------------- possession ---------------- */

    [HttpPost("possession/offer")]
    public async Task<ActionResult<PossessionDto>> OfferPossession(
        int bookingId, OfferPossessionRequest input, CancellationToken ct)
    {
        var possession = await lifecycle.OfferPossessionAsync(
            bookingId, input.On, input.MaintenanceAmount,
            input.MaintenanceMonths, input.CorpusDeposit, ct);

        return Ok(await WithBookingAsync(possession, ct));
    }

    [HttpPost("possession/inspect")]
    public async Task<ActionResult<PossessionDto>> Inspect(
        int bookingId, InspectRequest input, CancellationToken ct)
        => Ok(await WithBookingAsync(
            await lifecycle.InspectAsync(bookingId, input.On, input.SnagsRaised, ct), ct));

    [HttpPost("possession/snags")]
    public async Task<ActionResult<PossessionDto>> CloseSnags(
        int bookingId, CloseSnagsRequest input, CancellationToken ct)
        => Ok(await WithBookingAsync(
            await lifecycle.CloseSnagsAsync(bookingId, input.Closed, ct), ct));

    [HttpPost("possession/collect")]
    public async Task<ActionResult<PossessionDto>> Collect(
        int bookingId, CollectRequest input, CancellationToken ct)
        => Ok(await WithBookingAsync(
            await lifecycle.CollectAsync(bookingId, input.Maintenance, input.Corpus, ct), ct));

    [HttpPost("possession/handover")]
    public async Task<ActionResult<PossessionDto>> HandOver(
        int bookingId, HandOverRequest input, CancellationToken ct)
        => Ok(await WithBookingAsync(
            await lifecycle.HandOverAsync(bookingId, input.On, input.DocumentsHandedOver, ct), ct));

    /* ---------------- cancellation ---------------- */

    [HttpPost("cancellations")]
    public async Task<ActionResult<CancellationDto>> RequestCancellation(
        int bookingId, RequestCancellationRequest input, CancellationToken ct)
        => Ok(ToDto(await lifecycle.RequestCancellationAsync(
            bookingId, input.Reason, input.DeductionPercent, input.OtherDeductions, ct)));

    [HttpPost("cancellations/{id:int}/approve")]
    public async Task<ActionResult<CancellationDto>> ApproveCancellation(
        int bookingId, int id, CancellationToken ct)
        => Ok(ToDto(await lifecycle.ApproveCancellationAsync(id, ct)));

    [HttpPost("cancellations/{id:int}/refund")]
    public async Task<ActionResult<CancellationDto>> Refund(
        int bookingId, int id, RefundRequest input, CancellationToken ct)
        => Ok(ToDto(await lifecycle.RefundAsync(id, input.On, input.Reference, ct)));

    /* ---------------- transfer ---------------- */

    [HttpPost("transfers")]
    public async Task<ActionResult<TransferDto>> RequestTransfer(
        int bookingId, RequestTransferRequest input, CancellationToken ct)
        => Ok(ToDto(await lifecycle.RequestTransferAsync(
            bookingId, input.ToName, input.ToPhone, input.ToEmail,
            input.ToPan, input.TransferChargePercent, ct)));

    /// <summary>Records the transfer charge coming in, which is what unlocks completion.</summary>
    [HttpPost("transfers/{id:int}/payment")]
    public async Task<ActionResult<TransferDto>> TransferPayment(
        int bookingId, int id, TransferPaymentRequest input, CancellationToken ct)
    {
        var transfer = await Db.BookingTransfers.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Transfer");

        transfer.TransferChargeReceived += input.Amount;
        transfer.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);

        return Ok(ToDto(transfer));
    }

    [HttpPost("transfers/{id:int}/complete")]
    public async Task<ActionResult<TransferDto>> CompleteTransfer(
        int bookingId, int id, CancellationToken ct)
        => Ok(ToDto(await lifecycle.CompleteTransferAsync(id, ct)));

    /* ---------------- projections ---------------- */

    private async Task<PossessionDto> WithBookingAsync(Possession possession, CancellationToken ct)
    {
        var booking = await Db.Bookings.FirstAsync(b => b.Id == possession.BookingId, ct);
        return ToDto(possession, booking);
    }

    private static AgreementDto ToDto(BookingAgreement a)
    {
        var order = AgreementStatuses.All;
        var index = Array.IndexOf(order, a.Status);

        return new AgreementDto(
            a.BookingId, a.Status, AgreementStatuses.Label(a.Status),
            a.AllotmentLetterOn, a.DraftSharedOn, a.FrankedOn, a.ExecutedOn, a.RegisteredOn,
            a.ConsiderationValue, a.StampDuty, a.RegistrationFee,
            a.RegistrationNumber, a.SubRegistrarOffice, a.Notes,
            index >= 0 && index < order.Length - 1 ? order[index + 1] : null);
    }

    private static HomeLoanDto ToDto(HomeLoan l) => new(
        l.BookingId, l.BankName, l.BranchName, l.ApplicationNumber, l.AppliedOn,
        l.RequestedAmount, l.SanctionedAmount, l.SanctionedOn, l.SanctionValidUntil,
        l.TripartiteSignedOn, l.DisbursedAmount, l.UndisbursedAmount,
        l.Status, LoanStatuses.Label(l.Status),

        // A sanction that has lapsed with money still to come is the reason a
        // bank-funded instalment stops arriving, and nobody looks for it.
        l.SanctionValidUntil is DateTime until
            && until < DateTime.UtcNow
            && l.UndisbursedAmount > 0.5m);

    private static PossessionDto ToDto(Possession p, Booking booking)
    {
        var blockers = new List<string>();

        if (p.OfferedOn is null) blockers.Add("Possession has not been offered");
        if (p.SnagsOpen > 0) blockers.Add($"{p.SnagsOpen} snags still open");
        if (booking.Outstanding > 0.5m) blockers.Add($"{booking.Outstanding:N0} still outstanding");
        if (!p.MaintenanceCollected && p.MaintenanceAmount > 0)
            blockers.Add("Maintenance advance not collected");
        if (!p.CorpusCollected && p.CorpusDeposit > 0)
            blockers.Add("Corpus deposit not collected");

        return new PossessionDto(
            p.BookingId, p.CommittedOn, p.OfferedOn, p.InspectedOn,
            p.SnagsRaised, p.SnagsClosed, p.SnagsOpen,
            p.MaintenanceAdvanceMonths, p.MaintenanceAmount, p.MaintenanceCollected,
            p.CorpusDeposit, p.CorpusCollected,
            booking.Outstanding <= 0.5m, p.DocumentsHandedOver, p.HandedOverOn,
            p.Status, PossessionStatuses.Label(p.Status),
            blockers.Count == 0 && p.Status != PossessionStatuses.HandedOver,
            blockers);
    }

    private static CancellationDto ToDto(BookingCancellation c) => new(
        c.Id, c.BookingId, c.RequestedOn, c.Reason,
        c.AmountReceived, c.DeductionPercent, c.DeductionAmount,
        c.BrokerageRecovered, c.OtherDeductions, c.RefundAmount,
        c.ApprovedOn, c.RefundedOn, c.RefundReference, c.Status);

    private static TransferDto ToDto(BookingTransfer t) => new(
        t.Id, t.BookingId, t.RequestedOn, t.FromName, t.ToName,
        t.ToPhone, t.ToEmail, t.ToPan,
        t.TransferChargePercent, t.TransferChargeAmount, t.TransferChargeReceived,
        t.ApprovedOn, t.CompletedOn, t.Status);
}
