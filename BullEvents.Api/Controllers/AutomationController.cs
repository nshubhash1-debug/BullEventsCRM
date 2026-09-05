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

public record DayWindowDto(int Open, int Close);

public record HolidayDto(int Id, DateTime Date, string Name, bool IsRecurring);

public record BusinessHoursDto(
    int Id,
    string Name,
    int? BranchId,
    string? BranchName,
    string TimeZoneId,
    IReadOnlyList<DayWindowDto> Days,
    bool IsDefault,
    bool IsActive,
    IReadOnlyList<HolidayDto> Holidays,
    /// <summary>Whether the office is open at this moment — the fastest sanity check there is.</summary>
    bool OpenNow);

public record SaveBusinessHoursRequest(
    [Required] string Name,
    int? BranchId,
    string TimeZoneId,
    IReadOnlyList<DayWindowDto> Days,
    bool IsDefault,
    bool IsActive);

public record SaveHolidayRequest([Required] string Name, DateTime Date, bool IsRecurring);

/* ---------------- assignment ---------------- */

public record AssignmentRuleDto(
    int Id,
    string Name,
    string? Description,
    string Object,
    int SortOrder,
    string? CriteriaField,
    string? CriteriaOperator,
    string? CriteriaValue,
    string Strategy,
    string? PoolUserIds,
    string? PoolRoleKey,
    string? PoolRoleName,
    int? FixedUserId,
    string? FixedUserName,
    /// <summary>How many people the strategy currently has to choose from.</summary>
    int PoolSize,
    bool IsActive);

public record SaveAssignmentRuleRequest(
    [Required] string Name,
    string? Description,
    string Object,
    string? CriteriaField,
    string? CriteriaOperator,
    string? CriteriaValue,
    string Strategy,
    string? PoolUserIds,
    string? PoolRoleKey,
    int? FixedUserId,
    bool IsActive);

/* ---------------- duplicates ---------------- */

public record DuplicateRuleDto(
    int Id,
    string Name,
    string Object,
    string MatchFields,
    string Action,
    bool AcrossOwners,
    bool IsActive);

public record SaveDuplicateRuleRequest(
    [Required] string Name,
    string Object,
    [Required] string MatchFields,
    string Action,
    bool AcrossOwners,
    bool IsActive);

/* ---------------- escalation ---------------- */

public record EscalationRuleDto(
    int Id,
    string Name,
    string Object,
    string? CriteriaField,
    string? CriteriaOperator,
    string? CriteriaValue,
    string StartsFrom,
    int TargetMinutes,
    int? BusinessHoursId,
    string? BusinessHoursName,
    string Action,
    int? ReassignToUserId,
    string? ReassignToUserName,
    bool IsActive,
    /// <summary>How many times this rule has fired, ever.</summary>
    int FiredCount);

public record SaveEscalationRuleRequest(
    [Required] string Name,
    string Object,
    string? CriteriaField,
    string? CriteriaOperator,
    string? CriteriaValue,
    string StartsFrom,
    int TargetMinutes,
    int? BusinessHoursId,
    string Action,
    int? ReassignToUserId,
    bool IsActive);

/* ---------------- approvals ---------------- */

public record ApprovalStepDto(
    int Id,
    int SortOrder,
    string Name,
    string ApproverKind,
    string? ApproverRoleKey,
    string? ApproverRoleName,
    int? ApproverUserId,
    string? ApproverUserName);

public record ApprovalProcessDto(
    int Id,
    string Name,
    string? Description,
    string Object,
    string? CriteriaField,
    string? CriteriaOperator,
    string? CriteriaValue,
    bool LockRecord,
    bool IsActive,
    IReadOnlyList<ApprovalStepDto> Steps);

public record SaveApprovalStepRequest(
    [Required] string Name,
    string ApproverKind,
    string? ApproverRoleKey,
    int? ApproverUserId);

public record SaveApprovalProcessRequest(
    [Required] string Name,
    string? Description,
    string Object,
    string? CriteriaField,
    string? CriteriaOperator,
    string? CriteriaValue,
    bool LockRecord,
    bool IsActive,
    IReadOnlyList<SaveApprovalStepRequest> Steps);

/* ---------------- jobs ---------------- */

public record ScheduledJobDto(
    int Id,
    string Name,
    string Kind,
    string Cron,
    /// <summary>The cron expression as a sentence, so nobody has to decode it.</summary>
    string Schedule,
    bool IsActive,
    DateTime? LastRunAt,
    DateTime? NextRunAt,
    string? LastOutcome,
    string? LastMessage,
    int LastDurationMs,
    int RunCount,
    int FailureCount);

