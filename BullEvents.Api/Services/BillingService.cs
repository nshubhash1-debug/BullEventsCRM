using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// The statutory paperwork behind the money: tax invoices, credit notes, the
/// cheque drawer, and the TDS certificates a buyer owes back.
///
/// Kept apart from <see cref="BookingLedger"/> deliberately. The ledger answers
/// "what does this customer owe" — an internal question, recomputable, and free
/// to change its mind when a back-dated receipt arrives. This answers "what did
/// we tell the government", which is a different kind of fact: once a number is
/// issued it is never edited, only reversed by another document that points at
/// it. Mixing the two is how a system ends up silently renumbering an invoice
/// somebody has already claimed input credit against.
/// </summary>
public class BillingService(
    AppDbContext db,
    TenantContext tenant,
    GstEngine gst,
    BookingLedger ledger)
{
    /* ------------------------------------------------------------------ *
     * Tax invoices
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Raises the tax invoice for one demand.
    ///
    /// One invoice per demand, enforced. A demand billed twice is two documents
    /// claiming the same money, and the customer will quite reasonably pay once
    /// and claim credit twice.
    /// </summary>
    public async Task<TaxInvoice> InvoiceDemandAsync(
        int demandId, string? treatment = null, DateTime? on = null, CancellationToken ct = default)
    {
        var demand = await db.Demands
            .Include(d => d.Booking)
            .FirstOrDefaultAsync(d => d.Id == demandId, ct)
            ?? throw ApiException.NotFound("Demand");

        if (demand.Status == DemandStatuses.Cancelled)
        {
            throw ApiException.BadRequest(
                "That demand has been cancelled. A cancelled demand is not invoiced — "
                + "if an invoice was already raised against it, credit that instead.");
        }

        var existing = await db.Set<TaxInvoice>()
            .FirstOrDefaultAsync(i => i.DemandId == demandId
                && i.Status != InvoiceStatuses.Cancelled, ct);

        if (existing is not null)
        {
            throw ApiException.BadRequest(
                $"{demand.DemandNumber} is already invoiced as {existing.InvoiceNumber}. "
                + "Cancel or credit that invoice before raising another.");
        }

        var booking = demand.Booking
            ?? throw ApiException.BadRequest("That demand is not attached to a booking.");

        var profile = await gst.ProfileForAsync(booking.ProjectId, ct);

        // Once the occupancy certificate is in, the developer is selling a
        // finished building rather than supplying construction, and the supply
        // leaves GST entirely. Billing tax after that date collects money the
        // developer has no authority to collect.
        var raisedOn = on?.Date ?? DateTime.UtcNow.Date;

        var applied = profile.OccupancyCertificateOn is DateTime oc && raisedOn >= oc.Date
            ? GstTreatments.Exempt
            : treatment ?? profile.DefaultTreatment;

        if (!GstTreatments.All.Contains(applied))
        {
            throw ApiException.BadRequest($"'{applied}' is not a GST treatment this system knows.");
        }

        // Billed on the demand's principal. The demand's own TaxAmount was the
        // estimate carried from the cost sheet; the invoice is the figure that
        // counts, and computing it here is what makes the two reconcile.
        var computed = GstEngine.Compute(applied, demand.BasicAmount);

        var primary = await db.BookingApplicants
            .Where(a => a.BookingId == booking.Id && a.Role == ApplicantRoles.Primary)
            .OrderBy(a => a.SortOrder)
            .FirstOrDefaultAsync(ct);

        var invoice = new TaxInvoice
        {
            CompanyId = tenant.CompanyId,
            BookingId = booking.Id,
            DemandId = demand.Id,
            InvoiceNumber = await gst.NextInvoiceNumberAsync(profile, ct),
            InvoiceDate = raisedOn,
            Treatment = applied,
            SacCode = profile.DefaultSacCode,
            GrossValue = computed.GrossValue,
            LandAbatement = computed.LandAbatement,
            TaxableValue = computed.TaxableValue,
            GstRate = computed.Rate,
            CgstAmount = computed.Cgst,
            SgstAmount = computed.Sgst,
            IgstAmount = computed.Igst,
            CustomerName = primary?.Name ?? booking.BookingNumber,
            CustomerAddress = primary?.Address,
            CustomerPan = primary?.Pan,
            PlaceOfSupply = profile.StateName,
            Status = InvoiceStatuses.Draft,
        };

        db.Set<TaxInvoice>().Add(invoice);
        await db.SaveChangesAsync(ct);

        return invoice;
    }

    /// <summary>
    /// Invoices every demand on a project that has none yet.
    ///
    /// Reports what it skipped rather than failing on the first problem: a run
    /// over four hundred demands that stops on one misconfigured booking has
    /// wasted the operator's afternoon, and they cannot tell how far it got.
    /// </summary>
    public async Task<BulkResult> InvoiceOutstandingAsync(
        int? projectId, CancellationToken ct = default)
    {
        var pending = await db.Demands
            .Include(d => d.Booking)
            .Where(d => d.Status != DemandStatuses.Cancelled
                && (projectId == null || d.Booking!.ProjectId == projectId)
                && !db.Set<TaxInvoice>().Any(i => i.DemandId == d.Id
                    && i.Status != InvoiceStatuses.Cancelled))
            .OrderBy(d => d.RaisedOn)
            .Select(d => d.Id)
            .ToListAsync(ct);

        var raised = new List<string>();
        var skipped = new List<string>();

        foreach (var id in pending)
        {
            try
            {
                raised.Add((await InvoiceDemandAsync(id, ct: ct)).InvoiceNumber);
            }
            catch (ApiException error)
            {
                skipped.Add(error.Message);
            }
        }

        return new BulkResult(raised.Count, raised, skipped);
    }

    public record BulkResult(int Count, IReadOnlyList<string> Raised, IReadOnlyList<string> Skipped);

    /// <summary>
    /// Issues a draft invoice.
    ///
    /// The point of no return. After this the number has been reported and the
    /// document can only be reversed by a credit note, which is why it is a
    /// separate deliberate act rather than something that happens on save.
    /// </summary>
    public async Task<TaxInvoice> IssueInvoiceAsync(int id, CancellationToken ct = default)
    {
        var invoice = await Invoice(id, ct);

        if (invoice.Status != InvoiceStatuses.Draft)
        {
            throw ApiException.BadRequest(
                $"{invoice.InvoiceNumber} is {invoice.Status.ToLowerInvariant()}, not a draft.");
        }

        invoice.Status = InvoiceStatuses.Issued;
        invoice.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return invoice;
    }

    /// <summary>
    /// Cancels an invoice.
    ///
    /// Only a draft. An issued invoice is credited, never cancelled — the number
    /// is out in the world and the audit trail has to show what happened to the
    /// money rather than pretending the document never existed.
    /// </summary>
    public async Task<TaxInvoice> CancelInvoiceAsync(
        int id, string? reason, CancellationToken ct = default)
    {
        var invoice = await Invoice(id, ct);

        if (invoice.Status != InvoiceStatuses.Draft)
        {
            throw ApiException.BadRequest(
                $"{invoice.InvoiceNumber} has been issued. Raise a credit note against it "
                + "instead — an issued invoice cannot be cancelled.");
        }

        invoice.Status = InvoiceStatuses.Cancelled;
        invoice.Notes = reason;
        invoice.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return invoice;
    }

    /* ------------------------------------------------------------------ *
     * Credit notes
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Takes money back off an issued invoice.
    ///
    /// The credit carries the same treatment and rate as the invoice it
    /// reverses, not today's rate: the reversal has to undo what was charged,
    /// and rates change between the demand and the cancellation often enough to
    /// matter. Partial credits are allowed and are how an area reduction is
    /// handled; the running total is checked so the credits can never exceed
    /// what was charged.
    /// </summary>
    public async Task<CreditNote> CreditAsync(
        int invoiceId,
        decimal grossValue,
        string reason,
        string? narrative,
        DateTime? on = null,
        CancellationToken ct = default)
    {
        var invoice = await Invoice(invoiceId, ct);

        if (invoice.Status == InvoiceStatuses.Draft)
        {
            throw ApiException.BadRequest(
                $"{invoice.InvoiceNumber} has not been issued yet. Cancel the draft rather than "
                + "crediting it — a credit note against an unissued invoice means nothing.");
        }

        if (invoice.Status == InvoiceStatuses.Cancelled)
        {
            throw ApiException.BadRequest($"{invoice.InvoiceNumber} was cancelled.");
        }

        if (grossValue <= 0)
        {
            throw ApiException.BadRequest("A credit note has to be for a positive amount.");
        }

        if (!CreditReasons.All.Contains(reason))
        {
            throw ApiException.BadRequest($"'{reason}' is not a reason this system records.");
        }

        var alreadyCredited = await db.Set<CreditNote>()
            .Where(c => c.TaxInvoiceId == invoiceId)
            .SumAsync(c => (decimal?)c.GrossValue, ct) ?? 0m;

        if (alreadyCredited + grossValue > invoice.GrossValue)
        {
            var left = invoice.GrossValue - alreadyCredited;

            throw ApiException.BadRequest(
                $"{invoice.InvoiceNumber} is for {invoice.GrossValue:N2} and {alreadyCredited:N2} "
                + $"has already been credited. At most {left:N2} is left to credit.");
        }

        var booking = await db.Bookings.FirstOrDefaultAsync(b => b.Id == invoice.BookingId, ct)
            ?? throw ApiException.NotFound("Booking");

        var profile = await gst.ProfileForAsync(booking.ProjectId, ct);
        var computed = GstEngine.Compute(invoice.Treatment, grossValue);

        var note = new CreditNote
        {
            CompanyId = tenant.CompanyId,
            BookingId = invoice.BookingId,
            TaxInvoiceId = invoice.Id,
            CreditNoteNumber = await gst.NextCreditNoteNumberAsync(profile, ct),
            IssuedOn = on?.Date ?? DateTime.UtcNow.Date,
            Reason = reason,
            Narrative = narrative,
            GrossValue = computed.GrossValue,
            TaxableValue = computed.TaxableValue,
            GstRate = computed.Rate,
            CgstAmount = computed.Cgst,
            SgstAmount = computed.Sgst,
        };

        db.Set<CreditNote>().Add(note);

        // Fully credited means the invoice no longer stands for anything. Marked
        // rather than deleted, so the series stays unbroken.
        if (alreadyCredited + grossValue >= invoice.GrossValue)
        {
            invoice.Status = InvoiceStatuses.Credited;
            invoice.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);
        return note;
    }

    /* ------------------------------------------------------------------ *
     * Post-dated cheques
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Takes a cheque into the drawer.
    ///
    /// No receipt is raised. The money is promised, not received, and a ledger
    /// that credited it now would show a customer as paid while the cheque was
    /// still in a folder — which is exactly the mistake this register exists to
    /// stop. The receipt is raised when it clears.
    /// </summary>
    public async Task<PostDatedCheque> TakeChequeAsync(
        int bookingId,
        string chequeNumber,
        string bankName,
        string? branchName,
        decimal amount,
        DateTime chequeDate,
        int? demandId,
        string? notes,
        CancellationToken ct = default)
    {
        _ = await db.Bookings.FirstOrDefaultAsync(b => b.Id == bookingId, ct)
            ?? throw ApiException.NotFound("Booking");

        if (amount <= 0)
        {
            throw ApiException.BadRequest("A cheque has to be for a positive amount.");
        }

        var duplicate = await db.Set<PostDatedCheque>()
            .AnyAsync(p => p.BookingId == bookingId
                && EF.Functions.Collate(p.ChequeNumber, "C") == chequeNumber
                && p.Status != PdcStatuses.Returned, ct);

        if (duplicate)
        {
            throw ApiException.BadRequest(
                $"Cheque {chequeNumber} is already in the register for this booking.");
        }

        var cheque = new PostDatedCheque
        {
            CompanyId = tenant.CompanyId,
            BookingId = bookingId,
            DemandId = demandId,
            ChequeNumber = chequeNumber,
            BankName = bankName,
            BranchName = branchName,
            Amount = amount,
            ChequeDate = chequeDate.Date,
            ReceivedOn = DateTime.UtcNow.Date,
            Notes = notes,
            Status = PdcStatuses.Held,
        };

        db.Set<PostDatedCheque>().Add(cheque);
        await db.SaveChangesAsync(ct);

        return cheque;
    }

    /// <summary>
    /// Sends a cheque to the bank.
    ///
    /// Refused before the date on its face, because a bank will refuse it too
    /// and the developer will have burned a presentation on it.
    /// </summary>
    public async Task<PostDatedCheque> DepositAsync(
        int id, DateTime? on = null, CancellationToken ct = default)
    {
        var cheque = await Cheque(id, ct);
        var day = on?.Date ?? DateTime.UtcNow.Date;

        if (cheque.Status != PdcStatuses.Held)
        {
            throw ApiException.BadRequest(
                $"Cheque {cheque.ChequeNumber} is {cheque.Status.ToLowerInvariant()}, "
                + "so it cannot be deposited.");
        }

        if (day < cheque.ChequeDate)
        {
            throw ApiException.BadRequest(
                $"Cheque {cheque.ChequeNumber} is dated {cheque.ChequeDate:dd MMM yyyy} "
                + "and cannot be banked before then.");
        }

        cheque.Status = PdcStatuses.Deposited;
        cheque.DepositedOn = day;
        cheque.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return cheque;
    }

    /// <summary>
    /// The cheque cleared — so now, and only now, the money exists.
    ///
    /// This is where the receipt is raised and allocated, which is the whole
    /// point of the register: collection is recognised on clearing, not on
    /// collection of the paper.
    /// </summary>
    public async Task<PostDatedCheque> ClearAsync(
        int id, DateTime? on = null, CancellationToken ct = default)
    {
        var cheque = await Cheque(id, ct);

        if (cheque.Status is not (PdcStatuses.Deposited or PdcStatuses.Held))
        {
            throw ApiException.BadRequest(
                $"Cheque {cheque.ChequeNumber} is {cheque.Status.ToLowerInvariant()}.");
        }

        var day = on?.Date ?? DateTime.UtcNow.Date;

        var receipt = new Receipt
        {
            CompanyId = tenant.CompanyId,
            BookingId = cheque.BookingId,
            ReceiptNumber = await NextReceiptNumberAsync(ct),
            ReceivedOn = day,
            Amount = cheque.Amount,
            Mode = PaymentModes.Cheque,
            Instrument = cheque.ChequeNumber,
            BankName = cheque.BankName,
            InstrumentDate = cheque.ChequeDate,
            Status = ReceiptStatuses.Cleared,
            ClearedOn = day,
            Unallocated = cheque.Amount,
            Notes = $"Cheque {cheque.ChequeNumber} cleared.",
        };

        db.Receipts.Add(receipt);
        await db.SaveChangesAsync(ct);

        cheque.Status = PdcStatuses.Cleared;
        cheque.ClearedOn = day;
        cheque.ReceiptId = receipt.Id;
        cheque.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        await ledger.AllocateAsync(receipt.Id, ct);
        await ledger.RecomputeAsync(cheque.BookingId, ct: ct);

        return cheque;
    }

    /// <summary>
    /// The cheque bounced.
    ///
    /// If a receipt was raised — a cheque banked and credited before the return
    /// came back — it is marked bounced and the ledger recomputed, which un-does
    /// the allocation. The cheque itself stays in the register with the reason
    /// on it, because a bounce is a fact about the customer that the next
    /// person to grant them a credit period needs to see.
    /// </summary>
    public async Task<PostDatedCheque> BounceAsync(
        int id, string reason, DateTime? on = null, CancellationToken ct = default)
    {
        var cheque = await Cheque(id, ct);

        if (cheque.Status is PdcStatuses.Bounced or PdcStatuses.Returned)
        {
            throw ApiException.BadRequest(
                $"Cheque {cheque.ChequeNumber} is already {cheque.Status.ToLowerInvariant()}.");
        }

        var day = on?.Date ?? DateTime.UtcNow.Date;

        cheque.Status = PdcStatuses.Bounced;
        cheque.BouncedOn = day;
        cheque.BounceReason = reason;
        cheque.UpdatedAt = DateTime.UtcNow;

        if (cheque.ReceiptId is int receiptId)
        {
            var receipt = await db.Receipts.FirstOrDefaultAsync(r => r.Id == receiptId, ct);

            if (receipt is not null)
            {
                receipt.Status = ReceiptStatuses.Bounced;
                receipt.BouncedOn = day;
                receipt.BounceReason = reason;
                receipt.UpdatedAt = DateTime.UtcNow;
            }
        }

        await db.SaveChangesAsync(ct);
        await ledger.RecomputeAsync(cheque.BookingId, ct: ct);

        return cheque;
    }

    /// <summary>Handed back — usually because the customer paid another way.</summary>
    public async Task<PostDatedCheque> ReturnChequeAsync(
        int id, string? reason, CancellationToken ct = default)
    {
        var cheque = await Cheque(id, ct);

        if (cheque.Status == PdcStatuses.Cleared)
        {
            throw ApiException.BadRequest(
                $"Cheque {cheque.ChequeNumber} has already cleared and cannot be handed back.");
        }

        cheque.Status = PdcStatuses.Returned;
        cheque.Notes = reason ?? cheque.Notes;
        cheque.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return cheque;
    }

    /* ------------------------------------------------------------------ *
     * TDS
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Opens a certificate row for every receipt that had TDS deducted and does
    /// not have one yet.
    ///
    /// Run rather than required, because the deduction is recorded on the
    /// receipt at the counter and nobody is going to remember to also open a
    /// tracking row. What this produces is the list of Form 16Bs the developer
    /// is owed and has not chased — a number most developers cannot produce at
    /// all, and discover at assessment.
    /// </summary>
    public async Task<int> SyncTdsAsync(int? bookingId = null, CancellationToken ct = default)
    {
        var receipts = await db.Receipts
            .Where(r => r.TdsAmount > 0
                && r.Status == ReceiptStatuses.Cleared
                && (bookingId == null || r.BookingId == bookingId)
                && !db.Set<TdsCertificate>().Any(t => t.ReceiptId == r.Id))
            .ToListAsync(ct);

        if (receipts.Count == 0) return 0;

        var names = await db.BookingApplicants
            .Where(a => a.Role == ApplicantRoles.Primary)
            .Select(a => new { a.BookingId, a.Name, a.Pan })
            .ToListAsync(ct);

        var byBooking = names
            .GroupBy(a => a.BookingId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var receipt in receipts)
        {
            byBooking.TryGetValue(receipt.BookingId, out var applicant);

            db.Set<TdsCertificate>().Add(new TdsCertificate
            {
                CompanyId = tenant.CompanyId,
                BookingId = receipt.BookingId,
                ReceiptId = receipt.Id,
                DeductorName = applicant?.Name,
                DeductorPan = applicant?.Pan,
                AmountPaid = receipt.CreditedAmount,
                TdsAmount = receipt.TdsAmount,
                Quarter = GstEngine.Quarter(receipt.ReceivedOn),
                Status = TdsStatuses.Awaited,
            });
        }

        await db.SaveChangesAsync(ct);
        return receipts.Count;
    }

    /// <summary>
    /// Records the certificate the buyer finally produced.
    ///
    /// The amount on it is checked against what was deducted. A certificate for
    /// less than was withheld is the common and expensive case — the buyer
    /// deducted 1% and deposited less — and it is flagged as a mismatch rather
    /// than accepted, because the difference is money the developer will
    /// otherwise never see and never know about.
    /// </summary>
    public async Task<TdsCertificate> RecordCertificateAsync(
        int id,
        string certificateNumber,
        DateTime certificateDate,
        string? challanNumber,
        decimal? certifiedAmount,
        string? fileUrl,
        CancellationToken ct = default)
    {
        var certificate = await db.Set<TdsCertificate>()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("TDS record");

        certificate.CertificateNumber = certificateNumber;
        certificate.CertificateDate = certificateDate.Date;
        certificate.ChallanNumber = challanNumber;
        certificate.FileUrl = fileUrl;
        certificate.ReceivedOn = DateTime.UtcNow.Date;
        certificate.UpdatedAt = DateTime.UtcNow;

        var certified = certifiedAmount ?? certificate.TdsAmount;

        if (Math.Abs(certified - certificate.TdsAmount) > 1m)
        {
            certificate.Status = TdsStatuses.Mismatched;
            certificate.Notes =
                $"Certificate is for {certified:N2}; {certificate.TdsAmount:N2} was deducted "
                + $"— short by {certificate.TdsAmount - certified:N2}.";
        }
        else
        {
            certificate.Status = TdsStatuses.Received;

            // Cleared, not left behind. A buyer who produced a short
            // certificate and then the right one leaves a note describing a
            // shortfall that no longer exists, and the row goes on shouting
            // about 800 rupees nobody owes.
            certificate.Notes = null;
        }

        await db.SaveChangesAsync(ct);
        return certificate;
    }

    /// <summary>Checked against Form 26AS and found to agree. The end of the trail.</summary>
    public async Task<TdsCertificate> VerifyCertificateAsync(int id, CancellationToken ct = default)
    {
        var certificate = await db.Set<TdsCertificate>()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("TDS record");

        if (certificate.Status == TdsStatuses.Awaited)
        {
            throw ApiException.BadRequest(
                "Nothing has been received against this deduction yet, so there is "
                + "nothing to verify.");
        }

        certificate.Status = TdsStatuses.Verified;
        certificate.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return certificate;
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    private async Task<TaxInvoice> Invoice(int id, CancellationToken ct) =>
        await db.Set<TaxInvoice>().FirstOrDefaultAsync(i => i.Id == id, ct)
        ?? throw ApiException.NotFound("Invoice");

    private async Task<PostDatedCheque> Cheque(int id, CancellationToken ct) =>
        await db.Set<PostDatedCheque>().FirstOrDefaultAsync(p => p.Id == id, ct)
        ?? throw ApiException.NotFound("Cheque");

    /// <summary>
    /// The next receipt number, in the same series the counter uses.
    ///
    /// Deliberately the same stem as <c>BookingService</c>'s, not a parallel
    /// one: a cheque that cleared is a receipt like any other, and a second
    /// series would give the finance team two documents numbered 0007.
    /// </summary>
    private async Task<string> NextReceiptNumberAsync(CancellationToken ct)
    {
        var stem = $"BRG/RCP/{DateTime.UtcNow.Year}/";

        var last = await db.Receipts
            .IgnoreQueryFilters()
            .Where(r => r.CompanyId == tenant.CompanyId
                && EF.Functions.Collate(r.ReceiptNumber, "C").StartsWith(stem))
            .OrderByDescending(r => r.ReceiptNumber)
            .Select(r => r.ReceiptNumber)
            .FirstOrDefaultAsync(ct);

        var next = last is not null && int.TryParse(last[stem.Length..], out var parsed)
            ? parsed + 1
            : 1;

        return stem + next.ToString("D4");
    }
}
