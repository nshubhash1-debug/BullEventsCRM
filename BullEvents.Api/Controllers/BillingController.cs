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

public record TaxInvoiceDto(
    int Id,
    int BookingId,
    string BookingNumber,
    string ProjectName,
    string UnitNumber,
    int DemandId,
    string DemandNumber,
    string DemandLabel,
    string InvoiceNumber,
    DateTime InvoiceDate,
    string Treatment,
    string TreatmentLabel,
    string SacCode,
    decimal GrossValue,
    decimal LandAbatement,
    decimal TaxableValue,
    decimal GstRate,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal IgstAmount,
    decimal TotalTax,
    decimal InvoiceTotal,
    decimal Credited,
    string CustomerName,
    string? CustomerPan,
    string? PlaceOfSupply,
    string Status,
    string? Notes);

public record RaiseInvoiceRequest(string? Treatment, DateTime? On);

public record CreditNoteDto(
    int Id,
    int BookingId,
    string BookingNumber,
    int? TaxInvoiceId,
    string? InvoiceNumber,
    string CreditNoteNumber,
    DateTime IssuedOn,
    string Reason,
    string ReasonLabel,
    string? Narrative,
    decimal GrossValue,
    decimal TaxableValue,
    decimal GstRate,
    decimal CgstAmount,
    decimal SgstAmount,
    decimal CreditTotal);

public record CreditRequest(
    [Range(0.01, 1_000_000_000)] decimal GrossValue,
    [Required] string Reason,
    string? Narrative,
    DateTime? On);

public record PdcDto(
    int Id,
    int BookingId,
    string BookingNumber,
    string CustomerName,
    string ProjectName,
    string UnitNumber,
    int? DemandId,
    string? DemandNumber,
    string ChequeNumber,
    string BankName,
    string? BranchName,
    decimal Amount,
    DateTime ChequeDate,
    DateTime ReceivedOn,
    DateTime? DepositedOn,
    DateTime? ClearedOn,
    DateTime? BouncedOn,
    string? BounceReason,
    int? ReceiptId,
    string Status,
    string? Notes,
    /// <summary>Bankable today and still in the drawer. This is the banking run.</summary>
    bool DueForBanking,
    /// <summary>Negative until the date on its face, then days it has sat unbanked.</summary>
    int DaysSinceBankable);

public record TakeChequeRequest(
    [Required] string ChequeNumber,
    [Required] string BankName,
    string? BranchName,
    [Range(0.01, 1_000_000_000)] decimal Amount,
    [Required] DateTime ChequeDate,
    int? DemandId,
    string? Notes);

public record ChequeBounceRequest([Required] string Reason, DateTime? On);
public record DatedRequest(DateTime? On);
public record ReasonRequest(string? Reason);

public record TdsDto(
    int Id,
    int BookingId,
    string BookingNumber,
    int ReceiptId,
    string ReceiptNumber,
    DateTime ReceivedOn,
    string? DeductorName,
    string? DeductorPan,
    decimal AmountPaid,
    decimal TdsAmount,
    string? Quarter,
    string? CertificateNumber,
    DateTime? CertificateDate,
    string? ChallanNumber,
    DateTime? ReceivedCertificateOn,
    string Status,
    string? Notes,
    /// <summary>Days since the deduction with nothing furnished against it.</summary>
    int AwaitingDays);

public record RecordCertificateRequest(
    [Required] string CertificateNumber,
    [Required] DateTime CertificateDate,
    string? ChallanNumber,
    decimal? CertifiedAmount,
    string? FileUrl);

public record GstProfileDto(
    int Id,
    int? ProjectId,
    string? ProjectName,
    string LegalName,
    string? TradeName,
    string Gstin,
    string? Pan,
    string StateName,
    string StateCode,
    string? RegisteredAddress,
    string DefaultTreatment,
    string DefaultSacCode,
    DateTime? OccupancyCertificateOn,
    string InvoicePrefix,
    string CreditNotePrefix,
    string? BankAccountName,
    string? BankAccountNumber,
    string? BankIfsc,
    string? BankBranch,
    bool IsActive);

