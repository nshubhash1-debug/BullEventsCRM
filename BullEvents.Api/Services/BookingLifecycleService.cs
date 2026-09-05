using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// The file that follows a unit from allotment to keys.
///
/// Everything here is a transition with a rule attached, not a field somebody
/// edits. That distinction is the whole point: "registered on" is not a date an
/// executive types, it is what happens after franking, and a system that lets
/// the two be set in any order records a history that never occurred.
///
/// The booking's own status is derived from these transitions rather than kept
/// in step by hand, so the record page cannot disagree with the file.
/// </summary>
public class BookingLifecycleService(AppDbContext db, TenantContext tenant, BookingLedger ledger)
{
    /* ------------------------------------------------------------------ *
     * Agreement and registration
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Moves the agreement one rung up the ladder.
    ///
    /// The ladder is strictly ordered — drafted, with customer, franked,
    /// executed, registered — and a step cannot be skipped. A registration
    /// recorded against an unexecuted agreement is a data-entry slip that later
    /// reads as a legal claim.
    /// </summary>
    public async Task<BookingAgreement> AdvanceAgreementAsync(
        int bookingId,
        string toStatus,
        DateTime? on = null,
        string? registrationNumber = null,
        string? subRegistrarOffice = null,
        decimal? stampDuty = null,
        decimal? registrationFee = null,
        CancellationToken ct = default)
    {
        var booking = await RequireBookingAsync(bookingId, ct);

        var agreement = await db.BookingAgreements
            .FirstOrDefaultAsync(a => a.BookingId == bookingId, ct);

        if (agreement is null)
        {
            agreement = new BookingAgreement
            {
                CompanyId = tenant.CompanyId,
                BookingId = bookingId,
                ConsiderationValue = booking.AgreementValue,
            };

            db.BookingAgreements.Add(agreement);
        }

        var order = AgreementStatuses.All.ToList();
        var current = order.IndexOf(agreement.Status);
        var target = order.IndexOf(toStatus);

        if (target < 0) throw ApiException.BadRequest($"'{toStatus}' is not an agreement stage.");

        if (target <= current)
        {
            throw ApiException.BadRequest(
                $"The agreement is already {AgreementStatuses.Label(agreement.Status).ToLowerInvariant()}.");
        }

        if (target > current + 1)
        {
            throw ApiException.BadRequest(
                $"Record {AgreementStatuses.Label(order[current + 1]).ToLowerInvariant()} first.");
        }

        var when = (on ?? DateTime.UtcNow).Date;

        switch (toStatus)
        {
            case AgreementStatuses.Drafted:
                agreement.DraftSharedOn = when;

                // Snapshotted at drafting rather than read live, so a later edit
                // to the booking cannot restate what a registered deed says.
                agreement.ConsiderationValue = booking.AgreementValue;
                break;

            case AgreementStatuses.WithCustomer:
                agreement.AllotmentLetterOn ??= when;
                break;

            case AgreementStatuses.Franked:
                if (stampDuty is not decimal duty || duty <= 0)
                {
                    throw ApiException.BadRequest("Franking needs the stamp duty that was paid.");
                }

                agreement.FrankedOn = when;
                agreement.StampDuty = duty;
                break;

            case AgreementStatuses.Executed:
                agreement.ExecutedOn = when;
                break;

            case AgreementStatuses.Registered:
                if (string.IsNullOrWhiteSpace(registrationNumber))
                {
                    throw ApiException.BadRequest(
                        "Registration needs the number the sub-registrar issued.");
                }

                agreement.RegisteredOn = when;
                agreement.RegistrationNumber = registrationNumber.Trim();
                agreement.SubRegistrarOffice = subRegistrarOffice?.Trim();
                if (registrationFee is decimal fee) agreement.RegistrationFee = fee;
                break;
        }

        agreement.Status = toStatus;
        agreement.UpdatedAt = DateTime.UtcNow;

        // The booking's own status follows the paperwork, so the highlights
        // panel and the file can never tell different stories.
        booking.Status = toStatus switch
        {
            AgreementStatuses.Executed => BookingStatuses.AgreementExecuted,
            AgreementStatuses.Registered => BookingStatuses.Registered,
            AgreementStatuses.Drafted or AgreementStatuses.WithCustomer or AgreementStatuses.Franked
                => BookingStatuses.AgreementPending,
            _ => booking.Status,
        };

        await db.SaveChangesAsync(ct);

        return agreement;
    }

    /* ------------------------------------------------------------------ *
     * Home loan
     * ------------------------------------------------------------------ */

