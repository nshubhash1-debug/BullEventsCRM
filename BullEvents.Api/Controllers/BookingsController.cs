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

public record BookingRowDto(
    int Id,
    string BookingNumber,
    string Status,
    string StatusLabel,
    DateTime BookingDate,

    /// <summary>The contracted event date. Null on a contract raised before one was fixed.</summary>
    DateTime? EventDate,
    string? EventType,
    int GuestCount,

    /// <summary>
    /// Whole days until the event; negative once it has passed.
    ///
    /// Computed on read, because it is what the contract list sorts by — an
    /// events desk chases the wedding that is three weeks out, not the one
    /// booked longest ago.
    /// </summary>
    int? DaysToEvent,

    string ProjectName,
    string? TowerName,
    string UnitNumber,
    string? Configuration,
    decimal SaleableArea,
    string CustomerName,
    string? CustomerPhone,
    decimal AgreementValue,
    decimal GrandTotal,
    decimal Demanded,
    decimal Received,
    decimal Outstanding,
    decimal InterestDue,
    int OverdueDays,
    /// <summary>Received over grand total, as a fraction. What the collections desk sorts on.</summary>
    double CollectedFraction,
    string? OwnerName,
    string AgreementStatus,
    string PossessionStatus,
    bool HasLoan);

public record ApplicantDto(
    int Id,
    string Role,
    string? Salutation,
    string Name,
    string? Relation,
    string? Phone,
    string? Email,
    string? Address,
    DateTime? DateOfBirth,
    string? Pan,
    string? AadhaarLast4,
    string KycStatus,
    DateTime? KycVerifiedOn);

public record MilestoneDto(
    int Id,
    int SortOrder,
    string Label,
    decimal Percent,
    decimal BasicAmount,
    decimal TaxAmount,
    decimal TotalAmount,
    string? ConstructionStage,
    DateTime? DueDate,
    string Status,
    int? DemandId,
    /// <summary>Allocated against the demand raised for it.</summary>
    decimal Received);

public record BookingDetailDto(
    BookingRowDto Summary,
    IReadOnlyList<ApplicantDto> Applicants,
    IReadOnlyList<MilestoneDto> Milestones,
    string? PaymentPlanName,
    int? PaymentPlanId,
    string? Notes,
    int? QuotationId,
    int? LeadId,
    int UnitId);

public record SaveApplicantRequest(
    [Required] string Role,
    string? Salutation,
    [Required, MinLength(2)] string Name,
    string? Relation,
    string? Phone,
    [EmailAddress] string? Email,
    string? Address,
    DateTime? DateOfBirth,
    string? Pan,
    string? AadhaarLast4,
    string? KycStatus);

public record CreateBookingRequest(
    [Required] int QuotationId,
    DateTime? BookingDate,
    int? OwnerId,
    [Required, MinLength(1)] IReadOnlyList<SaveApplicantRequest> Applicants);

public record RescheduleRequest(DateTime DueDate, [Required, MinLength(3)] string Reason);
public record WaiveRequest([Required, MinLength(3)] string Reason);
public record AddInstalmentRequest(
    [Required, MinLength(2)] string Label,
    decimal BasicAmount,
    decimal TaxAmount,
    DateTime? DueDate);
public record RevisePlanRequest([Required] int PaymentPlanId, [Required, MinLength(3)] string Reason);

public record DocumentDto(
    int Id,
    string Key,
    string Name,
    string Stage,
    bool IsRequired,
    string Status,
    DateTime? ReceivedOn,
    DateTime? VerifiedOn,
    DateTime? ExpiresOn,
    string? FileName,
    string? FileUrl,
    string? Notes);

public record CustomerUnitDto(
    int BookingId,
    string BookingNumber,
    string Status,
    DateTime BookingDate,
    string ProjectName,
    string? TowerName,
    string UnitNumber,
    string? Configuration,
    decimal GrandTotal,
    decimal Received,
    decimal Outstanding,
    int OverdueDays);

public record CustomerRowDto(
    /// <summary>The lead this customer came from — the identity the row is keyed on.</summary>
    int? LeadId,
    string Name,
    string? Phone,
    string? Email,
    string? City,
    string? Source,
    string? OwnerName,
    string LifecycleStage,
    /// <summary>When they first bought. What the list sorts on.</summary>
    DateTime CustomerSince,
    int Units,
    decimal Portfolio,
    decimal Received,
    decimal Outstanding,
    int OverdueDays,
    int CoApplicants,
    int KycVerified,
    int KycPending,
    int DocumentsPending,
    IReadOnlyList<CustomerUnitDto> Bookings);