public record SaveGstProfileRequest(
    int? ProjectId,
    [Required] string LegalName,
    string? TradeName,
    [Required][StringLength(15, MinimumLength = 15)] string Gstin,
    string? Pan,
    [Required] string StateName,
    string? RegisteredAddress,
    string? DefaultTreatment,
    string? DefaultSacCode,
    DateTime? OccupancyCertificateOn,
    string? InvoicePrefix,
    string? CreditNotePrefix,
    string? BankAccountName,
    string? BankAccountNumber,
    string? BankIfsc,
    string? BankBranch,
    bool IsActive = true);

public record BillingSummaryDto(
    int InvoicesDraft,
    int InvoicesIssued,
    decimal TaxBilled,
    decimal TaxCredited,
    int ChequesHeld,
    decimal ChequesHeldValue,
    int ChequesDueForBanking,
    decimal ChequesDueValue,
    int ChequesBounced,
    int TdsAwaited,
    decimal TdsAwaitedValue,
    int TdsMismatched,
    decimal TdsMismatchedValue,
    /// <summary>True when no GST profile exists — nothing here can be raised until one does.</summary>
    bool NeedsGstProfile);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The statutory side of collections: what was invoiced, what was credited,
/// which cheques are due at the bank, and which TDS certificates are owed.
///
/// Kept off <c>CollectionsController</c> even though the two read the same
/// bookings. Collections is an operational desk asking who to chase; this is a
/// finance desk asking what was filed, and the two want different lists, sorted
/// differently, with different things going red.
/// </summary>
[ApiController]
[Route("api/billing")]
[Authorize]
[SecuredBy(SecuredObjects.Booking)]
[RequireModule(Modules.PostSales)]
public class BillingController(AppDbContext db, BillingService billing)
    : CrmControllerBase(db)
{
    /* ---------------- overview ---------------- */

    [HttpGet("summary")]
    public async Task<BillingSummaryDto> Summary(CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var invoices = await Db.Set<TaxInvoice>()
            .GroupBy(i => i.Status)
            .Select(g => new
            {
                Status = g.Key,
                Count = g.Count(),
                Tax = g.Sum(i => i.CgstAmount + i.SgstAmount + i.IgstAmount),
            })
            .ToListAsync(ct);

        var cheques = await Db.Set<PostDatedCheque>()
            .Select(p => new { p.Status, p.Amount, p.ChequeDate })
            .ToListAsync(ct);

        var tds = await Db.Set<TdsCertificate>()
            .GroupBy(t => t.Status)
            .Select(g => new { Status = g.Key, Count = g.Count(), Value = g.Sum(t => t.TdsAmount) })
            .ToListAsync(ct);

        var credited = await Db.Set<CreditNote>()
            .SumAsync(c => (decimal?)(c.CgstAmount + c.SgstAmount), ct) ?? 0m;

        var held = cheques.Where(c => c.Status == PdcStatuses.Held).ToList();
        var due = held.Where(c => c.ChequeDate.Date <= today).ToList();

        int CountOf(string status) =>
            tds.FirstOrDefault(t => t.Status == status)?.Count ?? 0;

        decimal ValueOf(string status) =>
            tds.FirstOrDefault(t => t.Status == status)?.Value ?? 0m;

        return new BillingSummaryDto(
            InvoicesDraft: invoices.FirstOrDefault(i => i.Status == InvoiceStatuses.Draft)?.Count ?? 0,
            InvoicesIssued: invoices
                .Where(i => i.Status is InvoiceStatuses.Issued or InvoiceStatuses.Credited)
                .Sum(i => i.Count),
            TaxBilled: invoices
                .Where(i => i.Status != InvoiceStatuses.Cancelled)
                .Sum(i => i.Tax),
            TaxCredited: credited,
            ChequesHeld: held.Count,
            ChequesHeldValue: held.Sum(c => c.Amount),
            ChequesDueForBanking: due.Count,
            ChequesDueValue: due.Sum(c => c.Amount),
            ChequesBounced: cheques.Count(c => c.Status == PdcStatuses.Bounced),
            TdsAwaited: CountOf(TdsStatuses.Awaited),
            TdsAwaitedValue: ValueOf(TdsStatuses.Awaited),
            TdsMismatched: CountOf(TdsStatuses.Mismatched),
            TdsMismatchedValue: ValueOf(TdsStatuses.Mismatched),
            NeedsGstProfile: !await Db.Set<GstProfile>().AnyAsync(p => p.IsActive, ct));
    }

    /* ---------------- invoices ---------------- */

    [HttpGet("invoices")]
    public async Task<IReadOnlyList<TaxInvoiceDto>> Invoices(
        [FromQuery] int? bookingId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        // Credits are summed in a subquery rather than joined, so an invoice
        // with none still appears — a left join here silently drops every
        // invoice that has never been credited, which is nearly all of them.
        var rows = await Db.Set<TaxInvoice>()
            .Where(i => (bookingId == null || i.BookingId == bookingId)
                && (status == null || i.Status == status))
            .OrderByDescending(i => i.InvoiceDate).ThenByDescending(i => i.Id)
            .Select(i => new
            {
                Invoice = i,
                i.Booking!.BookingNumber,
                i.Booking.ProjectName,
                i.Booking.UnitNumber,
                DemandNumber = i.Demand!.DemandNumber,
                DemandLabel = i.Demand.Label,
                Credited = Db.Set<CreditNote>()
                    .Where(c => c.TaxInvoiceId == i.Id)
                    .Sum(c => (decimal?)c.GrossValue) ?? 0m,
            })
            .Take(500)
            .ToListAsync(ct);

        return [.. rows.Select(r => new TaxInvoiceDto(
            r.Invoice.Id,
            r.Invoice.BookingId,
            r.BookingNumber,
            r.ProjectName,
            r.UnitNumber,
            r.Invoice.DemandId,
            r.DemandNumber,
            r.DemandLabel,
            r.Invoice.InvoiceNumber,
            r.Invoice.InvoiceDate,
            r.Invoice.Treatment,
            GstTreatments.Label(r.Invoice.Treatment),
            r.Invoice.SacCode,
            r.Invoice.GrossValue,
            r.Invoice.LandAbatement,
            r.Invoice.TaxableValue,
            r.Invoice.GstRate,
            r.Invoice.CgstAmount,
            r.Invoice.SgstAmount,
            r.Invoice.IgstAmount,
            r.Invoice.TotalTax,
            r.Invoice.InvoiceTotal,
            r.Credited,
            r.Invoice.CustomerName,
            r.Invoice.CustomerPan,
            r.Invoice.PlaceOfSupply,
            r.Invoice.Status,
            r.Invoice.Notes))];
    }

    [HttpPost("demands/{demandId:int}/invoice")]
    public async Task<ActionResult<TaxInvoiceDto>> Raise(
        int demandId, RaiseInvoiceRequest request, CancellationToken ct)
    {
        var invoice = await billing.InvoiceDemandAsync(demandId, request.Treatment, request.On, ct);
        return await One(invoice.Id, ct);
    }

    [HttpPost("invoices/bulk")]
    public Task<BillingService.BulkResult> Bulk(
        [FromQuery] int? projectId, CancellationToken ct)
        => billing.InvoiceOutstandingAsync(projectId, ct);

    [HttpPost("invoices/{id:int}/issue")]
    public async Task<ActionResult<TaxInvoiceDto>> Issue(int id, CancellationToken ct)
    {
        await billing.IssueInvoiceAsync(id, ct);
        return await One(id, ct);
    }

    [HttpPost("invoices/{id:int}/cancel")]
    public async Task<ActionResult<TaxInvoiceDto>> Cancel(
        int id, ReasonRequest request, CancellationToken ct)
    {
        await billing.CancelInvoiceAsync(id, request.Reason, ct);
        return await One(id, ct);
    }

    /// <summary>
    /// Reads back one invoice.
    ///
    /// A private helper rather than a call to the list action: an
    /// <c>ActionResult</c>'s <c>.Value</c> is null unless the framework put it
    /// there, so re-projecting here is the only way the mutating endpoints can
    /// return the row they just wrote.
    /// </summary>
    private async Task<ActionResult<TaxInvoiceDto>> One(int id, CancellationToken ct)
    {
        var rows = await Invoices(bookingId: null, status: null, ct);
        var row = rows.FirstOrDefault(r => r.Id == id);

        return row is null ? NotFound() : row;
    }

    /* ---------------- credit notes ---------------- */

    [HttpGet("credit-notes")]
    public async Task<IReadOnlyList<CreditNoteDto>> Credits(
        [FromQuery] int? bookingId, CancellationToken ct)
    {
        var rows = await Db.Set<CreditNote>()
            .Where(c => bookingId == null || c.BookingId == bookingId)
            .OrderByDescending(c => c.IssuedOn).ThenByDescending(c => c.Id)
            .Select(c => new
            {
                Note = c,
                c.Booking!.BookingNumber,
                InvoiceNumber = c.TaxInvoice != null ? c.TaxInvoice.InvoiceNumber : null,
            })
            .Take(500)
            .ToListAsync(ct);

        return [.. rows.Select(r => new CreditNoteDto(
            r.Note.Id,
            r.Note.BookingId,
            r.BookingNumber,
            r.Note.TaxInvoiceId,
            r.InvoiceNumber,
            r.Note.CreditNoteNumber,
            r.Note.IssuedOn,
            r.Note.Reason,
            CreditReasons.Label(r.Note.Reason),
            r.Note.Narrative,
            r.Note.GrossValue,
            r.Note.TaxableValue,
            r.Note.GstRate,
            r.Note.CgstAmount,
            r.Note.SgstAmount,
            r.Note.CreditTotal))];
    }

    [HttpPost("invoices/{id:int}/credit")]
    public async Task<ActionResult<CreditNoteDto>> Credit(
        int id, CreditRequest request, CancellationToken ct)
    {
        var note = await billing.CreditAsync(
            id, request.GrossValue, request.Reason, request.Narrative, request.On, ct);

        var rows = await Credits(bookingId: null, ct);
        var row = rows.FirstOrDefault(r => r.Id == note.Id);

        return row is null ? NotFound() : row;
    }

    /* ---------------- cheque register ---------------- */

    [HttpGet("cheques")]
    public async Task<IReadOnlyList<PdcDto>> Cheques(
        [FromQuery] int? bookingId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var rows = await Db.Set<PostDatedCheque>()
            .Where(p => (bookingId == null || p.BookingId == bookingId)
                && (status == null || p.Status == status))
            // The oldest bankable cheque first: that is the one costing the
            // most in lost days, and the register exists to surface it.
            .OrderBy(p => p.ChequeDate).ThenBy(p => p.Id)
            .Select(p => new
            {
                Cheque = p,
                p.Booking!.BookingNumber,
                p.Booking.ProjectName,
                p.Booking.UnitNumber,
                CustomerName = Db.BookingApplicants
                    .Where(a => a.BookingId == p.BookingId && a.Role == ApplicantRoles.Primary)
                    .Select(a => a.Name)
                    .FirstOrDefault(),
                DemandNumber = Db.Demands
                    .Where(d => d.Id == p.DemandId)
                    .Select(d => d.DemandNumber)
                    .FirstOrDefault(),
            })
            .Take(1000)
            .ToListAsync(ct);

        return [.. rows.Select(r => new PdcDto(
            r.Cheque.Id,
            r.Cheque.BookingId,
            r.BookingNumber,
            r.CustomerName ?? "—",
            r.ProjectName,
            r.UnitNumber,
            r.Cheque.DemandId,
            r.DemandNumber,
            r.Cheque.ChequeNumber,
            r.Cheque.BankName,
            r.Cheque.BranchName,
            r.Cheque.Amount,
            r.Cheque.ChequeDate,
            r.Cheque.ReceivedOn,
            r.Cheque.DepositedOn,
            r.Cheque.ClearedOn,
            r.Cheque.BouncedOn,
            r.Cheque.BounceReason,
            r.Cheque.ReceiptId,
            r.Cheque.Status,
            r.Cheque.Notes,
            r.Cheque.DueForBanking(today),
            (int)(today - r.Cheque.ChequeDate.Date).TotalDays))];
    }

    [HttpPost("bookings/{bookingId:int}/cheques")]
    public async Task<ActionResult<PdcDto>> Take(
        int bookingId, TakeChequeRequest request, CancellationToken ct)
    {
        var cheque = await billing.TakeChequeAsync(
            bookingId,
            request.ChequeNumber,
            request.BankName,
            request.BranchName,
            request.Amount,
            request.ChequeDate,
            request.DemandId,
            request.Notes,
            ct);

        return await OneCheque(cheque.Id, ct);
    }

    [HttpPost("cheques/{id:int}/deposit")]
    public async Task<ActionResult<PdcDto>> Deposit(
        int id, DatedRequest request, CancellationToken ct)
    {
        await billing.DepositAsync(id, request.On, ct);
        return await OneCheque(id, ct);
    }

    [HttpPost("cheques/{id:int}/clear")]
    public async Task<ActionResult<PdcDto>> Clear(
        int id, DatedRequest request, CancellationToken ct)
    {
        await billing.ClearAsync(id, request.On, ct);
        return await OneCheque(id, ct);
    }

    [HttpPost("cheques/{id:int}/bounce")]
    public async Task<ActionResult<PdcDto>> Bounce(
        int id, ChequeBounceRequest request, CancellationToken ct)
    {
        await billing.BounceAsync(id, request.Reason, request.On, ct);
        return await OneCheque(id, ct);
    }

    [HttpPost("cheques/{id:int}/return")]
    public async Task<ActionResult<PdcDto>> Return(
        int id, ReasonRequest request, CancellationToken ct)
    {
        await billing.ReturnChequeAsync(id, request.Reason, ct);
        return await OneCheque(id, ct);
    }

    private async Task<ActionResult<PdcDto>> OneCheque(int id, CancellationToken ct)
    {
        var rows = await Cheques(bookingId: null, status: null, ct);
        var row = rows.FirstOrDefault(r => r.Id == id);

        return row is null ? NotFound() : row;
    }

    /* ---------------- TDS ---------------- */

    [HttpGet("tds")]
    public async Task<IReadOnlyList<TdsDto>> Tds(
        [FromQuery] int? bookingId,
        [FromQuery] string? status,
        CancellationToken ct)
    {
        var today = DateTime.UtcNow.Date;

        var rows = await Db.Set<TdsCertificate>()
            .Where(t => (bookingId == null || t.BookingId == bookingId)
                && (status == null || t.Status == status))
            .OrderBy(t => t.Status == TdsStatuses.Awaited ? 0 : 1)
            .ThenBy(t => t.Receipt!.ReceivedOn)
            .Select(t => new
            {
                Certificate = t,
                t.Booking!.BookingNumber,
                t.Receipt!.ReceiptNumber,
                t.Receipt.ReceivedOn,
            })
            .Take(1000)
            .ToListAsync(ct);

        return [.. rows.Select(r => new TdsDto(
            r.Certificate.Id,
            r.Certificate.BookingId,
            r.BookingNumber,
            r.Certificate.ReceiptId,
            r.ReceiptNumber,
            r.ReceivedOn,
            r.Certificate.DeductorName,
            r.Certificate.DeductorPan,
            r.Certificate.AmountPaid,
            r.Certificate.TdsAmount,
            r.Certificate.Quarter,
            r.Certificate.CertificateNumber,
            r.Certificate.CertificateDate,
            r.Certificate.ChallanNumber,
            r.Certificate.ReceivedOn,
            r.Certificate.Status,
            r.Certificate.Notes,
            r.Certificate.Status == TdsStatuses.Awaited
                ? (int)(today - r.ReceivedOn.Date).TotalDays
                : 0))];
    }

    [HttpPost("tds/sync")]
    public async Task<object> SyncTds([FromQuery] int? bookingId, CancellationToken ct)
    {
        var opened = await billing.SyncTdsAsync(bookingId, ct);
        return new { opened };
    }

    [HttpPost("tds/{id:int}/certificate")]
    public async Task<ActionResult<TdsDto>> RecordCertificate(
        int id, RecordCertificateRequest request, CancellationToken ct)
    {
        await billing.RecordCertificateAsync(
            id,
            request.CertificateNumber,
            request.CertificateDate,
            request.ChallanNumber,
            request.CertifiedAmount,
            request.FileUrl,
            ct);

        return await OneTds(id, ct);
    }

    [HttpPost("tds/{id:int}/verify")]
    public async Task<ActionResult<TdsDto>> VerifyCertificate(int id, CancellationToken ct)
    {
        await billing.VerifyCertificateAsync(id, ct);
        return await OneTds(id, ct);
    }

    private async Task<ActionResult<TdsDto>> OneTds(int id, CancellationToken ct)
    {
        var rows = await Tds(bookingId: null, status: null, ct);
        var row = rows.FirstOrDefault(r => r.Id == id);

        return row is null ? NotFound() : row;
    }

    /* ---------------- GST profiles ---------------- */

    [HttpGet("gst-profiles")]
    public async Task<IReadOnlyList<GstProfileDto>> Profiles(CancellationToken ct)
    {
        var rows = await Db.Set<GstProfile>()
            .OrderBy(p => p.ProjectId == null ? 0 : 1).ThenBy(p => p.LegalName)
            .Select(p => new { Profile = p, ProjectName = p.Project!.Name })
            .ToListAsync(ct);

        return [.. rows.Select(r => Project(r.Profile, r.ProjectName))];
    }

    [HttpPost("gst-profiles")]
    public async Task<ActionResult<GstProfileDto>> SaveProfile(
        SaveGstProfileRequest request, CancellationToken ct)
    {
        var profile = await Db.Set<GstProfile>()
            .FirstOrDefaultAsync(p => p.ProjectId == request.ProjectId, ct);

        if (profile is null)
        {
            profile = new GstProfile { ProjectId = request.ProjectId };
            Db.Set<GstProfile>().Add(profile);
        }

        var treatment = request.DefaultTreatment ?? GstTreatments.ResidentialUnderConstruction;

        if (!GstTreatments.All.Contains(treatment))
        {
            throw ApiException.BadRequest($"'{treatment}' is not a GST treatment this system knows.");
        }

        // The first two digits of a GSTIN are the state code, so it is derived
        // rather than typed. A hand-entered code that disagrees with the GSTIN
        // is a rejected return, and nobody notices until the filing bounces.
        var gstin = request.Gstin.Trim().ToUpperInvariant();

        profile.LegalName = request.LegalName;
        profile.TradeName = request.TradeName;
        profile.Gstin = gstin;
        profile.Pan = request.Pan?.Trim().ToUpperInvariant();
        profile.StateName = request.StateName;
        profile.StateCode = gstin.Length >= 2 ? gstin[..2] : string.Empty;
        profile.RegisteredAddress = request.RegisteredAddress;
        profile.DefaultTreatment = treatment;
        profile.DefaultSacCode = request.DefaultSacCode ?? "9954";
        profile.OccupancyCertificateOn = request.OccupancyCertificateOn?.Date;
        profile.InvoicePrefix = request.InvoicePrefix ?? "INV";
        profile.CreditNotePrefix = request.CreditNotePrefix ?? "CN";
        profile.BankAccountName = request.BankAccountName;
        profile.BankAccountNumber = request.BankAccountNumber;
        profile.BankIfsc = request.BankIfsc?.Trim().ToUpperInvariant();
        profile.BankBranch = request.BankBranch;
        profile.IsActive = request.IsActive;
        profile.UpdatedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);

        var name = profile.ProjectId is null
            ? null
            : await Db.Projects.Where(p => p.Id == profile.ProjectId)
                .Select(p => p.Name).FirstOrDefaultAsync(ct);

        return Project(profile, name);
    }

    private static GstProfileDto Project(GstProfile p, string? projectName) => new(
        p.Id,
        p.ProjectId,
        projectName,
        p.LegalName,
        p.TradeName,
        p.Gstin,
        p.Pan,
        p.StateName,
        p.StateCode,
        p.RegisteredAddress,
        p.DefaultTreatment,
        p.DefaultSacCode,
        p.OccupancyCertificateOn,
        p.InvoicePrefix,
        p.CreditNotePrefix,
        p.BankAccountName,
        p.BankAccountNumber,
        p.BankIfsc,
        p.BankBranch,
        p.IsActive);

    /// <summary>The picklists the billing screens need, so they are never hard-coded twice.</summary>
    [HttpGet("reference")]
    public object Reference() => new
    {
        treatments = GstTreatments.All
            .Select(t => new { value = t, label = GstTreatments.Label(t), rate = GstTreatments.Rate(t) }),
        creditReasons = CreditReasons.All
            .Select(r => new { value = r, label = CreditReasons.Label(r) }),
        invoiceStatuses = InvoiceStatuses.All,
        chequeStatuses = PdcStatuses.All,
        tdsStatuses = TdsStatuses.All,
    };
}