    public async Task<HomeLoan> SaveLoanAsync(
        int bookingId,
        string bankName,
        string? branchName,
        string? applicationNumber,
        decimal requestedAmount,
        CancellationToken ct = default)
    {
        await RequireBookingAsync(bookingId, ct);

        var loan = await db.HomeLoans.FirstOrDefaultAsync(l => l.BookingId == bookingId, ct);

        if (loan is null)
        {
            loan = new HomeLoan
            {
                CompanyId = tenant.CompanyId,
                BookingId = bookingId,
                AppliedOn = DateTime.UtcNow.Date,
                Status = LoanStatuses.Applied,
            };

            db.HomeLoans.Add(loan);
        }

        loan.BankName = bankName.Trim();
        loan.BranchName = branchName?.Trim();
        loan.ApplicationNumber = applicationNumber?.Trim();
        loan.RequestedAmount = requestedAmount;
        loan.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return loan;
    }

    /// <summary>Records the sanction, which is when the collection risk moves to the bank.</summary>
    public async Task<HomeLoan> SanctionAsync(
        int bookingId,
        decimal sanctionedAmount,
        DateTime? sanctionedOn,
        DateTime? validUntil,
        CancellationToken ct = default)
    {
        var loan = await RequireLoanAsync(bookingId, ct);

        if (sanctionedAmount <= 0) throw ApiException.BadRequest("A sanction needs an amount.");

        var booking = await RequireBookingAsync(bookingId, ct);

        if (sanctionedAmount > booking.GrandTotal)
        {
            throw ApiException.BadRequest(
                "A sanction larger than the whole consideration is almost always a typing slip.");
        }

        loan.SanctionedAmount = sanctionedAmount;
        loan.SanctionedOn = (sanctionedOn ?? DateTime.UtcNow).Date;
        loan.SanctionValidUntil = validUntil;
        loan.Status = LoanStatuses.Sanctioned;
        loan.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return loan;
    }

    /// <summary>The tripartite. Disbursement does not begin without it.</summary>
    public async Task<HomeLoan> TripartiteAsync(
        int bookingId, DateTime? on, CancellationToken ct = default)
    {
        var loan = await RequireLoanAsync(bookingId, ct);

        if (loan.SanctionedOn is null)
        {
            throw ApiException.BadRequest("Record the sanction before the tripartite.");
        }

        loan.TripartiteSignedOn = (on ?? DateTime.UtcNow).Date;
        loan.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return loan;
    }

    /// <summary>
    /// A release from the bank.
    ///
    /// Takes a receipt too, so the money lands in the ledger and the loan's
    /// disbursed figure at the same moment. Recording one without the other is
    /// how a booking ends up showing a bank that has paid and a customer who
    /// still owes.
    /// </summary>
    public async Task<HomeLoan> DisburseAsync(
        int bookingId,
        decimal amount,
        DateTime? on,
        string? reference,
        BookingService bookings,
        CancellationToken ct = default)
    {
        var loan = await RequireLoanAsync(bookingId, ct);

        if (loan.TripartiteSignedOn is null)
        {
            throw ApiException.BadRequest("The tripartite has to be signed before a disbursement.");
        }

        if (amount <= 0) throw ApiException.BadRequest("A disbursement needs an amount.");

        if (amount > loan.UndisbursedAmount + 0.5m)
        {
            throw ApiException.BadRequest(
                $"Only {loan.UndisbursedAmount:N0} of the sanction is left to disburse.");
        }

        var receipt = new Receipt
        {
            CompanyId = tenant.CompanyId,
            BookingId = bookingId,
            ReceivedOn = (on ?? DateTime.UtcNow).Date,
            Amount = amount,
            TdsAmount = 0,
            Mode = PaymentModes.LoanDisbursement,
            Instrument = reference,
            BankName = loan.BankName,
            Status = ReceiptStatuses.Cleared,
            Notes = $"Disbursement from {loan.BankName}",
        };

        await bookings.ReceiveAsync(receipt, ct);

        loan.DisbursedAmount += amount;
        loan.Status = loan.UndisbursedAmount <= 0.5m
            ? LoanStatuses.FullyDisbursed
            : LoanStatuses.Disbursing;

        loan.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return loan;
    }

