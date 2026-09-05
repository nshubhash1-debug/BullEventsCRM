using System.Globalization;
using System.Text.Json;
using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Ml;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

[ApiController]
[Route("api/leads")]
[Authorize]
[SecuredBy(SecuredObjects.Lead)]
public class LeadsController(
    AppDbContext db,
    LeadScoringService scoring,
    EntitlementService entitlements,
    CustomFieldService customFields,
    AssignmentRuleEngine assignment,
    DuplicateRuleChecker duplicates,
    WebhookDispatcher webhooks) : CrmControllerBase(db)
{
    /// <summary>
    /// The queryable surface of a lead. Anything the advanced filter can reach
    /// is declared here and nowhere else — adding a filterable field is one
    /// line, and removing one closes it off everywhere at once.
    /// </summary>
    internal static readonly FieldMap<Lead> Fields = new FieldMap<Lead>()
        .Text("name", "Lead name", searchable: true)
        .Text("companyName", "Company", searchable: true)
        .Text("phone", "Mobile", searchable: true)
        .Text("phone2", "Alternate mobile")
        .Text("email", "Email", searchable: true)
        .Text("address", "Address")
        .Select("city", "City")
        .Select("state", "State")
        .Text("pincode", "Pincode")
        .Select("country", "Country")
        .Select("zone", "Zone")
        .Date("dateOfBirth", "Date of birth")
        .Date("anniversaryDate", "Anniversary")
        .Select("maritalStatus", "Marital status")
        .Text("fatherOrSpouseName", "Father / spouse name")
        .Select("occupation", "Occupation")
        .Text("designation", "Designation")
        .Select("nationality", "Nationality")
        .Text("partnerName", "Partner / guest of honour")
        .Text("partnerPhone", "Partner phone")
        .Text("partnerEmail", "Partner email")
        .Select("inquirerRole", "Inquirer role")
        .Select("source", "Source")
        .Select("stage", "Stage")
        .Select("subStatus", "Sub-status")
        .Select("priority", "Priority")
        .Text("notes", "Notes", searchable: true)
        .Number("budgetMin", "Budget from")
        .Number("budgetMax", "Budget to")
        .Select("eventType", "Event type")
        .Select("eventCategory", "Event category")
        .Date("eventDate", "Event date")
        .Date("eventEndDate", "Event end date")
        .Select("eventSlot", "Slot")
        .Bool("isDateFlexible", "Date flexible")
        .Number("guestCount", "Guest count")
        .Number("ceremonyGuestCount", "Ceremony guests")
        .Number("receptionGuestCount", "Reception guests")
        .Select("planningPackage", "Planning package")
        .Select("venueStatus", "Venue status")
        .Select("portalName", "Listing portal")
        .Select("ceremonyStyle", "Ceremony style")
        .Date("consultAt", "Consult at")
        .Select("questionnaireStatus", "Questionnaire")
        .Text("functions", "Functions", searchable: true)
        .Text("servicesNeeded", "Services needed", searchable: true)
        .Select("mealPreference", "Meal preference")
        .Select("projectName", "Preferred venue", "InterestedProject.Name")
        .Text("preferredLocality", "Preferred location", searchable: true)
        .Select("paymentMode", "Payment mode")
        .Select("campaign", "Campaign")
        .Select("utmSource", "UTM source")
        .Select("utmMedium", "UTM medium")
        .Text("referredBy", "Referred by")
        .Text("tags", "Tags", searchable: true)
        .Number("cachedScore", "AI score")
        .Select("cachedBand", "AI band")
        .Date("lastActivityAt", "Last activity")
        .Date("slaDueAt", "SLA due")
        .Date("firstResponseAt", "First response")
        .Bool("isConverted", "Converted")
        .Date("convertedAt", "Converted on")
        .Select("lossReason", "Loss reason")
        .Select("ownerName", "Owner", "Owner.Name")
        .Select("supportingManagerName", "Supporting manager", "SupportingManager.Name")
        .Select("branchName", "Branch", "Branch.Name")
        .Number("ownerId", "Owner ID")
        .Number("supportingManagerId", "Supporting manager ID")
        .Number("branchId", "Branch ID")
        .Date("createdAt", "Created")
        .Date("updatedAt", "Last modified");

    private IQueryable<Lead> Base() => Db.Leads
        .Include(l => l.Branch)
        .Include(l => l.Owner)
        .Include(l => l.SupportingManager)
        .Include(l => l.InterestedProject)
        .AsNoTracking();

    private static LeadDto ToDto(Lead l) => new()
    {
        Id = l.Id,

        Salutation = l.Salutation,
        Name = l.Name,
        CompanyName = l.CompanyName,
        Phone = l.Phone,
        Phone2 = l.Phone2,
        Email = l.Email,
        Address = l.Address,
        City = l.City,
        State = l.State,
        Pincode = l.Pincode,
        Country = l.Country,
        Zone = l.Zone,
        DateOfBirth = l.DateOfBirth,
        AnniversaryDate = l.AnniversaryDate,
        MaritalStatus = l.MaritalStatus,
        FatherOrSpouseName = l.FatherOrSpouseName,
        Occupation = l.Occupation,
        Designation = l.Designation,
        Nationality = l.Nationality,
        PartnerName = l.PartnerName,
        PartnerPhone = l.PartnerPhone,
        PartnerEmail = l.PartnerEmail,
        InquirerRole = l.InquirerRole,

        Source = l.Source,
        Stage = l.Stage,
        SubStatus = l.SubStatus,
        Priority = l.Priority,
        BranchId = l.BranchId,
        BranchName = l.Branch?.Name ?? "—",
        OwnerId = l.OwnerId,
        OwnerName = l.Owner?.Name,
        SupportingManagerId = l.SupportingManagerId,
        SupportingManagerName = l.SupportingManager?.Name,
        Notes = l.Notes,

        BudgetMin = l.BudgetMin,
        BudgetMax = l.BudgetMax,
        EventType = l.EventType,
        EventCategory = l.EventCategory,
        EventDate = l.EventDate,
        EventEndDate = l.EventEndDate,
        EventSlot = l.EventSlot,
        IsDateFlexible = l.IsDateFlexible,
        GuestCount = l.GuestCount,
        Functions = l.Functions,
        ServicesNeeded = l.ServicesNeeded,
        MealPreference = l.MealPreference,
        PreferredLocality = l.PreferredLocality,
        PaymentMode = l.PaymentMode,
        InterestedProjectId = l.InterestedProjectId,
        InterestedProjectName = l.InterestedProject?.Name,
        VenueStatus = l.VenueStatus,
        PlanningPackage = l.PlanningPackage,
        CeremonyGuestCount = l.CeremonyGuestCount,
        ReceptionGuestCount = l.ReceptionGuestCount,
        PortalName = l.PortalName,
        CeremonyStyle = l.CeremonyStyle,
        ConsultAt = l.ConsultAt,
        QuestionnaireStatus = l.QuestionnaireStatus,
        QuestionnaireSentAt = l.QuestionnaireSentAt,
        QuestionnaireCompletedAt = l.QuestionnaireCompletedAt,
        QuestionnaireToken = l.QuestionnaireToken,
        AutoAckAt = l.AutoAckAt,
        DaysToEvent = DaysToEvent(l.EventDate),

        Campaign = l.Campaign,
        UtmSource = l.UtmSource,
        UtmMedium = l.UtmMedium,
        ReferredBy = l.ReferredBy,
        Tags = l.Tags,

        Score = l.CachedScore,
        Band = l.CachedBand,
        ScoredAt = l.ScoredAt,

        LastActivityAt = l.LastActivityAt,
        SlaDueAt = l.SlaDueAt,
        FirstResponseAt = l.FirstResponseAt,
        SlaState = SlaState(l),

        IsConverted = l.IsConverted,
        ConvertedAt = l.ConvertedAt,
        ConvertedContactId = l.ConvertedContactId,
        ConvertedOpportunityId = l.ConvertedOpportunityId,
        ConvertedBookingId = l.ConvertedBookingId,
        LossReason = l.LossReason,

        CustomFields = CustomFieldService.Read(l.CustomFields),

        CreatedAt = l.CreatedAt,
        UpdatedAt = l.UpdatedAt,
    };

    private async Task RequireSupportingManagerAsync(int? userId, CancellationToken ct)
    {
        if (userId is null) return;

        var exists = await Db.Users.AnyAsync(
            u => u.Id == userId && u.CompanyId == Db.Tenant.CompanyId, ct);

        if (!exists) throw ApiException.BadRequest("Select a valid supporting manager.");
    }

    /// <summary>
    /// Fills in the fields that live on other tables — activity count, the
    /// newest timeline entry, and the latest site and OBM visit.
    ///
    /// Deliberately a second round-trip rather than correlated subqueries in
    /// the main projection: four bounded queries for the whole page instead of
    /// three per row, and the visit tables stay the single source of truth, so
    /// a rescheduled visit reads correctly without a backfill.
    /// </summary>
    private async Task<List<LeadDto>> EnrichAsync(
        IReadOnlyList<LeadDto> items, CancellationToken ct)
    {
        if (items.Count == 0) return [];

        var ids = items.Select(i => i.Id).ToList();

        // Count and newest-entry id in one grouped query; that row's detail is
        // then read by primary key. Ids are monotonic, so Max(Id) is the newest
        // entry even when two share a timestamp.
        var activity = await Db.LeadActivities
            .Where(a => ids.Contains(a.LeadId))
            .GroupBy(a => a.LeadId)
            .Select(g => new { LeadId = g.Key, Count = g.Count(), NewestId = g.Max(a => a.Id) })
            .ToDictionaryAsync(x => x.LeadId, ct);

        var newestIds = activity.Values.Select(a => a.NewestId).ToList();
        var newest = await Db.LeadActivities
            .Where(a => newestIds.Contains(a.Id))
            .Select(a => new { a.LeadId, a.Type, a.Remarks, a.FromStage, a.ToStage, a.CreatedAt })
            .ToDictionaryAsync(a => a.LeadId, ct);

        // Visits are read as rows and grouped in memory: a page carries at most
        // a few hundred of them, and it avoids leaning on the provider to
        // translate "latest row per group".
        var siteVisits = (await Db.SiteVisits
                .Where(v => v.LeadId != null && ids.Contains(v.LeadId!.Value))
                .Select(v => new { LeadId = v.LeadId!.Value, v.Status, v.ScheduledAt })
                .ToListAsync(ct))
            .GroupBy(v => v.LeadId)
            .ToDictionary(g => g.Key, g => new
            {
                Count = g.Count(),
                Latest = g.OrderByDescending(v => v.ScheduledAt).First(),
            });

        var obmVisits = (await Db.ObmVisits
                .Where(v => v.LeadId != null && ids.Contains(v.LeadId!.Value))
                .Select(v => new { LeadId = v.LeadId!.Value, v.Status, v.ScheduledAt })
                .ToListAsync(ct))
            .GroupBy(v => v.LeadId)
            .ToDictionary(g => g.Key, g => new
            {
                Count = g.Count(),
                Latest = g.OrderByDescending(v => v.ScheduledAt).First(),
            });

        return items.Select(item =>
        {
            var site = siteVisits.GetValueOrDefault(item.Id);
            var obm = obmVisits.GetValueOrDefault(item.Id);
            var last = newest.GetValueOrDefault(item.Id);

            return item with
            {
                ActivityCount = activity.GetValueOrDefault(item.Id)?.Count ?? 0,

                // The denormalised stamp on the lead is the fast path, but it
                // was only ever written by the API's own write paths — anything
                // imported or backfilled has a timeline and no stamp. Falling
                // back to the newest entry keeps the column honest.
                LastActivityAt = item.LastActivityAt ?? last?.CreatedAt,

                LastActivityType = last?.Type,
                LastActivitySummary = last is null
                    ? null
                    : last.Type == LeadActivityTypes.StageChange && last.ToStage is not null
                        ? $"{last.FromStage ?? "—"} → {last.ToStage}"
                        : last.Remarks,

                SiteVisitStatus = site?.Latest.Status,
                SiteVisitAt = site?.Latest.ScheduledAt,
                SiteVisitCount = site?.Count ?? 0,

                ObmStatus = obm?.Latest.Status,
                ObmVisitAt = obm?.Latest.ScheduledAt,
                ObmVisitCount = obm?.Count ?? 0,
            };
        }).ToList();
    }

    /// <summary>
    /// Computed rather than stored, so it can never be stale: a lead is
    /// breached the moment its SLA passes with no first response logged.
    /// </summary>
    private static string SlaState(Lead lead)
    {
        if (lead.FirstResponseAt is not null) return "Met";
        if (lead.SlaDueAt is null) return "None";

        var remaining = (lead.SlaDueAt.Value - DateTime.UtcNow).TotalMinutes;

        return remaining switch
        {
            < 0 => "Breached",
            < 60 => "AtRisk",
            _ => "OnTrack"
        };
    }

    /// <summary>
    /// Whole days from today to the event. Negative once the date has passed.
    ///
    /// Computed on read rather than stored because it changes every midnight,
    /// and it is what the list sorts by: an events desk works its pipeline in
    /// date order, not in created order — the wedding in three weeks outranks
    /// the enquiry that came in this morning for next December.
    /// </summary>
    private static int? DaysToEvent(DateTime? eventDate) =>
        eventDate is null
            ? null
            : (int)(eventDate.Value.Date - DateTime.UtcNow.Date).TotalDays;

    /* ------------------------------------------------------------------ *
     * Metadata + query
     * ------------------------------------------------------------------ */

    [HttpGet("fields")]
    public async Task<ActionResult<IReadOnlyList<FilterFieldDto>>> GetFields(CancellationToken ct)
    {
        var cities = await Db.Leads
            .Where(l => l.City != null && l.City != "")
            .Select(l => l.City!).Distinct().OrderBy(c => c).Take(200).ToListAsync(ct);

        var owners = await Db.Users
            .Where(u => u.CompanyId == Db.Tenant.CompanyId && u.IsActive)
            .Select(u => u.Name).OrderBy(n => n).ToListAsync(ct);

        var branches = await Db.Branches
            .Where(b => b.CompanyId == Db.Tenant.CompanyId)
            .Select(b => b.Name).OrderBy(n => n).ToListAsync(ct);

        var campaigns = await Db.Leads
            .Where(l => l.Campaign != null)
            .Select(l => l.Campaign!).Distinct().OrderBy(c => c).Take(100).ToListAsync(ct);

        var localities = await Db.Leads
            .Where(l => l.PreferredLocality != null)
            .Select(l => l.PreferredLocality!).Distinct().OrderBy(l => l).Take(100).ToListAsync(ct);

        var lossReasons = await Db.Leads
            .Where(l => l.LossReason != null)
            .Select(l => l.LossReason!).Distinct().ToListAsync(ct);

        return Ok(DescribeFields(Fields, new Dictionary<string, IReadOnlyList<FilterOptionDto>>
        {
            ["stage"] = Options(LeadStages.All),
            ["source"] = Options(LeadSources.All),
            ["priority"] = Options(LeadPriorities.All),
            ["eventType"] = Options(EventTypes.All),
            ["eventCategory"] = Options(EventCategories.All),
            ["eventSlot"] = Options(EventSlots.All),
            ["mealPreference"] = Options(MealPreferences.All),
            ["paymentMode"] = Options(PaymentPreferences.ForEvents),
            ["planningPackage"] = Options(PlanningPackages.All),
            ["venueStatus"] = Options(EnquiryVenueStatuses.All),
            ["inquirerRole"] = Options(InquirerRoles.All),
            ["portalName"] = Options(EventPortals.All),
            ["ceremonyStyle"] = Options(CeremonyStyles.All),
            ["questionnaireStatus"] = Options(QuestionnaireStatuses.All),
            ["cachedBand"] = Options("Hot", "Warm", "Cool", "Cold"),
            ["city"] = Options([.. cities]),
            ["ownerName"] = Options([.. owners]),
            ["branchName"] = Options([.. branches]),
            ["campaign"] = Options([.. campaigns]),
            ["utmSource"] = Options("google", "meta", "linkedin", "direct", "portal", "referral"),
            ["utmMedium"] = Options("cpc", "organic", "email", "social", "affiliate"),
            ["lossReason"] = Options([.. lossReasons]),
            ["country"] = Options("India", "UAE", "Singapore", "United Kingdom", "United States"),
        }, new Dictionary<string, string>
        {
            ["name"] = "Identity",
            ["companyName"] = "Identity",
            ["phone"] = "Reach",
            ["phone2"] = "Reach",
            ["email"] = "Reach",
            ["address"] = "Location",
            ["city"] = "Location",
            ["country"] = "Location",
            ["eventType"] = "Event",
            ["eventCategory"] = "Event",
            ["eventDate"] = "Event",
            ["eventEndDate"] = "Event",
            ["eventSlot"] = "Event",
            ["isDateFlexible"] = "Event",
            ["guestCount"] = "Event",
            ["functions"] = "Event",
            ["servicesNeeded"] = "Event",
            ["mealPreference"] = "Event",
            ["preferredLocality"] = "Event",
            ["projectName"] = "Event",
            ["budgetMin"] = "Budget",
            ["budgetMax"] = "Budget",
            ["paymentMode"] = "Budget",
            ["stage"] = "Pipeline",
            ["priority"] = "Pipeline",
            ["source"] = "Attribution",
            ["campaign"] = "Attribution",
            ["utmSource"] = "Attribution",
            ["utmMedium"] = "Attribution",
            ["referredBy"] = "Attribution",
            ["cachedScore"] = "Intelligence",
            ["cachedBand"] = "Intelligence",
            ["lastActivityAt"] = "Lifecycle",
            ["slaDueAt"] = "Lifecycle",
            ["firstResponseAt"] = "Lifecycle",
            ["isConverted"] = "Lifecycle",
            ["convertedAt"] = "Lifecycle",
            ["lossReason"] = "Lifecycle",
        }));
    }

    /// <summary>
    /// The list view's single endpoint: filter tree, sort, facets and page all
    /// resolved in SQL. Nothing is filtered client-side, so the counts on screen
    /// describe the whole dataset rather than the current page.
    /// </summary>
    [PermissionAction(ObjectAction.View)]
    [HttpPost("query")]
    public async Task<ActionResult<PagedResult<LeadDto>>> Query(QueryRequest request, CancellationToken ct)
    {
        var now = DateTime.UtcNow;

        var result = await RunQueryAsync(
            Base(),
            request,
            Fields,
            l => ToDto(l),
            defaultSortPath: "CreatedAt",
            aggregates: async filtered => new Dictionary<string, decimal>
            {
                ["total"] = await filtered.CountAsync(ct),
                ["unassigned"] = await filtered.CountAsync(l => l.OwnerId == null, ct),
                ["hot"] = await filtered.CountAsync(l => l.Priority == LeadPriorities.Hot, ct),
                ["breached"] = await filtered.CountAsync(l =>
                    l.FirstResponseAt == null && l.SlaDueAt != null && l.SlaDueAt < now, ct),
                ["pipelineValue"] = await filtered.SumAsync(l => l.BudgetMax ?? 0m, ct),
                ["averageScore"] = decimal.Round(
                    (decimal)(await filtered.AverageAsync(l => (double?)l.CachedScore, ct) ?? 0), 1),
            },
            cancellationToken: ct);

        result.Items = await EnrichAsync(result.Items, ct);

        return Ok(result);
    }

    /// <summary>Enquiry board: open leads grouped by sales stage, with time-in-stage.</summary>
    [PermissionAction(ObjectAction.View)]
    [HttpGet("board")]
    public async Task<ActionResult<LeadBoardDto>> Board(
        [FromQuery] string? search,
        [FromQuery] string? month,
        CancellationToken ct)
    {
        var query = await ScopedAsync(Base(), ct);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(l =>
                l.Name.Contains(term)
                || (l.Phone != null && l.Phone.Contains(term))
                || (l.Email != null && l.Email.Contains(term)));
        }

        if (DateTime.TryParse($"{month}-01", out var monthStart))
        {
            var monthEnd = monthStart.AddMonths(1);
            query = query.Where(l => l.EventDate >= monthStart && l.EventDate < monthEnd);
        }

        var open = await query
            .Where(l => l.Stage != LeadStages.Booked
                && l.Stage != LeadStages.Lost
                && l.Stage != LeadStages.Nurture)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        var items = await EnrichAsync(open.Select(ToDto).ToList(), ct);

        var columns = LeadStages.Pipeline.Select(stage =>
        {
            var rows = items.Where(i => i.Stage == stage).ToList();
            var days = rows
                .Select(r => Math.Max(0, (now - r.UpdatedAt).TotalDays))
                .DefaultIfEmpty(0)
                .Average();

            return new LeadBoardColumnDto(
                stage,
                LeadStages.Label(stage),
                rows.Count,
                rows.Sum(r => r.BudgetMax ?? 0m),
                days,
                rows);
        }).ToList();

        return Ok(new LeadBoardDto(columns, items.Count, columns.Sum(c => c.Value)));
    }

    [PermissionAction(ObjectAction.View)]
    [HttpGet("availability")]
    public async Task<ActionResult<IReadOnlyList<LeadConflictDto>>> Availability(
        [FromQuery] DateTime date,
        CancellationToken ct)
    {
        var start = date.Date.AddDays(-((int)date.DayOfWeek + 2) % 7);
        if (start > date.Date) start = start.AddDays(-7);
        var end = start.AddDays(3);

        var leads = await Db.Leads.AsNoTracking()
            .Where(l => l.EventDate != null
                && l.EventDate >= start
                && l.EventDate < end
                && l.Stage != LeadStages.Lost)
            .OrderBy(l => l.EventDate)
            .Select(l => new LeadConflictDto(
                l.Id,
                l.Name,
                l.Stage,
                l.EventDate,
                l.EventEndDate,
                l.GuestCount,
                l.InterestedProject != null ? l.InterestedProject.Name : null))
            .ToListAsync(ct);

        return Ok(leads);
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/questionnaire/send")]
    public async Task<ActionResult<LeadDto>> SendQuestionnaire(int id, CancellationToken ct)
    {
        var lead = await Db.Leads
            .Include(l => l.Branch)
            .Include(l => l.Owner)
            .Include(l => l.InterestedProject)
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        lead.QuestionnaireToken = Guid.NewGuid().ToString("N");
        lead.QuestionnaireStatus = QuestionnaireStatuses.Sent;
        lead.QuestionnaireSentAt = DateTime.UtcNow;
        lead.LastActivityAt = DateTime.UtcNow;

        Db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.Questionnaire,
            Remarks = "Qualification questionnaire sent to the couple.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);
        return Ok((await EnrichAsync([ToDto(lead)], ct))[0]);
    }

    /* ------------------------------------------------------------------ *
     * Reads
     * ------------------------------------------------------------------ */

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LeadDto>>> GetLeads(CancellationToken ct)
    {
        var leads = await Base().OrderByDescending(l => l.CreatedAt).Take(1000).ToListAsync(ct);
        return Ok(await EnrichAsync(leads.Select(ToDto).ToList(), ct));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<LeadDto>> GetLead(int id, CancellationToken ct)
    {
        var lead = await Base().FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        return Ok((await EnrichAsync([ToDto(lead)], ct))[0]);
    }

    [HttpGet("{id:int}/activities")]
    public async Task<ActionResult<IReadOnlyList<LeadActivityDto>>> GetActivities(int id, CancellationToken ct)
    {
        if (!await Db.Leads.AnyAsync(l => l.Id == id, ct)) throw ApiException.NotFound("Lead");

        var activities = await Db.LeadActivities
            .Where(a => a.LeadId == id)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => new LeadActivityDto(
                a.Id, a.Type, a.Remarks, a.FromStage, a.ToStage, a.ActorName, a.CreatedAt))
            .ToListAsync(ct);

        return Ok(activities);
    }

    /* ------------------------------------------------------------------ *
     * Writes
     * ------------------------------------------------------------------ */

    [HttpPost]
    public async Task<ActionResult<LeadDto>> CreateLead(CreateLeadRequest request, CancellationToken ct)
    {
        // The lead ceiling is the one a growing company actually meets. Checked
        // here rather than on the import path alone, because a lead arrives one
        // at a time far more often than ten thousand at once.
        await entitlements.EnsureRoomAsync(Limits.Leads, ct: ct);

        var branch = await RequireBranchAsync(request.BranchId, ct);
        await RequireOwnerAsync(request.OwnerId, ct);
        await RequireSupportingManagerAsync(request.SupportingManagerId, ct);

        // Duplicate rules run before anything is written. A blocking rule has to
        // refuse the save, not undo it — the lead ceiling above has already been
        // consumed by then, and a rolled-back create still moves the counter.
        var verdict = await duplicates.CheckLeadAsync(
            request.Name, request.Phone, request.Email, eventDate: request.EventDate, ct: ct);

        if (verdict is not null && verdict.Blocks)
        {
            return Conflict(new
            {
                message = $"{verdict.RuleName}: this looks like a lead that is already here.",
                duplicates = verdict.Matches,
            });
        }

        var lead = new Lead
        {
            CompanyId = Db.Tenant.CompanyId,
            BranchId = branch.Id,
            Stage = LeadStages.New,
            OwnerId = request.OwnerId,
            // First response is expected inside the working day for a normal
            // lead, and within the hour for a hot one.
            SlaDueAt = DateTime.UtcNow.AddHours(
                request.Priority == LeadPriorities.Hot ? 1 : 8),
        };

        ApplyCore(lead, request);

        // Routing runs after the fields are on the record, because a rule tests
        // them — "leads from Facebook go to the tele desk" needs the source. An
        // explicit owner on the request always wins: somebody chose it.
        if (lead.OwnerId is null)
        {
            lead.OwnerId = await assignment.ResolveOwnerAsync(SecuredObjects.Lead, lead, ct);
        }

        Db.Leads.Add(lead);
        await Db.SaveChangesAsync(ct);

        Db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.Created,
            Remarks = $"Lead captured from {lead.Source}.",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        if (!string.IsNullOrWhiteSpace(lead.Email))
        {
            lead.AutoAckAt = DateTime.UtcNow;
            Db.LeadActivities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.Email,
                Remarks =
                    "Automatic enquiry acknowledgement queued. A planner will reply inside the SLA — this message does not count as first response.",
                ActorId = Db.Tenant.UserId,
                ActorName = "System",
            });
        }

        lead.CustomFields = await customFields.NormaliseAsync(
            SecuredObjects.Lead, request.CustomFields, null, ct);

        var score = scoring.Score(lead, DateTime.UtcNow);
        lead.CachedScore = score.Score;
        lead.CachedBand = score.Band;
        lead.ScoredAt = DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);

        lead.Branch = branch;
        lead.Owner = request.OwnerId is null ? null : await Db.Users.FindAsync([request.OwnerId], ct);
        lead.SupportingManager = lead.SupportingManagerId is null
            ? null
            : await Db.Users.FindAsync([lead.SupportingManagerId], ct);

        // Raised after the save, so a receiver that calls straight back finds
        // the record it was told about. Queued rather than posted, so the
        // customer's endpoint is never on this request's critical path.
        await webhooks.RaiseAsync(WebhookEvents.LeadCreated, ToDto(lead), ct);

        return CreatedAtAction(nameof(GetLead), new { id = lead.Id }, ToDto(lead));
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<LeadDto>> UpdateLead(int id, UpdateLeadRequest request, CancellationToken ct)
    {
        var lead = await Db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        var branch = await RequireBranchAsync(request.BranchId, ct);
        await RequireOwnerAsync(request.OwnerId, ct);
        await RequireSupportingManagerAsync(request.SupportingManagerId, ct);

        var stage = Require(request.Stage, LeadStages.All, "stage");

        if (lead.Stage != stage)
        {
            Db.LeadActivities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.StageChange,
                FromStage = lead.Stage,
                ToStage = stage,
                ActorId = Db.Tenant.UserId,
                ActorName = Db.Tenant.UserName,
            });
        }

        if (lead.OwnerId != request.OwnerId)
        {
            var newOwnerName = request.OwnerId is null
                ? "Unassigned"
                : (await Db.Users.FindAsync([request.OwnerId], ct))?.Name ?? "Unknown";

            Db.LeadActivities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.OwnerChange,
                Remarks = $"Reassigned to {newOwnerName}.",
                ActorId = Db.Tenant.UserId,
                ActorName = Db.Tenant.UserName,
            });
        }

        var stageMoved = lead.Stage != stage;
        var ownerMoved = lead.OwnerId != request.OwnerId;

        lead.BranchId = branch.Id;
        lead.OwnerId = request.OwnerId;
        lead.Stage = stage;
        lead.LossReason = stage == LeadStages.Lost ? request.LossReason : null;
        lead.LastActivityAt = DateTime.UtcNow;

        ApplyCore(lead, request);

        // Merged into what is already stored rather than replacing it, so a
        // client that sends three of eight fields does not blank the other five.
        lead.CustomFields = await customFields.NormaliseAsync(
            SecuredObjects.Lead, request.CustomFields, lead.CustomFields, ct);

        EnsureCanEnterStage(lead, stage);

        var score = scoring.Score(lead, DateTime.UtcNow);
        lead.CachedScore = score.Score;
        lead.CachedBand = score.Band;
        lead.ScoredAt = DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);

        lead.Branch = branch;
        lead.Owner = lead.OwnerId is null ? null : await Db.Users.FindAsync([lead.OwnerId], ct);
        lead.SupportingManager = lead.SupportingManagerId is null
            ? null
            : await Db.Users.FindAsync([lead.SupportingManagerId], ct);

        var dto = ToDto(lead);

        if (stageMoved) await webhooks.RaiseAsync(WebhookEvents.LeadStageChanged, dto, ct);
        if (ownerMoved) await webhooks.RaiseAsync(WebhookEvents.LeadAssigned, dto, ct);

        return Ok((await EnrichAsync([dto], ct))[0]);
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("{id:int}/activities")]
    public async Task<ActionResult<LeadActivityDto>> CreateActivity(
        int id,
        CreateLeadActivityRequest request,
        CancellationToken ct)
    {
        var lead = await Db.Leads.FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        var type = Require(request.Type, LeadActivityTypes.Loggable, "activity type");

        var activity = new LeadActivity
        {
            LeadId = id,
            Type = type,
            Remarks = request.Remarks,
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        };

        Db.LeadActivities.Add(activity);

        lead.LastActivityAt = DateTime.UtcNow;
        if (type is LeadActivityTypes.Call or LeadActivityTypes.Email or LeadActivityTypes.WhatsApp)
        {
            lead.FirstResponseAt ??= DateTime.UtcNow;
        }

        await Db.SaveChangesAsync(ct);

        return Ok(new LeadActivityDto(
            activity.Id, activity.Type, activity.Remarks, activity.FromStage,
            activity.ToStage, activity.ActorName, activity.CreatedAt));
    }

    /* ------------------------------------------------------------------ *
     * Bulk operations
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Reassigns a selection in one round trip. Every move is logged
    /// individually so the timeline still reads correctly on each lead.
    /// </summary>
    [PermissionAction(ObjectAction.ModifyAll)]
    [HttpPost("bulk/assign")]
    public async Task<ActionResult<BulkResultDto>> BulkAssign(BulkAssignRequest request, CancellationToken ct)
    {
        await RequireOwnerAsync(request.OwnerId, ct);

        var leads = await Db.Leads.Where(l => request.LeadIds.Contains(l.Id)).ToListAsync(ct);

        var ownerName = request.OwnerId is null
            ? "Unassigned"
            : (await Db.Users.FindAsync([request.OwnerId], ct))?.Name ?? "Unknown";

        foreach (var lead in leads.Where(l => l.OwnerId != request.OwnerId))
        {
            lead.OwnerId = request.OwnerId;
            Db.LeadActivities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.OwnerChange,
                Remarks = $"Bulk reassigned to {ownerName}.",
                ActorId = Db.Tenant.UserId,
                ActorName = Db.Tenant.UserName,
            });
        }

        await Db.SaveChangesAsync(ct);
        return Ok(new BulkResultDto(leads.Count, $"{leads.Count} lead(s) assigned to {ownerName}."));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("bulk/stage")]
    public async Task<ActionResult<BulkResultDto>> BulkStage(BulkStageRequest request, CancellationToken ct)
    {
        var stage = Require(request.Stage, LeadStages.All, "stage");
        var leads = await Db.Leads.Where(l => request.LeadIds.Contains(l.Id)).ToListAsync(ct);

        foreach (var lead in leads.Where(l => l.Stage != stage))
        {
            EnsureCanEnterStage(lead, stage);
            Db.LeadActivities.Add(new LeadActivity
            {
                LeadId = lead.Id,
                Type = LeadActivityTypes.StageChange,
                FromStage = lead.Stage,
                ToStage = stage,
                Remarks = "Bulk stage update.",
                ActorId = Db.Tenant.UserId,
                ActorName = Db.Tenant.UserName,
            });

            lead.Stage = stage;
            lead.LastActivityAt = DateTime.UtcNow;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(new BulkResultDto(leads.Count, $"{leads.Count} lead(s) moved to {stage}."));
    }

    [PermissionAction(ObjectAction.Delete)]
    [HttpPost("bulk/delete")]
    public async Task<ActionResult<BulkResultDto>> BulkDelete(BulkIdsRequest request, CancellationToken ct)
    {
        var leads = await Db.Leads.Where(l => request.LeadIds.Contains(l.Id)).ToListAsync(ct);

        foreach (var lead in leads) SoftDelete(lead);

        await Db.SaveChangesAsync(ct);
        return Ok(new BulkResultDto(leads.Count, $"{leads.Count} lead(s) deleted."));
    }


    /* ------------------------------------------------------------------ *
     * Record page: related records, history, inline edit, transfer
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Everything attached to a lead, in one round trip.
    ///
    /// The record page would otherwise fire five list calls on open; this keeps
    /// it to one, and returns the rollup counts alongside the rows so the tab
    /// badges do not need a sixth.
    /// </summary>
    [HttpGet("{id:int}/related")]
    public async Task<ActionResult<LeadRelatedDto>> GetRelated(int id, CancellationToken ct)
    {
        var lead = await Db.Leads.AsNoTracking().FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        var calls = await Db.CallLogs
            .Where(c => c.RelatedType == RelatedTypes.Lead && c.RelatedId == id)
            .OrderByDescending(c => c.StartedAt)
            .Select(c => new RelatedRecordDto(
                "Call",
                c.Id,
                c.Direction + " · " + c.Outcome,
                c.Disposition ?? c.AgentName,
                c.Outcome,
                null,
                c.StartedAt))
            .Take(50)
            .ToListAsync(ct);

        var visits = await Db.SiteVisits
            .Where(v => v.LeadId == id)
            .OrderByDescending(v => v.ScheduledAt)
            .Select(v => new RelatedRecordDto(
                "SiteVisit",
                v.Id,
                v.VisitCode,
                v.Project != null ? v.Project.Name : v.VisitType,
                v.Status,
                v.BudgetDiscussed,
                v.ScheduledAt))
            .Take(50)
            .ToListAsync(ct);

        var obmVisits = await Db.ObmVisits
            .Where(v => v.LeadId == id)
            .OrderByDescending(v => v.ScheduledAt)
            .Select(v => new RelatedRecordDto(
                "ObmVisit",
                v.Id,
                v.VisitCode,
                v.PartnerName,
                v.Status,
                v.BusinessValue,
                v.ScheduledAt))
            .Take(50)
            .ToListAsync(ct);

        var followUps = await Db.FollowUps
            .Where(f => f.RelatedType == RelatedTypes.Lead && f.RelatedId == id)
            .OrderByDescending(f => f.DueAt)
            .Select(f => new RelatedRecordDto(
                "FollowUp",
                f.Id,
                f.Subject,
                f.Channel,
                f.Status,
                null,
                f.DueAt))
            .Take(50)
            .ToListAsync(ct);

        var quotations = await Db.Quotations
            .Where(q => q.LeadId == id)
            .OrderByDescending(q => q.IssueDate)
            .Select(q => new RelatedRecordDto(
                "Quotation",
                q.Id,
                q.QuoteNumber,
                q.Title,
                q.Status,
                q.Total,
                q.IssueDate))
            .Take(50)
            .ToListAsync(ct);

        // Deals reach a lead either directly or through the contact it
        // converted into, so both routes are checked.
        var opportunities = await Db.Opportunities
            .Where(o => o.LeadId == id
                || (lead.ConvertedContactId != null && o.ContactId == lead.ConvertedContactId))
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new RelatedRecordDto(
                "Opportunity",
                o.Id,
                o.Name,
                o.Project != null ? o.Project.Name : o.Type,
                o.Stage,
                o.Amount,
                o.CreatedAt))
            .Take(50)
            .ToListAsync(ct);

        return Ok(new LeadRelatedDto(
            calls,
            visits,
            obmVisits,
            followUps,
            quotations,
            opportunities,
            calls.Count,
            visits.Count,
            visits.Count(v => v.Status == VisitStatuses.Completed),
            obmVisits.Count,
            followUps.Count,
            followUps.Count(f => f.Status == FollowUpStatuses.Open
                || f.Status == FollowUpStatuses.InProgress),
            quotations.Count,
            quotations.Sum(q => q.Amount ?? 0m)));
    }

    /// <summary>
    /// Field-level change history, read straight off the audit trail.
    ///
    /// Nothing extra is recorded to make this work — the SaveChanges
    /// interceptor already logs every write, so the record page shows the same
    /// trail a compliance export would.
    /// </summary>
    [HttpGet("{id:int}/history")]
    public async Task<ActionResult<IReadOnlyList<FieldChangeDto>>> GetHistory(int id, CancellationToken ct)
    {
        if (!await Db.Leads.AnyAsync(l => l.Id == id, ct)) throw ApiException.NotFound("Lead");

        var entries = await Db.AuditLogs
            .Where(a => a.CompanyId == Db.Tenant.CompanyId)
            .Where(a => a.Entity == "Lead" && a.EntityId == id.ToString())
            .OrderByDescending(a => a.At)
            .Take(200)
            .AsNoTracking()
            .ToListAsync(ct);

        var changes = new List<FieldChangeDto>();

        foreach (var entry in entries)
        {
            if (entry.Action != AuditActions.Update || entry.Changes is null)
            {
                changes.Add(new FieldChangeDto(entry.At, entry.UserName, "—", null, null, entry.Action));
                continue;
            }

            using var document = JsonDocument.Parse(entry.Changes);

            foreach (var property in document.RootElement.EnumerateObject())
            {
                changes.Add(new FieldChangeDto(
                    entry.At,
                    entry.UserName,
                    Humanise(property.Name),
                    property.Value.TryGetProperty("from", out var from) ? Stringify(from) : null,
                    property.Value.TryGetProperty("to", out var to) ? Stringify(to) : null,
                    entry.Action));
            }
        }

        return Ok(changes);

        static string? Stringify(JsonElement element) => element.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => element.GetString(),
            _ => element.ToString(),
        };
    }

    /// <summary>
    /// Fields the record page may write one at a time, and how to parse each.
    ///
    /// A whitelist rather than reflection over the entity: it keeps stage,
    /// ownership and conversion — which carry side effects and have their own
    /// endpoints — out of reach of a hover-to-edit.
    /// </summary>
    private static readonly Dictionary<string, Action<Lead, string?>> EditableFields =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["salutation"] = (l, v) => l.Salutation = v,
            ["name"] = (l, v) => l.Name = RequiredText(v),
            ["companyName"] = (l, v) => l.CompanyName = v,
            ["phone"] = (l, v) => l.Phone = v,
            ["phone2"] = (l, v) => l.Phone2 = v,
            ["email"] = (l, v) => l.Email = v,
            ["address"] = (l, v) => l.Address = v,
            ["city"] = (l, v) => l.City = v,
            ["state"] = (l, v) => l.State = v,
            ["pincode"] = (l, v) => l.Pincode = v,
            ["country"] = (l, v) => l.Country = v,
            ["zone"] = (l, v) => l.Zone = v,
            ["dateOfBirth"] = (l, v) => l.DateOfBirth = ParseDate(v),
            ["anniversaryDate"] = (l, v) => l.AnniversaryDate = ParseDate(v),
            ["maritalStatus"] = (l, v) => l.MaritalStatus = v,
            ["fatherOrSpouseName"] = (l, v) => l.FatherOrSpouseName = v,
            ["occupation"] = (l, v) => l.Occupation = v,
            ["designation"] = (l, v) => l.Designation = v,
            ["nationality"] = (l, v) => l.Nationality = v,
            ["partnerName"] = (l, v) => l.PartnerName = v,
            ["partnerPhone"] = (l, v) => l.PartnerPhone = v,
            ["partnerEmail"] = (l, v) => l.PartnerEmail = v,
            ["inquirerRole"] = (l, v) => l.InquirerRole = v is null ? null : Require(v, InquirerRoles.All, "inquirer role"),

            ["source"] = (l, v) => l.Source = Require(v, LeadSources.All, "lead source"),
            ["priority"] = (l, v) => l.Priority = Require(v, LeadPriorities.All, "priority"),
            ["subStatus"] = (l, v) => l.SubStatus = v,
            ["supportingManagerId"] = (l, v) => l.SupportingManagerId = ParseInteger(v),
            ["notes"] = (l, v) => l.Notes = v,

            ["budgetMin"] = (l, v) => l.BudgetMin = ParseMoney(v),
            ["budgetMax"] = (l, v) => l.BudgetMax = ParseMoney(v),
            ["eventType"] = (l, v) =>
            {
                l.EventType = v;

                // The category is a function of the occasion, so it is derived
                // rather than asked for twice — and re-derived here so an inline
                // edit of the type cannot leave a stale category behind.
                if (v is not null) l.EventCategory = EventTypes.CategoryOf(v);
            },
            ["eventCategory"] = (l, v) => l.EventCategory = v,
            ["eventDate"] = (l, v) => l.EventDate = ParseDate(v),
            ["eventEndDate"] = (l, v) => l.EventEndDate = ParseDate(v),
            ["eventSlot"] = (l, v) => l.EventSlot = v,
            ["isDateFlexible"] = (l, v) => l.IsDateFlexible = ParseBool(v),
            ["guestCount"] = (l, v) => l.GuestCount = ParseInteger(v),
            ["functions"] = (l, v) => l.Functions = v,
            ["servicesNeeded"] = (l, v) => l.ServicesNeeded = v,
            ["mealPreference"] = (l, v) => l.MealPreference = v,
            ["preferredLocality"] = (l, v) => l.PreferredLocality = v,
            ["paymentMode"] = (l, v) => l.PaymentMode = v,
            ["interestedProjectId"] = (l, v) => l.InterestedProjectId = ParseInteger(v),
            ["venueStatus"] = (l, v) => l.VenueStatus = v is null ? null : Require(v, EnquiryVenueStatuses.All, "venue status"),
            ["planningPackage"] = (l, v) => l.PlanningPackage = v is null ? null : Require(v, PlanningPackages.All, "planning package"),
            ["ceremonyGuestCount"] = (l, v) => l.CeremonyGuestCount = ParseInteger(v),
            ["receptionGuestCount"] = (l, v) => l.ReceptionGuestCount = ParseInteger(v),
            ["portalName"] = (l, v) => l.PortalName = v is null ? null : Require(v, EventPortals.All, "listing portal"),
            ["ceremonyStyle"] = (l, v) => l.CeremonyStyle = v is null ? null : Require(v, CeremonyStyles.All, "ceremony style"),
            ["consultAt"] = (l, v) => l.ConsultAt = ParseDate(v),

            ["campaign"] = (l, v) => l.Campaign = v,
            ["utmSource"] = (l, v) => l.UtmSource = v,
            ["utmMedium"] = (l, v) => l.UtmMedium = v,
            ["referredBy"] = (l, v) => l.ReferredBy = v,
            ["tags"] = (l, v) => l.Tags = v,
        };

    private static string RequiredText(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? throw ApiException.BadRequest("This field cannot be empty.")
            : value.Trim();

    private static DateTime? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal,
            out var parsed)
            ? parsed
            : throw ApiException.BadRequest($"'{value}' is not a valid date.");
    }

    private static decimal? ParseMoney(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? Math.Max(0m, parsed)
            : throw ApiException.BadRequest($"'{value}' is not a valid amount.");
    }

    private static int? ParseInteger(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        return int.TryParse(value, out var parsed)
            ? parsed
            : throw ApiException.BadRequest($"'{value}' is not a whole number.");
    }

    /// <summary>
    /// Parses a checkbox write. Blank clears to false rather than erroring —
    /// an unticked box posts nothing, and that is a valid "no".
    /// </summary>
    private static bool ParseBool(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return false;

        return value.Trim().ToLowerInvariant() switch
        {
            "true" or "1" or "yes" or "on" => true,
            "false" or "0" or "no" or "off" => false,
            _ => throw ApiException.BadRequest($"'{value}' is not a yes or no."),
        };
    }

    /// <summary>
    /// Writes one field. The audit interceptor records what changed, so the
    /// record page's History tab fills itself in without this endpoint doing
    /// anything extra.
    /// </summary>
    [HttpPatch("{id:int}/field")]
    public async Task<ActionResult<LeadDto>> PatchField(
        int id,
        PatchLeadFieldRequest request,
        CancellationToken ct)
    {
        var lead = await Db.Leads
            .Include(l => l.Branch).Include(l => l.Owner)
            .Include(l => l.SupportingManager).Include(l => l.InterestedProject)
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        if (!EditableFields.TryGetValue(request.Field, out var apply))
        {
            throw ApiException.BadRequest(
                $"'{request.Field}' cannot be edited inline. Editable fields: " +
                string.Join(", ", EditableFields.Keys.Order()));
        }

        apply(lead, request.Value);
        lead.LastActivityAt = DateTime.UtcNow;

        var score = scoring.Score(lead, DateTime.UtcNow);
        lead.CachedScore = score.Score;
        lead.CachedBand = score.Band;
        lead.ScoredAt = DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);

        return Ok((await EnrichAsync([ToDto(lead)], ct))[0]);
    }

    /// <summary>
    /// Hands a lead to another rep, with the reason on the record.
    ///
    /// Separate from a plain owner edit because a transfer is an event people
    /// argue about later — it belongs in the timeline with a stated reason, not
    /// buried as a field change.
    /// </summary>
    [PermissionAction(ObjectAction.ModifyAll)]
    [HttpPost("{id:int}/transfer")]
    public async Task<ActionResult<LeadDto>> Transfer(
        int id,
        LeadTransferRequest request,
        CancellationToken ct)
    {
        var lead = await Db.Leads
            .Include(l => l.Branch).Include(l => l.Owner)
            .Include(l => l.SupportingManager).Include(l => l.InterestedProject)
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        var owner = await Db.Users
            .FirstOrDefaultAsync(u => u.Id == request.OwnerId && u.CompanyId == Db.Tenant.CompanyId, ct)
            ?? throw ApiException.BadRequest("Select a valid owner.");

        if (lead.OwnerId == owner.Id)
        {
            throw ApiException.Conflict($"{owner.Name} already owns this lead.");
        }

        var previous = lead.Owner?.Name ?? "Unassigned";

        lead.OwnerId = owner.Id;
        lead.Owner = owner;
        lead.LastActivityAt = DateTime.UtcNow;

        Db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.OwnerChange,
            Remarks = string.IsNullOrWhiteSpace(request.Reason)
                ? $"Transferred from {previous} to {owner.Name}."
                : $"Transferred from {previous} to {owner.Name}. {request.Reason.Trim()}",
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);

        return Ok((await EnrichAsync([ToDto(lead)], ct))[0]);
    }

    /* ------------------------------------------------------------------ *
     * Conversion
     * ------------------------------------------------------------------ */

    /// <summary>
    /// Salesforce convert is kept as an optional deal. Default path books the
    /// enquiry: a contact plus, when a venue unit is named, a booking row.
    /// </summary>
    [HttpPost("{id:int}/convert")]
    public async Task<ActionResult<ConvertLeadResultDto>> Convert(
        int id,
        ConvertLeadRequest request,
        CancellationToken ct)
    {
        var lead = await Db.Leads
            .Include(l => l.Branch)
            .Include(l => l.InterestedProject)
            .FirstOrDefaultAsync(l => l.Id == id, ct)
            ?? throw ApiException.NotFound("Lead");

        if (lead.IsConverted)
        {
            throw ApiException.Conflict("This lead has already been converted.");
        }

        await using var transaction = await Db.Database.BeginTransactionAsync(ct);

        var nameParts = lead.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);

        var contact = new Contact
        {
            CompanyId = lead.CompanyId,
            BranchId = lead.BranchId,
            Salutation = lead.Salutation,
            FirstName = nameParts.ElementAtOrDefault(0) ?? lead.Name,
            LastName = nameParts.ElementAtOrDefault(1),
            FullName = lead.Name,
            AccountName = lead.CompanyName,
            Phone = lead.Phone,
            Phone2 = lead.Phone2,
            Email = lead.Email,
            Address = lead.Address,
            City = lead.City,
            Country = lead.Country,
            Type = ContactTypes.Contact,
            LifecycleStage = LifecycleStages.Engaged,
            Source = lead.Source,
            BudgetMin = lead.BudgetMin,
            BudgetMax = lead.BudgetMax,
            PreferredConfiguration = lead.EventType,
            PreferredLocality = lead.PreferredLocality,
            OwnerId = lead.OwnerId,
            ConvertedFromLeadId = lead.Id,
            LastActivityAt = DateTime.UtcNow,
        };

        Db.Contacts.Add(contact);
        await Db.SaveChangesAsync(ct);

        Opportunity? opportunity = null;

        if (request.CreateOpportunity)
        {
            opportunity = new Opportunity
            {
                CompanyId = lead.CompanyId,
                BranchId = lead.BranchId,
                Name = request.OpportunityName ?? $"{lead.Name} — {lead.EventType ?? "event"}",
                ContactId = contact.Id,
                LeadId = lead.Id,
                ProjectId = request.ProjectId,
                UnitId = request.UnitId,
                Stage = OpportunityStages.Qualification,
                Type = OpportunityTypes.NewBusiness,
                Source = lead.Source,
                Amount = request.Amount ?? lead.BudgetMax ?? 0m,
                Probability = OpportunityStages.DefaultProbability(OpportunityStages.Qualification),
                ExpectedCloseDate = request.ExpectedCloseDate
                    ?? lead.EventDate?.AddDays(-14)
                    ?? DateTime.UtcNow.AddDays(45),
                OwnerId = lead.OwnerId,
                ForecastCategory = ForecastCategories.Pipeline,
                StageEnteredAt = DateTime.UtcNow,
            };

            Db.Opportunities.Add(opportunity);
            await Db.SaveChangesAsync(ct);
        }

        Booking? booking = null;
        var unitId = request.UnitId;
        if (request.CreateEvent && unitId is int uid)
        {
            var unit = await Db.Units.Include(u => u.Project).FirstOrDefaultAsync(u => u.Id == uid, ct);
            if (unit is null) throw ApiException.BadRequest("Select a valid venue unit to book.");

            booking = new Booking
            {
                CompanyId = lead.CompanyId,
                BranchId = lead.BranchId,
                BookingNumber = $"EVT-{lead.Id}-{DateTime.UtcNow:yyyyMMddHHmm}",
                LeadId = lead.Id,
                ContactId = contact.Id,
                UnitId = unit.Id,
                ProjectId = unit.ProjectId,
                ProjectName = unit.Project?.Name ?? lead.InterestedProject?.Name ?? "Venue",
                UnitNumber = unit.UnitNumber,
                Configuration = unit.Configuration,
                SaleableArea = unit.SuperArea ?? unit.CarpetArea,
                AreaUnit = unit.AreaUnit,
                EventType = lead.EventType,
                EventDate = lead.EventDate,
                EventEndDate = lead.EventEndDate,
                EventSlot = lead.EventSlot,
                Functions = lead.Functions,
                GuestCount = lead.GuestCount ?? 0,
                GrandTotal = lead.BudgetMax ?? 0m,
                AgreementValue = lead.BudgetMax ?? 0m,
                BookingDate = DateTime.UtcNow,
                OwnerId = lead.OwnerId,
                Status = BookingStatuses.Booked,
                Notes = $"Booked from enquiry #{lead.Id}.",
            };
            Db.Bookings.Add(booking);
            await Db.SaveChangesAsync(ct);
        }

        var previousStage = lead.Stage;
        lead.IsConverted = true;
        lead.ConvertedAt = DateTime.UtcNow;
        lead.ConvertedContactId = contact.Id;
        lead.ConvertedOpportunityId = opportunity?.Id;
        lead.ConvertedBookingId = booking?.Id;
        lead.Stage = LeadStages.Booked;
        lead.LastActivityAt = DateTime.UtcNow;

        var remark = booking is not null
            ? $"Booked event {booking.BookingNumber} for contact #{contact.Id}."
            : opportunity is null
                ? $"Converted to client #{contact.Id}. Attach a venue unit from post-sales when it is known."
                : $"Converted to contact #{contact.Id} and opportunity #{opportunity.Id}.";

        Db.LeadActivities.Add(new LeadActivity
        {
            LeadId = lead.Id,
            Type = LeadActivityTypes.StageChange,
            FromStage = previousStage,
            ToStage = LeadStages.Booked,
            Remarks = remark,
            ActorId = Db.Tenant.UserId,
            ActorName = Db.Tenant.UserName,
        });

        await Db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);

        return Ok(new ConvertLeadResultDto(
            lead.Id,
            contact.Id,
            contact.FullName,
            opportunity?.Id,
            opportunity?.Name,
            booking?.Id,
            booking?.BookingNumber,
            remark));
    }

    /* ------------------------------------------------------------------ *
     * Helpers
     * ------------------------------------------------------------------ */

    private static void ApplyCore(Lead lead, CreateLeadRequest request)
    {
        // ---- personal ----
        lead.Salutation = request.Salutation;
        lead.Name = request.Name.Trim();
        lead.CompanyName = request.CompanyName;
        lead.Phone = request.Phone;
        lead.Phone2 = request.Phone2;
        lead.Email = request.Email;
        lead.Address = request.Address;
        lead.City = request.City;
        lead.State = request.State;
        lead.Pincode = request.Pincode;
        lead.Country = request.Country;
        lead.Zone = request.Zone;
        lead.DateOfBirth = request.DateOfBirth;
        lead.AnniversaryDate = request.AnniversaryDate;
        lead.MaritalStatus = request.MaritalStatus;
        lead.FatherOrSpouseName = request.FatherOrSpouseName;
        lead.Occupation = request.Occupation;
        lead.Designation = request.Designation;
        lead.Nationality = request.Nationality;
        lead.PartnerName = request.PartnerName;
        lead.PartnerPhone = request.PartnerPhone;
        lead.PartnerEmail = request.PartnerEmail;
        lead.InquirerRole = request.InquirerRole is null
            ? null
            : Require(request.InquirerRole, InquirerRoles.All, "inquirer role");

        // ---- lead ----
        lead.Source = Require(request.Source, LeadSources.All, "lead source");
        lead.Priority = LeadPriorities.All.Contains(request.Priority ?? "")
            ? request.Priority!
            : LeadPriorities.Medium;
        lead.SubStatus = request.SubStatus;
        lead.SupportingManagerId = request.SupportingManagerId;
        lead.Notes = request.Notes;

        // ---- the event ----
        lead.BudgetMin = request.BudgetMin;
        lead.BudgetMax = request.BudgetMax;
        lead.EventType = request.EventType;

        // Derived from the occasion rather than asked for separately, so the two
        // can never disagree. An explicit category still wins, for the enquiry
        // that is genuinely a corporate wedding or a sponsored social event.
        lead.EventCategory = request.EventCategory
            ?? (request.EventType is null ? null : EventTypes.CategoryOf(request.EventType));

        lead.EventDate = request.EventDate;
        lead.EventEndDate = request.EventEndDate;
        lead.EventSlot = request.EventSlot;
        lead.IsDateFlexible = request.IsDateFlexible ?? false;
        lead.GuestCount = request.GuestCount;
        lead.Functions = request.Functions;
        lead.ServicesNeeded = request.ServicesNeeded;
        lead.MealPreference = request.MealPreference;
        lead.PreferredLocality = request.PreferredLocality;
        lead.PaymentMode = request.PaymentMode;
        lead.InterestedProjectId = request.InterestedProjectId;
        lead.VenueStatus = request.VenueStatus is null
            ? null
            : Require(request.VenueStatus, EnquiryVenueStatuses.All, "venue status");
        lead.PlanningPackage = request.PlanningPackage is null
            ? null
            : Require(request.PlanningPackage, PlanningPackages.All, "planning package");
        lead.CeremonyGuestCount = request.CeremonyGuestCount;
        lead.ReceptionGuestCount = request.ReceptionGuestCount;
        lead.PortalName = request.PortalName is null
            ? null
            : Require(request.PortalName, EventPortals.All, "listing portal");
        lead.CeremonyStyle = request.CeremonyStyle is null
            ? null
            : Require(request.CeremonyStyle, CeremonyStyles.All, "ceremony style");
        lead.ConsultAt = request.ConsultAt;

        if (lead.EventEndDate is not null && lead.EventDate is not null
            && lead.EventEndDate < lead.EventDate)
        {
            throw ApiException.BadRequest("The event cannot end before it starts.");
        }

        // ---- attribution ----
        lead.Campaign = request.Campaign;
        lead.UtmSource = request.UtmSource;
        lead.UtmMedium = request.UtmMedium;
        lead.ReferredBy = request.ReferredBy;
        lead.Tags = request.Tags;
    }

    private static void EnsureCanEnterStage(Lead lead, string stage)
    {
        if (stage is not (LeadStages.Qualified or LeadStages.ProposalSent or LeadStages.ContractSent))
            return;

        var questionnaireDone = lead.QuestionnaireStatus == QuestionnaireStatuses.Completed;
        var factsKnown = lead.EventDate is not null
            && lead.GuestCount is > 0
            && (lead.BudgetMin is not null || lead.BudgetMax is not null);

        if (stage == LeadStages.Qualified && !questionnaireDone && !factsKnown)
        {
            throw ApiException.BadRequest(
                "Qualify only once the date, guest count and budget are known, or the questionnaire is complete.");
        }
    }
}

