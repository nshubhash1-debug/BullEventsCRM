using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Quotations. Line maths is done here and nowhere else — the client sends
/// quantities and rates, the server decides what the document is worth.
/// </summary>
[ApiController]
[Route("api/quotations")]
[Authorize]
[SecuredBy(SecuredObjects.Quotation)]
public class QuotationsController(AppDbContext db) : CrmControllerBase(db)
{
    internal static readonly FieldMap<Quotation> Fields = new FieldMap<Quotation>()
        .Text("quoteNumber", "Quote number", searchable: true)
        .Text("title", "Title", searchable: true)
        .Number("version", "Version")
        .Text("customerName", "Customer", searchable: true)
        .Text("customerEmail", "Customer email", searchable: true)
        .Text("customerPhone", "Customer phone", searchable: true)
        .Select("status", "Status")
        .Date("issueDate", "Issued")
        .Date("validUntil", "Valid until")
        .Date("sentAt", "Sent")
        .Date("respondedAt", "Responded")
        .Number("subtotal", "Subtotal")
        .Number("discountPercent", "Discount %")
        .Number("discountAmount", "Discount amount")
        .Number("taxPercent", "Tax %")
        .Number("taxAmount", "Tax amount")
        .Number("total", "Total")
        .Text("paymentTerms", "Payment terms")
        .Text("notes", "Notes", searchable: true)
        .Select("projectName", "Project", "Project.Name")
        .Select("ownerName", "Owner", "Owner.Name")
        .Select("branchName", "Branch", "Branch.Name")
        .Number("ownerId", "Owner ID")
        .Number("branchId", "Branch ID")
        .Number("opportunityId", "Opportunity ID")
        .Number("contactId", "Contact ID")
        .Date("createdAt", "Created")
        .Date("updatedAt", "Last modified");

    private IQueryable<Quotation> Base() => Db.Quotations
        .Include(q => q.Branch)
        .Include(q => q.Owner)
        .Include(q => q.Project)
        .Include(q => q.Unit)
        .Include(q => q.Lines)
        .AsNoTracking();

    private static QuotationDto ToDto(Quotation q) => new(
        q.Id, q.QuoteNumber, q.Title, q.Version,
        q.ContactId, q.LeadId, q.OpportunityId,
        q.ProjectId, q.Project?.Name, q.UnitId, q.Unit?.UnitNumber,
        q.CustomerName, q.CustomerEmail, q.CustomerPhone, q.BillingAddress,
        q.Status, q.IssueDate, q.ValidUntil, q.SentAt, q.RespondedAt,
        q.ValidUntil < DateTime.UtcNow && q.Status is not (QuotationStatuses.Accepted or QuotationStatuses.Rejected),
        q.Currency, q.Subtotal, q.DiscountPercent, q.DiscountAmount,
        q.TaxPercent, q.TaxAmount, q.Total,
        q.ChargesTotal,
        // Quotations raised before the charge heads existed carry a zero grand
        // total; on those the unit cost was the whole consideration, so it
        // stands in rather than the list showing a nil against a real quote.
        q.GrandTotal == 0 ? q.Total : q.GrandTotal,
        q.ApprovalStatus,
        q.PaymentTerms, q.Notes, q.TermsAndConditions, q.RejectionReason,
        q.BranchId, q.Branch?.Name ?? "—", q.OwnerId, q.Owner?.Name,
        q.Lines
            .OrderBy(l => l.SortOrder)
            .Select(l => new QuotationLineDto(
                l.Id, l.Description, l.Category, l.Quantity, l.Unit,
                l.UnitPrice, l.DiscountPercent, l.LineTotal, l.SortOrder))
            .ToList(),
        q.CreatedAt, q.UpdatedAt);