    /* ------------------------------------------------------------------ *
     * Possession and handover
     * ------------------------------------------------------------------ */

    public async Task<Possession> OfferPossessionAsync(
        int bookingId,
        DateTime? on,
        decimal maintenanceAmount,
        int maintenanceMonths,
        decimal corpusDeposit,
        CancellationToken ct = default)
    {
        var booking = await RequireBookingAsync(bookingId, ct);
        var possession = await PossessionRowAsync(bookingId, ct);

        possession.OfferedOn = (on ?? DateTime.UtcNow).Date;
        possession.MaintenanceAmount = maintenanceAmount;
        possession.MaintenanceAdvanceMonths = maintenanceMonths;
        possession.CorpusDeposit = corpusDeposit;
        possession.Status = PossessionStatuses.Offered;
        possession.UpdatedAt = DateTime.UtcNow;

        booking.Status = BookingStatuses.PossessionOffered;

        await db.SaveChangesAsync(ct);

        return possession;
    }

    /// <summary>The joint inspection, and what it found.</summary>
    public async Task<Possession> InspectAsync(
        int bookingId, DateTime? on, int snagsRaised, CancellationToken ct = default)
    {
        var possession = await PossessionRowAsync(bookingId, ct);

        if (possession.OfferedOn is null)
        {
            throw ApiException.BadRequest("Offer possession before the inspection.");
        }

        possession.InspectedOn = (on ?? DateTime.UtcNow).Date;
        possession.SnagsRaised = Math.Max(possession.SnagsRaised, snagsRaised);

        possession.Status = possession.SnagsOpen > 0
            ? PossessionStatuses.SnagsOpen
            : PossessionStatuses.Inspected;

        possession.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return possession;
    }

    public async Task<Possession> CloseSnagsAsync(
        int bookingId, int closed, CancellationToken ct = default)
    {
        var possession = await PossessionRowAsync(bookingId, ct);

        possession.SnagsClosed = Math.Clamp(
            possession.SnagsClosed + closed, 0, possession.SnagsRaised);

        possession.UpdatedAt = DateTime.UtcNow;

        await RefreshPossessionAsync(possession, ct);
        await db.SaveChangesAsync(ct);

        return possession;
    }

    public async Task<Possession> CollectAsync(
        int bookingId, bool maintenance, bool corpus, CancellationToken ct = default)
    {
        var possession = await PossessionRowAsync(bookingId, ct);

        if (maintenance) possession.MaintenanceCollected = true;
        if (corpus) possession.CorpusCollected = true;

        possession.UpdatedAt = DateTime.UtcNow;

        await RefreshPossessionAsync(possession, ct);
        await db.SaveChangesAsync(ct);

        return possession;
    }

