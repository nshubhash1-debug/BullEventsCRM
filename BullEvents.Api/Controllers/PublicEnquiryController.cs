using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

public record PublicEnquiryRequest(
    string Name,
    string? PartnerName,
    string? Phone,
    string? Email,
    string? EventType,
    DateTime? EventDate,
    int? GuestCount,
    decimal? BudgetMax,
    string? PlanningPackage,
    string? Notes,
    int? PreferredUnitId = null,
    string? EventSlot = null);

public record PublicQuestionnaireRequest(
    DateTime? EventDate,
    int? GuestCount,
    decimal? BudgetMax,
    string? ServicesNeeded,
    string? PartnerName,
    string? Notes);

[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicEnquiryController(
    AppDbContext db,
    TenantContext tenant,
    SpaceBookingService spaceBookings) : ControllerBase
{
    [HttpGet("{slug}/availability")]
    public async Task<IActionResult> Availability(
        string slug,
        [FromQuery] DateOnly eventDate,
        [FromQuery] DateOnly? eventEndDate,
        [FromQuery] string? slot,
        [FromQuery] int? unitId,
        CancellationToken ct)
    {
        var company = await db.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Slug == slug && c.Status == "Active", ct);
        if (company is null) return NotFound(new { message = "Unknown workspace." });

        tenant.ResolveSystem(company.Id);

        var conflicts = await spaceBookings.FindConflictsAsync(
            projectId: null,
            unitId is int uid ? [uid] : null,
            eventDate,
            eventEndDate ?? eventDate,
            slot ?? EventSlots.Evening,
            ignoreGroupRef: null,
            ct);

        return Ok(new
        {
            available = conflicts.Count == 0,
            conflicts,
        });
    }

    [HttpPost("{slug}/enquiries")]
    public async Task<IActionResult> Capture(string slug, PublicEnquiryRequest request, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(request.Name)
            || (string.IsNullOrWhiteSpace(request.Phone) && string.IsNullOrWhiteSpace(request.Email)))
        {
            return BadRequest(new { message = "Name and a phone or email are required." });
        }

        var company = await db.Companies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Slug == slug && c.Status == "Active", ct);

        if (company is null) return NotFound(new { message = "Unknown workspace." });

        tenant.ResolveSystem(company.Id);

        var branchId = await db.Branches.OrderBy(b => b.Id).Select(b => b.Id).FirstOrDefaultAsync(ct);
        if (branchId == 0) return BadRequest(new { message = "This workspace has no branches." });

        object? availability = null;
        if (request.EventDate is DateTime eventDt)
        {
            var day = DateOnly.FromDateTime(eventDt);
            var conflicts = await spaceBookings.FindConflictsAsync(
                projectId: null,
                request.PreferredUnitId is int uid ? [uid] : null,
                day,
                day,
                request.EventSlot ?? EventSlots.Evening,
                ignoreGroupRef: null,
                ct);

            availability = new { available = conflicts.Count == 0, conflicts };
        }

        var lead = new Lead
        {
            CompanyId = company.Id,
            BranchId = branchId,
            Name = request.Name.Trim(),
            PartnerName = string.IsNullOrWhiteSpace(request.PartnerName) ? null : request.PartnerName.Trim(),
            Phone = request.Phone,
            Email = request.Email,
            Source = LeadSources.Website,
            Stage = LeadStages.New,
            Priority = LeadPriorities.High,
            EventType = request.EventType,
            EventCategory = request.EventType is null ? null : EventTypes.CategoryOf(request.EventType),
            EventDate = request.EventDate,
            GuestCount = request.GuestCount,
            BudgetMax = request.BudgetMax,
            PlanningPackage = request.PlanningPackage,
            Notes = request.Notes,
            SlaDueAt = DateTime.UtcNow.AddHours(1),
            AutoAckAt = DateTime.UtcNow,
            QuestionnaireStatus = QuestionnaireStatuses.None,
        };

        db.Leads.Add(lead);
        await db.SaveChangesAsync(ct);

        db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.Created,
            Remarks = "Captured from the public enquiry form.",
            ActorName = "Website",
        });
        db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.Email,
            Remarks = "Automatic acknowledgement: a planner will reply shortly.",
            ActorName = "System",
        });
        await db.SaveChangesAsync(ct);

        return Created($"/enquire/{slug}", new
        {
            id = lead.Id,
            stage = lead.Stage,
            availability,
        });
    }

    [HttpGet("questionnaire/{token}")]
    public async Task<IActionResult> Questionnaire(string token, CancellationToken ct)
    {
        var lead = await db.Leads.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.QuestionnaireToken == token, ct);

        if (lead is null) return NotFound();

        return Ok(new
        {
            lead.Name,
            lead.PartnerName,
            lead.EventType,
            lead.EventDate,
            lead.GuestCount,
            lead.BudgetMax,
            lead.ServicesNeeded,
            completed = lead.QuestionnaireStatus == QuestionnaireStatuses.Completed,
        });
    }

    [HttpPost("questionnaire/{token}")]
    public async Task<IActionResult> CompleteQuestionnaire(
        string token,
        PublicQuestionnaireRequest request,
        CancellationToken ct)
    {
        var lead = await db.Leads.IgnoreQueryFilters()
            .FirstOrDefaultAsync(l => l.QuestionnaireToken == token, ct);

        if (lead is null) return NotFound();

        tenant.ResolveSystem(lead.CompanyId);

        if (request.EventDate is not null) lead.EventDate = request.EventDate;
        if (request.GuestCount is not null) lead.GuestCount = request.GuestCount;
        if (request.BudgetMax is not null) lead.BudgetMax = request.BudgetMax;
        if (!string.IsNullOrWhiteSpace(request.ServicesNeeded)) lead.ServicesNeeded = request.ServicesNeeded;
        if (!string.IsNullOrWhiteSpace(request.PartnerName)) lead.PartnerName = request.PartnerName;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            lead.Notes = string.IsNullOrWhiteSpace(lead.Notes) ? request.Notes : $"{lead.Notes}\n{request.Notes}";

        lead.QuestionnaireStatus = QuestionnaireStatuses.Completed;
        lead.QuestionnaireCompletedAt = DateTime.UtcNow;
        lead.LastActivityAt = DateTime.UtcNow;

        if (lead.Stage is LeadStages.New or LeadStages.Contacted)
            lead.Stage = LeadStages.Qualified;

        db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.Questionnaire,
            Remarks = "Couple completed the qualification questionnaire.",
            ActorName = lead.Name,
        });

        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }
}