    /* ------------------------------------------------------------------ *
     * Reads
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<FilterFieldDto>>> GetFields(CancellationToken ct)
    {
        var owners = await Db.Users
            .Where(u => u.CompanyId == Db.Tenant.CompanyId && u.IsActive)
            .Select(u => u.Name).OrderBy(n => n).ToListAsync(ct);

        var branches = await Db.Branches
            .Where(b => b.CompanyId == Db.Tenant.CompanyId)
            .Select(b => b.Name).OrderBy(n => n).ToListAsync(ct);

        var projects = await Db.Projects.Select(p => p.Name).OrderBy(n => n).ToListAsync(ct);

        return Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["status"] = Options(QuotationStatuses.All),
            ["ownerName"] = Options([.. owners]),
            ["branchName"] = Options([.. branches]),
            ["projectName"] = Options([.. projects]),
        }, new Dictionary<string, string>
        {
            ["quoteNumber"] = "Document",
            ["title"] = "Document",
            ["version"] = "Document",
            ["status"] = "Document",
            ["customerName"] = "Customer",
            ["customerEmail"] = "Customer",
            ["customerPhone"] = "Customer",
            ["subtotal"] = "Money",
            ["discountPercent"] = "Money",
            ["discountAmount"] = "Money",
            ["taxPercent"] = "Money",
            ["taxAmount"] = "Money",
            ["total"] = "Money",
            ["issueDate"] = "Timing",
            ["validUntil"] = "Timing",
            ["sentAt"] = "Timing",
            ["respondedAt"] = "Timing",
        }));
    }

    [PermissionAction(ObjectAction.View)]
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<QuotationDto>>> Query(QueryRequest request, CancellationToken ct)
    {
        return Ok(await RunQueryAsync(
            Base(),
            request,
            Fields,
            q => ToDto(q),
            defaultSortPath: "IssueDate",
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["quoted"] = await filtered.SumAsync(q => q.Total, ct),
                ["accepted"] = await filtered
                    .Where(q => q.Status == QuotationStatuses.Accepted)
                    .SumAsync(q => q.Total, ct),
                ["pending"] = await filtered.CountAsync(q =>
                    q.Status == QuotationStatuses.Sent ||
                    q.Status == QuotationStatuses.UnderReview ||
                    q.Status == QuotationStatuses.Negotiation, ct),
                ["expiring"] = await filtered.CountAsync(q =>
                    q.ValidUntil < DateTime.UtcNow.AddDays(7) &&
                    q.ValidUntil >= DateTime.UtcNow &&
                    q.Status != QuotationStatuses.Accepted &&
                    q.Status != QuotationStatuses.Rejected, ct),
            },
            cancellationToken: ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<QuotationDto>> GetOne(int id, CancellationToken ct)
    {
        var quotation = await Base().FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        return Ok(ToDto(quotation));
    }

    /* ------------------------------------------------------------------ *
     * Writes
     * ------------------------------------------------------------------ */

    [HttpPost]
    public async Task<ActionResult<QuotationDto>> Create(QuotationInput input, CancellationToken ct)
    {
        var branch = await RequireBranchAsync(input.BranchId, ct);
        await RequireOwnerAsync(input.OwnerId, ct);

        var quotation = new Quotation
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            OwnerId = input.OwnerId ?? (Db.Tenant.UserId > 0 ? Db.Tenant.UserId : null),
            QuoteNumber = Code("QT"),
        };

        Apply(quotation, input);

        Db.Quotations.Add(quotation);
        await Db.SaveChangesAsync(ct);

        quotation.Branch = branch;
        return CreatedAtAction(nameof(GetOne), new { id = quotation.Id }, ToDto(quotation));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<QuotationDto>> Update(int id, QuotationInput input, CancellationToken ct)
    {
        var quotation = await Db.Quotations
            .Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        if (quotation.Status == QuotationStatuses.Accepted)
        {
            throw ApiException.Conflict(
                "An accepted quotation cannot be edited. Create a revision instead.");
        }

        var branch = await RequireBranchAsync(input.BranchId, ct);
        await RequireOwnerAsync(input.OwnerId, ct);

        quotation.BranchId = branch.Id;
        quotation.OwnerId = input.OwnerId;

        Db.QuotationLines.RemoveRange(quotation.Lines);
        quotation.Lines.Clear();

        Apply(quotation, input);
        await Db.SaveChangesAsync(ct);

        quotation.Branch = branch;
        return Ok(ToDto(quotation));
    }