    /// <summary>
    /// Hands over the keys.
    ///
    /// Every gate is checked here rather than trusted to the screen. Handover is
    /// the point after which a developer's leverage is gone — a unit handed over
    /// with dues outstanding is money that gets collected by asking nicely, if
    /// at all.
    /// </summary>
    public async Task<Possession> HandOverAsync(
        int bookingId, DateTime? on, bool documentsHandedOver, CancellationToken ct = default)
    {
        var booking = await RequireBookingAsync(bookingId, ct);
        var possession = await PossessionRowAsync(bookingId, ct);

        await RefreshPossessionAsync(possession, ct);

        var blockers = new List<string>();

        if (possession.OfferedOn is null) blockers.Add("possession has not been offered");
        if (possession.SnagsOpen > 0) blockers.Add($"{possession.SnagsOpen} snags are still open");
        if (!possession.DuesCleared) blockers.Add($"{booking.Outstanding:N0} is still outstanding");
        if (!possession.MaintenanceCollected && possession.MaintenanceAmount > 0)
            blockers.Add("the maintenance advance has not been collected");
        if (!possession.CorpusCollected && possession.CorpusDeposit > 0)
            blockers.Add("the corpus deposit has not been collected");

        if (blockers.Count > 0)
        {
            throw ApiException.BadRequest($"Cannot hand over — {string.Join(", ", blockers)}.");
        }

        possession.HandedOverOn = (on ?? DateTime.UtcNow).Date;
        possession.DocumentsHandedOver = documentsHandedOver;
        possession.Status = PossessionStatuses.HandedOver;
        possession.UpdatedAt = DateTime.UtcNow;

        booking.Status = BookingStatuses.HandedOver;

        await db.SaveChangesAsync(ct);

        return possession;
    }

    /* ------------------------------------------------------------------ *
     * Cancellation and refund
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Opens a cancellation and computes what would come back.
    ///
    /// The arithmetic is done here rather than typed, because a refund figure
    /// somebody worked out on paper is a figure that gets argued about. What was
    /// received, what the clause forfeits, what brokerage was already paid out,
    /// and what is left — in that order, on the record.
    /// </summary>
    public async Task<BookingCancellation> RequestCancellationAsync(
        int bookingId,
        string reason,
        decimal deductionPercent,
        decimal otherDeductions,
        CancellationToken ct = default)
    {
        var booking = await RequireBookingAsync(bookingId, ct);

        if (booking.Status == BookingStatuses.Cancelled)
        {
            throw ApiException.BadRequest("This booking is already cancelled.");
        }

        if (booking.Status == BookingStatuses.HandedOver)
        {
            throw ApiException.BadRequest(
                "The unit has been handed over. A cancellation after possession is a resale, not a cancellation.");
        }

        var open = await db.BookingCancellations
            .AnyAsync(c => c.BookingId == bookingId && c.Status == CancellationStatuses.Requested, ct);

        if (open) throw ApiException.BadRequest("A cancellation is already open on this booking.");

        // Gross of TDS: the customer paid it, even though part of it went to the
        // government rather than to the developer.
        var received = await db.Receipts
            .Where(r => r.BookingId == bookingId && r.Status == ReceiptStatuses.Cleared)
            .SumAsync(r => (decimal?)r.CreditedAmount, ct) ?? 0;

        var brokerage = await db.Brokerages
            .Where(b => b.BookingId == bookingId)
            .SumAsync(b => (decimal?)b.PaidAmount, ct) ?? 0;

        var deduction = Math.Round(received * (deductionPercent / 100m), 2);
        var refund = Math.Max(0, received - deduction - brokerage - otherDeductions);

        var cancellation = new BookingCancellation
        {
            CompanyId = tenant.CompanyId,
            BookingId = bookingId,
            RequestedOn = DateTime.UtcNow,
            Reason = reason.Trim(),
            AmountReceived = received,
            DeductionPercent = deductionPercent,
            DeductionAmount = deduction,
            BrokerageRecovered = brokerage,
            OtherDeductions = otherDeductions,
            RefundAmount = refund,
            Status = CancellationStatuses.Requested,
        };

        db.BookingCancellations.Add(cancellation);
        await db.SaveChangesAsync(ct);

        return cancellation;
    }

    /// <summary>
    /// Approves the cancellation, releases the unit and closes the schedule.
    ///
    /// The unit going back on the board is the point of the whole operation, and
    /// doing it here rather than leaving it to somebody to remember is what
    /// stops a cancelled booking sitting on inventory for a month.
    /// </summary>
    public async Task<BookingCancellation> ApproveCancellationAsync(
        int cancellationId, CancellationToken ct = default)
    {
        var cancellation = await db.BookingCancellations
            .FirstOrDefaultAsync(c => c.Id == cancellationId, ct)
            ?? throw ApiException.NotFound("Cancellation");

        if (cancellation.Status != CancellationStatuses.Requested)
        {
            throw ApiException.BadRequest("Only an open cancellation can be approved.");
        }

        var booking = await RequireBookingAsync(cancellation.BookingId, ct);

        cancellation.Status = CancellationStatuses.Approved;
        cancellation.ApprovedOn = DateTime.UtcNow;
        cancellation.ApprovedById = tenant.UserId;
        cancellation.UpdatedAt = DateTime.UtcNow;

        booking.Status = BookingStatuses.Cancelled;

        // Nothing further is owed on a cancelled booking, and a demand left open
        // would keep accruing interest against a customer who has left.
        await db.Demands
            .Where(d => d.BookingId == booking.Id && d.Status != DemandStatuses.Paid)
            .ExecuteUpdateAsync(d => d
                .SetProperty(x => x.Status, DemandStatuses.Cancelled)
                .SetProperty(x => x.CancelledOn, DateTime.UtcNow), ct);

        var unit = await db.Units.FirstOrDefaultAsync(u => u.Id == booking.UnitId, ct);

        if (unit is not null)
        {
            var was = unit.Status;
            unit.Status = UnitStatuses.Available;

            db.UnitStatusHistories.Add(new UnitStatusHistory
            {
                UnitId = unit.Id,
                FromStatus = was,
                ToStatus = UnitStatuses.Available,
                Reason = $"Booking {booking.BookingNumber} cancelled",
                ActorId = tenant.UserId,
                ActorName = tenant.UserName,
            });
        }

        await db.SaveChangesAsync(ct);

        return cancellation;
    }

    public async Task<BookingCancellation> RefundAsync(
        int cancellationId, DateTime? on, string reference, CancellationToken ct = default)
    {
        var cancellation = await db.BookingCancellations
            .FirstOrDefaultAsync(c => c.Id == cancellationId, ct)
            ?? throw ApiException.NotFound("Cancellation");

        if (cancellation.Status != CancellationStatuses.Approved)
        {
            throw ApiException.BadRequest("The cancellation has to be approved before the refund.");
        }

        cancellation.Status = CancellationStatuses.Refunded;
        cancellation.RefundedOn = (on ?? DateTime.UtcNow).Date;
        cancellation.RefundReference = reference.Trim();
        cancellation.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return cancellation;
    }

    /* ------------------------------------------------------------------ *
     * Transfer
     * ------------------------------------------------------------------ */

