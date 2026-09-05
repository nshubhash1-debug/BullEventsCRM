using System.Text;
using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

[ApiController]
[Route("api/hr")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrOpsController(
    AppDbContext db, LeaveLedger ledger, SalaryStructureResolver resolver)
    : CrmControllerBase(db)
{
    [HttpGet("dashboard")]
    public async Task<ActionResult<HrDashboardDto>> Dashboard(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var monthStart = new DateTime(today.Year, today.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var rolls = EmploymentStatuses.OnRolls;
        var headcount = await Db.HrEmployees.CountAsync(e => rolls.Contains(e.Status), ct);
        var marks = await Db.HrAttendances.AsNoTracking()
            .Where(a => a.WorkDate == today)
            .ToListAsync(ct);

        var pending =
            await Db.HrLeaveRequests.CountAsync(r =>
                r.Status == HrRequestStatuses.PendingManager
                || r.Status == HrRequestStatuses.PendingHr, ct)
            + await Db.HrAttendanceCorrections.CountAsync(r =>
                r.Status == HrRequestStatuses.PendingManager
                || r.Status == HrRequestStatuses.PendingHr, ct)
            + await Db.HrResignations.CountAsync(r =>
                r.Status == HrRequestStatuses.PendingManager
                || r.Status == HrRequestStatuses.PendingHr, ct);

        var weekEnd = today.AddDays(7);
        return Ok(new HrDashboardDto(
            headcount,
            marks.Count(a => a.Status is AttendanceDayStatuses.Present or AttendanceDayStatuses.Late),
            marks.Count(a => a.Status == AttendanceDayStatuses.Absent),
            marks.Count(a => a.Status == AttendanceDayStatuses.Leave),
            marks.Count(a => a.Status == AttendanceDayStatuses.WorkFromHome),
            marks.Count(a => a.IsLate || a.Status == AttendanceDayStatuses.Late),
            await Db.HrEmployees.CountAsync(e => e.JoiningDate >= monthStart, ct),
            await Db.HrEmployees.CountAsync(
                e => e.Status == EmploymentStatuses.Relieved && e.UpdatedAt >= monthStart, ct),
            pending,
            await Db.HrExpenseClaims.CountAsync(x =>
                x.Status == HrRequestStatuses.PendingManager || x.Status == HrRequestStatuses.PendingHr, ct),
            await Db.HrHelpdeskTickets.CountAsync(x =>
                x.Status != HrTicketStatuses.Closed && x.Status != HrTicketStatuses.Resolved, ct),
            await Db.HrGoals.CountAsync(x => x.Status != "Done", ct),
            await Db.HrInterviews.CountAsync(x =>
                x.ScheduledAt >= DateTime.UtcNow && x.ScheduledAt < DateTime.UtcNow.AddDays(7), ct),
            await Db.HrHolidays.CountAsync(h => h.OnDate >= today && h.OnDate <= weekEnd, ct)));
    }

    [HttpGet("me")]
    public async Task<ActionResult<HrMeDto>> Me(CancellationToken ct)
    {
        var mine = await Db.HrEmployees
            .Include(e => e.Department).Include(e => e.Designation)
            .Include(e => e.Manager).Include(e => e.Shift)
            .FirstOrDefaultAsync(e => e.UserId == Db.Tenant.UserId, ct)
            ?? throw ApiException.NotFound("Your employee record");

        var year = DateTime.UtcNow.Year;
        var balances = await Db.HrLeaveBalances
            .Include(b => b.LeaveType)
            .Where(b => b.EmployeeId == mine.Id && b.Year == year)
            .ToListAsync(ct);

        var slips = await Db.HrPayslips
            .Include(s => s.Employee)
            .Where(s => s.EmployeeId == mine.Id)
            .OrderByDescending(s => s.Id)
            .Take(6)
            .ToListAsync(ct);

        return Ok(new HrMeDto(
            HrEmployeesController.ToDto(mine),
            balances.Select(b => new HrLeaveBalanceDto(
                b.LeaveTypeId, b.LeaveType?.Name ?? "—", b.Opening, b.Accrued, b.Taken, b.Closing))
                .ToList(),
            slips.Select(ToSlip).ToList()));
    }

    /* ---------------- masters ---------------- */

    [HttpGet("departments")]
    public async Task<ActionResult<IReadOnlyList<HrDepartmentDto>>> Departments(CancellationToken ct) =>
        Ok(await Db.HrDepartments.AsNoTracking().OrderBy(d => d.Name)
            .Select(d => new HrDepartmentDto(d.Id, d.Name, d.Code, d.Location, d.IsActive))
            .ToListAsync(ct));

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("departments")]
    public async Task<ActionResult<HrDepartmentDto>> CreateDepartment(
        HrDepartmentInput input, CancellationToken ct)
    {
        var row = new HrDepartment
        {
            CompanyId = Db.Tenant.CompanyId,
            Name = input.Name.Trim(),
            Code = input.Code,
            Location = input.Location,
            IsActive = input.IsActive,
        };
        Db.HrDepartments.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrDepartmentDto(row.Id, row.Name, row.Code, row.Location, row.IsActive));
    }

    [HttpGet("shifts")]
    public async Task<ActionResult<IReadOnlyList<HrShiftDto>>> Shifts(CancellationToken ct) =>
        Ok(await Db.HrShifts.AsNoTracking().OrderBy(s => s.Name)
            .Select(s => new HrShiftDto(
                s.Id, s.Name, s.StartTime, s.EndTime, s.GraceMinutes, s.WeeklyOff, s.IsActive))
            .ToListAsync(ct));

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("shifts")]
    public async Task<ActionResult<HrShiftDto>> CreateShift(HrShiftInput input, CancellationToken ct)
    {
        var row = new HrShift
        {
            CompanyId = Db.Tenant.CompanyId,
            Name = input.Name.Trim(),
            StartTime = input.StartTime,
            EndTime = input.EndTime,
            GraceMinutes = input.GraceMinutes,
            WeeklyOff = input.WeeklyOff,
            IsActive = input.IsActive,
        };
        Db.HrShifts.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrShiftDto(
            row.Id, row.Name, row.StartTime, row.EndTime, row.GraceMinutes, row.WeeklyOff, row.IsActive));
    }

    [HttpGet("leave-types")]
    public async Task<ActionResult<IReadOnlyList<HrLeaveTypeDto>>> LeaveTypes(CancellationToken ct) =>
        Ok(await Db.HrLeaveTypes.AsNoTracking().OrderBy(t => t.Code)
            .Select(t => new HrLeaveTypeDto(
                t.Id, t.Code, t.Name, t.MonthlyEntitlement, t.Paid, t.CarryForward,
                t.ApprovalLevels, t.IsActive))
            .ToListAsync(ct));

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("leave-types")]
    public async Task<ActionResult<HrLeaveTypeDto>> CreateLeaveType(
        HrLeaveTypeInput input, CancellationToken ct)
    {
        var row = new HrLeaveType
        {
            CompanyId = Db.Tenant.CompanyId,
            Code = input.Code.Trim().ToUpperInvariant(),
            Name = input.Name.Trim(),
            MonthlyEntitlement = input.MonthlyEntitlement,
            Paid = input.Paid,
            CarryForward = input.CarryForward,
            ApprovalLevels = Math.Clamp(input.ApprovalLevels, 1, 2),
            IsActive = input.IsActive,
        };
        Db.HrLeaveTypes.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrLeaveTypeDto(
            row.Id, row.Code, row.Name, row.MonthlyEntitlement, row.Paid, row.CarryForward,
            row.ApprovalLevels, row.IsActive));
    }

    /* ---------------- attendance ---------------- */

    [HttpGet("attendance")]
    public async Task<ActionResult<IReadOnlyList<HrAttendanceDto>>> Attendance(
        [FromQuery] DateOnly? from,
        [FromQuery] DateOnly? to,
        [FromQuery] int? employeeId,
        CancellationToken ct)
    {
        var start = from ?? DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-14);
        var end = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var query = Db.HrAttendances.AsNoTracking().Include(a => a.Employee)
            .Where(a => a.WorkDate >= start && a.WorkDate <= end);
        if (employeeId is int id) query = query.Where(a => a.EmployeeId == id);

        var rows = await query.OrderByDescending(a => a.WorkDate).Take(500).ToListAsync(ct);
        return Ok(rows.Select(a => new HrAttendanceDto(
            a.Id, a.EmployeeId, a.Employee?.Name ?? "—", a.WorkDate, a.InTime, a.OutTime,
            a.Status, a.IsLate, a.LeftEarly, a.Notes)));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("attendance")]
    public async Task<ActionResult<HrAttendanceDto>> MarkAttendance(
        HrAttendanceInput input, CancellationToken ct)
    {
        var status = Require(input.Status, AttendanceDayStatuses.All, "attendance status");
        var row = await Db.HrAttendances.FirstOrDefaultAsync(
            a => a.EmployeeId == input.EmployeeId && a.WorkDate == input.WorkDate, ct);
        if (row is null)
        {
            row = new HrAttendance
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = input.EmployeeId,
                WorkDate = input.WorkDate,
            };
            Db.HrAttendances.Add(row);
        }

        row.InTime = input.InTime;
        row.OutTime = input.OutTime;
        row.Status = status;
        row.IsLate = status == AttendanceDayStatuses.Late;
        row.LeftEarly = status == AttendanceDayStatuses.EarlyLeaving;
        row.Notes = input.Notes;
        row.Source = "Manual";
        await Db.SaveChangesAsync(ct);

        var name = await Db.HrEmployees.Where(e => e.Id == row.EmployeeId)
            .Select(e => e.Name).FirstAsync(ct);
        return Ok(new HrAttendanceDto(
            row.Id, row.EmployeeId, name, row.WorkDate, row.InTime, row.OutTime,
            row.Status, row.IsLate, row.LeftEarly, row.Notes));
    }

    [HttpGet("corrections")]
    public async Task<ActionResult<IReadOnlyList<HrCorrectionDto>>> Corrections(CancellationToken ct)
    {
        var rows = await Db.HrAttendanceCorrections.AsNoTracking().Include(c => c.Employee)
            .OrderByDescending(c => c.Id).Take(200).ToListAsync(ct);
        return Ok(rows.Select(c => new HrCorrectionDto(
            c.Id, c.EmployeeId, c.Employee?.Name ?? "—", c.WorkDate, c.Reason,
            c.CorrectInTime, c.CorrectOutTime, c.Status)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("corrections")]
    public async Task<ActionResult<HrCorrectionDto>> RequestCorrection(
        HrCorrectionInput input, CancellationToken ct)
    {
        var row = new HrAttendanceCorrection
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            WorkDate = input.WorkDate,
            Reason = input.Reason.Trim(),
            CorrectInTime = input.CorrectInTime,
            CorrectOutTime = input.CorrectOutTime,
            AttachmentUrl = input.AttachmentUrl,
            Status = HrRequestStatuses.PendingManager,
        };
        Db.HrAttendanceCorrections.Add(row);
        await Db.SaveChangesAsync(ct);
        var name = await Db.HrEmployees.Where(e => e.Id == row.EmployeeId)
            .Select(e => e.Name).FirstAsync(ct);
        return Ok(new HrCorrectionDto(
            row.Id, row.EmployeeId, name, row.WorkDate, row.Reason,
            row.CorrectInTime, row.CorrectOutTime, row.Status));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("corrections/{id:int}/decide")]
    public async Task<IActionResult> DecideCorrection(
        int id, [FromQuery] bool approve, CancellationToken ct)
    {
        var row = await Db.HrAttendanceCorrections.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Correction");
        if (approve)
        {
            row.Status = NextApproval(row.Status);
            if (row.Status == HrRequestStatuses.Approved)
            {
                await UpsertAttendance(
                    row.EmployeeId, row.WorkDate, row.CorrectInTime, row.CorrectOutTime,
                    AttendanceDayStatuses.Present, ct);
            }
        }
        else row.Status = HrRequestStatuses.Rejected;

        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- leave ---------------- */

    [HttpGet("leave-requests")]
    public async Task<ActionResult<IReadOnlyList<HrLeaveRequestDto>>> LeaveRequests(CancellationToken ct)
    {
        var rows = await Db.HrLeaveRequests.AsNoTracking()
            .Include(r => r.Employee).Include(r => r.LeaveType)
            .OrderByDescending(r => r.Id).Take(200).ToListAsync(ct);
        return Ok(rows.Select(ToLeave));
    }

    [HttpGet("employees/{employeeId:int}/leave-balances")]
    public async Task<ActionResult<IReadOnlyList<HrLeaveBalanceDto>>> Balances(
        int employeeId, CancellationToken ct)
    {
        var year = DateTime.UtcNow.Year;
        var rows = await Db.HrLeaveBalances.Include(b => b.LeaveType)
            .Where(b => b.EmployeeId == employeeId && b.Year == year)
            .ToListAsync(ct);
        return Ok(rows.Select(b => new HrLeaveBalanceDto(
            b.LeaveTypeId, b.LeaveType?.Name ?? "—", b.Opening, b.Accrued, b.Taken, b.Closing)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("leave-requests")]
    public async Task<ActionResult<HrLeaveRequestDto>> ApplyLeave(
        HrLeaveRequestInput input, CancellationToken ct)
    {
        if (input.ToDate < input.FromDate)
            throw ApiException.BadRequest("To date must be on or after from date.");

        var type = await Db.HrLeaveTypes.FirstOrDefaultAsync(t => t.Id == input.LeaveTypeId, ct)
            ?? throw ApiException.BadRequest("Unknown leave type.");

        var employee = await Db.HrEmployees.AsNoTracking().Include(e => e.Shift)
            .FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        // A hard block is refused here rather than at approval, so nobody
        // writes a reason for dates they were never going to get.
        var blocks = await ledger.BlocksForAsync(
            input.FromDate, input.ToDate, employee.DepartmentId, ct);
        var hard = blocks.FirstOrDefault(b => !b.AllowOverride);
        if (hard is not null)
        {
            throw ApiException.BadRequest(
                $"Leave is blocked from {hard.FromDate:d MMM} to {hard.ToDate:d MMM}: {hard.Reason}.");
        }

        var period = await ledger.PeriodForAsync(input.FromDate, ct);
        if (period is not null)
        {
            var days = CountableLeaveDays(
                input.FromDate, input.ToDate, input.HalfDay,
                employee.Shift?.WeeklyOff,
                await Db.HrHolidays.AsNoTracking()
                    .Where(h => h.OnDate >= input.FromDate && h.OnDate <= input.ToDate)
                    .Select(h => h.OnDate).ToListAsync(ct));

            var shortfall = await ledger.ShortfallAsync(input.EmployeeId, type, period, days, ct);
            if (shortfall is not null) throw ApiException.BadRequest(shortfall);
        }

        var row = new HrLeaveRequest
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            LeaveTypeId = input.LeaveTypeId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            HalfDay = input.HalfDay,
            Reason = input.Reason,
            AttachmentUrl = input.AttachmentUrl,
            Status = HrRequestStatuses.PendingManager,
        };
        Db.HrLeaveRequests.Add(row);
        await Db.SaveChangesAsync(ct);
        await Db.Entry(row).Reference(r => r.Employee).LoadAsync(ct);
        await Db.Entry(row).Reference(r => r.LeaveType).LoadAsync(ct);
        _ = type;
        return Ok(ToLeave(row));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("leave-requests/{id:int}/decide")]
    public async Task<IActionResult> DecideLeave(int id, [FromQuery] bool approve, CancellationToken ct)
    {
        var row = await Db.HrLeaveRequests.Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Leave request");

        if (!approve)
        {
            row.Status = HrRequestStatuses.Rejected;
            await Db.SaveChangesAsync(ct);
            return NoContent();
        }

        var next = NextApproval(row.Status, row.LeaveType?.ApprovalLevels ?? 1);
        row.Status = next;
        if (next == HrRequestStatuses.Approved)
        {
            var employee = await Db.HrEmployees.AsNoTracking().Include(e => e.Shift)
                .FirstOrDefaultAsync(e => e.Id == row.EmployeeId, ct);

            var holidays = await Db.HrHolidays.AsNoTracking()
                .Where(h => h.OnDate >= row.FromDate && h.OnDate <= row.ToDate)
                .Select(h => h.OnDate).ToListAsync(ct);

            // Holidays and the weekly off falling inside a leave are not leave.
            // Charging for them is the commonest way a balance goes wrong.
            var days = CountableLeaveDays(
                row.FromDate, row.ToDate, row.HalfDay, employee?.Shift?.WeeklyOff, holidays);

            // Booked to the period the leave starts in, not to today's year —
            // a February leave belongs to the year that began the previous April.
            var period = await ledger.PeriodForAsync(row.FromDate, ct);
            if (period is not null)
            {
                await ledger.ConsumeAsync(
                    row.EmployeeId, row.LeaveTypeId, period, days, ct);
            }

            foreach (var day in EachDay(row.FromDate, row.ToDate))
            {
                if (holidays.Contains(day)) continue;
                await UpsertAttendance(
                    row.EmployeeId, day, null, null, AttendanceDayStatuses.Leave, ct);
            }
        }

        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- payroll ---------------- */

    [HttpGet("salary/{employeeId:int}")]
    public async Task<ActionResult<HrSalaryDto?>> Salary(int employeeId, CancellationToken ct)
    {
        var row = await Db.HrSalaryStructures.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId)
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(ct);
        if (row is null) return Ok((HrSalaryDto?)null);
        return Ok(ToSalary(row));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("salary")]
    public async Task<ActionResult<HrSalaryDto>> SaveSalary(HrSalaryInput input, CancellationToken ct)
    {
        var previous = await Db.HrSalaryStructures
            .Where(s => s.EmployeeId == input.EmployeeId)
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        var row = new HrSalaryStructure
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            EffectiveFrom = input.EffectiveFrom,
            Basic = input.Basic,
            Hra = input.Hra,
            Allowances = input.Allowances,
            Incentive = input.Incentive,
            Bonus = input.Bonus,
            OvertimeRate = input.OvertimeRate,
            Deductions = input.Deductions,
        };
        Db.HrSalaryStructures.Add(row);

        if (previous is not null && previous.Gross != row.Gross)
        {
            Db.HrSalaryRevisions.Add(new HrSalaryRevision
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = input.EmployeeId,
                OldGross = previous.Gross,
                NewGross = row.Gross,
                EffectiveFrom = input.EffectiveFrom,
                Reason = "Structure update",
            });
        }

        await Db.SaveChangesAsync(ct);
        return Ok(ToSalary(row));
    }

    [HttpGet("payroll")]
    public async Task<ActionResult<IReadOnlyList<HrPayrollRunDto>>> PayrollRuns(CancellationToken ct)
    {
        var runs = await Db.HrPayrollRuns.AsNoTracking()
            .Include(r => r.Slips)
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Month)
            .Take(24)
            .ToListAsync(ct);
        return Ok(runs.Select(r => new HrPayrollRunDto(
            r.Id, r.Year, r.Month, r.Status, r.AttendanceLockedAt, r.ProcessedAt,
            r.Slips.Count, r.Slips.Sum(s => s.Net))));
    }

    [HttpGet("payroll/{id:int}/slips")]
    public async Task<ActionResult<IReadOnlyList<HrPayslipDto>>> Slips(int id, CancellationToken ct)
    {
        var rows = await Db.HrPayslips.AsNoTracking().Include(s => s.Employee)
            .Where(s => s.PayrollRunId == id)
            .OrderBy(s => s.Employee!.Name)
            .ToListAsync(ct);
        return Ok(rows.Select(ToSlip));
    }

    /// <summary>
    /// What one payslip's totals are made of.
    ///
    /// Separate from the list because a payroll run of two hundred people would
    /// otherwise carry two thousand lines nobody has asked to see.
    /// </summary>
    [HttpGet("payslips/{id:int}/lines")]
    public async Task<ActionResult<IReadOnlyList<HrPayslipLineDto>>> SlipLines(
        int id, CancellationToken ct)
    {
        var rows = await Db.HrPayslipLines.AsNoTracking()
            .Where(l => l.PayslipId == id)
            .OrderBy(l => l.SortOrder).ThenBy(l => l.Id)
            .ToListAsync(ct);

        return Ok(rows.Select(l => new HrPayslipLineDto(
            l.Name, l.Abbreviation, l.ComponentType, l.Amount, l.IsStatutory, l.SortOrder))
            .ToList());
    }

    /// <summary>
    /// The payslip as a document — the thing an employee is actually handed.
    ///
    /// It shows every figure the engine computed and, where the law defines the
    /// arithmetic, the arithmetic itself: which wage PF was taken on, how the
    /// employer's share split between pension and provident fund, and what annual
    /// projection this month's TDS is a fraction of. A slip that states a number
    /// it cannot break down is one the employee has no way to check.
    /// </summary>
    [HttpGet("payslips/{id:int}/html")]
    public async Task<IActionResult> PayslipHtml(int id, CancellationToken ct)
    {
        var slip = await Db.HrPayslips.AsNoTracking()
            .Include(s => s.Employee)
            .Include(s => s.Run)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw ApiException.NotFound("Payslip");

        var company = await Db.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == slip.CompanyId, ct);

        // The earnings split comes from the structure that was in force for the
        // period, not from today's — a slip reprinted after a raise must still
        // show what was paid then.
        var periodEnd = new DateOnly(slip.Run?.Year ?? DateTime.UtcNow.Year,
            slip.Run?.Month ?? 1, 1).AddMonths(1).AddDays(-1);
        var structure = await Db.HrSalaryStructures.AsNoTracking()
            .Where(x => x.EmployeeId == slip.EmployeeId && x.EffectiveFrom <= periodEnd)
            .OrderByDescending(x => x.EffectiveFrom).ThenByDescending(x => x.Id)
            .FirstOrDefaultAsync(ct);

        // Loss of pay is shown once, as its own deduction line, rather than
        // pro-rating every earning — the earnings stay the contracted figures
        // and the shortfall is visible instead of buried in them.
        var basic = structure?.Basic ?? 0m;
        var hra = structure?.Hra ?? 0m;
        var allowances = structure?.Allowances ?? 0m;
        var stated = basic + hra + allowances;
        var unitemised = Math.Max(0m, slip.Gross - stated - slip.Incentive - slip.OvertimeAmount);

        var period = $"{Months[Math.Clamp(slip.Run?.Month ?? 1, 1, 12) - 1]} {slip.Run?.Year}";
        var sb = new System.Text.StringBuilder();

        void Row(string label, decimal amount, bool bold = false)
        {
            if (amount == 0m && !bold) return;
            var o = bold ? "<b>" : "";
            var c = bold ? "</b>" : "";
            sb.Append($"<tr class=\"{(bold ? "tot" : "")}\"><td>{o}{Esc(label)}{c}</td>"
                + $"<td class=\"n\">{o}{amount:N2}{c}</td></tr>");
        }

        /* ---------------- earnings ---------------- */

        sb.Append("<div class=\"cols\"><div class=\"col\"><table><thead><tr>"
            + "<th>Earnings</th><th class=\"n\">Amount</th></tr></thead><tbody>");
        Row("Basic", basic);
        Row("House rent allowance", hra);
        Row("Other allowances", allowances);
        Row("Unitemised earnings", unitemised);
        Row("Incentive", slip.Incentive);
        Row("Overtime", slip.OvertimeAmount);
        sb.Append("</tbody><tfoot>");
        Row("Gross earnings", slip.Gross, bold: true);
        sb.Append("</tfoot></table>");

        if (slip.LopDays > 0m)
        {
            sb.Append($"<p class=\"note\">Loss of pay: {slip.LopDays:0.##} day"
                + $"{(slip.LopDays == 1m ? "" : "s")}, {slip.LopAmount:N2}. Statutory "
                + $"contributions were computed on the payable gross of "
                + $"{slip.PayableGross:N2}.</p>");
        }

        /* ---------------- deductions ---------------- */

        sb.Append("</div><div class=\"col\"><table><thead><tr>"
            + "<th>Deductions</th><th class=\"n\">Amount</th></tr></thead><tbody>");
        Row("Loss of pay", slip.LopAmount);
        Row("Provident fund (employee)", slip.PfEmployee);
        Row("ESI (employee)", slip.EsicEmployee);
        Row("Professional tax", slip.ProfessionalTax);
        Row("Labour welfare fund", slip.LwfEmployee);
        Row("Income tax (TDS)", slip.Tds);
        Row("Other deductions", slip.OtherDeductions);
        sb.Append("</tbody><tfoot>");
        Row("Total deductions", slip.TotalDeductions, bold: true);
        sb.Append("</tfoot></table></div></div>");

        sb.Append($"<div class=\"net\"><span>Net payable</span>"
            + $"<span class=\"amt\">{slip.Net:N2}</span></div>");

        /* ---------------- the employer's side ---------------- */

        var employerTotal = slip.PfEmployer + slip.Edli + slip.PfAdminCharges
            + slip.EsicEmployer + slip.LwfEmployer;
        if (employerTotal > 0m || slip.GratuityAccrual > 0m)
        {
            sb.Append("<h2>Employer contributions</h2><p class=\"note\">Paid by the "
                + "company on top of your gross. Not deducted from your salary.</p>"
                + "<table class=\"wide\"><tbody>");
            if (slip.PfWage > 0m)
            {
                Row($"Provident fund on wage of {slip.PfWage:N2}", slip.PfEmployer);
                Row("— pension fund (EPS)", slip.EpsEmployer);
                Row("— provident fund (EPF)", slip.EpfEmployer);
            }
            Row("EDLI insurance", slip.Edli);
            Row("PF administration charges", slip.PfAdminCharges);
            Row("ESI (employer)", slip.EsicEmployer);
            Row("Labour welfare fund (employer)", slip.LwfEmployer);
            Row("Gratuity accrued this month", slip.GratuityAccrual);
            Row("Cost to company for the month", slip.CostToCompany, bold: true);
            sb.Append("</tbody></table>");
        }

        /* ---------------- how the TDS was arrived at ---------------- */

        if (slip.ProjectedAnnualTax > 0m || slip.Tds > 0m || slip.HraExemption > 0m)
        {
            sb.Append($"<h2>Income tax working — {Esc(slip.TaxRegime ?? "—")} regime</h2>"
                + "<p class=\"note\">This month's deduction is the balance of the year's "
                + "projected liability spread over the months remaining. It moves as "
                + "declarations and earnings change.</p><table class=\"wide\"><tbody>");
            Row("House rent exempt u/s 10(13A)", slip.HraExemption);
            Row("Projected taxable income for the year", slip.ProjectedAnnualTaxable);
            Row("Projected tax for the year", slip.ProjectedAnnualTax);
            Row("Deducted this month", slip.Tds, bold: true);
            sb.Append("</tbody></table>");
        }

        var e = slip.Employee;
        var ids = new List<string>();
        if (!string.IsNullOrWhiteSpace(e?.Pan)) ids.Add($"PAN {Esc(e!.Pan!)}");
        if (!string.IsNullOrWhiteSpace(e?.Uan)) ids.Add($"UAN {Esc(e!.Uan!)}");
        if (!string.IsNullOrWhiteSpace(e?.EsicIp)) ids.Add($"ESI IP {Esc(e!.EsicIp!)}");
        var idLine = ids.Count > 0
            ? $"<div><span>Identifiers</span> {string.Join(" &middot; ", ids)}</div>"
            : "";

        var html = $$"""
            <!doctype html>
            <html lang="en"><head><meta charset="utf-8">
            <title>Payslip — {{Esc(e?.Name ?? "Employee")}} — {{Esc(period)}}</title>
            <style>
              :root { color-scheme: light; }
              body { font: 13px/1.5 ui-sans-serif, system-ui, "Segoe UI", sans-serif;
                     color: #18181b; background: #fff; margin: 0; padding: 28px; }
              .sheet { max-width: 760px; margin: 0 auto; }
              header { border-bottom: 2px solid #18181b; padding-bottom: 12px;
                       margin-bottom: 16px; }
              .co { font-size: 17px; font-weight: 650; letter-spacing: -.01em; }
              h1 { font-size: 12px; font-weight: 600; text-transform: uppercase;
                   letter-spacing: .08em; color: #71717a; margin: 3px 0 0; }
              h2 { font-size: 11px; font-weight: 600; text-transform: uppercase;
                   letter-spacing: .06em; color: #71717a; margin: 24px 0 6px; }
              .who { display: flex; flex-wrap: wrap; gap: 4px 28px; margin-bottom: 18px; }
              .who div { font-size: 12px; }
              .who span { color: #71717a; }
              .cols { display: flex; flex-wrap: wrap; gap: 18px; }
              .col { flex: 1 1 300px; min-width: 0; }
              table { width: 100%; border-collapse: collapse; }
              th { text-align: left; font-size: 11px; text-transform: uppercase;
                   letter-spacing: .05em; color: #71717a; font-weight: 600;
                   border-bottom: 1px solid #d4d4d8; padding: 5px 0; }
              td { padding: 4px 0; border-bottom: 1px solid #f4f4f5; }
              .n { text-align: right; font-variant-numeric: tabular-nums;
                   white-space: nowrap; }
              tr.tot td { border-top: 1px solid #d4d4d8; border-bottom: none;
                   padding-top: 6px; }
              .net { display: flex; justify-content: space-between; align-items: baseline;
                     margin-top: 18px; padding: 12px 14px; background: #f4f4f5;
                     border-radius: 6px; font-weight: 650; }
              .net .amt { font-size: 19px; font-variant-numeric: tabular-nums; }
              .note { font-size: 11.5px; color: #71717a; margin: 8px 0 0; }
              footer { margin-top: 28px; padding-top: 10px; border-top: 1px solid #e4e4e7;
                       font-size: 11px; color: #a1a1aa; }
              @media print { body { padding: 0; } }
            </style></head><body><div class="sheet">
            <header>
              <div class="co">{{Esc(company?.Name ?? "—")}}</div>
              <h1>Salary slip &middot; {{Esc(period)}}</h1>
            </header>
            <div class="who">
              <div><span>Employee</span> {{Esc(e?.Name ?? "—")}}</div>
              <div><span>Code</span> {{Esc(e?.EmployeeCode ?? "—")}}</div>
              {{idLine}}
            </div>
            {{sb}}
            <footer>Computer generated — valid without a signature. Contributions and
            tax are computed from the statutory rates in force for the period and are
            subject to correction on assessment.</footer>
            </div></body></html>
            """;
        return Content(html, "text/html; charset=utf-8");
    }

    private static readonly string[] Months =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December",
    ];

    /// <summary>Names and identifiers land in markup — escape them.</summary>
    private static string Esc(string value) => System.Net.WebUtility.HtmlEncode(value);

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("payroll/process")]
    public async Task<ActionResult<HrPayrollRunDto>> ProcessPayroll(
        [FromQuery] int year, [FromQuery] int month, CancellationToken ct)
    {
        if (month is < 1 or > 12) throw ApiException.BadRequest("Month must be 1–12.");

        var run = await Db.HrPayrollRuns
            .Include(r => r.Slips)
            .FirstOrDefaultAsync(r => r.Year == year && r.Month == month, ct);
        if (run is null)
        {
            run = new HrPayrollRun
            {
                CompanyId = Db.Tenant.CompanyId,
                Year = year,
                Month = month,
            };
            Db.HrPayrollRuns.Add(run);
            await Db.SaveChangesAsync(ct);
        }

        run.AttendanceLockedAt ??= DateTime.UtcNow;
        run.Status = PayrollRunStatuses.Processed;
        run.ProcessedAt = DateTime.UtcNow;

        var start = new DateOnly(year, month, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var daysInMonth = DateTime.DaysInMonth(year, month);

        var employees = await Db.HrEmployees
            .Where(e => EmploymentStatuses.OnRolls.Contains(e.Status))
            .ToListAsync(ct);

        Db.HrPayslips.RemoveRange(run.Slips);
        run.Slips.Clear();

        /* ---------------- the statute in force for this month ---------------- */
        //
        // Read once for the run rather than per employee: the rates are the same
        // for everybody, and a two-hundred-payslip run should not fetch the same
        // slab table two hundred times. The row picked is the latest one that
        // took effect on or before the month being paid, so reprocessing an old
        // month gets that month's law rather than today's.

        var config = await Db.HrStatutoryConfigs
            .Where(c => c.EffectiveFrom <= start)
            .OrderByDescending(c => c.EffectiveFrom)
            .FirstOrDefaultAsync(ct);

        // An Indian financial year runs April to March, so January to March
        // belong to the year that started the previous April.
        var financialYear = month >= 4 ? year : year - 1;

        var ptSlabs = config is null
            ? []
            : await Db.HrProfessionalTaxSlabs.AsNoTracking()
                .Where(s => s.IsActive && s.EffectiveFrom <= start)
                .ToListAsync(ct);

        var taxSlabs = await Db.HrIncomeTaxSlabs.AsNoTracking()
            .Where(s => s.FinancialYear == financialYear)
            .ToListAsync(ct);

        var regimeConfigs = await Db.HrTaxRegimeConfigs.AsNoTracking()
            .Where(r => r.FinancialYear == financialYear)
            .ToListAsync(ct);

        var taxProfiles = await Db.HrEmployeeTaxProfiles.AsNoTracking()
            .Where(p => p.FinancialYear == financialYear)
            .ToDictionaryAsync(p => p.EmployeeId, ct);

        // One-off pay and advance recovery, read once for the run for the same
        // reason the slabs are: they are the same query two hundred times over.
        var additional = await Db.HrAdditionalSalaries.AsNoTracking()
            .Include(a => a.SalaryComponent)
            .Where(a => a.Status == HrRequestStatuses.Approved)
            .ToListAsync(ct);

        var openAdvances = await Db.HrEmployeeAdvances
            .Include(a => a.Repayments)
            .Where(a => AdvanceStatuses.Open.Contains(a.Status))
            .ToListAsync(ct);

        // What has already been paid and deducted this financial year, so the
        // projection picks up where the last month left off.
        var yearStart = new DateOnly(financialYear, 4, 1);

        var priorSlips = await Db.HrPayslips.AsNoTracking()
            .Include(s => s.Run)
            .Where(s => s.Run != null
                && s.PayrollRunId != run.Id
                && (s.Run.Year > financialYear
                    || (s.Run.Year == financialYear && s.Run.Month >= 4)))
            .Where(s => s.Run!.Year < year
                || (s.Run.Year == year && s.Run.Month < month))
            .GroupBy(s => s.EmployeeId)
            .Select(g => new
            {
                EmployeeId = g.Key,
                Gross = g.Sum(s => s.PayableGross),
                Tds = g.Sum(s => s.Tds),
            })
            .ToDictionaryAsync(x => x.EmployeeId, ct);

        // March is month 12 of the financial year, so the months left including
        // this one run 12 down to 1 as April becomes March.
        var monthsElapsed = month >= 4 ? month - 4 : month + 8;
        var remainingMonths = Math.Max(1, 12 - monthsElapsed);

        foreach (var employee in employees)
        {
            // What this person's pay is made of. Falls back to the older
            // Basic/HRA/Allowances structure when no component assignment
            // exists, so the two shapes can coexist while a company moves over.
            var salary = await resolver.ResolveAsync(employee.Id, end, ct);

            var components = salary is null
                ? new List<ResolvedComponent>()
                : [.. salary.Components];

            var pfBase = salary?.PfWageBase ?? 0m;
            var hraAmount = salary?.HraAmount ?? 0m;
            var otherEarnings = salary?.OtherEarnings ?? 0m;
            var otherDed = salary?.NonStatutoryDeductions ?? 0m;
            var incentive = salary?.IncentiveAmount ?? 0m;

            /* ---------------- one-off pay ---------------- */

            foreach (var extra in additional.Where(a =>
                a.EmployeeId == employee.Id && a.AppliesTo(year, month)))
            {
                var earning = extra.SalaryComponent?.ComponentType != SalaryComponentTypes.Deduction;

                components.Add(new ResolvedComponent(
                    extra.SalaryComponentId,
                    extra.SalaryComponent?.Name ?? (earning ? "Additional pay" : "Recovery"),
                    extra.SalaryComponent?.Abbreviation ?? "ADHOC",
                    earning ? SalaryComponentTypes.Earning : SalaryComponentTypes.Deduction,
                    extra.Amount,
                    AffectsPf: extra.SalaryComponent?.AffectsPf ?? false,
                    AffectsEsi: extra.SalaryComponent?.AffectsEsi ?? true,
                    IsTaxable: extra.SalaryComponent?.IsTaxable ?? true,
                    IsHra: false,
                    extra.DependsOnPaymentDays,
                    SortOrder: 5000));

                if (earning)
                {
                    // A one-off addition is variable pay, which is what the
                    // payslip's incentive column has always meant.
                    otherEarnings += extra.Amount;
                    incentive += extra.Amount;
                }
                else
                {
                    otherDed += extra.Amount;
                }
            }

            var gross = pfBase + hraAmount + otherEarnings;
            var basic = pfBase;

            var lopDays = await Db.HrAttendances.CountAsync(
                a => a.EmployeeId == employee.Id
                    && a.WorkDate >= start && a.WorkDate <= end
                    && (a.Status == AttendanceDayStatuses.Absent), ct);

            var unpaidLeave = await (
                from r in Db.HrLeaveRequests
                join t in Db.HrLeaveTypes on r.LeaveTypeId equals t.Id
                where r.EmployeeId == employee.Id
                    && r.Status == HrRequestStatuses.Approved
                    && !t.Paid
                    && r.FromDate <= end && r.ToDate >= start
                select r).CountAsync(ct);

            var lop = lopDays + unpaidLeave;
            var otRate = salary?.OvertimeRate ?? 0m;
            var ot = await Db.HrEventDeployments
                .Where(d => d.EmployeeId == employee.Id
                    && d.EventDate >= start && d.EventDate <= end)
                .SumAsync(d => (decimal?)(d.OvertimeHours * otRate + d.IncentiveAmount), ct) ?? 0;

            /* ---------------- advance recovery ---------------- */
            //
            // Recorded against the run rather than as a decreasing balance, so
            // reprocessing a month cannot take a second instalment: the row for
            // this run is either already there or it is not.

            var advanceDeduction = 0m;
            foreach (var advance in openAdvances.Where(a => a.EmployeeId == employee.Id))
            {
                var startsOn = new DateOnly(advance.RecoveryStartYear, advance.RecoveryStartMonth, 1);
                if (start < startsOn) continue;

                var alreadyThisRun = advance.Repayments.FirstOrDefault(r => r.PayrollRunId == run.Id);
                var recoveredBefore = advance.Repayments
                    .Where(r => r.PayrollRunId != run.Id).Sum(r => r.Amount);
                var outstanding = advance.Amount - recoveredBefore;
                if (outstanding <= 0m) continue;

                // The last instalment absorbs the rounding, so the total
                // recovered comes to the amount advanced and not a rupee more.
                var instalment = Math.Min(advance.InstalmentAmount, outstanding);
                if (instalment <= 0m) continue;

                if (alreadyThisRun is not null) alreadyThisRun.Amount = instalment;
                else
                {
                    Db.HrAdvanceRepayments.Add(new HrAdvanceRepayment
                    {
                        CompanyId = Db.Tenant.CompanyId,
                        EmployeeAdvanceId = advance.Id,
                        PayrollRunId = run.Id,
                        Amount = instalment,
                    });
                }

                advance.Status = outstanding - instalment <= 0m
                    ? AdvanceStatuses.Closed
                    : AdvanceStatuses.Recovering;

                advanceDeduction += instalment;
                components.Add(new ResolvedComponent(
                    null, $"Advance recovery ({advance.Purpose ?? "advance"})", "ADV",
                    SalaryComponentTypes.Deduction, instalment,
                    AffectsPf: false, AffectsEsi: false, IsTaxable: false, IsHra: false,
                    DependsOnPaymentDays: false, SortOrder: 6000));
            }

            otherDed += advanceDeduction;

            var profile = taxProfiles.GetValueOrDefault(employee.Id);
            var regime = profile?.Regime ?? TaxRegimes.New;
            var prior = priorSlips.GetValueOrDefault(employee.Id);

            // No statutory row means nothing has been set up yet. The run still
            // produces payslips — gross less loss of pay — rather than refusing,
            // because a company mid-onboarding still has to pay people; it just
            // deducts nothing it has not been told the rates for.
            var result = config is null
                ? null
                : PayrollEngine.Compute(
                    new PayrollInput
                    {
                        Basic = basic,
                        Hra = hraAmount,
                        // Everything else earned is one figure to the engine;
                        // it needs the provident-fund base and the house rent
                        // separately, and nothing more.
                        Allowances = otherEarnings,
                        Incentive = 0,
                        Bonus = 0,
                        OvertimeAmount = Math.Round(ot, 2),
                        OtherDeductions = otherDed,
                        LopDays = lop,
                        DaysInMonth = daysInMonth,
                        Month = month,
                        Year = year,
                        PtState = employee.Location,
                        RemainingMonthsInYear = remainingMonths,
                        YearToDateTaxableSalary = prior?.Gross ?? 0,
                        YearToDateTaxDeducted = prior?.Tds ?? 0,
                    },
                    new PayrollRates
                    {
                        Config = config,
                        PtSlabs = ptSlabs,
                        TaxSlabs = taxSlabs.Where(s => s.Regime == regime).ToList(),
                        RegimeConfig = regimeConfigs.FirstOrDefault(r => r.Regime == regime),
                        TaxProfile = profile,
                        YearsOfService = employee.JoiningDate == default
                            ? 0
                            : (decimal)(end.ToDateTime(TimeOnly.MinValue)
                                - employee.JoiningDate).TotalDays / 365m,
                    });

            if (result is null)
            {
                var daily = daysInMonth == 0 ? 0 : gross / daysInMonth;
                var lopAmount = Math.Round(daily * lop, 2);

                var fallbackSlip = new HrPayslip
                {
                    CompanyId = Db.Tenant.CompanyId,
                    EmployeeId = employee.Id,
                    Gross = gross,
                    PayableGross = Math.Max(0, gross - lopAmount),
                    LopDays = lop,
                    LopAmount = lopAmount,
                    OtherDeductions = otherDed,
                    Incentive = incentive,
                    OvertimeAmount = Math.Round(ot, 2),
                    TotalDeductions = otherDed,
                    Net = Math.Max(0, gross - lopAmount - otherDed + ot),
                };

                AddLines(fallbackSlip, components, lop, daysInMonth);
                run.Slips.Add(fallbackSlip);

                continue;
            }

            var slip = new HrPayslip
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employee.Id,
                Gross = result.Gross,
                PayableGross = result.PayableGross,
                LopDays = lop,
                LopAmount = result.LopAmount,
                OtherDeductions = otherDed,

                PfWage = result.PfWage,
                PfEmployee = result.PfEmployee,
                PfEmployer = result.PfEmployer,
                EpsEmployer = result.EpsEmployer,
                EpfEmployer = result.EpfEmployer,
                Edli = result.Edli,
                PfAdminCharges = result.PfAdminCharges,

                EsicEmployee = result.EsiEmployee,
                EsicEmployer = result.EsiEmployer,
                ProfessionalTax = result.ProfessionalTax,
                LwfEmployee = result.LwfEmployee,
                LwfEmployer = result.LwfEmployer,

                Tds = result.Tds,
                ProjectedAnnualTaxable = result.ProjectedAnnualTaxable,
                ProjectedAnnualTax = result.ProjectedAnnualTax,
                HraExemption = result.HraExemption,
                TaxRegime = regime,

                Incentive = incentive,
                OvertimeAmount = result.PayableGross == 0 ? 0 : Math.Round(ot, 2),
                GratuityAccrual = result.GratuityAccrual,
                CostToCompany = result.CostToCompany,
                TotalDeductions = result.TotalDeductions,
                Net = result.Net,
            };

            // The statutory deductions join the list so a payslip is one
            // itemised statement rather than a set of components with four
            // unexplained numbers underneath it.
            AddLines(slip, components, lop, daysInMonth);
            AddStatutoryLines(slip, result, ot);
            run.Slips.Add(slip);
        }

        await Db.SaveChangesAsync(ct);
        await Db.Entry(run).Collection(r => r.Slips).LoadAsync(ct);
        return Ok(new HrPayrollRunDto(
            run.Id, run.Year, run.Month, run.Status, run.AttendanceLockedAt, run.ProcessedAt,
            run.Slips.Count, run.Slips.Sum(s => s.Net)));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("payroll/{id:int}/review")]
    public async Task<IActionResult> ReviewPayroll(int id, CancellationToken ct)
    {
        var run = await Db.HrPayrollRuns.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Payroll run");
        run.Status = PayrollRunStatuses.Reviewed;
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ---------------- recruitment ---------------- */

    [HttpGet("vacancies")]
    public async Task<ActionResult<IReadOnlyList<HrVacancyDto>>> Vacancies(CancellationToken ct)
    {
        var rows = await Db.HrVacancies.AsNoTracking().Include(v => v.Department)
            .OrderByDescending(v => v.Id).ToListAsync(ct);
        return Ok(rows.Select(v => new HrVacancyDto(
            v.Id, v.Position, v.DepartmentId, v.Department?.Name, v.Location, v.Experience,
            v.SalaryMin, v.SalaryMax, v.JobDescription, v.Status)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("vacancies")]
    public async Task<ActionResult<HrVacancyDto>> CreateVacancy(HrVacancyInput input, CancellationToken ct)
    {
        var row = new HrVacancy
        {
            CompanyId = Db.Tenant.CompanyId,
            Position = input.Position.Trim(),
            DepartmentId = input.DepartmentId,
            Location = input.Location,
            Experience = input.Experience,
            SalaryMin = input.SalaryMin,
            SalaryMax = input.SalaryMax,
            JobDescription = input.JobDescription,
            HiringManagerEmployeeId = input.HiringManagerEmployeeId,
            Status = input.Status,
        };
        Db.HrVacancies.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrVacancyDto(
            row.Id, row.Position, row.DepartmentId, null, row.Location, row.Experience,
            row.SalaryMin, row.SalaryMax, row.JobDescription, row.Status));
    }

    [HttpGet("candidates")]
    public async Task<ActionResult<IReadOnlyList<HrCandidateDto>>> Candidates(CancellationToken ct)
    {
        var rows = await Db.HrCandidates.AsNoTracking().OrderByDescending(c => c.Id).ToListAsync(ct);
        return Ok(rows.Select(ToCandidate));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("candidates")]
    public async Task<ActionResult<HrCandidateDto>> CreateCandidate(
        HrCandidateInput input, CancellationToken ct)
    {
        var row = new HrCandidate
        {
            CompanyId = Db.Tenant.CompanyId,
            VacancyId = input.VacancyId,
            Name = input.Name.Trim(),
            Phone = input.Phone,
            Email = input.Email,
            CvUrl = input.CvUrl,
            Source = input.Source,
            Experience = input.Experience,
            InterviewNotes = input.InterviewNotes,
            Stage = Require(input.Stage, RecruitmentStages.All, "stage"),
            OfferJoiningDate = input.OfferJoiningDate,
            OfferSalary = input.OfferSalary,
        };
        Db.HrCandidates.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(ToCandidate(row));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("candidates/{id:int}/stage")]
    public async Task<ActionResult<HrCandidateDto>> MoveStage(
        int id, [FromQuery] string stage, CancellationToken ct)
    {
        var row = await Db.HrCandidates.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Candidate");
        row.Stage = Require(stage, RecruitmentStages.All, "stage");
        await Db.SaveChangesAsync(ct);
        return Ok(ToCandidate(row));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("candidates/{id:int}/convert")]
    public async Task<ActionResult<HrEmployeeDto>> ConvertCandidate(int id, CancellationToken ct)
    {
        var cand = await Db.HrCandidates.FirstOrDefaultAsync(c => c.Id == id, ct)
            ?? throw ApiException.NotFound("Candidate");
        if (cand.ConvertedEmployeeId is int existing)
        {
            var already = await Db.HrEmployees.Include(e => e.Department)
                .Include(e => e.Designation).Include(e => e.Manager).Include(e => e.Shift)
                .FirstAsync(e => e.Id == existing, ct);
            return Ok(HrEmployeesController.ToDto(already));
        }

        var code = $"EMP{DateTime.UtcNow:yyMMdd}{id:000}";
        var employee = new HrEmployee
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeCode = code,
            Name = cand.Name,
            Phone = cand.Phone,
            Email = cand.Email,
            JoiningDate = DateTime.SpecifyKind(
                (cand.OfferJoiningDate ?? DateTime.UtcNow).Date, DateTimeKind.Utc),
            Status = EmploymentStatuses.Probation,
            EmploymentType = EmploymentTypes.Permanent,
            OwnerId = Db.Tenant.UserId,
        };
        Db.HrEmployees.Add(employee);
        await Db.SaveChangesAsync(ct);
        cand.ConvertedEmployeeId = employee.Id;
        cand.Stage = RecruitmentStages.Joined;
        await Db.SaveChangesAsync(ct);

        var loaded = await Db.HrEmployees.Include(e => e.Department)
            .Include(e => e.Designation).Include(e => e.Manager).Include(e => e.Shift)
            .FirstAsync(e => e.Id == employee.Id, ct);
        return Ok(HrEmployeesController.ToDto(loaded));
    }

    /* ---------------- letters / exit / events / assets ---------------- */

    [HttpGet("letters")]
    public async Task<ActionResult<IReadOnlyList<HrLetterDto>>> Letters(
        [FromQuery] int? employeeId, CancellationToken ct)
    {
        var query = Db.HrLetters.AsNoTracking().AsQueryable();
        if (employeeId is int id) query = query.Where(l => l.EmployeeId == id);
        var rows = await query.OrderByDescending(l => l.Id).Take(100).ToListAsync(ct);
        return Ok(rows.Select(l => new HrLetterDto(l.Id, l.EmployeeId, l.Kind, l.Body, l.CreatedAt)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("letters")]
    public async Task<ActionResult<HrLetterDto>> GenerateLetter(HrLetterInput input, CancellationToken ct)
    {
        var kind = Require(input.Kind, HrLetterKinds.All, "letter kind");
        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");
        var body =
            $"{kind} letter\n\nThis is to certify that {employee.Name} ({employee.EmployeeCode}) "
            + $"is engaged with us as on {DateTime.UtcNow:dd MMM yyyy}. "
            + $"Employment status: {employee.Status}. Joining date: {employee.JoiningDate:dd MMM yyyy}.";
        var row = new HrLetter
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = employee.Id,
            Kind = kind,
            Body = body,
        };
        Db.HrLetters.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrLetterDto(row.Id, row.EmployeeId, row.Kind, row.Body, row.CreatedAt));
    }

    [HttpGet("resignations")]
    public async Task<ActionResult<IReadOnlyList<HrResignationDto>>> Resignations(CancellationToken ct)
    {
        var rows = await Db.HrResignations.AsNoTracking().Include(r => r.Employee)
            .OrderByDescending(r => r.Id).ToListAsync(ct);
        return Ok(rows.Select(r => new HrResignationDto(
            r.Id, r.EmployeeId, r.Employee?.Name ?? "—", r.ResignationDate, r.NoticeDays,
            r.LastWorkingDay, r.Reason, r.Status)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("resignations")]
    public async Task<ActionResult<HrResignationDto>> Resign(HrResignationInput input, CancellationToken ct)
    {
        var row = new HrResignation
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            ResignationDate = input.ResignationDate,
            NoticeDays = input.NoticeDays,
            LastWorkingDay = input.LastWorkingDay,
            Reason = input.Reason,
            Status = HrRequestStatuses.PendingManager,
        };
        Db.HrResignations.Add(row);
        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");
        employee.Status = EmploymentStatuses.NoticePeriod;
        await Db.SaveChangesAsync(ct);
        return Ok(new HrResignationDto(
            row.Id, row.EmployeeId, employee.Name, row.ResignationDate, row.NoticeDays,
            row.LastWorkingDay, row.Reason, row.Status));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("resignations/{id:int}/decide")]
    public async Task<IActionResult> DecideResignation(
        int id, [FromQuery] bool approve, CancellationToken ct)
    {
        var row = await Db.HrResignations.Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Resignation");
        if (!approve)
        {
            row.Status = HrRequestStatuses.Rejected;
            if (row.Employee is not null) row.Employee.Status = EmploymentStatuses.Confirmed;
        }
        else
        {
            row.Status = NextApproval(row.Status);
            if (row.Status == HrRequestStatuses.Approved && row.Employee is not null)
                row.Employee.Status = EmploymentStatuses.NoticePeriod;
        }
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("fnf")]
    public async Task<ActionResult<IReadOnlyList<HrFnfDto>>> Fnf(CancellationToken ct)
    {
        var rows = await Db.HrFullAndFinals.AsNoTracking().OrderByDescending(f => f.Id).ToListAsync(ct);
        return Ok(rows.Select(f => new HrFnfDto(
            f.Id, f.EmployeeId, f.SalaryDue, f.LopAmount, f.LeaveEncashment, f.Deductions,
            f.AssetsRecovered, f.Payable, f.Status)));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("fnf")]
    public async Task<ActionResult<HrFnfDto>> SaveFnf(HrFnfInput input, CancellationToken ct)
    {
        var payable = input.SalaryDue - input.LopAmount + input.LeaveEncashment
            - input.Deductions - input.AssetsRecovered;
        var row = new HrFullAndFinal
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            ResignationId = input.ResignationId,
            SalaryDue = input.SalaryDue,
            LopAmount = input.LopAmount,
            LeaveEncashment = input.LeaveEncashment,
            Deductions = input.Deductions,
            AssetsRecovered = input.AssetsRecovered,
            Payable = payable,
            Status = "Settled",
        };
        Db.HrFullAndFinals.Add(row);
        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct);
        if (employee is not null) employee.Status = EmploymentStatuses.Relieved;
        await Db.SaveChangesAsync(ct);
        return Ok(new HrFnfDto(
            row.Id, row.EmployeeId, row.SalaryDue, row.LopAmount, row.LeaveEncashment,
            row.Deductions, row.AssetsRecovered, row.Payable, row.Status));
    }

    [HttpGet("deployments")]
    public async Task<ActionResult<IReadOnlyList<HrDeploymentDto>>> Deployments(CancellationToken ct)
    {
        var rows = await Db.HrEventDeployments.AsNoTracking().Include(d => d.Employee)
            .OrderByDescending(d => d.EventDate).Take(300).ToListAsync(ct);
        return Ok(rows.Select(d => new HrDeploymentDto(
            d.Id, d.EmployeeId, d.Employee?.Name ?? "—", d.EventName, d.EventDate, d.Venue,
            d.RoleOnSite, d.ShiftName, d.ReportingTime, d.ClosingTime, d.AttendanceStatus,
            d.OvertimeHours, d.IncentiveAmount)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("deployments")]
    public async Task<ActionResult<HrDeploymentDto>> Deploy(HrDeploymentInput input, CancellationToken ct)
    {
        var row = new HrEventDeployment
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            EventName = input.EventName.Trim(),
            EventDate = input.EventDate,
            Venue = input.Venue,
            RoleOnSite = input.RoleOnSite,
            ShiftName = input.ShiftName,
            ReportingTime = input.ReportingTime,
            ClosingTime = input.ClosingTime,
            AttendanceStatus = Require(input.AttendanceStatus, AttendanceDayStatuses.All, "status"),
            OvertimeHours = input.OvertimeHours,
            IncentiveAmount = input.IncentiveAmount,
        };
        Db.HrEventDeployments.Add(row);
        await Db.SaveChangesAsync(ct);
        var name = await Db.HrEmployees.Where(e => e.Id == row.EmployeeId)
            .Select(e => e.Name).FirstAsync(ct);
        return Ok(new HrDeploymentDto(
            row.Id, row.EmployeeId, name, row.EventName, row.EventDate, row.Venue,
            row.RoleOnSite, row.ShiftName, row.ReportingTime, row.ClosingTime,
            row.AttendanceStatus, row.OvertimeHours, row.IncentiveAmount));
    }

    [HttpGet("assets")]
    public async Task<ActionResult<IReadOnlyList<HrAssetDto>>> Assets(
        [FromQuery] int? employeeId, CancellationToken ct)
    {
        var query = Db.HrAssetIssues.AsNoTracking().AsQueryable();
        if (employeeId is int id) query = query.Where(a => a.EmployeeId == id);
        var rows = await query.OrderByDescending(a => a.Id).ToListAsync(ct);
        return Ok(rows.Select(a => new HrAssetDto(
            a.Id, a.EmployeeId, a.AssetType, a.SerialNo, a.IssueDate, a.Condition, a.ReturnDate)));
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("assets")]
    public async Task<ActionResult<HrAssetDto>> IssueAsset(HrAssetInput input, CancellationToken ct)
    {
        var row = new HrAssetIssue
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            AssetType = input.AssetType.Trim(),
            SerialNo = input.SerialNo,
            IssueDate = input.IssueDate,
            Condition = input.Condition,
        };
        Db.HrAssetIssues.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(new HrAssetDto(
            row.Id, row.EmployeeId, row.AssetType, row.SerialNo, row.IssueDate,
            row.Condition, row.ReturnDate));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("assets/{id:int}/return")]
    public async Task<IActionResult> ReturnAsset(int id, CancellationToken ct)
    {
        var row = await Db.HrAssetIssues.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Asset");
        row.ReturnDate = DateOnly.FromDateTime(DateTime.UtcNow);
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    [HttpGet("reports/employees.csv")]
    public async Task<IActionResult> EmployeeCsv(CancellationToken ct)
    {
        var rows = await Db.HrEmployees.AsNoTracking()
            .Include(e => e.Department).Include(e => e.Designation)
            .OrderBy(e => e.Name)
            .ToListAsync(ct);
        var sb = new StringBuilder();
        sb.AppendLine("Code,Name,Department,Designation,Status,Type,Location,JoiningDate,Phone,Email");
        foreach (var e in rows)
        {
            sb.AppendLine(string.Join(',',
                Csv(e.EmployeeCode), Csv(e.Name), Csv(e.Department?.Name),
                Csv(e.Designation?.Name), Csv(e.Status), Csv(e.EmploymentType),
                Csv(e.Location), e.JoiningDate.ToString("yyyy-MM-dd"),
                Csv(e.Phone), Csv(e.Email)));
        }
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "employees.csv");
    }

    private static string Csv(string? value) =>
        "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\"";

    private static string NextApproval(string status, int levels = 2) =>
        status switch
        {
            HrRequestStatuses.PendingManager when levels > 1 => HrRequestStatuses.PendingHr,
            _ => HrRequestStatuses.Approved,
        };

    /// <summary>
    /// Write the components onto the payslip.
    ///
    /// Loss of pay is applied here, per component, because it does not touch
    /// them all: salary is pro-rated, a fixed reimbursement or a one-off bonus
    /// is not. Applying it to the gross instead — which is what the totals do —
    /// would give a payslip whose lines did not add up to its own total.
    /// </summary>
    private static void AddLines(
        HrPayslip slip, IReadOnlyList<ResolvedComponent> components,
        decimal lopDays, int daysInMonth)
    {
        var payableShare = daysInMonth <= 0 || lopDays <= 0m
            ? 1m
            : Math.Max(0m, (daysInMonth - lopDays) / daysInMonth);

        foreach (var component in components)
        {
            var amount = component.DependsOnPaymentDays
                ? Math.Round(component.Amount * payableShare, 2, MidpointRounding.AwayFromZero)
                : component.Amount;

            if (amount == 0m) continue;

            slip.Lines.Add(new HrPayslipLine
            {
                CompanyId = slip.CompanyId,
                SalaryComponentId = component.ComponentId,
                Name = component.Name,
                Abbreviation = component.Abbreviation,
                ComponentType = component.ComponentType,
                Amount = amount,
                IsStatutory = false,
                SortOrder = component.SortOrder,
            });
        }

        if (lopDays > 0m && slip.LopAmount > 0m)
        {
            slip.Lines.Add(new HrPayslipLine
            {
                CompanyId = slip.CompanyId,
                Name = $"Loss of pay ({lopDays:0.##} day{(lopDays == 1m ? "" : "s")})",
                Abbreviation = "LOP",
                ComponentType = SalaryComponentTypes.Deduction,
                Amount = slip.LopAmount,
                IsStatutory = false,
                SortOrder = 100,
            });
        }
    }

    /// <summary>The engine's own figures, as payslip lines.</summary>
    private static void AddStatutoryLines(HrPayslip slip, PayrollResult result, decimal overtime)
    {
        void Line(string name, string abbr, decimal amount, string type, int order)
        {
            if (amount == 0m) return;
            slip.Lines.Add(new HrPayslipLine
            {
                CompanyId = slip.CompanyId,
                Name = name,
                Abbreviation = abbr,
                ComponentType = type,
                Amount = amount,
                IsStatutory = true,
                SortOrder = order,
            });
        }

        Line("Overtime", "OT", slip.OvertimeAmount, SalaryComponentTypes.Earning, 90);
        Line("Provident fund", "PF", result.PfEmployee, SalaryComponentTypes.Deduction, 7000);
        Line("ESI", "ESI", result.EsiEmployee, SalaryComponentTypes.Deduction, 7010);
        Line("Professional tax", "PT", result.ProfessionalTax,
            SalaryComponentTypes.Deduction, 7020);
        Line("Labour welfare fund", "LWF", result.LwfEmployee,
            SalaryComponentTypes.Deduction, 7030);
        Line("Income tax", "TDS", result.Tds, SalaryComponentTypes.Deduction, 7040);
        _ = overtime;
    }

    private static decimal Days(DateOnly from, DateOnly to) =>
        (to.DayNumber - from.DayNumber) + 1;

    private static IEnumerable<DateOnly> EachDay(DateOnly from, DateOnly to)
    {
        for (var day = from; day <= to; day = day.AddDays(1)) yield return day;
    }

    /// <summary>
    /// The days in a leave that actually come off the balance.
    ///
    /// A holiday or a weekly off inside a leave is not leave — somebody taking
    /// Friday to Tuesday over a weekend has spent three days, not five. Getting
    /// this wrong is the commonest reason a leave balance and an employee
    /// disagree.
    /// </summary>
    private static decimal CountableLeaveDays(
        DateOnly from, DateOnly to, bool halfDay, string? weeklyOff,
        IReadOnlyCollection<DateOnly> holidays)
    {
        var counted = 0m;
        foreach (var day in EachDay(from, to))
        {
            if (holidays.Contains(day)) continue;
            if (!string.IsNullOrWhiteSpace(weeklyOff)
                && string.Equals(day.DayOfWeek.ToString(), weeklyOff,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }
            counted++;
        }

        if (halfDay && counted > 0m) counted = counted == 1m ? 0.5m : counted - 0.5m;
        return counted;
    }

    private async Task UpsertAttendance(
        int employeeId, DateOnly day, TimeSpan? inn, TimeSpan? outt, string status,
        CancellationToken ct)
    {
        var row = await Db.HrAttendances.FirstOrDefaultAsync(
            a => a.EmployeeId == employeeId && a.WorkDate == day, ct);
        if (row is null)
        {
            row = new HrAttendance
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employeeId,
                WorkDate = day,
            };
            Db.HrAttendances.Add(row);
        }
        row.Status = status;
        if (inn is not null) row.InTime = inn;
        if (outt is not null) row.OutTime = outt;
    }

    private static HrLeaveRequestDto ToLeave(HrLeaveRequest r) => new(
        r.Id, r.EmployeeId, r.Employee?.Name ?? "—", r.LeaveTypeId, r.LeaveType?.Name ?? "—",
        r.FromDate, r.ToDate, r.HalfDay, r.Reason, r.Status);

    private static HrSalaryDto ToSalary(HrSalaryStructure s) => new(
        s.Id, s.EmployeeId, s.EffectiveFrom, s.Basic, s.Hra, s.Allowances, s.Incentive,
        s.Bonus, s.OvertimeRate, s.Deductions, s.Gross);

    private static HrPayslipDto ToSlip(HrPayslip s) => new(
        s.Id, s.PayrollRunId, s.EmployeeId, s.Employee?.Name ?? "—", s.Gross, s.LopDays,
        s.LopAmount, s.OtherDeductions, s.PfEmployee, s.EsicEmployee, s.Incentive,
        s.OvertimeAmount, s.Net,
        s.PayableGross, s.PfWage, s.PfEmployer, s.EpsEmployer, s.EpfEmployer,
        s.Edli, s.PfAdminCharges, s.EsicEmployer, s.ProfessionalTax,
        s.LwfEmployee, s.LwfEmployer, s.Tds, s.ProjectedAnnualTaxable,
        s.ProjectedAnnualTax, s.HraExemption, s.TaxRegime, s.GratuityAccrual,
        s.CostToCompany, s.TotalDeductions);

    private static HrCandidateDto ToCandidate(HrCandidate c) => new(
        c.Id, c.VacancyId, c.Name, c.Phone, c.Email, c.Source, c.Experience, c.Stage,
        c.OfferJoiningDate, c.OfferSalary, c.ConvertedEmployeeId);
}