public record SaveScheduledJobRequest(
    [Required] string Name,
    [Required] string Kind,
    [Required] string Cron,
    bool IsActive);

/// <summary>Everything the automation screens need to draw a picker.</summary>
public record AutomationReferenceDto(
    IReadOnlyList<NamedOptionDto> Roles,
    IReadOnlyList<NamedOptionDto> Users,
    IReadOnlyList<NamedOptionDto> Branches,
    IReadOnlyList<NamedOptionDto> BusinessHours,
    string[] Objects,
    string[] Strategies,
    string[] DuplicateActions,
    string[] EscalationActions,
    string[] ApproverKinds,
    string[] JobKinds);

/* ------------------------------------------------------------------ *
 * Controller
 * ------------------------------------------------------------------ */

/// <summary>
/// The rules that run by themselves: routing, duplicate checks, SLA
/// escalations, approvals and scheduled work.
///
/// Gated on the same "act on other people's users" power as the access console.
/// Every one of these changes what happens to somebody else's records without
/// them asking, which is the same kind of authority.
/// </summary>
[ApiController]
[Route("api/admin/automation")]
[Authorize]
[RequirePermission(SecuredObjects.User, ObjectAction.ModifyAll)]
public class AutomationController(AppDbContext db, JobRunner jobs) : ControllerBase
{
    /* ---------------- reference ---------------- */

    [HttpGet("reference")]
    public async Task<ActionResult<AutomationReferenceDto>> Reference(CancellationToken ct)
    {
        var users = await db.Users
            .Where(u => u.IsActive)
            .OrderBy(u => u.Name)
            .Select(u => new NamedOptionDto(u.Id.ToString(), u.Name + " · " + u.Role))
            .ToListAsync(ct);

        var branches = await db.Branches
            .Select(b => new NamedOptionDto(b.Id.ToString(), b.Name))
            .ToListAsync(ct);

        var hours = await db.BusinessHours
            .Where(b => b.IsActive)
            .Select(b => new NamedOptionDto(b.Id.ToString(), b.Name))
            .ToListAsync(ct);

        return Ok(new AutomationReferenceDto(
            RoleCatalog.All.Select(r => new NamedOptionDto(r.Key, r.Name)).ToList(),
            users,
            branches,
            hours,
            SecuredObjects.All,
            AssignmentStrategies.All,
            Models.DuplicateActions.All,
            Models.EscalationActions.All,
            Models.ApproverKinds.All,
            JobKinds.All));
    }

    /* ---------------- business hours ---------------- */

    [HttpGet("business-hours")]
    public async Task<ActionResult<IReadOnlyList<BusinessHoursDto>>> BusinessHoursList(
        CancellationToken ct)
    {
        var rows = await db.BusinessHours
            .Include(b => b.Holidays)
            .OrderByDescending(b => b.IsDefault).ThenBy(b => b.Name)
            .ToListAsync(ct);

        var branches = await db.Branches.ToDictionaryAsync(b => b.Id, b => b.Name, ct);
        var now = DateTime.UtcNow;

        return Ok(rows.Select(b => ToDto(b, branches, now)).ToList());
    }

    [HttpPost("business-hours")]
    public async Task<ActionResult<BusinessHoursDto>> CreateBusinessHours(
        SaveBusinessHoursRequest input, CancellationToken ct)
    {
        var row = new BusinessHours { CompanyId = User.GetCompanyId() };
        await ApplyHoursAsync(row, input, ct);

        db.BusinessHours.Add(row);
        await db.SaveChangesAsync(ct);

        return Ok(await OneHoursAsync(row.Id, ct));
    }

    [HttpPut("business-hours/{id:int}")]
    public async Task<ActionResult<BusinessHoursDto>> UpdateBusinessHours(
        int id, SaveBusinessHoursRequest input, CancellationToken ct)
    {
        var row = await db.BusinessHours.Include(b => b.Holidays)
            .FirstOrDefaultAsync(b => b.Id == id, ct);

        if (row is null) return NotFound(new { message = "Those hours no longer exist." });

        await ApplyHoursAsync(row, input, ct);
        await db.SaveChangesAsync(ct);

        return Ok(await OneHoursAsync(id, ct));
    }