    public async Task<BookingTransfer> RequestTransferAsync(
        int bookingId,
        string toName,
        string? toPhone,
        string? toEmail,
        string? toPan,
        decimal chargePercent,
        CancellationToken ct = default)
    {
        var booking = await RequireBookingAsync(bookingId, ct);

        if (booking.Status is BookingStatuses.Cancelled or BookingStatuses.HandedOver)
        {
            throw ApiException.BadRequest(
                "A booking can only be transferred while it is live and before handover.");
        }

        var primary = await db.BookingApplicants
            .Where(a => a.BookingId == bookingId && a.Role == ApplicantRoles.Primary)
            .Select(a => a.Name)
            .FirstOrDefaultAsync(ct);

        var charge = Math.Round(booking.AgreementValue * (chargePercent / 100m), 2);

        var transfer = new BookingTransfer
        {
            CompanyId = tenant.CompanyId,
            BookingId = bookingId,
            RequestedOn = DateTime.UtcNow,
            FromName = primary ?? "—",
            ToName = toName.Trim(),
            ToPhone = toPhone?.Trim(),
            ToEmail = toEmail?.Trim(),
            ToPan = toPan?.Trim().ToUpperInvariant(),
            TransferChargePercent = chargePercent,
            TransferChargeAmount = charge,
            Status = TransferStatuses.Requested,
        };

        db.BookingTransfers.Add(transfer);
        await db.SaveChangesAsync(ct);

        return transfer;
    }