public record BulkAssignRequest(IReadOnlyList<int> LeadIds, int? OwnerId);
public record BulkStageRequest(IReadOnlyList<int> LeadIds, string Stage);
public record BulkIdsRequest(IReadOnlyList<int> LeadIds);
public record BulkResultDto(int Affected, string Message);

public record ConvertLeadRequest(
    bool CreateOpportunity,
    bool CreateEvent,
    string? OpportunityName,
    decimal? Amount,
    DateTime? ExpectedCloseDate,
    int? ProjectId,
    int? UnitId
);

public record ConvertLeadResultDto(
    int LeadId,
    int ContactId,
    string ContactName,
    int? OpportunityId,
    string? OpportunityName,
    int? BookingId,
    string? BookingNumber,
    string Message
);

public record LeadBoardColumnDto(
    string Stage,
    string Label,
    int Count,
    decimal Value,
    double AverageDaysInStage,
    IReadOnlyList<LeadDto> Leads);

public record LeadBoardDto(
    IReadOnlyList<LeadBoardColumnDto> Columns,
    int Total,
    decimal PipelineValue);

public record LeadConflictDto(
    int Id,
    string Name,
    string Stage,
    DateTime? EventDate,
    DateTime? EventEndDate,
    int? GuestCount,
    string? VenueName);
