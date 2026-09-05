using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

[ApiController]
[Route("api/hr")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrAdvancedController(AppDbContext db) : CrmControllerBase(db)
{
    private static DateTime IndiaNow()
    {
        try
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata"));
        }
    }

    /* ---------------- org ---------------- */

    [HttpGet("org")]
    public async Task<ActionResult<IReadOnlyList<HrOrgNodeDto>>> Org(CancellationToken ct)
    {
        var people = await Db.HrEmployees.AsNoTracking()
            .Include(e => e.Department).Include(e => e.Designation)
            .Where(e => EmploymentStatuses.OnRolls.Contains(e.Status))
            .ToListAsync(ct);
        var byId = people.ToDictionary(e => e.Id);
        IReadOnlyList<HrOrgNodeDto> ChildrenOf(int? managerId) =>
            people.Where(e => e.ManagerEmployeeId == managerId && e.Id != managerId)
                .Select(e => new HrOrgNodeDto(
                    e.Id, e.EmployeeCode, e.Name,
                    e.Designation?.Name ?? "—",
                    e.Department?.Name ?? "—",
                    ChildrenOf(e.Id)))
                .ToList();

        var roots = people
            .Where(e => e.ManagerEmployeeId is not int mid || !byId.ContainsKey(mid) || mid == e.Id)
            .Select(e => new HrOrgNodeDto(
                e.Id, e.EmployeeCode, e.Name,
                e.Designation?.Name ?? "—",
                e.Department?.Name ?? "—",
                ChildrenOf(e.Id)))
            .ToList();
        return Ok(roots);
    }

    /* ---------------- punches / geo attendance ---------------- */

    [HttpGet("punches")]
    public async Task<ActionResult<IReadOnlyList<HrPunchDto>>> Punches(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, CancellationToken ct)
    {
        var start = from ?? DateOnly.FromDateTime(IndiaNow()).AddDays(-7);
        var end = to ?? DateOnly.FromDateTime(IndiaNow());
        var startUtc = DateTime.SpecifyKind(start.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
        var endUtc = DateTime.SpecifyKind(end.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);
        var rows = await Db.HrPunches.AsNoTracking()
            .Include(p => p.Employee)
            .Where(p => p.At >= startUtc && p.At <= endUtc)
            .OrderByDescending(p => p.At)
            .Take(400)
            .ToListAsync(ct);
        return Ok(rows.Select(ToPunch));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("punches")]
    public async Task<ActionResult<HrPunchDto>> Punch(HrPunchInput input, CancellationToken ct)
    {
        var kind = Require(input.Kind, HrPunchKinds.All, "kind");
        var employeeId = input.EmployeeId ?? 0;
        if (employeeId == 0)
        {
            employeeId = await Db.HrEmployees
                .Where(e => e.UserId == Db.Tenant.UserId)
                .Select(e => e.Id)
                .FirstOrDefaultAsync(ct);
        }
        if (employeeId == 0) throw ApiException.NotFound("Employee");

        var india = IndiaNow();
        var workDate = DateOnly.FromDateTime(india);
        var punch = new HrPunch
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = employeeId,
            At = DateTime.UtcNow,
            Kind = kind,
            Latitude = input.Latitude,
            Longitude = input.Longitude,
            Address = input.Address,
            Device = input.Device ?? "Web",
        };
        Db.HrPunches.Add(punch);

        var day = await Db.HrAttendances
            .FirstOrDefaultAsync(a => a.EmployeeId == employeeId && a.WorkDate == workDate, ct);
        var shift = await Db.HrEmployees.Where(e => e.Id == employeeId)
            .Select(e => e.Shift)
            .FirstOrDefaultAsync(ct);
        if (day is null)
        {
            day = new HrAttendance
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employeeId,
                WorkDate = workDate,
                Status = AttendanceDayStatuses.Present,
                Source = "Punch",
            };
            Db.HrAttendances.Add(day);
        }

        if (kind == HrPunchKinds.In)
        {
            day.InTime ??= india.TimeOfDay;
            if (shift is not null && india.TimeOfDay > shift.StartTime.Add(TimeSpan.FromMinutes(shift.GraceMinutes)))
            {
                day.IsLate = true;
                day.Status = AttendanceDayStatuses.Late;
            }
            else
            {
                day.Status = AttendanceDayStatuses.Present;
            }
        }
        else
        {
            day.OutTime = india.TimeOfDay;
            if (shift is not null && india.TimeOfDay < shift.EndTime.Add(TimeSpan.FromMinutes(-shift.GraceMinutes)))
                day.LeftEarly = true;
        }

        await Db.SaveChangesAsync(ct);
        var name = await Db.HrEmployees.Where(e => e.Id == employeeId).Select(e => e.Name).FirstAsync(ct);
        punch.Employee = new HrEmployee { Name = name };
        return Ok(ToPunch(punch));
    }

    /* ---------------- holidays / policies / news ---------------- */

    [HttpGet("holidays")]
    public async Task<ActionResult<IReadOnlyList<HrHolidayDto>>> Holidays(CancellationToken ct)
    {
        var rows = await Db.HrHolidays.AsNoTracking().OrderBy(h => h.OnDate).ToListAsync(ct);
        return Ok(rows.Select(h => new HrHolidayDto(h.Id, h.Name, h.OnDate, h.Optional, h.Locations)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("holidays")]
    public async Task<ActionResult<HrHolidayDto>> CreateHoliday(HrHolidayInput input, CancellationToken ct)
    {
        var row = new HrHoliday
        {
            CompanyId = Db.Tenant.CompanyId,
            Name = input.Name.Trim(),
            OnDate = input.OnDate,
            Optional = input.Optional,
            Locations = input.Locations,
        };
        Db.HrHolidays.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrHolidayDto(row.Id, row.Name, row.OnDate, row.Optional, row.Locations));
    }

    [HttpGet("policies")]
    public async Task<ActionResult<IReadOnlyList<HrPolicyDto>>> Policies(CancellationToken ct)
    {
        var rows = await Db.HrPolicies.AsNoTracking().OrderByDescending(p => p.Id).ToListAsync(ct);
        return Ok(rows.Select(p => new HrPolicyDto(p.Id, p.Title, p.Category, p.Body, p.EffectiveFrom)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("policies")]
    public async Task<ActionResult<HrPolicyDto>> CreatePolicy(HrPolicyInput input, CancellationToken ct)
    {
        var row = new HrPolicy
        {
            CompanyId = Db.Tenant.CompanyId,
            Title = input.Title.Trim(),
            Category = string.IsNullOrWhiteSpace(input.Category) ? "General" : input.Category,
            Body = input.Body,
            EffectiveFrom = input.EffectiveFrom,
            AttachmentUrl = input.AttachmentUrl,
        };
        Db.HrPolicies.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrPolicyDto(row.Id, row.Title, row.Category, row.Body, row.EffectiveFrom));
    }

    [HttpGet("announcements")]
    public async Task<ActionResult<IReadOnlyList<HrAnnouncementDto>>> Announcements(CancellationToken ct)
    {
        var rows = await Db.HrAnnouncements.AsNoTracking().OrderByDescending(a => a.Id).Take(40).ToListAsync(ct);
        return Ok(rows.Select(a => new HrAnnouncementDto(a.Id, a.Title, a.Body, a.PinUntil, a.Audience)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("announcements")]
    public async Task<ActionResult<HrAnnouncementDto>> CreateAnnouncement(HrAnnouncementInput input, CancellationToken ct)
    {
        var row = new HrAnnouncement
        {
            CompanyId = Db.Tenant.CompanyId,
            Title = input.Title.Trim(),
            Body = input.Body,
            PinUntil = input.PinUntil,
            Audience = string.IsNullOrWhiteSpace(input.Audience) ? "All" : input.Audience,
        };
        Db.HrAnnouncements.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrAnnouncementDto(row.Id, row.Title, row.Body, row.PinUntil, row.Audience));
    }

    /* ---------------- expenses ---------------- */

    [HttpGet("expenses")]
    public async Task<ActionResult<IReadOnlyList<HrExpenseDto>>> Expenses(CancellationToken ct)
    {
        var rows = await Db.HrExpenseClaims.AsNoTracking().Include(x => x.Employee)
            .OrderByDescending(x => x.Id).Take(200).ToListAsync(ct);
        return Ok(rows.Select(x => new HrExpenseDto(
            x.Id, x.EmployeeId, x.Employee?.Name ?? "—", x.ClaimDate, x.Category,
            x.Amount, x.Description, x.Status)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("expenses")]
    public async Task<ActionResult<HrExpenseDto>> CreateExpense(HrExpenseInput input, CancellationToken ct)
    {
        var row = new HrExpenseClaim
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            ClaimDate = input.ClaimDate,
            Category = input.Category,
            Amount = input.Amount,
            Description = input.Description,
            BillUrl = input.BillUrl,
            Status = HrRequestStatuses.PendingManager,
        };
        Db.HrExpenseClaims.Add(row);
        await Db.SaveChangesAsync(ct);
        var name = await NameOf(input.EmployeeId, ct);
        return Ok(new HrExpenseDto(
            row.Id, row.EmployeeId, name, row.ClaimDate, row.Category, row.Amount, row.Description, row.Status));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("expenses/{id:int}/decide")]
    public async Task<IActionResult> DecideExpense(int id, [FromQuery] bool approve, CancellationToken ct)
    {
        var row = await Db.HrExpenseClaims.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiException.NotFound("Expense");
        row.Status = Advance(row.Status, approve);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- helpdesk ---------------- */

    [HttpGet("tickets")]
    public async Task<ActionResult<IReadOnlyList<HrTicketDto>>> Tickets(CancellationToken ct)
    {
        var rows = await Db.HrHelpdeskTickets.AsNoTracking().Include(t => t.Employee)
            .OrderByDescending(t => t.Id).Take(200).ToListAsync(ct);
        return Ok(rows.Select(t => new HrTicketDto(
            t.Id, t.EmployeeId, t.Employee?.Name ?? "—", t.Subject, t.Category,
            t.Priority, t.Body, t.Status, t.AssignedToEmployeeId)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("tickets")]
    public async Task<ActionResult<HrTicketDto>> CreateTicket(HrTicketInput input, CancellationToken ct)
    {
        var row = new HrHelpdeskTicket
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            Subject = input.Subject.Trim(),
            Category = input.Category,
            Priority = input.Priority,
            Body = input.Body,
        };
        Db.HrHelpdeskTickets.Add(row);
        await Db.SaveChangesAsync(ct);
        var name = await NameOf(input.EmployeeId, ct);
        return Ok(new HrTicketDto(
            row.Id, row.EmployeeId, name, row.Subject, row.Category, row.Priority, row.Body, row.Status, null));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("tickets/{id:int}/status")]
    public async Task<IActionResult> TicketStatus(int id, [FromQuery] string status, CancellationToken ct)
    {
        var row = await Db.HrHelpdeskTickets.FirstOrDefaultAsync(t => t.Id == id, ct)
            ?? throw ApiException.NotFound("Ticket");
        row.Status = Require(status, HrTicketStatuses.All, "status");
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- PMS ---------------- */

    [HttpGet("cycles")]
    public async Task<ActionResult<IReadOnlyList<HrCycleDto>>> Cycles(CancellationToken ct)
    {
        var rows = await Db.HrAppraisalCycles.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync(ct);
        return Ok(rows.Select(c => new HrCycleDto(c.Id, c.Name, c.FromDate, c.ToDate, c.Status)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("cycles")]
    public async Task<ActionResult<HrCycleDto>> CreateCycle(HrCycleInput input, CancellationToken ct)
    {
        var row = new HrAppraisalCycle
        {
            CompanyId = Db.Tenant.CompanyId,
            Name = input.Name.Trim(),
            FromDate = input.FromDate,
            ToDate = input.ToDate,
        };
        Db.HrAppraisalCycles.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrCycleDto(row.Id, row.Name, row.FromDate, row.ToDate, row.Status));
    }

    [HttpGet("goals")]
    public async Task<ActionResult<IReadOnlyList<HrGoalDto>>> Goals(CancellationToken ct)
    {
        var rows = await Db.HrGoals.AsNoTracking().Include(g => g.Employee)
            .OrderByDescending(g => g.Id).Take(200).ToListAsync(ct);
        return Ok(rows.Select(g => new HrGoalDto(
            g.Id, g.EmployeeId, g.Employee?.Name ?? "—", g.CycleId, g.Title, g.Kra,
            g.Target, g.Weight, g.Progress, g.Status)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("goals")]
    public async Task<ActionResult<HrGoalDto>> CreateGoal(HrGoalInput input, CancellationToken ct)
    {
        var row = new HrGoal
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            CycleId = input.CycleId,
            Title = input.Title.Trim(),
            Kra = input.Kra,
            Target = input.Target,
            Weight = input.Weight <= 0 ? 100 : input.Weight,
        };
        Db.HrGoals.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrGoalDto(
            row.Id, row.EmployeeId, await NameOf(row.EmployeeId, ct), row.CycleId, row.Title,
            row.Kra, row.Target, row.Weight, row.Progress, row.Status));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("goals/{id:int}/progress")]
    public async Task<IActionResult> GoalProgress(int id, [FromQuery] decimal progress, CancellationToken ct)
    {
        var row = await Db.HrGoals.FirstOrDefaultAsync(g => g.Id == id, ct)
            ?? throw ApiException.NotFound("Goal");
        row.Progress = Math.Clamp(progress, 0, 100);
        if (row.Progress >= 100) row.Status = "Done";
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("appraisals")]
    public async Task<ActionResult<IReadOnlyList<HrAppraisalDto>>> Appraisals(CancellationToken ct)
    {
        var rows = await Db.HrAppraisals.AsNoTracking().Include(a => a.Employee)
            .OrderByDescending(a => a.Id).ToListAsync(ct);
        return Ok(rows.Select(a => new HrAppraisalDto(
            a.Id, a.CycleId, a.EmployeeId, a.Employee?.Name ?? "—", a.SelfScore,
            a.ManagerScore, a.Rating, a.Comments, a.Status)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("appraisals")]
    public async Task<ActionResult<HrAppraisalDto>> SaveAppraisal(HrAppraisalInput input, CancellationToken ct)
    {
        var row = await Db.HrAppraisals
            .FirstOrDefaultAsync(a => a.CycleId == input.CycleId && a.EmployeeId == input.EmployeeId, ct);
        if (row is null)
        {
            row = new HrAppraisal
            {
                CompanyId = Db.Tenant.CompanyId,
                CycleId = input.CycleId,
                EmployeeId = input.EmployeeId,
            };
            Db.HrAppraisals.Add(row);
        }
        row.SelfScore = input.SelfScore;
        row.ManagerScore = input.ManagerScore;
        row.Rating = input.Rating;
        row.Comments = input.Comments;
        row.Status = input.ManagerScore is not null ? "Reviewed" : "Submitted";
        await Db.SaveChangesAsync(ct);
        return Ok(new HrAppraisalDto(
            row.Id, row.CycleId, row.EmployeeId, await NameOf(row.EmployeeId, ct),
            row.SelfScore, row.ManagerScore, row.Rating, row.Comments, row.Status));
    }

    /* ---------------- interviews ---------------- */

    [HttpGet("interviews")]
    public async Task<ActionResult<IReadOnlyList<HrInterviewDto>>> Interviews(CancellationToken ct)
    {
        var rows = await Db.HrInterviews.AsNoTracking().Include(i => i.Candidate)
            .OrderByDescending(i => i.ScheduledAt).Take(200).ToListAsync(ct);
        return Ok(rows.Select(i => new HrInterviewDto(
            i.Id, i.CandidateId, i.Candidate?.Name ?? "—", i.ScheduledAt, i.Panel,
            i.Mode, i.Score, i.Recommendation, i.Status, i.Notes)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("interviews")]
    public async Task<ActionResult<HrInterviewDto>> CreateInterview(HrInterviewInput input, CancellationToken ct)
    {
        var row = new HrInterview
        {
            CompanyId = Db.Tenant.CompanyId,
            CandidateId = input.CandidateId,
            ScheduledAt = input.ScheduledAt,
            Panel = input.Panel,
            Mode = string.IsNullOrWhiteSpace(input.Mode) ? "InPerson" : input.Mode,
            Score = input.Score,
            Recommendation = input.Recommendation,
            Notes = input.Notes,
        };
        Db.HrInterviews.Add(row);
        await Db.SaveChangesAsync(ct);
        var name = await Db.HrCandidates.Where(c => c.Id == row.CandidateId)
            .Select(c => c.Name).FirstOrDefaultAsync(ct) ?? "—";
        return Ok(new HrInterviewDto(
            row.Id, row.CandidateId, name, row.ScheduledAt, row.Panel, row.Mode,
            row.Score, row.Recommendation, row.Status, row.Notes));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("interviews/{id:int}/score")]
    public async Task<IActionResult> ScoreInterview(
        int id, [FromQuery] decimal score, [FromQuery] string? recommendation, CancellationToken ct)
    {
        var row = await Db.HrInterviews.FirstOrDefaultAsync(i => i.Id == id, ct)
            ?? throw ApiException.NotFound("Interview");
        row.Score = score;
        row.Recommendation = recommendation;
        row.Status = "Completed";
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- training ---------------- */

    [HttpGet("trainings")]
    public async Task<ActionResult<IReadOnlyList<HrTrainingDto>>> Trainings(CancellationToken ct)
    {
        var counts = await Db.HrTrainingEnrolments.AsNoTracking()
            .GroupBy(e => e.TrainingId)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.N, ct);
        var rows = await Db.HrTrainings.AsNoTracking().OrderByDescending(t => t.Id).ToListAsync(ct);
        return Ok(rows.Select(t => new HrTrainingDto(
            t.Id, t.Title, t.Trainer, t.FromDate, t.ToDate, t.Venue, t.Status,
            counts.GetValueOrDefault(t.Id))));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("trainings")]
    public async Task<ActionResult<HrTrainingDto>> CreateTraining(HrTrainingInput input, CancellationToken ct)
    {
        var row = new HrTraining
        {
            CompanyId = Db.Tenant.CompanyId,
            Title = input.Title.Trim(),
            Trainer = input.Trainer,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Venue = input.Venue,
        };
        Db.HrTrainings.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrTrainingDto(row.Id, row.Title, row.Trainer, row.FromDate, row.ToDate, row.Venue, row.Status, 0));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("trainings/{id:int}/enrol")]
    public async Task<IActionResult> Enrol(int id, [FromQuery] int employeeId, CancellationToken ct)
    {
        if (await Db.HrTrainingEnrolments.AnyAsync(e => e.TrainingId == id && e.EmployeeId == employeeId, ct))
            return NoContent();
        Db.HrTrainingEnrolments.Add(new HrTrainingEnrolment
        {
            CompanyId = Db.Tenant.CompanyId,
            TrainingId = id,
            EmployeeId = employeeId,
        });
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- lifecycle ---------------- */

    [HttpGet("lifecycle")]
    public async Task<ActionResult<IReadOnlyList<HrLifecycleDto>>> Lifecycle(CancellationToken ct)
    {
        var rows = await Db.HrLifecycleEvents.AsNoTracking().Include(x => x.Employee)
            .OrderByDescending(x => x.Id).Take(200).ToListAsync(ct);
        return Ok(rows.Select(x => new HrLifecycleDto(
            x.Id, x.EmployeeId, x.Employee?.Name ?? "—", x.Kind, x.EffectiveOn,
            x.FromValue, x.ToValue, x.Notes, x.Status)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("lifecycle")]
    public async Task<ActionResult<HrLifecycleDto>> CreateLifecycle(HrLifecycleInput input, CancellationToken ct)
    {
        var kind = Require(input.Kind, HrLifecycleKinds.All, "kind");
        var row = new HrLifecycleEvent
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            Kind = kind,
            EffectiveOn = input.EffectiveOn,
            FromValue = input.FromValue,
            ToValue = input.ToValue,
            Notes = input.Notes,
        };
        Db.HrLifecycleEvents.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrLifecycleDto(
            row.Id, row.EmployeeId, await NameOf(row.EmployeeId, ct), row.Kind, row.EffectiveOn,
            row.FromValue, row.ToValue, row.Notes, row.Status));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("lifecycle/{id:int}/apply")]
    public async Task<IActionResult> ApplyLifecycle(int id, CancellationToken ct)
    {
        var row = await Db.HrLifecycleEvents.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiException.NotFound("Lifecycle event");
        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == row.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");
        row.Status = HrRequestStatuses.Approved;
        if (row.Kind == HrLifecycleKinds.Confirmation)
            employee.Status = EmploymentStatuses.Confirmed;
        else if (row.Kind == HrLifecycleKinds.Promotion && !string.IsNullOrWhiteSpace(row.ToValue))
            employee.Status = EmploymentStatuses.Confirmed;
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private Task<string> NameOf(int employeeId, CancellationToken ct) =>
        Db.HrEmployees.Where(e => e.Id == employeeId).Select(e => e.Name).FirstAsync(ct);

    private static HrPunchDto ToPunch(HrPunch p) =>
        new(p.Id, p.EmployeeId, p.Employee?.Name ?? "—", p.At, p.Kind, p.Latitude, p.Longitude, p.Address);

    private static string Advance(string status, bool approve)
    {
        if (!approve) return HrRequestStatuses.Rejected;
        return status == HrRequestStatuses.PendingManager
            ? HrRequestStatuses.PendingHr
            : HrRequestStatuses.Approved;
    }
}