    /// <summary>
    /// A revision, not an edit: the original stays on record and the new one
    /// carries the next version number under the same quote reference.
    /// </summary>
    [HttpPost("{id:int}/revise")]
    public async Task<ActionResult<QuotationDto>> Revise(int id, CancellationToken ct)
    {
        var source = await Db.Quotations
            .Include(q => q.Lines)
            .Include(q => q.Milestones)
            .Include(q => q.Charges)
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        var revision = new Quotation
        {
            CompanyId = source.CompanyId,
            BranchId = source.BranchId,
            Title = source.Title,
            Version = source.Version + 1,
            ContactId = source.ContactId,
            LeadId = source.LeadId,
            OpportunityId = source.OpportunityId,
            ProjectId = source.ProjectId,
            UnitId = source.UnitId,
            CustomerName = source.CustomerName,
            CustomerEmail = source.CustomerEmail,
            CustomerPhone = source.CustomerPhone,
            BillingAddress = source.BillingAddress,
            Status = QuotationStatuses.Draft,
            IssueDate = DateTime.UtcNow,
            ValidUntil = DateTime.UtcNow.AddDays(15),
            PaymentTerms = source.PaymentTerms,
            Notes = source.Notes,
            TermsAndConditions = source.TermsAndConditions,
            OwnerId = source.OwnerId,

            // The whole priced basis comes across, not just the lines.
            //
            // A revision that carried lines alone printed a document with no
            // rate, no schedule and no charge heads — a blank cost sheet under a
            // real quote number. The revision starts as an exact copy and is
            // then re-priced through the builder if the terms are what changed.
            PaymentPlanId = source.PaymentPlanId,
            PaymentPlanName = source.PaymentPlanName,
            TowerName = source.TowerName,
            UnitNumber = source.UnitNumber,
            UnitType = source.UnitType,
            SaleableArea = source.SaleableArea,
            BuiltUpArea = source.BuiltUpArea,
            CarpetArea = source.CarpetArea,
            RateCardId = source.RateCardId,
            RateCardLabel = source.RateCardLabel,
            RatePerSqft = source.RatePerSqft,
            PlcPerSqft = source.PlcPerSqft,
            EffectiveRatePerSqft = source.EffectiveRatePerSqft,
            StandardDiscountPercent = source.StandardDiscountPercent,
            DiscountPercent = source.DiscountPercent,
            DiscountAmount = source.DiscountAmount,
            TaxPercent = source.TaxPercent,
            ChargesBasic = source.ChargesBasic,
            ChargesTax = source.ChargesTax,
            ChargesTotal = source.ChargesTotal,
            RefundableTotal = source.RefundableTotal,
            GrandTotal = source.GrandTotal,
            ScheduledTotal = source.ScheduledTotal,
            GrandTotalInWords = source.GrandTotalInWords,
            Subtotal = source.Subtotal,
            TaxAmount = source.TaxAmount,
            Total = source.Total,
            AmountInWords = source.AmountInWords,

            Lines = source.Lines.Select(l => new QuotationLine
            {
                Description = l.Description,
                Category = l.Category,
                Quantity = l.Quantity,
                Unit = l.Unit,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                LineTotal = l.LineTotal,
                SortOrder = l.SortOrder,
            }).ToList(),

            Milestones = source.Milestones.Select(m => new QuotationMilestone
            {
                SortOrder = m.SortOrder,
                Label = m.Label,
                Percent = m.Percent,
                BasicAmount = m.BasicAmount,
                TaxAmount = m.TaxAmount,
                TotalAmount = m.TotalAmount,
                DueDate = m.DueDate,
            }).ToList(),

            Charges = source.Charges.Select(c => new QuotationCharge
            {
                ChargeHeadId = c.ChargeHeadId,
                SortOrder = c.SortOrder,
                Group = c.Group,
                Name = c.Name,
                Basis = c.Basis,
                Quantity = c.Quantity,
                QuantityUnit = c.QuantityUnit,
                Rate = c.Rate,
                BasicAmount = c.BasicAmount,
                TaxRate = c.TaxRate,
                TaxAmount = c.TaxAmount,
                TotalAmount = c.TotalAmount,
                IsRefundable = c.IsRefundable,
                IncludeInSchedule = c.IncludeInSchedule,
                DueLabel = c.DueLabel,
            }).ToList(),
        };

        revision.QuoteNumber = $"{Code("QT")}-R{revision.Version}";

        // Only the hand-built quotations are recalculated from their lines.
        //
        // Recalculate reads DiscountPercent and TaxPercent as percentages and
        // divides by a hundred; a quotation the builder priced stores them as
        // fractions, so running it over one turns 12% GST into 0.12% and leaves
        // the revision worth roughly its own subtotal. The priced figures are
        // copied verbatim instead, which is what a revision should start as.
        if (revision.PaymentPlanId is null)
        {
            Recalculate(revision);
        }

        Db.Quotations.Add(revision);
        await Db.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetOne), new { id = revision.Id }, ToDto(revision));
    }

    /// <summary>Status transitions that carry side effects — sending, accepting, rejecting.</summary>
    [HttpPatch("{id:int}/status")]
    public async Task<ActionResult<QuotationDto>> ChangeStatus(
        int id,
        QuotationStatusRequest request,
        CancellationToken ct)
    {
        var quotation = await Db.Quotations
            .Include(q => q.Branch).Include(q => q.Owner).Include(q => q.Lines)
            .FirstOrDefaultAsync(q => q.Id == id, ct)
            ?? throw ApiException.NotFound("Quotation");

        var status = Require(request.Status, QuotationStatuses.All, "status");

        if (status == QuotationStatuses.Rejected && string.IsNullOrWhiteSpace(request.Reason))
        {
            throw ApiException.BadRequest("A reason is required when rejecting a quotation.");
        }

        quotation.Status = status;
        quotation.RejectionReason = status == QuotationStatuses.Rejected ? request.Reason : null;

        if (status == QuotationStatuses.Sent) quotation.SentAt ??= DateTime.UtcNow;
        if (status is QuotationStatuses.Accepted or QuotationStatuses.Rejected)
        {
            quotation.RespondedAt = DateTime.UtcNow;
        }

        // Acceptance moves the deal, not just the document.
        if (status == QuotationStatuses.Accepted && quotation.OpportunityId is int opportunityId)
        {
            var opportunity = await Db.Opportunities.FirstOrDefaultAsync(o => o.Id == opportunityId, ct);
            if (opportunity is not null && OpportunityStages.Open.Contains(opportunity.Stage))
            {
                opportunity.Stage = OpportunityStages.Negotiation;
                opportunity.StageEnteredAt = DateTime.UtcNow;
                opportunity.Probability = Math.Max(opportunity.Probability, 75);
                opportunity.ForecastCategory = ForecastCategories.Commit;
            }
        }

        await Db.SaveChangesAsync(ct);
        return Ok(ToDto(quotation));
    }

    // Deletion lives on QuotationBuilderController: removing the row is the
    // easy half, and the half that matters is withdrawing any open approval and
    // handing the unit back. Two actions on the same route was an ambiguous
    // match that surfaced as a 500 on every delete.

    /* ------------------------------------------------------------------ *
     * Money
     * ------------------------------------------------------------------ */

    private void Apply(Quotation quotation, QuotationInput input)
    {
        quotation.Title = input.Title.Trim();
        quotation.ContactId = input.ContactId;
        quotation.LeadId = input.LeadId;
        quotation.OpportunityId = input.OpportunityId;
        quotation.ProjectId = input.ProjectId;
        quotation.UnitId = input.UnitId;
        quotation.CustomerName = input.CustomerName.Trim();
        quotation.CustomerEmail = input.CustomerEmail;
        quotation.CustomerPhone = input.CustomerPhone;
        quotation.BillingAddress = input.BillingAddress;
        quotation.Status = Require(input.Status, QuotationStatuses.All, "status");
        quotation.IssueDate = input.IssueDate;
        quotation.ValidUntil = input.ValidUntil;
        quotation.DiscountPercent = Math.Clamp(input.DiscountPercent, 0m, 100m);
        quotation.TaxPercent = Math.Clamp(input.TaxPercent, 0m, 100m);
        quotation.PaymentTerms = input.PaymentTerms;
        quotation.Notes = input.Notes;
        quotation.TermsAndConditions = input.TermsAndConditions;
        quotation.RejectionReason = input.RejectionReason;

        var sortOrder = 0;
        foreach (var line in input.Lines)
        {
            quotation.Lines.Add(new QuotationLine
            {
                Description = line.Description.Trim(),
                Category = line.Category,
                Quantity = Math.Max(0m, line.Quantity),
                Unit = line.Unit,
                UnitPrice = Math.Max(0m, line.UnitPrice),
                DiscountPercent = Math.Clamp(line.DiscountPercent, 0m, 100m),
                SortOrder = sortOrder++,
            });
        }

        Recalculate(quotation);
    }

    /// <summary>
    /// Line discount first, then the header discount on the subtotal, then tax
    /// on what's left. Rounding happens once per stage so the printed document
    /// and the stored total always agree to the paisa.
    /// </summary>
    private static void Recalculate(Quotation quotation)
    {
        foreach (var line in quotation.Lines)
        {
            var gross = line.Quantity * line.UnitPrice;
            line.LineTotal = decimal.Round(gross * (1 - (line.DiscountPercent / 100m)), 2);
        }

        quotation.Subtotal = decimal.Round(quotation.Lines.Sum(l => l.LineTotal), 2);
        quotation.DiscountAmount = decimal.Round(
            quotation.Subtotal * quotation.DiscountPercent / 100m, 2);

        var taxable = quotation.Subtotal - quotation.DiscountAmount;
        quotation.TaxAmount = decimal.Round(taxable * quotation.TaxPercent / 100m, 2);
        quotation.Total = decimal.Round(taxable + quotation.TaxAmount, 2);
    }

}

public record QuotationStatusRequest(string Status, string? Reason);