    [HttpDelete("business-hours/{id:int}")]
    public async Task<IActionResult> DeleteBusinessHours(int id, CancellationToken ct)
    {
        var row = await db.BusinessHours.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (row is null) return NoContent();

        // The default set is what every escalation without its own hours falls
        // back to. Deleting it would stop the SLA clock everywhere at once.
        if (row.IsDefault)
        {
            return BadRequest(new
            {
                message = "These are the default hours. Make another set the default first.",
            });
        }

        db.BusinessHours.Remove(row);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpPost("business-hours/{id:int}/holidays")]
    public async Task<ActionResult<HolidayDto>> AddHoliday(
        int id, SaveHolidayRequest input, CancellationToken ct)
    {
        var hours = await db.BusinessHours.FirstOrDefaultAsync(b => b.Id == id, ct);
        if (hours is null) return NotFound(new { message = "Those hours no longer exist." });

        var holiday = new Holiday
        {
            CompanyId = hours.CompanyId,
            BusinessHoursId = id,
            Name = input.Name.Trim(),
            Date = input.Date.Date,
            IsRecurring = input.IsRecurring,
        };

        db.Holidays.Add(holiday);
        await db.SaveChangesAsync(ct);

        return Ok(new HolidayDto(holiday.Id, holiday.Date, holiday.Name, holiday.IsRecurring));
    }

