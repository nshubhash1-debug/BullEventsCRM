using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The parts of employment that are not pay, time or a job title: the list
/// somebody works through on their first and last day, the complaints they
/// raise, what they can actually do, where the company sent them, and which
/// job their hours belong to.
/// </summary>
[ApiController]
[Route("api/hr/workplace")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrWorkplaceController(AppDbContext db) : CrmControllerBase(db)
{
    /* ================================================================== *
     * Checklists
     * ================================================================== */

    [HttpGet("checklists")]
    public async Task<ActionResult<IReadOnlyList<HrChecklistTemplateDto>>> Checklists(
        [FromQuery] string? kind, CancellationToken ct)
    {
        var query = Db.HrChecklistTemplates.AsNoTracking()
            .Include(t => t.Tasks).Include(t => t.Department).AsQueryable();

        if (!string.IsNullOrWhiteSpace(kind)) query = query.Where(t => t.Kind == kind);

        var rows = await query.OrderBy(t => t.Kind).ThenBy(t => t.Name).ToListAsync(ct);

        return Ok(rows.Select(t => new HrChecklistTemplateDto(
            t.Id, t.Name, t.Kind, t.DepartmentId, t.Department?.Name, t.CollarType,
            t.Notes, t.IsActive,
            t.Tasks.Count(x => x.IsBlocking),
            t.Tasks.OrderBy(x => x.SortOrder).Select(x => new HrChecklistTaskDto(
                x.Id, x.Title, x.Description, x.Owner, x.DueOffsetDays,
                x.IsBlocking, x.SortOrder)).ToList())).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("checklists")]
    public async Task<ActionResult<int>> SaveChecklist(
        [FromBody] HrChecklistTemplateInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A checklist needs a name.");
        if (!ChecklistKinds.All.Contains(input.Kind))
            throw ApiException.BadRequest("A checklist is for joining or for leaving.");
        if (input.Tasks.Count == 0)
            throw ApiException.BadRequest("An empty checklist reminds nobody of anything.");
        if (input.Tasks.Any(x => !ChecklistOwners.All.Contains(x.Owner)))
            throw ApiException.BadRequest("Unknown task owner.");

        var template = input.Id is int id and > 0
            ? await Db.HrChecklistTemplates.Include(t => t.Tasks)
                .FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw ApiException.NotFound("Checklist")
            : new HrChecklistTemplate { CompanyId = Db.Tenant.CompanyId };

        template.Name = input.Name.Trim();
        template.Kind = input.Kind;
        template.DepartmentId = input.DepartmentId;
        template.CollarType = input.CollarType;
        template.Notes = input.Notes;
        template.IsActive = input.IsActive;

        if (template.Id == 0) Db.HrChecklistTemplates.Add(template);

        Db.HrChecklistTasks.RemoveRange(template.Tasks);
        template.Tasks.Clear();

        var order = 0;
        foreach (var task in input.Tasks)
        {
            template.Tasks.Add(new HrChecklistTask
            {
                CompanyId = Db.Tenant.CompanyId,
                Title = task.Title.Trim(),
                Description = task.Description,
                Owner = task.Owner,
                DueOffsetDays = task.DueOffsetDays,
                IsBlocking = task.IsBlocking,
                SortOrder = order,
            });
            order += 10;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(template.Id);
    }

    /// <summary>
    /// Put a checklist onto somebody, dated from their joining or last day.
    ///
    /// The template that matches most closely wins — a Production-specific list
    /// beats the company-wide one — because a crew joiner needs boots and a
    /// safety briefing, and a designer needs neither.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("checklists/apply")]
    public async Task<ActionResult<HrChecklistRunDto>> ApplyChecklist(
        [FromBody] HrApplyChecklistInput input, CancellationToken ct)
    {
        if (!ChecklistKinds.All.Contains(input.Kind))
            throw ApiException.BadRequest("A checklist is for joining or for leaving.");

        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        var templates = await Db.HrChecklistTemplates.AsNoTracking()
            .Include(t => t.Tasks)
            .Where(t => t.Kind == input.Kind && t.IsActive)
            .ToListAsync(ct);

        var matching = templates
            .Where(t => t.AppliesTo(employee.DepartmentId, employee.CollarType))
            // The most specific match: a department-and-collar template beats a
            // department one, which beats the company-wide default.
            .OrderByDescending(t => (t.DepartmentId is null ? 0 : 2) + (t.CollarType is null ? 0 : 1))
            .ToList();

        var template = input.ChecklistTemplateId is int wanted
            ? templates.FirstOrDefault(t => t.Id == wanted)
            : matching.FirstOrDefault();

        if (template is null)
        {
            throw ApiException.BadRequest(
                $"No {input.Kind.ToLowerInvariant()} checklist fits {employee.Name}.");
        }

        var anchor = input.AnchorDate
            ?? (input.Kind == ChecklistKinds.Onboarding
                ? DateOnly.FromDateTime(employee.JoiningDate)
                : DateOnly.FromDateTime(DateTime.UtcNow));

        var existing = await Db.HrOnboardingItems
            .Where(i => i.EmployeeId == employee.Id && i.Kind == input.Kind)
            .Select(i => i.ChecklistTaskId)
            .ToListAsync(ct);

        var added = 0;
        foreach (var task in template.Tasks.OrderBy(t => t.SortOrder))
        {
            // Re-applying tops up rather than duplicating — a template that
            // gained a task after somebody joined should still reach them.
            if (existing.Contains(task.Id)) continue;

            Db.HrOnboardingItems.Add(new HrOnboardingItem
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employee.Id,
                Title = task.Title,
                Description = task.Description,
                Kind = input.Kind,
                Owner = task.Owner,
                DueOn = anchor.AddDays(task.DueOffsetDays),
                IsBlocking = task.IsBlocking,
                ChecklistTaskId = task.Id,
                SortOrder = task.SortOrder,
            });
            added++;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(await ChecklistRunAsync(employee.Id, input.Kind, added, ct));
    }

    /// <summary>Somebody's checklist, and whether anything is holding them up.</summary>
    [HttpGet("checklists/employee/{employeeId:int}")]
    public async Task<ActionResult<HrChecklistRunDto>> EmployeeChecklist(
        int employeeId, [FromQuery] string? kind, CancellationToken ct)
        => Ok(await ChecklistRunAsync(employeeId, kind ?? ChecklistKinds.Onboarding, 0, ct));

    private async Task<HrChecklistRunDto> ChecklistRunAsync(
        int employeeId, string kind, int added, CancellationToken ct)
    {
        var employee = await Db.HrEmployees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        var items = await Db.HrOnboardingItems.AsNoTracking()
            .Where(i => i.EmployeeId == employeeId && i.Kind == kind)
            .OrderBy(i => i.SortOrder).ThenBy(i => i.Id)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var blocking = items.Where(i => i.IsBlocking && !i.Done).ToList();

        return new HrChecklistRunDto(
            employee.Id, employee.Name, kind, added,
            items.Count, items.Count(i => i.Done),
            items.Count(i => i.IsOverdue(today)),
            blocking.Count,
            // The one sentence somebody actually needs: can this person start,
            // or be relieved, yet.
            blocking.Count == 0
                ? null
                : $"{blocking.Count} blocking task{(blocking.Count == 1 ? "" : "s")} "
                    + $"outstanding: {string.Join(", ", blocking.Take(3).Select(b => b.Title))}"
                    + (blocking.Count > 3 ? "…" : ""),
            items.Select(i => new HrChecklistItemDto(
                i.Id, i.Title, i.Description, i.Owner, i.DueOn, i.Done, i.DoneAt,
                i.IsBlocking, i.IsOverdue(today))).ToList());
    }

    /* ================================================================== *
     * Grievances
     * ================================================================== */

    [HttpGet("grievances")]
    public async Task<ActionResult<IReadOnlyList<HrGrievanceDto>>> Grievances(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrGrievances.AsNoTracking()
            .Include(g => g.Employee).Include(g => g.AssignedToEmployee)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(g => g.Status == status);

        var rows = await query.OrderByDescending(g => g.Id).Take(300).ToListAsync(ct);
        return Ok(rows.Select(ToGrievance).ToList());
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("grievances")]
    public async Task<ActionResult<HrGrievanceDto>> Raise(
        [FromBody] HrGrievanceInput input, CancellationToken ct)
    {
        if (!GrievanceCategories.All.Contains(input.Category))
            throw ApiException.BadRequest("Unknown category.");
        if (string.IsNullOrWhiteSpace(input.Subject))
            throw ApiException.BadRequest("Say what it is about.");

        _ = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        if (input.AgainstEmployeeId == input.EmployeeId)
            throw ApiException.BadRequest("A grievance against yourself is not a grievance.");

        var row = new HrGrievance
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            Category = input.Category,
            Subject = input.Subject.Trim(),
            Details = input.Details,
            AgainstEmployeeId = input.AgainstEmployeeId,
            // Confidential unless the person raising it says otherwise. A
            // complaint anybody in HR can browse is one nobody raises twice.
            IsConfidential = input.IsConfidential,
            AttachmentUrl = input.AttachmentUrl,
            Status = GrievanceStatuses.Raised,
        };
        Db.HrGrievances.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(g => g.Employee).LoadAsync(ct);
        return Ok(ToGrievance(row));
    }

    /// <summary>
    /// Assign it to somebody to look into.
    ///
    /// Refuses to hand it to the person it is about, and refuses to route the
    /// categories the law says belong to a committee through a line manager.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("grievances/{id:int}/assign")]
    public async Task<ActionResult<HrGrievanceDto>> AssignGrievance(
        int id, [FromQuery] int toEmployeeId, [FromQuery] bool committee,
        CancellationToken ct)
    {
        var row = await Db.HrGrievances
            .Include(g => g.Employee).Include(g => g.AssignedToEmployee)
            .FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw ApiException.NotFound("Grievance");

        if (row.AgainstEmployeeId == toEmployeeId)
        {
            throw ApiException.BadRequest(
                "That is the person the complaint is about. Assign it to somebody else.");
        }

        if (GrievanceCategories.RequireCommittee.Contains(row.Category) && !committee)
        {
            throw ApiException.BadRequest(
                $"A {row.Category.ToLowerInvariant()} complaint has to go to the constituted "
                + "committee, not to a line manager. Confirm the assignee sits on it.");
        }

        _ = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == toEmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        row.AssignedToEmployeeId = toEmployeeId;
        row.AcknowledgedAt ??= DateTime.UtcNow;
        if (row.Status == GrievanceStatuses.Raised) row.Status = GrievanceStatuses.Investigating;

        await Db.SaveChangesAsync(ct);
        await Db.Entry(row).Reference(g => g.AssignedToEmployee).LoadAsync(ct);
        return Ok(ToGrievance(row));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("grievances/{id:int}/resolve")]
    public async Task<ActionResult<HrGrievanceDto>> ResolveGrievance(
        int id, [FromBody] HrGrievanceResolveInput input, CancellationToken ct)
    {
        var row = await Db.HrGrievances
            .Include(g => g.Employee).Include(g => g.AssignedToEmployee)
            .FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw ApiException.NotFound("Grievance");

        if (row.Status is GrievanceStatuses.Resolved or GrievanceStatuses.Withdrawn)
            throw ApiException.BadRequest("That grievance is already closed.");

        if (string.IsNullOrWhiteSpace(input.Resolution))
        {
            throw ApiException.BadRequest(
                "Write what was done. A complaint closed with no record of the outcome is "
                + "one the company cannot show it took seriously.");
        }

        row.Resolution = input.Resolution.Trim();
        row.ComplainantSatisfied = input.ComplainantSatisfied;
        row.Status = input.Withdrawn ? GrievanceStatuses.Withdrawn : GrievanceStatuses.Resolved;
        row.ResolvedAt = DateTime.UtcNow;
        row.AcknowledgedAt ??= DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);
        return Ok(ToGrievance(row));
    }

    private static HrGrievanceDto ToGrievance(HrGrievance g) => new(
        g.Id, g.EmployeeId, g.Employee?.Name ?? "—", g.Category, g.Subject, g.Details,
        g.AgainstEmployeeId, g.AssignedToEmployeeId, g.AssignedToEmployee?.Name,
        g.Status, g.IsConfidential, g.AcknowledgedAt, g.ResolvedAt,
        g.Resolution, g.ComplainantSatisfied, g.AttachmentUrl,
        g.AgeInDays, g.CreatedAt,
        GrievanceCategories.RequireCommittee.Contains(g.Category));

    /* ================================================================== *
     * Skills
     * ================================================================== */

    [HttpGet("skills")]
    public async Task<ActionResult<IReadOnlyList<HrSkillDto>>> Skills(CancellationToken ct)
    {
        var skills = await Db.HrSkills.AsNoTracking()
            .OrderBy(s => s.Category).ThenBy(s => s.Name).ToListAsync(ct);

        var held = await Db.HrEmployeeSkills.AsNoTracking()
            .GroupBy(x => x.SkillId)
            .Select(g => new { SkillId = g.Key, Count = g.Count(), Average = g.Average(x => (decimal)x.Proficiency) })
            .ToDictionaryAsync(x => x.SkillId, ct);

        return Ok(skills.Select(s => new HrSkillDto(
            s.Id, s.Name, s.Category, s.RequiresCertification, s.Notes, s.IsActive,
            held.TryGetValue(s.Id, out var h) ? h.Count : 0,
            held.TryGetValue(s.Id, out var a) ? Math.Round(a.Average, 1) : null)).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("skills")]
    public async Task<ActionResult<int>> SaveSkill(
        [FromBody] HrSkillInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A skill needs a name.");

        var name = input.Name.Trim();
        var clash = await Db.HrSkills
            .FirstOrDefaultAsync(s => s.Name == name && s.Id != (input.Id ?? 0), ct);
        if (clash is not null)
            throw ApiException.BadRequest($"{name} is already on the list.");

        var skill = input.Id is int id and > 0
            ? await Db.HrSkills.FirstOrDefaultAsync(s => s.Id == id, ct)
                ?? throw ApiException.NotFound("Skill")
            : new HrSkill { CompanyId = Db.Tenant.CompanyId };

        skill.Name = name;
        skill.Category = input.Category;
        skill.RequiresCertification = input.RequiresCertification;
        skill.Notes = input.Notes;
        skill.IsActive = input.IsActive;

        if (skill.Id == 0) Db.HrSkills.Add(skill);
        await Db.SaveChangesAsync(ct);
        return Ok(skill.Id);
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("employee-skills")]
    public async Task<ActionResult<HrEmployeeSkillDto>> SetEmployeeSkill(
        [FromBody] HrEmployeeSkillInput input, CancellationToken ct)
    {
        if (input.Proficiency is < 1 or > 5)
            throw ApiException.BadRequest("Proficiency runs from one to five.");

        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");
        var skill = await Db.HrSkills.FirstOrDefaultAsync(s => s.Id == input.SkillId, ct)
            ?? throw ApiException.BadRequest("Unknown skill.");

        if (skill.RequiresCertification && input.CertifiedUntil is null)
        {
            throw ApiException.BadRequest(
                $"{skill.Name} needs a certificate with an expiry. An expired certificate "
                + "is worse than none, because everybody assumes it is valid.");
        }

        var row = await Db.HrEmployeeSkills.FirstOrDefaultAsync(
            x => x.EmployeeId == employee.Id && x.SkillId == skill.Id, ct)
            ?? new HrEmployeeSkill
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employee.Id,
                SkillId = skill.Id,
            };

        row.Proficiency = input.Proficiency;
        row.YearsOfExperience = Math.Max(0, input.YearsOfExperience);
        row.AssessedByEmployeeId = input.AssessedByEmployeeId;
        row.AssessedOn = input.AssessedByEmployeeId is null
            ? row.AssessedOn
            : DateOnly.FromDateTime(DateTime.UtcNow);
        row.CertificateNumber = input.CertificateNumber;
        row.CertifiedUntil = input.CertifiedUntil;
        row.Notes = input.Notes;

        if (row.Id == 0) Db.HrEmployeeSkills.Add(row);
        await Db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(new HrEmployeeSkillDto(
            row.Id, employee.Id, employee.Name, skill.Id, skill.Name, skill.Category,
            row.Proficiency, SkillProficiencies.Describe(row.Proficiency),
            row.YearsOfExperience, row.AssessedOn,
            row.CertificateNumber, row.CertifiedUntil,
            row.CertificateExpired(today), row.Notes));
    }

    /// <summary>
    /// Who can do a thing, and how well.
    ///
    /// This is the question asked before every job — who can rig at height, who
    /// can drive the tempo, who has actually done a mandap. Answering it from
    /// memory is how a crew arrives without an electrician.
    /// </summary>
    [HttpGet("skills/who-can")]
    public async Task<ActionResult<HrSkillSearchDto>> WhoCan(
        [FromQuery] int skillId, [FromQuery] int minimumProficiency,
        [FromQuery] bool onlyCertified, CancellationToken ct)
    {
        var skill = await Db.HrSkills.AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == skillId, ct)
            ?? throw ApiException.NotFound("Skill");

        var floor = Math.Clamp(minimumProficiency, 1, 5);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var rows = await Db.HrEmployeeSkills.AsNoTracking()
            .Include(x => x.Employee)
            .Where(x => x.SkillId == skillId && x.Proficiency >= floor
                && EmploymentStatuses.OnRolls.Contains(x.Employee!.Status))
            .OrderByDescending(x => x.Proficiency).ThenByDescending(x => x.YearsOfExperience)
            .ToListAsync(ct);

        // An expired certificate is reported rather than hidden: the person
        // exists and somebody has to decide whether to send them.
        var people = rows
            .Where(x => !onlyCertified || !x.CertificateExpired(today))
            .Select(x => new HrEmployeeSkillDto(
                x.Id, x.EmployeeId, x.Employee?.Name ?? "—", skill.Id, skill.Name,
                skill.Category, x.Proficiency, SkillProficiencies.Describe(x.Proficiency),
                x.YearsOfExperience, x.AssessedOn, x.CertificateNumber, x.CertifiedUntil,
                x.CertificateExpired(today), x.Notes))
            .ToList();

        return Ok(new HrSkillSearchDto(
            skill.Id, skill.Name, floor, people.Count,
            rows.Count(x => x.CertificateExpired(today)), people));
    }

    /// <summary>Everything one person can do.</summary>
    [HttpGet("employee-skills/{employeeId:int}")]
    public async Task<ActionResult<IReadOnlyList<HrEmployeeSkillDto>>> EmployeeSkills(
        int employeeId, CancellationToken ct)
    {
        var rows = await Db.HrEmployeeSkills.AsNoTracking()
            .Include(x => x.Employee).Include(x => x.Skill)
            .Where(x => x.EmployeeId == employeeId)
            .OrderByDescending(x => x.Proficiency).ThenBy(x => x.Skill!.Name)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(rows.Select(x => new HrEmployeeSkillDto(
            x.Id, x.EmployeeId, x.Employee?.Name ?? "—",
            x.SkillId, x.Skill?.Name ?? "—", x.Skill?.Category,
            x.Proficiency, SkillProficiencies.Describe(x.Proficiency),
            x.YearsOfExperience, x.AssessedOn, x.CertificateNumber, x.CertifiedUntil,
            x.CertificateExpired(today), x.Notes)).ToList());
    }

    /* ================================================================== *
     * Expense claim types
     * ================================================================== */

    [HttpGet("expense-types")]
    public async Task<ActionResult<IReadOnlyList<HrExpenseTypeDto>>> ExpenseTypes(
        CancellationToken ct)
    {
        var rows = await Db.HrExpenseClaimTypes.AsNoTracking()
            .OrderBy(t => t.Name).ToListAsync(ct);

        return Ok(rows.Select(t => new HrExpenseTypeDto(
            t.Id, t.Name, t.Description, t.PerClaimLimit, t.MonthlyLimit,
            t.RequiresReceipt, t.ReceiptWaivedBelow, t.RequiresTravelRequest,
            t.IsActive)).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("expense-types")]
    public async Task<ActionResult<int>> SaveExpenseType(
        [FromBody] HrExpenseTypeInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A claim type needs a name.");
        if (input.PerClaimLimit < 0m || input.MonthlyLimit < 0m)
            throw ApiException.BadRequest("A limit cannot be negative.");
        if (input.MonthlyLimit > 0m && input.PerClaimLimit > input.MonthlyLimit)
        {
            throw ApiException.BadRequest(
                "One claim may not be allowed more than the whole month is.");
        }

        var name = input.Name.Trim();
        var clash = await Db.HrExpenseClaimTypes
            .FirstOrDefaultAsync(t => t.Name == name && t.Id != (input.Id ?? 0), ct);
        if (clash is not null)
            throw ApiException.BadRequest($"{name} already exists.");

        var type = input.Id is int id and > 0
            ? await Db.HrExpenseClaimTypes.FirstOrDefaultAsync(t => t.Id == id, ct)
                ?? throw ApiException.NotFound("Claim type")
            : new HrExpenseClaimType { CompanyId = Db.Tenant.CompanyId };

        type.Name = name;
        type.Description = input.Description;
        type.PerClaimLimit = input.PerClaimLimit;
        type.MonthlyLimit = input.MonthlyLimit;
        type.RequiresReceipt = input.RequiresReceipt;
        type.ReceiptWaivedBelow = input.ReceiptWaivedBelow;
        type.RequiresTravelRequest = input.RequiresTravelRequest;
        type.IsActive = input.IsActive;

        if (type.Id == 0) Db.HrExpenseClaimTypes.Add(type);
        await Db.SaveChangesAsync(ct);
        return Ok(type.Id);
    }

    /// <summary>
    /// What a claim would run into, before it is made.
    ///
    /// Called as the amount is typed, so somebody finds out about the limit
    /// while they can still split the bill rather than after an approver has
    /// rejected it.
    /// </summary>
    [HttpGet("expense-types/{id:int}/check")]
    public async Task<ActionResult<HrExpenseCheckDto>> CheckClaim(
        int id, [FromQuery] int employeeId, [FromQuery] decimal amount,
        [FromQuery] DateOnly? claimDate, [FromQuery] bool hasReceipt,
        [FromQuery] int? travelRequestId, CancellationToken ct)
    {
        var type = await Db.HrExpenseClaimTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Claim type");

        var on = claimDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateOnly(on.Year, on.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var alreadyThisMonth = await Db.HrExpenseClaims.AsNoTracking()
            .Where(c => c.EmployeeId == employeeId
                && c.ExpenseClaimTypeId == id
                && c.ClaimDate >= monthStart && c.ClaimDate <= monthEnd
                && c.Status != HrRequestStatuses.Rejected
                && c.Status != HrRequestStatuses.Cancelled)
            .SumAsync(c => (decimal?)(c.ApprovedAmount ?? c.Amount), ct) ?? 0m;

        var problems = new List<string>();

        if (type.PerClaimLimit > 0m && amount > type.PerClaimLimit)
        {
            problems.Add($"{type.Name} allows {type.PerClaimLimit:0.00} on one claim.");
        }

        if (type.MonthlyLimit > 0m && alreadyThisMonth + amount > type.MonthlyLimit)
        {
            problems.Add($"{type.Name} allows {type.MonthlyLimit:0.00} a month and "
                + $"{alreadyThisMonth:0.00} has already been claimed.");
        }

        if (type.RequiresReceipt && !hasReceipt && amount > type.ReceiptWaivedBelow)
        {
            problems.Add(type.ReceiptWaivedBelow > 0m
                ? $"A bill is needed above {type.ReceiptWaivedBelow:0.00}."
                : "A bill is needed.");
        }

        if (type.RequiresTravelRequest)
        {
            var approved = travelRequestId is int trip
                && await Db.HrTravelRequests.AsNoTracking().AnyAsync(
                    r => r.Id == trip && r.EmployeeId == employeeId
                        && (r.Status == TravelStatuses.Approved
                            || r.Status == TravelStatuses.Completed), ct);

            if (!approved) problems.Add($"{type.Name} has to hang off an approved travel request.");
        }

        return Ok(new HrExpenseCheckDto(
            type.Id, type.Name, amount, alreadyThisMonth,
            type.MonthlyLimit > 0m
                ? Math.Max(0m, type.MonthlyLimit - alreadyThisMonth)
                : null,
            problems.Count == 0, problems));
    }

    /* ================================================================== *
     * Travel
     * ================================================================== */

    [HttpGet("travel")]
    public async Task<ActionResult<IReadOnlyList<HrTravelDto>>> Travel(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrTravelRequests.AsNoTracking()
            .Include(r => r.Employee).Include(r => r.Legs).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var rows = await query.OrderByDescending(r => r.FromDate).Take(300).ToListAsync(ct);
        return Ok(rows.Select(ToTravel).ToList());
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("travel")]
    public async Task<ActionResult<HrTravelDto>> RequestTravel(
        [FromBody] HrTravelInput input, CancellationToken ct)
    {
        if (input.ToDate < input.FromDate)
            throw ApiException.BadRequest("A trip has to end on or after it starts.");
        if (string.IsNullOrWhiteSpace(input.Purpose))
            throw ApiException.BadRequest("Say what the trip is for.");
        if (input.AdvanceRequested > input.EstimatedCost)
        {
            throw ApiException.BadRequest(
                "The advance asked for is more than the trip is expected to cost.");
        }

        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        // Somebody cannot be in Udaipur and Jaipur in the same week, and a
        // clash here is usually a duplicate rather than a plan.
        var clash = await Db.HrTravelRequests.AsNoTracking()
            .Where(r => r.EmployeeId == employee.Id
                && r.Status != TravelStatuses.Rejected
                && r.Status != TravelStatuses.Cancelled
                && r.FromDate <= input.ToDate && r.ToDate >= input.FromDate)
            .FirstOrDefaultAsync(ct);

        if (clash is not null)
        {
            throw ApiException.BadRequest(
                $"{employee.Name} is already travelling to {clash.Destination ?? "somewhere"} "
                + $"from {clash.FromDate:d MMM} to {clash.ToDate:d MMM}.");
        }

        var row = new HrTravelRequest
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = employee.Id,
            Purpose = input.Purpose.Trim(),
            LeadId = input.LeadId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Destination = input.Destination,
            EstimatedCost = input.EstimatedCost,
            AdvanceRequested = input.AdvanceRequested,
            Status = TravelStatuses.PendingApproval,
        };

        var order = 0;
        foreach (var leg in input.Legs ?? [])
        {
            row.Legs.Add(new HrTravelLeg
            {
                CompanyId = Db.Tenant.CompanyId,
                OnDate = leg.OnDate,
                From = leg.From,
                To = leg.To,
                Mode = leg.Mode,
                EstimatedCost = leg.EstimatedCost,
                Notes = leg.Notes,
                SortOrder = order,
            });
            order += 10;
        }

        Db.HrTravelRequests.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(r => r.Employee).LoadAsync(ct);
        return Ok(ToTravel(row));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("travel/{id:int}/decide")]
    public async Task<ActionResult<HrTravelDto>> DecideTravel(
        int id, [FromQuery] bool approve, [FromQuery] decimal? advancePaid,
        [FromQuery] string? note, CancellationToken ct)
    {
        var row = await Db.HrTravelRequests
            .Include(r => r.Employee).Include(r => r.Legs)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Travel request");

        if (row.Status is not TravelStatuses.PendingApproval and not TravelStatuses.Draft)
            throw ApiException.BadRequest("That trip has already been decided.");

        if (!approve)
        {
            if (string.IsNullOrWhiteSpace(note))
                throw ApiException.BadRequest("Say why — somebody has planned around this.");

            row.Status = TravelStatuses.Rejected;
            row.DecisionNote = note;
            row.DecidedAt = DateTime.UtcNow;
            await Db.SaveChangesAsync(ct);
            return Ok(ToTravel(row));
        }

        var paid = advancePaid ?? row.AdvanceRequested;
        if (paid > row.EstimatedCost)
        {
            throw ApiException.BadRequest(
                "The advance is more than the trip is expected to cost.");
        }

        row.Status = TravelStatuses.Approved;
        row.AdvancePaid = paid;
        row.DecisionNote = note;
        row.DecidedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);
        return Ok(ToTravel(row));
    }

    private static HrTravelDto ToTravel(HrTravelRequest r) => new(
        r.Id, r.EmployeeId, r.Employee?.Name ?? "—", r.Purpose, r.LeadId,
        r.FromDate, r.ToDate, r.Nights, r.Destination,
        r.EstimatedCost, r.AdvanceRequested, r.AdvancePaid,
        r.Status, r.DecisionNote, r.DecidedAt,
        r.Legs.OrderBy(l => l.SortOrder).Select(l => new HrTravelLegDto(
            l.Id, l.OnDate, l.From, l.To, l.Mode, l.EstimatedCost, l.Notes)).ToList());

    /* ================================================================== *
     * Timesheets
     * ================================================================== */

    [HttpGet("timesheets")]
    public async Task<ActionResult<IReadOnlyList<HrTimesheetDto>>> Timesheets(
        [FromQuery] int? employeeId, [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrTimesheets.AsNoTracking()
            .Include(t => t.Employee).Include(t => t.Lines).AsQueryable();

        if (employeeId is int id) query = query.Where(t => t.EmployeeId == id);
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(t => t.Status == status);

        var rows = await query
            .OrderByDescending(t => t.WeekStarting).ThenBy(t => t.Employee!.Name)
            .Take(200).ToListAsync(ct);

        return Ok(rows.Select(ToTimesheet).ToList());
    }

    /// <summary>
    /// Write a week of hours.
    ///
    /// Replaces the week wholesale rather than merging: a timesheet is a small
    /// closed list and diffing it buys nothing but a chance to double an entry.
    /// The totals are computed here, so they cannot disagree with the lines.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("timesheets")]
    public async Task<ActionResult<HrTimesheetDto>> SaveTimesheet(
        [FromBody] HrTimesheetInput input, CancellationToken ct)
    {
        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        // Anchored to the Monday, so two people entering the same week cannot
        // create two sheets for it by starting on different days.
        var week = input.WeekStarting.AddDays(
            -(((int)input.WeekStarting.DayOfWeek + 6) % 7));

        if (input.Lines.Any(l => l.Hours <= 0m || l.Hours > 24m))
            throw ApiException.BadRequest("A line's hours have to be between nothing and 24.");

        foreach (var day in input.Lines.GroupBy(l => l.OnDate))
        {
            if (day.Sum(l => l.Hours) > 24m)
            {
                throw ApiException.BadRequest(
                    $"{day.Key:d MMM} adds up to more than 24 hours.");
            }
            if (day.Key < week || day.Key > week.AddDays(6))
            {
                throw ApiException.BadRequest(
                    $"{day.Key:d MMM} is outside the week beginning {week:d MMM}.");
            }
        }

        var sheet = await Db.HrTimesheets.Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.EmployeeId == employee.Id && t.WeekStarting == week, ct)
            ?? new HrTimesheet
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employee.Id,
                WeekStarting = week,
            };

        if (sheet.Status == TimesheetStatuses.Approved)
            throw ApiException.BadRequest("That week has been approved and cannot be rewritten.");

        if (sheet.Id == 0) Db.HrTimesheets.Add(sheet);

        Db.HrTimesheetLines.RemoveRange(sheet.Lines);
        sheet.Lines.Clear();

        foreach (var line in input.Lines)
        {
            sheet.Lines.Add(new HrTimesheetLine
            {
                CompanyId = Db.Tenant.CompanyId,
                OnDate = line.OnDate,
                LeadId = line.LeadId,
                Activity = line.Activity,
                Hours = line.Hours,
                // Overhead cannot be billable — there is no event to bill it to.
                IsBillable = line.IsBillable && line.LeadId is not null,
                Notes = line.Notes,
            });
        }

        sheet.TotalHours = sheet.Lines.Sum(l => l.Hours);
        sheet.BillableHours = sheet.Lines.Where(l => l.IsBillable).Sum(l => l.Hours);
        sheet.Status = input.Submit ? TimesheetStatuses.Submitted : TimesheetStatuses.Draft;
        if (input.Submit) sheet.SubmittedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);
        await Db.Entry(sheet).Reference(t => t.Employee).LoadAsync(ct);
        return Ok(ToTimesheet(sheet));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("timesheets/{id:int}/decide")]
    public async Task<ActionResult<HrTimesheetDto>> DecideTimesheet(
        int id, [FromQuery] bool approve, [FromQuery] string? note, CancellationToken ct)
    {
        var sheet = await Db.HrTimesheets.Include(t => t.Employee).Include(t => t.Lines)
            .FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Timesheet");

        if (sheet.Status != TimesheetStatuses.Submitted)
            throw ApiException.BadRequest("Only a submitted timesheet can be decided.");

        sheet.Status = approve ? TimesheetStatuses.Approved : TimesheetStatuses.Rejected;
        sheet.DecisionNote = note;
        if (approve) sheet.ApprovedAt = DateTime.UtcNow;

        await Db.SaveChangesAsync(ct);
        return Ok(ToTimesheet(sheet));
    }

    /// <summary>
    /// Hours charged to an event, by person and activity.
    ///
    /// The number that says whether a wedding made money: attendance shows the
    /// crew were at work, this shows the three extra days they spent on one job.
    /// </summary>
    [HttpGet("timesheets/by-event")]
    public async Task<ActionResult<IReadOnlyList<HrEventHoursDto>>> HoursByEvent(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var query = Db.HrTimesheetLines.AsNoTracking()
            .Include(l => l.Timesheet).ThenInclude(t => t!.Employee)
            .Where(l => l.LeadId != null
                && l.Timesheet!.Status == TimesheetStatuses.Approved);

        if (from is DateOnly f) query = query.Where(l => l.OnDate >= f);
        if (to is DateOnly t) query = query.Where(l => l.OnDate <= t);

        var lines = await query.ToListAsync(ct);

        return Ok(lines
            .GroupBy(l => l.LeadId!.Value)
            .Select(g => new HrEventHoursDto(
                g.Key,
                g.Select(l => l.Timesheet!.EmployeeId).Distinct().Count(),
                g.Sum(l => l.Hours),
                g.Where(l => l.IsBillable).Sum(l => l.Hours),
                g.GroupBy(l => l.Activity)
                    .Select(a => new HrActivityHoursDto(a.Key, a.Sum(l => l.Hours)))
                    .OrderByDescending(a => a.Hours).ToList()))
            .OrderByDescending(x => x.TotalHours)
            .ToList());
    }

    private static HrTimesheetDto ToTimesheet(HrTimesheet t) => new(
        t.Id, t.EmployeeId, t.Employee?.Name ?? "—", t.WeekStarting,
        t.Status, t.TotalHours, t.BillableHours,
        t.TotalHours == 0m ? 0m : Math.Round(100m * t.BillableHours / t.TotalHours, 0),
        t.SubmittedAt, t.ApprovedAt, t.DecisionNote,
        t.Lines.OrderBy(l => l.OnDate).ThenBy(l => l.Id).Select(l => new HrTimesheetLineDto(
            l.Id, l.OnDate, l.LeadId, l.Activity, l.Hours, l.IsBillable, l.Notes)).ToList());
}