    /// <summary>
    /// Completes the transfer: the incoming buyer becomes the primary applicant.
    ///
    /// The outgoing applicant is kept on the booking rather than overwritten.
    /// Their payments have to stay attributable to them for the rest of the
    /// file's life — a TDS certificate issued three years ago names a person,
    /// and that person has to still be findable here.
    /// </summary>
    public async Task<BookingTransfer> CompleteTransferAsync(
        int transferId, CancellationToken ct = default)
    {
        var transfer = await db.BookingTransfers.FirstOrDefaultAsync(t => t.Id == transferId, ct)
            ?? throw ApiException.NotFound("Transfer");

        if (transfer.Status == TransferStatuses.Completed)
        {
            throw ApiException.BadRequest("That transfer is already complete.");
        }

        if (transfer.TransferChargeReceived < transfer.TransferChargeAmount - 0.5m)
        {
            throw ApiException.BadRequest(
                $"The transfer charge of {transfer.TransferChargeAmount:N0} has not been received in full.");
        }

        var applicants = await db.BookingApplicants
            .Where(a => a.BookingId == transfer.BookingId)
            .ToListAsync(ct);

        foreach (var outgoing in applicants.Where(a => a.Role == ApplicantRoles.Primary))
        {
            outgoing.Role = ApplicantRoles.PreviousOwner;
        }

        db.BookingApplicants.Add(new BookingApplicant
        {
            CompanyId = transfer.CompanyId,
            BookingId = transfer.BookingId,
            Role = ApplicantRoles.Primary,
            Name = transfer.ToName,
            Phone = transfer.ToPhone,
            Email = transfer.ToEmail,
            Pan = transfer.ToPan,
            Address = transfer.ToAddress,
            SortOrder = 0,
            KycStatus = KycStatuses.Pending,
        });

        transfer.Status = TransferStatuses.Completed;
        transfer.ApprovedOn ??= DateTime.UtcNow;
        transfer.ApprovedById ??= tenant.UserId;
        transfer.CompletedOn = DateTime.UtcNow;
        transfer.UpdatedAt = DateTime.UtcNow;

        var booking = await RequireBookingAsync(transfer.BookingId, ct);
        booking.Status = BookingStatuses.Transferred;

        await db.SaveChangesAsync(ct);

        return transfer;
    }

    /* ------------------------------------------------------------------ *
     * Helpers
     * ------------------------------------------------------------------ */

    private async Task<Booking> RequireBookingAsync(int id, CancellationToken ct) =>
        await db.Bookings.FirstOrDefaultAsync(b => b.Id == id, ct)
        ?? throw ApiException.NotFound("Booking");

    private async Task<HomeLoan> RequireLoanAsync(int bookingId, CancellationToken ct) =>
        await db.HomeLoans.FirstOrDefaultAsync(l => l.BookingId == bookingId, ct)
        ?? throw ApiException.BadRequest("No home loan is recorded on this booking.");

    private async Task<Possession> PossessionRowAsync(int bookingId, CancellationToken ct)
    {
        var possession = await db.Possessions.FirstOrDefaultAsync(p => p.BookingId == bookingId, ct);

        if (possession is null)
        {
            possession = new Possession { CompanyId = tenant.CompanyId, BookingId = bookingId };
            db.Possessions.Add(possession);
        }

        return possession;
    }

    /// <summary>
    /// Recomputes the gates and the status from what is currently true.
    ///
    /// Dues are read from the ledger rather than trusted to a flag: the whole
    /// point of the handover check is that it reflects the account as it stands
    /// this minute, not as somebody ticked it last week.
    /// </summary>
    private async Task RefreshPossessionAsync(Possession possession, CancellationToken ct)
    {
        var summary = await ledger.RecomputeAsync(possession.BookingId, ct: ct);

        possession.DuesCleared = summary.Outstanding <= 0.5m;

        if (possession.Status != PossessionStatuses.HandedOver)
        {
            possession.Status = possession.OfferedOn is null
                ? PossessionStatuses.NotDue
                : possession.SnagsOpen > 0
                    ? PossessionStatuses.SnagsOpen
                    : possession.ReadyToHandOver
                        ? PossessionStatuses.ReadyToHandOver
                        : possession.InspectedOn is not null
                            ? PossessionStatuses.Inspected
                            : PossessionStatuses.Offered;
        }
    }
}