public record SaveDocumentRequest(
    [Required] string Status,
    DateTime? ReceivedOn,
    DateTime? ExpiresOn,
    string? FileName,
    string? FileUrl,
    string? Notes,
    bool? IsRequired);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// Sold units, and the file that follows each one.
///
/// Gated on the booking object rather than on the sales objects: post-sales is a
/// different desk with a different reach. A rep who closed the deal has no
/// business editing the agreement value afterwards, and the person chasing the
/// third instalment has no business seeing the open pipeline.
/// </summary>
[ApiController]
[Route("api/bookings")]
[Authorize]
[SecuredBy(SecuredObjects.Booking)]
[RequireModule(Modules.PostSales)]
public class BookingsController(
    AppDbContext db,
    BookingService bookings,
    BookingPlanService plans,
    BookingLedger ledger) : CrmControllerBase(db)
{
    /// <summary>
    /// Whole days from today to the event. Negative once it has passed.
    ///
    /// Computed on read rather than stored because it changes every midnight,
    /// and it is what the contract list is worked in: an events desk chases the
    /// wedding that is three weeks out, not the one booked longest ago.
    /// </summary>
    private static int? DaysToEvent(DateTime? eventDate) =>
        eventDate is null
            ? null
            : (int)(eventDate.Value.Date - DateTime.UtcNow.Date).TotalDays;

    /* ---------------- list ---------------- */

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BookingRowDto>>> List(
        [FromQuery] string? status = null,
        [FromQuery] int? projectId = null,
        [FromQuery] string? tower = null,
        [FromQuery] bool overdueOnly = false,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var query = Db.Bookings
            .Include(b => b.Applicants)
            .Include(b => b.Owner)
            .AsQueryable();

        query = await ScopedAsync(query, ct);

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(b => b.Status == status);
        if (projectId is int project) query = query.Where(b => b.ProjectId == project);
        if (!string.IsNullOrWhiteSpace(tower)) query = query.Where(b => b.TowerName == tower);
        if (overdueOnly) query = query.Where(b => b.OverdueDays > 0);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            query = query.Where(b =>
                b.BookingNumber.Contains(needle)
                || b.UnitNumber.Contains(needle)
                || b.Applicants.Any(a => a.Name.Contains(needle) || a.Phone!.Contains(needle)));
        }

        var rows = await query
            .OrderByDescending(b => b.OverdueDays).ThenByDescending(b => b.BookingDate)
            .Take(500)
            .ToListAsync(ct);

        return Ok(await ProjectAsync(rows, ct));
    }

    /* ---------------- customers ---------------- */

    /// <summary>
    /// The people who actually bought, one row each.
    ///
    /// Keyed on the lead rather than on the booking, because a customer who
    /// takes two flats is one customer with two units — and because the lead is
    /// where the rest of the CRM already knows them: their source, their city,
    /// the rep who worked them, the whole timeline before they signed.
    ///
    /// A row appears only once its lead has reached Booked. That gate is the
    /// point of the screen: post-sales should never be able to see, or start
    /// working, somebody the sales desk has not closed yet.
    /// </summary>
    [HttpGet("customers")]
    public async Task<ActionResult<IReadOnlyList<CustomerRowDto>>> Customers(
        [FromQuery] string? search = null,
        [FromQuery] bool overdueOnly = false,
        CancellationToken ct = default)
    {
        var query = Db.Bookings
            .Include(b => b.Applicants)
            .Include(b => b.Owner)
            .Where(b => b.Status != BookingStatuses.Cancelled);

        query = await ScopedAsync(query, ct);

        var bookings = await query.ToListAsync(ct);
        if (bookings.Count == 0) return Ok(Array.Empty<CustomerRowDto>());

        // The gate. Only leads the sales desk has closed, and only the ones this
        // user is allowed to see — a booking whose lead is out of scope still
        // resolves to no customer rather than to a nameless row.
        var leadIds = bookings
            .Where(b => b.LeadId is int)
            .Select(b => b.LeadId!.Value)
            .Distinct()
            .ToList();

        var leads = await Db.Leads
            .Where(l => leadIds.Contains(l.Id) && l.Stage == LeadStages.Booked)
            .Select(l => new
            {
                l.Id, l.Name, l.Phone, l.Email, l.City, l.Source,
                OwnerName = l.Owner == null ? null : l.Owner.Name,
            })
            .ToDictionaryAsync(l => l.Id, ct);

        var bookingIds = bookings.Select(b => b.Id).ToList();

        var pendingDocs = await Db.BookingDocuments
            .Where(d => bookingIds.Contains(d.BookingId)
                && d.IsRequired
                && d.Status != DocumentStatuses.Verified
                && d.Status != DocumentStatuses.NotApplicable)
            .GroupBy(d => d.BookingId)
            .Select(g => new { BookingId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.BookingId, g => g.Count, ct);

        var rows = bookings
            .Where(b => b.LeadId is int id && leads.ContainsKey(id))
            .GroupBy(b => b.LeadId!.Value)
            .Select(group =>
            {
                var lead = leads[group.Key];
                var units = group.OrderByDescending(b => b.BookingDate).ToList();
                var applicants = units.SelectMany(b => b.Applicants).ToList();

                return new CustomerRowDto(
                    lead.Id,
                    lead.Name,
                    lead.Phone,
                    lead.Email,
                    lead.City,
                    lead.Source,
                    lead.OwnerName,

                    // Derived rather than stored: a lead carries no lifecycle
                    // column, and the only thing this list needs it to say is
                    // whether they came back. Same thresholds the seeder uses.
                    units.Count >= 2 ? LifecycleStages.Repeat : LifecycleStages.Customer,
                    units.Min(b => b.BookingDate),
                    units.Count,
                    units.Sum(b => b.GrandTotal),
                    units.Sum(b => b.Received),
                    units.Sum(b => b.Outstanding),
                    units.Max(b => b.OverdueDays),
                    applicants.Count(a => a.Role != ApplicantRoles.Primary),
                    applicants.Count(a => a.KycStatus == KycStatuses.Verified),
                    applicants.Count(a => a.KycStatus != KycStatuses.Verified),
                    units.Sum(b => pendingDocs.GetValueOrDefault(b.Id)),
                    units.Select(b => new CustomerUnitDto(
                        b.Id, b.BookingNumber, b.Status, b.BookingDate,
                        b.ProjectName, b.TowerName, b.UnitNumber, b.Configuration,
                        b.GrandTotal, b.Received, b.Outstanding, b.OverdueDays)).ToList());
            })
            .ToList();

        if (overdueOnly) rows = rows.Where(r => r.OverdueDays > 0).ToList();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var needle = search.Trim();
            rows = rows
                .Where(r => r.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                    || (r.Phone?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (r.Email?.Contains(needle, StringComparison.OrdinalIgnoreCase) ?? false)
                    || r.Bookings.Any(b =>
                        b.UnitNumber.Contains(needle, StringComparison.OrdinalIgnoreCase)
                        || b.BookingNumber.Contains(needle, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return Ok(rows
            .OrderByDescending(r => r.OverdueDays)
            .ThenByDescending(r => r.CustomerSince)
            .ToList());
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<BookingDetailDto>> Get(int id, CancellationToken ct)
    {
        var booking = await Db.Bookings
            .Include(b => b.Applicants)
            .Include(b => b.Milestones)
            .Include(b => b.Owner)
            .FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw ApiException.NotFound("Booking");

        var summary = (await ProjectAsync([booking], ct))[0];

        var demands = await Db.Demands
            .Where(d => d.BookingId == id && d.Status != DemandStatuses.Cancelled)
            .ToDictionaryAsync(d => d.Id, d => d.Received, ct);

        return Ok(new BookingDetailDto(
            summary,
            booking.Applicants.OrderBy(a => a.SortOrder).Select(ToDto).ToList(),
            booking.Milestones
                .OrderBy(m => m.SortOrder)
                .Select(m => new MilestoneDto(
                    m.Id, m.SortOrder, m.Label, m.Percent, m.BasicAmount, m.TaxAmount,
                    m.TotalAmount, m.ConstructionStage, m.DueDate, m.Status, m.DemandId,
                    m.DemandId is int demandId ? demands.GetValueOrDefault(demandId) : 0))
                .ToList(),
            booking.PaymentPlanName,
            booking.PaymentPlanId,
            booking.Notes,
            booking.QuotationId,
            booking.LeadId,
            booking.UnitId));
    }

    /* ---------------- booking ---------------- */

    /// <summary>
    /// Books the unit a quotation is against.
    ///
    /// Create rather than edit, because this is the moment a pipeline record
    /// becomes an account — and the moment a flat stops being sellable to
    /// anybody else.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<BookingDetailDto>> Create(
        CreateBookingRequest request, CancellationToken ct)
    {
        var applicants = request.Applicants.Select(ToEntity).ToList();

        if (!applicants.Any(a => a.Role == ApplicantRoles.Primary))
        {
            return BadRequest(new { message = "A booking needs a primary applicant." });
        }

        var booking = await bookings.BookAsync(
            request.QuotationId,
            (request.BookingDate ?? DateTime.UtcNow).Date,
            request.OwnerId,
            applicants,
            ct);

        return await Get(booking.Id, ct);
    }

    /* ---------------- applicants ---------------- */

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/applicants")]
    public async Task<ActionResult<ApplicantDto>> AddApplicant(
        int id, SaveApplicantRequest request, CancellationToken ct)
    {
        var exists = await Db.Bookings.AnyAsync(b => b.Id == id, ct);
        if (!exists) throw ApiException.NotFound("Booking");

        var order = await Db.BookingApplicants
            .Where(a => a.BookingId == id)
            .MaxAsync(a => (int?)a.SortOrder, ct) ?? -1;

        var applicant = ToEntity(request);
        applicant.BookingId = id;
        applicant.SortOrder = order + 1;

        Db.BookingApplicants.Add(applicant);
        await Db.SaveChangesAsync(ct);

        return Ok(ToDto(applicant));
    }

    [HttpPut("applicants/{applicantId:int}")]
    public async Task<ActionResult<ApplicantDto>> UpdateApplicant(
        int applicantId, SaveApplicantRequest request, CancellationToken ct)
    {
        var applicant = await Db.BookingApplicants
            .FirstOrDefaultAsync(a => a.Id == applicantId, ct)
            ?? throw ApiException.NotFound("Applicant");

        applicant.Role = Require(request.Role, ApplicantRoles.All, "role");
        applicant.Salutation = request.Salutation;
        applicant.Name = request.Name.Trim();
        applicant.Relation = request.Relation;
        applicant.Phone = request.Phone;
        applicant.Email = request.Email;
        applicant.Address = request.Address;
        applicant.DateOfBirth = request.DateOfBirth;
        applicant.Pan = request.Pan?.Trim().ToUpperInvariant();
        applicant.AadhaarLast4 = Last4(request.AadhaarLast4);

        if (request.KycStatus is not null)
        {
            var was = applicant.KycStatus;
            applicant.KycStatus = Require(request.KycStatus, KycStatuses.All, "KYC status");

            if (applicant.KycStatus == KycStatuses.Verified && was != KycStatuses.Verified)
            {
                applicant.KycVerifiedOn = DateTime.UtcNow;
            }
        }

        await Db.SaveChangesAsync(ct);

        return Ok(ToDto(applicant));
    }

    [HttpDelete("applicants/{applicantId:int}")]
    public async Task<IActionResult> RemoveApplicant(int applicantId, CancellationToken ct)
    {
        var applicant = await Db.BookingApplicants
            .FirstOrDefaultAsync(a => a.Id == applicantId, ct)
            ?? throw ApiException.NotFound("Applicant");

        if (applicant.Role == ApplicantRoles.Primary)
        {
            throw ApiException.Conflict(
                "The primary applicant cannot be removed. Change who it is instead.");
        }

        Db.BookingApplicants.Remove(applicant);
        await Db.SaveChangesAsync(ct);

        return NoContent();
    }

    /* ---------------- the payment plan ---------------- */

    /// <summary>
    /// Moves an instalment.
    ///
    /// Post-sales owns the schedule from booking onwards — a slab that slipped
    /// three weeks has to be able to move the due dates it pushed, and the
    /// demand raised against them.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/plan/{milestoneId:int}/reschedule")]
    public async Task<ActionResult<MilestoneDto>> Reschedule(
        int id, int milestoneId, RescheduleRequest request, CancellationToken ct)
    {
        var milestone = await plans.RescheduleAsync(id, milestoneId, request.DueDate, request.Reason, ct);
        return Ok(ToDto(milestone, 0));
    }

    /// <summary>Writes an instalment off, with the reason on the record.</summary>
    [PermissionAction(ObjectAction.ModifyAll)]
    [HttpPost("{id:int}/plan/{milestoneId:int}/waive")]
    public async Task<ActionResult<MilestoneDto>> WaiveInstalment(
        int id, int milestoneId, WaiveRequest request, CancellationToken ct)
    {
        var milestone = await plans.WaiveAsync(id, milestoneId, request.Reason, ct);
        return Ok(ToDto(milestone, 0));
    }

    /// <summary>Adds something the plan did not carry — a second parking space, a late upgrade.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/plan/instalments")]
    public async Task<ActionResult<MilestoneDto>> AddInstalment(
        int id, AddInstalmentRequest request, CancellationToken ct)
    {
        var milestone = await plans.AddInstalmentAsync(
            id, request.Label, request.BasicAmount, request.TaxAmount, request.DueDate, ct);

        return Ok(ToDto(milestone, 0));
    }

    /// <summary>
    /// Swaps the rest of the schedule for a different plan. Paid and demanded
    /// instalments are left alone.
    /// </summary>
    [PermissionAction(ObjectAction.ModifyAll)]
    [HttpPost("{id:int}/plan/revise")]
    public async Task<ActionResult<BookingDetailDto>> RevisePlan(
        int id, RevisePlanRequest request, CancellationToken ct)
    {
        await plans.RevisePlanAsync(id, request.PaymentPlanId, request.Reason, ct);
        return await Get(id, ct);
    }

    /* ---------------- documents ---------------- */

    [HttpGet("{id:int}/documents")]
    public async Task<ActionResult<IReadOnlyList<DocumentDto>>> Documents(int id, CancellationToken ct)
    {
        var rows = await Db.BookingDocuments
            .Where(d => d.BookingId == id)
            .OrderBy(d => d.Stage).ThenBy(d => d.Id)
            .Select(d => new DocumentDto(
                d.Id, d.Key, d.Name, d.Stage, d.IsRequired, d.Status,
                d.ReceivedOn, d.VerifiedOn, d.ExpiresOn, d.FileName, d.FileUrl, d.Notes))
            .ToListAsync(ct);

        return Ok(rows);
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPut("documents/{documentId:int}")]
    public async Task<ActionResult<DocumentDto>> SaveDocument(
        int documentId, SaveDocumentRequest request, CancellationToken ct)
    {
        var document = await Db.BookingDocuments
            .FirstOrDefaultAsync(d => d.Id == documentId, ct)
            ?? throw ApiException.NotFound("Document");

        var was = document.Status;
        document.Status = Require(request.Status, DocumentStatuses.All, "status");

        // Stamped when it happens rather than left to the client, so the file's
        // dates are the system's rather than whatever a form posted.
        if (document.Status == DocumentStatuses.Received && was != DocumentStatuses.Received)
        {
            document.ReceivedOn = request.ReceivedOn ?? DateTime.UtcNow;
        }

        if (document.Status == DocumentStatuses.Verified && was != DocumentStatuses.Verified)
        {
            document.VerifiedOn = DateTime.UtcNow;
            document.VerifiedById = Db.Tenant.UserId;
            document.ReceivedOn ??= DateTime.UtcNow;
        }

        document.ExpiresOn = request.ExpiresOn;
        document.FileName = request.FileName;
        document.FileUrl = request.FileUrl;
        document.Notes = request.Notes;
        if (request.IsRequired is bool required) document.IsRequired = required;

        document.UpdatedAt = DateTime.UtcNow;
        await Db.SaveChangesAsync(ct);

        return Ok(new DocumentDto(
            document.Id, document.Key, document.Name, document.Stage, document.IsRequired,
            document.Status, document.ReceivedOn, document.VerifiedOn, document.ExpiresOn,
            document.FileName, document.FileUrl, document.Notes));
    }

    /* ------------------------------------------------------------------ *
     * helpers
     * ------------------------------------------------------------------ */

    private async Task<List<BookingRowDto>> ProjectAsync(
        IReadOnlyList<Booking> rows, CancellationToken ct)
    {
        var ids = rows.Select(b => b.Id).ToList();

        var agreements = await Db.BookingAgreements
            .Where(a => ids.Contains(a.BookingId))
            .ToDictionaryAsync(a => a.BookingId, a => a.Status, ct);

        var possessions = await Db.Possessions
            .Where(p => ids.Contains(p.BookingId))
            .ToDictionaryAsync(p => p.BookingId, p => p.Status, ct);

        var loans = await Db.HomeLoans
            .Where(l => ids.Contains(l.BookingId) && l.Status != LoanStatuses.Rejected)
            .Select(l => l.BookingId)
            .Distinct()
            .ToListAsync(ct);

        var interest = await Db.Demands
            .Where(d => ids.Contains(d.BookingId) && d.Status != DemandStatuses.Cancelled)
            .GroupBy(d => d.BookingId)
            .Select(g => new
            {
                BookingId = g.Key,
                Due = g.Sum(d => d.InterestAccrued - d.InterestWaived),
            })
            .ToDictionaryAsync(x => x.BookingId, x => x.Due, ct);

        return rows.Select(b =>
        {
            var primary = b.Applicants
                .OrderBy(a => a.Role == ApplicantRoles.Primary ? 0 : 1)
                .ThenBy(a => a.SortOrder)
                .FirstOrDefault();

            return new BookingRowDto(
                b.Id, b.BookingNumber, b.Status, BookingStatuses.Label(b.Status),
                b.BookingDate,
                b.EventDate, b.EventType, b.GuestCount, DaysToEvent(b.EventDate),
                b.ProjectName, b.TowerName, b.UnitNumber, b.Configuration,
                b.SaleableArea,
                primary?.Name ?? "—",
                primary?.Phone,
                b.AgreementValue, b.GrandTotal, b.Demanded, b.Received, b.Outstanding,
                Math.Max(0, interest.GetValueOrDefault(b.Id)),
                b.OverdueDays,
                b.GrandTotal <= 0 ? 0 : (double)(b.Received / b.GrandTotal),
                b.Owner?.Name,
                agreements.GetValueOrDefault(b.Id, AgreementStatuses.NotStarted),
                possessions.GetValueOrDefault(b.Id, PossessionStatuses.NotDue),
                loans.Contains(b.Id));
        }).ToList();
    }

    private static ApplicantDto ToDto(BookingApplicant a) => new(
        a.Id, a.Role, a.Salutation, a.Name, a.Relation, a.Phone, a.Email, a.Address,
        a.DateOfBirth, a.Pan, a.AadhaarLast4, a.KycStatus, a.KycVerifiedOn);

    private static MilestoneDto ToDto(BookingMilestone m, decimal received) => new(
        m.Id, m.SortOrder, m.Label, m.Percent, m.BasicAmount, m.TaxAmount, m.TotalAmount,
        m.ConstructionStage, m.DueDate, m.Status, m.DemandId, received);

    private BookingApplicant ToEntity(SaveApplicantRequest request) => new()
    {
        CompanyId = Db.Tenant.CompanyId,
        Role = Require(request.Role, ApplicantRoles.All, "role"),
        Salutation = request.Salutation,
        Name = request.Name.Trim(),
        Relation = request.Relation,
        Phone = request.Phone,
        Email = request.Email,
        Address = request.Address,
        DateOfBirth = request.DateOfBirth,
        Pan = request.Pan?.Trim().ToUpperInvariant(),
        AadhaarLast4 = Last4(request.AadhaarLast4),
        KycStatus = request.KycStatus is null
            ? KycStatuses.Pending
            : Require(request.KycStatus, KycStatuses.All, "KYC status"),
    };

    /// <summary>
    /// Keeps the last four digits and nothing else.
    ///
    /// A full Aadhaar number posted by a well-meaning form must not be stored:
    /// the registrar sees the physical document, and the only thing this system
    /// needs it for is telling two applicants apart on a list.
    /// </summary>
    private static string? Last4(string? value)
    {
        var digits = new string((value ?? "").Where(char.IsDigit).ToArray());
        return digits.Length == 0 ? null : digits[^Math.Min(4, digits.Length)..];
    }

    private static string Require(string value, string[] allowed, string what) =>
        allowed.FirstOrDefault(a => string.Equals(a, value, StringComparison.OrdinalIgnoreCase))
        ?? throw ApiException.BadRequest($"'{value}' is not a valid {what}.");
}