    [HttpDelete("holidays/{id:int}")]
    public async Task<IActionResult> DeleteHoliday(int id, CancellationToken ct)
    {
        var holiday = await db.Holidays.FirstOrDefaultAsync(h => h.Id == id, ct);
        if (holiday is null) return NoContent();

        db.Holidays.Remove(holiday);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /* ---------------- assignment rules ---------------- */

    [HttpGet("assignment-rules")]
    public async Task<ActionResult<IReadOnlyList<AssignmentRuleDto>>> AssignmentRules(CancellationToken ct)
        => Ok(await AssignmentRulesAsync(ct));

    /// <summary>
    /// The list itself, so a create or an update can return the row it just
    /// wrote without going back through the action.
    ///
    /// Reading <c>.Value</c> off an <c>ActionResult</c> that <c>Ok()</c> produced
    /// returns null every time — the payload lives in <c>.Result</c> — and every
    /// one of those paths threw on exactly that.
    /// </summary>
    private async Task<List<AssignmentRuleDto>> AssignmentRulesAsync(CancellationToken ct)
    {
        var rules = await db.AssignmentRules
            .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
            .ToListAsync(ct);

        var users = await db.Users.Where(u => u.IsActive)
            .Select(u => new { u.Id, u.Name, u.Role })
            .ToListAsync(ct);

        var names = users.ToDictionary(u => u.Id, u => u.Name);
        var roleNames = RoleCatalog.All.ToDictionary(
            r => r.Key, r => r.Name, StringComparer.OrdinalIgnoreCase);

        return rules.Select(r => new AssignmentRuleDto(
            r.Id, r.Name, r.Description, r.Object, r.SortOrder,
            r.CriteriaField, r.CriteriaOperator, r.CriteriaValue,
            r.Strategy, r.PoolUserIds, r.PoolRoleKey,
            r.PoolRoleKey is null ? null : roleNames.GetValueOrDefault(r.PoolRoleKey, r.PoolRoleKey),
            r.FixedUserId,
            r.FixedUserId is int f ? names.GetValueOrDefault(f) : null,
            PoolSize(r, users.Select(u => (u.Id, u.Role)).ToList()),
            r.IsActive)).ToList();
    }

    /// <summary>How many active people the rule can actually route to right now.</summary>
    private static int PoolSize(AssignmentRule rule, List<(int Id, string Role)> users)
    {
        if (rule.Strategy == AssignmentStrategies.Fixed)
        {
            return users.Any(u => u.Id == rule.FixedUserId) ? 1 : 0;
        }

        var ids = (rule.PoolUserIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => int.TryParse(x, out var id) ? id : 0)
            .Where(id => id > 0)
            .ToHashSet();

        return users.Count(u => ids.Contains(u.Id)
            || (!string.IsNullOrWhiteSpace(rule.PoolRoleKey)
                && string.Equals(u.Role, rule.PoolRoleKey, StringComparison.OrdinalIgnoreCase)));
    }

    [HttpPost("assignment-rules")]
    public async Task<ActionResult<AssignmentRuleDto>> CreateAssignmentRule(
        SaveAssignmentRuleRequest input, CancellationToken ct)
    {
        if (ValidateAssignment(input) is string error) return BadRequest(new { message = error });

        var last = await db.AssignmentRules.MaxAsync(r => (int?)r.SortOrder, ct) ?? 0;

        var rule = new AssignmentRule
        {
            CompanyId = User.GetCompanyId(),

            // New rules go last. A rule that silently inserted itself above an
            // existing one would change routing for records nobody was thinking
            // about when they wrote it.
            SortOrder = last + 1,
        };

        ApplyAssignment(rule, input);

        db.AssignmentRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return Ok((await AssignmentRulesAsync(ct)).First(r => r.Id == rule.Id));
    }

    [HttpPut("assignment-rules/{id:int}")]
    public async Task<ActionResult<AssignmentRuleDto>> UpdateAssignmentRule(
        int id, SaveAssignmentRuleRequest input, CancellationToken ct)
    {
        if (ValidateAssignment(input) is string error) return BadRequest(new { message = error });

        var rule = await db.AssignmentRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NotFound(new { message = "That rule no longer exists." });

        ApplyAssignment(rule, input);
        await db.SaveChangesAsync(ct);

        return Ok((await AssignmentRulesAsync(ct)).First(r => r.Id == id));
    }

    /// <summary>Reorders the whole set. Order is the rule, so it is saved as one.</summary>
    [HttpPut("assignment-rules/order")]
    public async Task<IActionResult> ReorderAssignmentRules(
        [FromBody] int[] orderedIds, CancellationToken ct)
    {
        var rules = await db.AssignmentRules.ToListAsync(ct);

        for (var i = 0; i < orderedIds.Length; i++)
        {
            var rule = rules.FirstOrDefault(r => r.Id == orderedIds[i]);
            if (rule is not null) rule.SortOrder = i + 1;
        }

        await db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpDelete("assignment-rules/{id:int}")]
    public async Task<IActionResult> DeleteAssignmentRule(int id, CancellationToken ct)
    {
        var rule = await db.AssignmentRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NoContent();

        db.AssignmentRules.Remove(rule);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static string? ValidateAssignment(SaveAssignmentRuleRequest input)
    {
        if (!AssignmentStrategies.All.Contains(input.Strategy))
        {
            return $"'{input.Strategy}' is not a routing strategy.";
        }

        if (input.Strategy == AssignmentStrategies.Fixed && input.FixedUserId is null)
        {
            return "Choose the person this rule routes to.";
        }

        if (input.Strategy != AssignmentStrategies.Fixed
            && string.IsNullOrWhiteSpace(input.PoolRoleKey)
            && string.IsNullOrWhiteSpace(input.PoolUserIds))
        {
            return "A rotating rule needs a pool — a role, some people, or both.";
        }

        return null;
    }

    private static void ApplyAssignment(AssignmentRule rule, SaveAssignmentRuleRequest input)
    {
        rule.Name = input.Name.Trim();
        rule.Description = input.Description?.Trim();
        rule.Object = input.Object;
        rule.CriteriaField = Blank(input.CriteriaField);
        rule.CriteriaOperator = Blank(input.CriteriaOperator) ?? "equals";
        rule.CriteriaValue = Blank(input.CriteriaValue);
        rule.Strategy = input.Strategy;
        rule.PoolUserIds = Blank(input.PoolUserIds);
        rule.PoolRoleKey = Blank(input.PoolRoleKey);
        rule.FixedUserId = input.Strategy == AssignmentStrategies.Fixed ? input.FixedUserId : null;
        rule.IsActive = input.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
    }

    /* ---------------- duplicate rules ---------------- */

    [HttpGet("duplicate-rules")]
    public async Task<ActionResult<IReadOnlyList<DuplicateRuleDto>>> DuplicateRules(
        CancellationToken ct)
        => Ok(await db.DuplicateRules
            .OrderBy(r => r.Name)
            .Select(r => new DuplicateRuleDto(
                r.Id, r.Name, r.Object, r.MatchFields, r.Action, r.AcrossOwners, r.IsActive))
            .ToListAsync(ct));

    [HttpPost("duplicate-rules")]
    public async Task<ActionResult<DuplicateRuleDto>> CreateDuplicateRule(
        SaveDuplicateRuleRequest input, CancellationToken ct)
    {
        if (!Models.DuplicateActions.All.Contains(input.Action))
        {
            return BadRequest(new { message = $"'{input.Action}' is not a duplicate action." });
        }

        var rule = new DuplicateRule { CompanyId = User.GetCompanyId() };
        ApplyDuplicate(rule, input);

        db.DuplicateRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return Ok(new DuplicateRuleDto(
            rule.Id, rule.Name, rule.Object, rule.MatchFields,
            rule.Action, rule.AcrossOwners, rule.IsActive));
    }

    [HttpPut("duplicate-rules/{id:int}")]
    public async Task<ActionResult<DuplicateRuleDto>> UpdateDuplicateRule(
        int id, SaveDuplicateRuleRequest input, CancellationToken ct)
    {
        var rule = await db.DuplicateRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NotFound(new { message = "That rule no longer exists." });

        ApplyDuplicate(rule, input);
        await db.SaveChangesAsync(ct);

        return Ok(new DuplicateRuleDto(
            rule.Id, rule.Name, rule.Object, rule.MatchFields,
            rule.Action, rule.AcrossOwners, rule.IsActive));
    }

    [HttpDelete("duplicate-rules/{id:int}")]
    public async Task<IActionResult> DeleteDuplicateRule(int id, CancellationToken ct)
    {
        var rule = await db.DuplicateRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NoContent();

        db.DuplicateRules.Remove(rule);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static void ApplyDuplicate(DuplicateRule rule, SaveDuplicateRuleRequest input)
    {
        rule.Name = input.Name.Trim();
        rule.Object = input.Object;
        rule.MatchFields = input.MatchFields;
        rule.Action = input.Action;
        rule.AcrossOwners = input.AcrossOwners;
        rule.IsActive = input.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
    }

    /* ---------------- escalation rules ---------------- */

    [HttpGet("escalation-rules")]
    public async Task<ActionResult<IReadOnlyList<EscalationRuleDto>>> EscalationRules(CancellationToken ct)
        => Ok(await EscalationRulesAsync(ct));

    /// <summary>
    /// The list itself, so a create or an update can return the row it just
    /// wrote without going back through the action.
    ///
    /// Reading <c>.Value</c> off an <c>ActionResult</c> that <c>Ok()</c> produced
    /// returns null every time — the payload lives in <c>.Result</c> — and every
    /// one of those paths threw on exactly that.
    /// </summary>
    private async Task<List<EscalationRuleDto>> EscalationRulesAsync(CancellationToken ct)
    {
        var rules = await db.EscalationRules
            .Include(r => r.BusinessHours)
            .OrderBy(r => r.Name)
            .ToListAsync(ct);

        var names = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name, ct);

        var fired = await db.EscalationEvents
            .GroupBy(e => e.EscalationRuleId)
            .Select(g => new { RuleId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.RuleId, x => x.Count, ct);

        return rules.Select(r => new EscalationRuleDto(
            r.Id, r.Name, r.Object,
            r.CriteriaField, r.CriteriaOperator, r.CriteriaValue,
            r.StartsFrom, r.TargetMinutes,
            r.BusinessHoursId, r.BusinessHours?.Name,
            r.Action, r.ReassignToUserId,
            r.ReassignToUserId is int u ? names.GetValueOrDefault(u) : null,
            r.IsActive,
            fired.GetValueOrDefault(r.Id))).ToList();
    }

    [HttpPost("escalation-rules")]
    public async Task<ActionResult<EscalationRuleDto>> CreateEscalationRule(
        SaveEscalationRuleRequest input, CancellationToken ct)
    {
        if (ValidateEscalation(input) is string error) return BadRequest(new { message = error });

        var rule = new EscalationRule { CompanyId = User.GetCompanyId() };
        ApplyEscalation(rule, input);

        db.EscalationRules.Add(rule);
        await db.SaveChangesAsync(ct);

        return Ok((await EscalationRulesAsync(ct)).First(r => r.Id == rule.Id));
    }

    [HttpPut("escalation-rules/{id:int}")]
    public async Task<ActionResult<EscalationRuleDto>> UpdateEscalationRule(
        int id, SaveEscalationRuleRequest input, CancellationToken ct)
    {
        if (ValidateEscalation(input) is string error) return BadRequest(new { message = error });

        var rule = await db.EscalationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NotFound(new { message = "That rule no longer exists." });

        ApplyEscalation(rule, input);
        await db.SaveChangesAsync(ct);

        return Ok((await EscalationRulesAsync(ct)).First(r => r.Id == id));
    }

    [HttpDelete("escalation-rules/{id:int}")]
    public async Task<IActionResult> DeleteEscalationRule(int id, CancellationToken ct)
    {
        var rule = await db.EscalationRules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null) return NoContent();

        db.EscalationRules.Remove(rule);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private static string? ValidateEscalation(SaveEscalationRuleRequest input)
    {
        if (!Models.EscalationActions.All.Contains(input.Action))
        {
            return $"'{input.Action}' is not an escalation action.";
        }

        if (input.TargetMinutes <= 0) return "The target has to be more than zero minutes.";

        if (input.Action == Models.EscalationActions.Reassign && input.ReassignToUserId is null)
        {
            return "Choose who the record should be reassigned to.";
        }

        return null;
    }

    private static void ApplyEscalation(EscalationRule rule, SaveEscalationRuleRequest input)
    {
        rule.Name = input.Name.Trim();
        rule.Object = input.Object;
        rule.CriteriaField = Blank(input.CriteriaField);
        rule.CriteriaOperator = Blank(input.CriteriaOperator) ?? "equals";
        rule.CriteriaValue = Blank(input.CriteriaValue);
        rule.StartsFrom = input.StartsFrom;
        rule.TargetMinutes = input.TargetMinutes;
        rule.BusinessHoursId = input.BusinessHoursId;
        rule.Action = input.Action;
        rule.ReassignToUserId = input.Action == Models.EscalationActions.Reassign
            ? input.ReassignToUserId : null;
        rule.IsActive = input.IsActive;
        rule.UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>Runs the sweep now, so a rule can be tried without waiting for the schedule.</summary>
    [HttpPost("escalation-rules/run")]
    public async Task<ActionResult<object>> RunSweep(
        [FromServices] EscalationSweep sweep, CancellationToken ct)
    {
        var result = await sweep.RunAsync(ct);
        return Ok(new { result.Checked, result.Breached, result.Acted, result.Notes });
    }

    /* ---------------- approval processes ---------------- */

    [HttpGet("approval-processes")]
    public async Task<ActionResult<IReadOnlyList<ApprovalProcessDto>>> ApprovalProcesses(CancellationToken ct)
        => Ok(await ApprovalProcessesAsync(ct));

    /// <summary>
    /// The list itself, so a create or an update can return the row it just
    /// wrote without going back through the action.
    ///
    /// Reading <c>.Value</c> off an <c>ActionResult</c> that <c>Ok()</c> produced
    /// returns null every time — the payload lives in <c>.Result</c> — and every
    /// one of those paths threw on exactly that.
    /// </summary>
    private async Task<List<ApprovalProcessDto>> ApprovalProcessesAsync(CancellationToken ct)
    {
        var processes = await db.ApprovalProcesses
            .Include(p => p.Steps)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var names = await db.Users.ToDictionaryAsync(u => u.Id, u => u.Name, ct);
        var roleNames = RoleCatalog.All.ToDictionary(
            r => r.Key, r => r.Name, StringComparer.OrdinalIgnoreCase);

        return processes.Select(p => new ApprovalProcessDto(
            p.Id, p.Name, p.Description, p.Object,
            p.CriteriaField, p.CriteriaOperator, p.CriteriaValue,
            p.LockRecord, p.IsActive,
            p.Steps.OrderBy(s => s.SortOrder).Select(s => new ApprovalStepDto(
                s.Id, s.SortOrder, s.Name, s.ApproverKind,
                s.ApproverRoleKey,
                s.ApproverRoleKey is null
                    ? null : roleNames.GetValueOrDefault(s.ApproverRoleKey, s.ApproverRoleKey),
                s.ApproverUserId,
                s.ApproverUserId is int u ? names.GetValueOrDefault(u) : null)).ToList()))
            .ToList();
    }

    [HttpPost("approval-processes")]
    public async Task<ActionResult<ApprovalProcessDto>> CreateApprovalProcess(
        SaveApprovalProcessRequest input, CancellationToken ct)
    {
        if (input.Steps.Count == 0)
        {
            return BadRequest(new { message = "An approval needs at least one step." });
        }

        var process = new ApprovalProcess { CompanyId = User.GetCompanyId() };
        ApplyApproval(process, input);

        db.ApprovalProcesses.Add(process);
        await db.SaveChangesAsync(ct);

        return Ok((await ApprovalProcessesAsync(ct)).First(p => p.Id == process.Id));
    }

    [HttpPut("approval-processes/{id:int}")]
    public async Task<ActionResult<ApprovalProcessDto>> UpdateApprovalProcess(
        int id, SaveApprovalProcessRequest input, CancellationToken ct)
    {
        if (input.Steps.Count == 0)
        {
            return BadRequest(new { message = "An approval needs at least one step." });
        }

        var process = await db.ApprovalProcesses
            .Include(p => p.Steps)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

        if (process is null) return NotFound(new { message = "That process no longer exists." });

        // Steps are replaced wholesale rather than diffed. They are an ordered
        // list whose meaning is the order, and matching them up by name across
        // an edit is guesswork that silently reorders somebody's approval chain.
        db.ApprovalSteps.RemoveRange(process.Steps);
        process.Steps.Clear();

        ApplyApproval(process, input);
        await db.SaveChangesAsync(ct);

        return Ok((await ApprovalProcessesAsync(ct)).First(p => p.Id == id));
    }

    [HttpDelete("approval-processes/{id:int}")]
    public async Task<IActionResult> DeleteApprovalProcess(int id, CancellationToken ct)
    {
        var process = await db.ApprovalProcesses.FirstOrDefaultAsync(p => p.Id == id, ct);
        if (process is null) return NoContent();

        db.ApprovalProcesses.Remove(process);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    private void ApplyApproval(ApprovalProcess process, SaveApprovalProcessRequest input)
    {
        process.Name = input.Name.Trim();
        process.Description = input.Description?.Trim();
        process.Object = input.Object;
        process.CriteriaField = Blank(input.CriteriaField);
        process.CriteriaOperator = Blank(input.CriteriaOperator) ?? "equals";
        process.CriteriaValue = Blank(input.CriteriaValue);
        process.LockRecord = input.LockRecord;
        process.IsActive = input.IsActive;
        process.UpdatedAt = DateTime.UtcNow;

        var order = 1;

        foreach (var step in input.Steps)
        {
            process.Steps.Add(new ApprovalStep
            {
                CompanyId = process.CompanyId,
                SortOrder = order++,
                Name = step.Name.Trim(),
                ApproverKind = step.ApproverKind,
                ApproverRoleKey = step.ApproverKind == Models.ApproverKinds.Role
                    ? step.ApproverRoleKey : null,
                ApproverUserId = step.ApproverKind == Models.ApproverKinds.User
                    ? step.ApproverUserId : null,
            });
        }
    }

    /* ---------------- scheduled jobs ---------------- */

    [HttpGet("jobs")]
    public async Task<ActionResult<IReadOnlyList<ScheduledJobDto>>> Jobs(CancellationToken ct)
        => Ok(await JobsAsync(ct));

    /// <summary>
    /// The list itself, so a create or an update can return the row it just
    /// wrote without going back through the action.
    ///
    /// Reading <c>.Value</c> off an <c>ActionResult</c> that <c>Ok()</c> produced
    /// returns null every time — the payload lives in <c>.Result</c> — and every
    /// one of those paths threw on exactly that.
    /// </summary>
    private async Task<List<ScheduledJobDto>> JobsAsync(CancellationToken ct)
        => await db.ScheduledJobs
            .OrderBy(j => j.Name)
            .Select(j => new ScheduledJobDto(
                j.Id, j.Name, j.Kind, j.Cron, Cron.Describe(j.Cron), j.IsActive,
                j.LastRunAt, j.NextRunAt, j.LastOutcome, j.LastMessage,
                j.LastDurationMs, j.RunCount, j.FailureCount))
            .ToListAsync(ct);

    [HttpPost("jobs")]
    public async Task<ActionResult<ScheduledJobDto>> CreateJob(
        SaveScheduledJobRequest input, CancellationToken ct)
    {
        if (!JobKinds.All.Contains(input.Kind))
        {
            return BadRequest(new { message = $"'{input.Kind}' is not a job this system runs." });
        }

        if (Cron.Next(input.Cron, DateTime.UtcNow) is null)
        {
            return BadRequest(new { message = "That schedule never comes round." });
        }

        var job = new ScheduledJob
        {
            CompanyId = User.GetCompanyId(),
            Name = input.Name.Trim(),
            Kind = input.Kind,
            Cron = input.Cron.Trim(),
            IsActive = input.IsActive,
            NextRunAt = Cron.Next(input.Cron, DateTime.UtcNow),
        };

        db.ScheduledJobs.Add(job);
        await db.SaveChangesAsync(ct);

        return Ok((await JobsAsync(ct)).First(j => j.Id == job.Id));
    }

    [HttpPut("jobs/{id:int}")]
    public async Task<ActionResult<ScheduledJobDto>> UpdateJob(
        int id, SaveScheduledJobRequest input, CancellationToken ct)
    {
        var job = await db.ScheduledJobs.FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job is null) return NotFound(new { message = "That job no longer exists." });

        if (Cron.Next(input.Cron, DateTime.UtcNow) is null)
        {
            return BadRequest(new { message = "That schedule never comes round." });
        }

        job.Name = input.Name.Trim();
        job.Kind = input.Kind;
        job.Cron = input.Cron.Trim();
        job.IsActive = input.IsActive;
        job.NextRunAt = Cron.Next(job.Cron, DateTime.UtcNow);
        job.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);

        return Ok((await JobsAsync(ct)).First(j => j.Id == id));
    }

    [HttpDelete("jobs/{id:int}")]
    public async Task<IActionResult> DeleteJob(int id, CancellationToken ct)
    {
        var job = await db.ScheduledJobs.FirstOrDefaultAsync(j => j.Id == id, ct);
        if (job is null) return NoContent();

        db.ScheduledJobs.Remove(job);
        await db.SaveChangesAsync(ct);

        return NoContent();
    }

    /// <summary>Runs a job immediately, so a schedule can be tried without waiting for it.</summary>
    [HttpPost("jobs/{id:int}/run")]
    public async Task<ActionResult<ScheduledJobDto>> RunJob(int id, CancellationToken ct)
    {
        var exists = await db.ScheduledJobs.AnyAsync(j => j.Id == id, ct);
        if (!exists) return NotFound(new { message = "That job no longer exists." });

        await jobs.RunNowAsync(id, ct);

        return Ok((await JobsAsync(ct)).First(j => j.Id == id));
    }

    /* ---------------- helpers ---------------- */

    private static string? Blank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static BusinessHoursDto ToDto(
        BusinessHours b, Dictionary<int, string> branches, DateTime now) =>
        new(b.Id, b.Name, b.BranchId,
            b.BranchId is int id ? branches.GetValueOrDefault(id) : null,
            b.TimeZoneId,
            [
                new DayWindowDto(b.SundayOpen, b.SundayClose),
                new DayWindowDto(b.MondayOpen, b.MondayClose),
                new DayWindowDto(b.TuesdayOpen, b.TuesdayClose),
                new DayWindowDto(b.WednesdayOpen, b.WednesdayClose),
                new DayWindowDto(b.ThursdayOpen, b.ThursdayClose),
                new DayWindowDto(b.FridayOpen, b.FridayClose),
                new DayWindowDto(b.SaturdayOpen, b.SaturdayClose),
            ],
            b.IsDefault, b.IsActive,
            b.Holidays.OrderBy(h => h.Date)
                .Select(h => new HolidayDto(h.Id, h.Date, h.Name, h.IsRecurring)).ToList(),
            WorkingHours.IsOpen(b, now));

    private async Task<BusinessHoursDto> OneHoursAsync(int id, CancellationToken ct)
    {
        var row = await db.BusinessHours.Include(b => b.Holidays).FirstAsync(b => b.Id == id, ct);
        var branches = await db.Branches.ToDictionaryAsync(b => b.Id, b => b.Name, ct);

        return ToDto(row, branches, DateTime.UtcNow);
    }

    private async Task ApplyHoursAsync(
        BusinessHours row, SaveBusinessHoursRequest input, CancellationToken ct)
    {
        row.Name = input.Name.Trim();
        row.BranchId = input.BranchId;
        row.TimeZoneId = string.IsNullOrWhiteSpace(input.TimeZoneId)
            ? "Asia/Kolkata" : input.TimeZoneId.Trim();
        row.IsActive = input.IsActive;
        row.UpdatedAt = DateTime.UtcNow;

        var days = input.Days;

        if (days.Count == 7)
        {
            (row.SundayOpen, row.SundayClose) = (days[0].Open, days[0].Close);
            (row.MondayOpen, row.MondayClose) = (days[1].Open, days[1].Close);
            (row.TuesdayOpen, row.TuesdayClose) = (days[2].Open, days[2].Close);
            (row.WednesdayOpen, row.WednesdayClose) = (days[3].Open, days[3].Close);
            (row.ThursdayOpen, row.ThursdayClose) = (days[4].Open, days[4].Close);
            (row.FridayOpen, row.FridayClose) = (days[5].Open, days[5].Close);
            (row.SaturdayOpen, row.SaturdayClose) = (days[6].Open, days[6].Close);
        }

        // Exactly one default. Clearing the others here rather than trusting the
        // caller means the fallback the SLA clock depends on is never ambiguous.
        if (input.IsDefault)
        {
            await db.BusinessHours
                .Where(b => b.Id != row.Id && b.IsDefault)
                .ExecuteUpdateAsync(b => b.SetProperty(x => x.IsDefault, false), ct);
        }

        row.IsDefault = input.IsDefault;
    }
}
