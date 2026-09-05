using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The customer-facing view of a shared quotation.
///
/// No authentication — the unguessable token in the URL is the credential.
/// This keeps the link shareable by WhatsApp, email, or SMS without the
/// customer needing an account.
/// </summary>
[ApiController]
[Route("api/public/quotation")]
public class PublicQuotationController(AppDbContext db) : ControllerBase
{
    /// <summary>
    /// Returns the quotation the token points at, or 404 if the link is dead.
    ///
    /// Bumps the view count on every hit — the rep can see on the detail sheet
    /// how many times the customer opened it, and whether they have.
    /// </summary>
    [HttpGet("{token}")]
    public async Task<ActionResult<PublicQuotationDto>> View(string token, CancellationToken ct)
    {
        var link = await db.QuotationShareLinks
            .FirstOrDefaultAsync(l => l.Token == token, ct);

        if (link is null || !link.IsActive || link.ExpiresAt < DateTime.UtcNow)
            return NotFound(new { message = "This link is no longer valid." });

        var q = await db.Quotations.AsNoTracking()
            .Include(x => x.Milestones)
            .Include(x => x.Charges)
            .Include(x => x.Project)
            // Split: joined, the schedule and the charge heads multiply against
            // each other and the customer's own page is the slowest read here.
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == link.QuotationId && !x.IsDeleted, ct);

        if (q is null)
            return NotFound(new { message = "Quotation not found." });

        // Unfiltered: this endpoint is anonymous, so there is no ambient tenant
        // for the query filter to apply. The quotation's own CompanyId is what
        // decides whose branding the buyer sees.
        var seller = await db.Companies
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstAsync(c => c.Id == q.CompanyId, ct);

        // Track the view.
        link.ViewCount += 1;
        link.LastViewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var isExpired = q.ValidUntil < DateTime.UtcNow;
        var canRespond = !isExpired
            && link.RespondedAt is null
            && q.Status is not (QuotationStatuses.Accepted or QuotationStatuses.Rejected);

        return Ok(new PublicQuotationDto(
            q.QuoteNumber, q.Title, q.Version, q.Status,
            q.CustomerName, q.Project?.Name, q.TowerName,
            q.UnitNumber, q.UnitType,
            q.SaleableArea, q.CarpetArea, q.BuiltUpArea,
            q.RateCardLabel, q.RatePerSqft, q.PlcPerSqft, q.EffectiveRatePerSqft,
            q.DiscountPercent,
            q.Subtotal, q.TaxPercent, q.TaxAmount, q.Total,
            q.ChargesTotal, q.GrandTotal == 0 ? q.Total : q.GrandTotal,
            q.GrandTotalInWords ?? q.AmountInWords,
            q.PaymentPlanName,
            q.Charges.OrderBy(c => c.SortOrder)
                .Select(c => new QuoteChargeDto(
                    c.ChargeHeadId, c.SortOrder, c.Group, c.Name, c.Basis,
                    c.Quantity, c.QuantityUnit, c.Rate, c.BasicAmount,
                    c.TaxRate, c.TaxAmount, c.TotalAmount,
                    c.IsRefundable, c.IncludeInSchedule, c.DueLabel))
                .ToList(),
            q.Milestones.OrderBy(m => m.SortOrder)
                .Select(m => new QuoteMilestoneDto(
                    m.SortOrder, m.Label, m.Percent,
                    m.BasicAmount, m.TaxAmount, m.TotalAmount, m.DueDate))
                .ToList(),
            // The same offer the PDF carries, rebuilt from the quotation's own
            // snapshot so the online copy and the printed one cannot diverge.
            QuotationBuilderController.PublicAnnexure(q),
            q.IssueDate, q.ValidUntil, q.Notes, isExpired, canRespond,
            seller.Name, seller.BrandColor, seller.LogoUrl));
    }

    /// <summary>
    /// The customer accepts or declines through the link.
    ///
    /// One response per link — hitting accept twice does not double-book.
    /// The quotation itself is moved to Accepted only on accept; a decline
    /// through the link does not hard-reject it because the sales desk may
    /// still want to negotiate.
    /// </summary>
    [HttpPost("{token}/respond")]
    public async Task<IActionResult> Respond(
        string token, PublicQuotationResponseRequest request, CancellationToken ct)
    {
        var link = await db.QuotationShareLinks
            .FirstOrDefaultAsync(l => l.Token == token, ct);

        if (link is null || !link.IsActive || link.ExpiresAt < DateTime.UtcNow)
            return NotFound(new { message = "This link is no longer valid." });

        if (link.RespondedAt is not null)
            return Conflict(new { message = "You have already responded to this quotation." });

        var q = await db.Quotations
            .FirstOrDefaultAsync(x => x.Id == link.QuotationId && !x.IsDeleted, ct);

        if (q is null)
            return NotFound(new { message = "Quotation not found." });

        if (q.ValidUntil < DateTime.UtcNow)
            return Conflict(new { message = "This quotation has expired." });

        if (q.Status is QuotationStatuses.Accepted or QuotationStatuses.Rejected)
            return Conflict(new { message = $"This quotation is already {q.Status.ToLowerInvariant()}." });

        var status = request.Status == "Accepted" ? "Accepted" : "Declined";

        link.RespondedAt = DateTime.UtcNow;
        link.ResponseStatus = status;
        link.CustomerComment = request.Comment;

        db.QuotationActivities.Add(new QuotationActivity
        {
            QuotationId = q.Id,
            Type = status == "Accepted" ? QuotationActivityTypes.Accepted : QuotationActivityTypes.Declined,
            Description = $"Customer responded via link: {status}."
                + (string.IsNullOrWhiteSpace(request.Comment) ? "" : $" Comment: {request.Comment.Trim()}"),
            ActorName = q.CustomerName,
        });

        // Only an explicit accept moves the quotation status; a decline through
        // the portal is informational — the rep may still chase.
        if (status == "Accepted")
        {
            q.Status = QuotationStatuses.Accepted;
            q.RespondedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync(ct);

        return Ok(new { message = $"Thank you. Your response ({status}) has been recorded." });
    }
}
