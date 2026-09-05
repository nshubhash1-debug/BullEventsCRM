using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Who is on which shift, and the requests that put them there.
///
/// The employee record carries a default shift, which is the right answer for
/// an office and the wrong one for a crew that works nights through the
/// season. An assignment overrides the default for a window; the roster for a
/// day is whichever assignment covers it, falling back to the default.
/// </summary>
[ApiController]
[Route("api/hr/shifts")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrShiftController(AppDbContext db) : CrmControllerBase(db)
{
    /* ================================================================== *
     * Assignments
     * ================================================================== */

    [HttpGet("assignments")]
    public async Task<ActionResult<IReadOnlyList<HrShiftAssignmentDto>>> Assignments(
        [FromQuery] int? employeeId, [FromQuery] DateOnly? onDate, CancellationToken ct)
    {
        var query = Db.HrShiftAssignments.AsNoTracking()
            .Include(a => a.Employee).Include(a => a.Shift)
            .AsQueryable();

        if (employeeId is int id) query = query.Where(a => a.EmployeeId == id);
        if (onDate is DateOnly date)
        {
            query = query.Where(a => a.IsActive && a.FromDate <= date
                && (a.ToDate == null || a.ToDate >= date));
        }

        var rows = await query
            .OrderByDescending(a => a.FromDate).ThenBy(a => a.Employee!.Name).ThenBy(a => a.Id)
            .Take(500)
            .ToListAsync(ct);

        return Ok(rows.Select(ToAssignment).ToList());
    }

    /// <summary>
    /// Put somebody on a shift for a window.
    ///
    /// Overlapping assignments are refused rather than merged: two shifts
    /// covering the same day would make "which shift was this person on" a
    /// question with two answers, and attendance is computed from that answer.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("assignments")]
    public async Task<ActionResult<HrShiftAssignmentDto>> Assign(
        [FromBody] HrShiftAssignmentInput input, CancellationToken ct)
    {
        if (input.ToDate is DateOnly to && to < input.FromDate)
            throw ApiException.BadRequest("A shift assignment has to end on or after it starts.");

        _ = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");
        _ = await Db.HrShifts.FirstOrDefaultAsync(s => s.Id == input.ShiftId && s.IsActive, ct)
            ?? throw ApiException.BadRequest("Unknown shift.");

        var existing = await Db.HrShiftAssignments
            .Where(a => a.EmployeeId == input.EmployeeId && a.IsActive && a.Id != (input.Id ?? 0))
            .ToListAsync(ct);

        var clash = existing.FirstOrDefault(a => a.Overlaps(input.FromDate, input.ToDate));
        if (clash is not null)
        {
            throw ApiException.BadRequest(
                $"That overlaps an assignment running from {clash.FromDate:d MMM yyyy}"
                + (clash.ToDate is DateOnly end ? $" to {end:d MMM yyyy}." : " with no end date."));
        }

        var row = input.Id is int id and > 0
            ? await Db.HrShiftAssignments.FirstOrDefaultAsync(a => a.Id == id, ct)
                ?? throw ApiException.NotFound("Shift assignment")
            : new HrShiftAssignment { CompanyId = Db.Tenant.CompanyId };

        row.EmployeeId = input.EmployeeId;
        row.ShiftId = input.ShiftId;
        row.FromDate = input.FromDate;
        row.ToDate = input.ToDate;
        row.LeadId = input.LeadId;
        row.Notes = input.Notes;
        row.IsActive = true;

        if (row.Id == 0) Db.HrShiftAssignments.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(a => a.Employee).LoadAsync(ct);
        await Db.Entry(row).Reference(a => a.Shift).LoadAsync(ct);
        return Ok(ToAssignment(row));
    }

    /// <summary>
    /// Roster a group in one go — the whole crew onto the night shift for an
    /// event week. Anybody who already has an overlapping assignment is
    /// reported back rather than failing the batch.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("assignments/bulk")]
    public async Task<ActionResult<HrRosterResultDto>> AssignMany(
        [FromBody] HrBulkShiftInput input, CancellationToken ct)
    {
        if (input.EmployeeIds.Count == 0)
            throw ApiException.BadRequest("Pick at least one person.");
        if (input.ToDate is DateOnly to && to < input.FromDate)
            throw ApiException.BadRequest("A shift assignment has to end on or after it starts.");

        _ = await Db.HrShifts.FirstOrDefaultAsync(s => s.Id == input.ShiftId && s.IsActive, ct)
            ?? throw ApiException.BadRequest("Unknown shift.");

        var ids = input.EmployeeIds.Distinct().ToList();
        var existing = await Db.HrShiftAssignments
            .Where(a => a.IsActive && ids.Contains(a.EmployeeId))
            .ToListAsync(ct);
        var names = await Db.HrEmployees.AsNoTracking()
            .Where(e => ids.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => e.Name, ct);

        var assigned = 0;
        var clashes = new List<string>();

        foreach (var employeeId in ids)
        {
            if (!names.ContainsKey(employeeId)) continue;

            if (existing.Any(a => a.EmployeeId == employeeId
                && a.Overlaps(input.FromDate, input.ToDate)))
            {
                clashes.Add(names[employeeId]);
                continue;
            }

            Db.HrShiftAssignments.Add(new HrShiftAssignment
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employeeId,
                ShiftId = input.ShiftId,
                FromDate = input.FromDate,
                ToDate = input.ToDate,
                LeadId = input.LeadId,
                Notes = input.Notes,
            });
            assigned++;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(new HrRosterResultDto(assigned, clashes));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("assignments/{id:int}/end")]
    public async Task<IActionResult> EndAssignment(
        int id, [FromQuery] DateOnly? on, CancellationToken ct)
    {
        var row = await Db.HrShiftAssignments.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Shift assignment");

        var end = on ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (end < row.FromDate)
        {
            // Ending before it began means it never should have existed.
            row.IsActive = false;
        }
        else
        {
            row.ToDate = end;
        }

        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    /* ================================================================== *
     * The roster
     * ================================================================== */

    /// <summary>
    /// Who is on what, for a day.
    ///
    /// Falls back to the employee's default shift where no assignment covers
    /// the date, because that is what actually happens — most people are on
    /// their usual shift most days, and a roster that only showed exceptions
    /// would be a roster nobody could staff a site from.
    /// </summary>
    [HttpGet("roster")]
    public async Task<ActionResult<HrRosterDto>> Roster(
        [FromQuery] DateOnly? onDate, [FromQuery] int? departmentId, CancellationToken ct)
    {
        var date = onDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var employeesQuery = Db.HrEmployees.AsNoTracking()
            .Include(e => e.Department).Include(e => e.Shift)
            .Where(e => EmploymentStatuses.OnRolls.Contains(e.Status));

        if (departmentId is int dept)
            employeesQuery = employeesQuery.Where(e => e.DepartmentId == dept);

        var employees = await employeesQuery.OrderBy(e => e.Name).ToListAsync(ct);
        var employeeIds = employees.Select(e => e.Id).ToList();

        var assignments = await Db.HrShiftAssignments.AsNoTracking()
            .Include(a => a.Shift)
            .Where(a => a.IsActive && employeeIds.Contains(a.EmployeeId)
                && a.FromDate <= date && (a.ToDate == null || a.ToDate >= date))
            .ToListAsync(ct);

        var byEmployee = assignments
            .GroupBy(a => a.EmployeeId)
            // Latest-starting assignment wins, which is the one somebody most
            // recently decided on.
            .ToDictionary(g => g.Key, g => g.OrderByDescending(a => a.FromDate)
                .ThenByDescending(a => a.Id).First());

        var onLeave = await Db.HrLeaveRequests.AsNoTracking()
            .Where(r => r.Status == HrRequestStatuses.Approved
                && r.FromDate <= date && r.ToDate >= date
                && employeeIds.Contains(r.EmployeeId))
            .Select(r => r.EmployeeId)
            .ToListAsync(ct);

        var isHoliday = await Db.HrHolidays.AsNoTracking().AnyAsync(h => h.OnDate == date, ct);

        var rows = employees.Select(e =>
        {
            var assignment = byEmployee.GetValueOrDefault(e.Id);
            var shift = assignment?.Shift ?? e.Shift;
            var weeklyOff = shift?.WeeklyOff is { Length: > 0 } off
                && string.Equals(date.DayOfWeek.ToString(), off, StringComparison.OrdinalIgnoreCase);

            return new HrRosterRowDto(
                e.Id, e.Name, e.EmployeeCode, e.Department?.Name ?? "—",
                shift?.Id, shift?.Name ?? "—",
                shift?.StartTime, shift?.EndTime,
                assignment is not null,
                assignment?.LeadId,
                onLeave.Contains(e.Id) ? "Leave"
                    : isHoliday ? "Holiday"
                    : weeklyOff ? "Weekly off"
                    : "Working");
        }).ToList();

        return Ok(new HrRosterDto(
            date, isHoliday,
            rows.Count(r => r.State == "Working"),
            rows.Count(r => r.State == "Leave"),
            rows.Count(r => r.OnAssignment),
            rows));
    }

    /* ================================================================== *
     * Shift requests
     * ================================================================== */

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<HrShiftRequestDto>>> Requests(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrShiftRequests.AsNoTracking()
            .Include(r => r.Employee).Include(r => r.Shift).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var rows = await query.OrderByDescending(r => r.Id).Take(200).ToListAsync(ct);
        return Ok(rows.Select(ToRequest).ToList());
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("requests")]
    public async Task<ActionResult<HrShiftRequestDto>> Request(
        [FromBody] HrShiftRequestInput input, CancellationToken ct)
    {
        if (input.ToDate is DateOnly to && to < input.FromDate)
            throw ApiException.BadRequest("A shift request has to end on or after it starts.");

        _ = await Db.HrShifts.FirstOrDefaultAsync(s => s.Id == input.ShiftId && s.IsActive, ct)
            ?? throw ApiException.BadRequest("Unknown shift.");

        var row = new HrShiftRequest
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            ShiftId = input.ShiftId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            Reason = input.Reason,
            Status = HrRequestStatuses.PendingManager,
        };
        Db.HrShiftRequests.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(r => r.Employee).LoadAsync(ct);
        await Db.Entry(row).Reference(r => r.Shift).LoadAsync(ct);
        return Ok(ToRequest(row));
    }

    /// <summary>Approving creates the assignment the request was asking for.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("requests/{id:int}/decide")]
    public async Task<ActionResult<HrShiftRequestDto>> DecideRequest(
        int id, [FromQuery] bool approve, CancellationToken ct)
    {
        var row = await Db.HrShiftRequests
            .Include(r => r.Employee).Include(r => r.Shift)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Shift request");

        if (row.Status is HrRequestStatuses.Approved or HrRequestStatuses.Rejected)
            throw ApiException.BadRequest("That request has already been decided.");

        if (!approve)
        {
            row.Status = HrRequestStatuses.Rejected;
            await Db.SaveChangesAsync(ct);
            return Ok(ToRequest(row));
        }

        var existing = await Db.HrShiftAssignments
            .Where(a => a.EmployeeId == row.EmployeeId && a.IsActive)
            .ToListAsync(ct);

        if (existing.Any(a => a.Overlaps(row.FromDate, row.ToDate)))
        {
            throw ApiException.BadRequest(
                "That person already has a shift assignment covering those dates. "
                + "End it first, then approve this.");
        }

        var assignment = new HrShiftAssignment
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = row.EmployeeId,
            ShiftId = row.ShiftId,
            FromDate = row.FromDate,
            ToDate = row.ToDate,
            Notes = $"From shift request #{row.Id}",
        };
        Db.HrShiftAssignments.Add(assignment);
        row.Status = HrRequestStatuses.Approved;
        await Db.SaveChangesAsync(ct);

        row.ShiftAssignmentId = assignment.Id;
        await Db.SaveChangesAsync(ct);

        return Ok(ToRequest(row));
    }

    /* ================================================================== *
     * Attendance requests
     * ================================================================== */

    [HttpGet("attendance-requests")]
    public async Task<ActionResult<IReadOnlyList<HrAttendanceRequestDto>>> AttendanceRequests(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrAttendanceRequests.AsNoTracking()
            .Include(r => r.Employee).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var rows = await query.OrderByDescending(r => r.Id).Take(200).ToListAsync(ct);
        return Ok(rows.Select(ToAttendanceRequest).ToList());
    }

    /// <summary>
    /// Ask for days to be marked that the punch machine never saw.
    ///
    /// This is the request an events company lives on: a week at a venue, a
    /// recce in another city, a load-out that ran past midnight. Without it
    /// payroll reads the silence as absence and deducts pay for days worked.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("attendance-requests")]
    public async Task<ActionResult<HrAttendanceRequestDto>> RequestAttendance(
        [FromBody] HrAttendanceRequestInput input, CancellationToken ct)
    {
        if (input.ToDate < input.FromDate)
            throw ApiException.BadRequest("To date must be on or after from date.");
        if (input.FromDate > DateOnly.FromDateTime(DateTime.UtcNow))
            throw ApiException.BadRequest("Attendance cannot be requested for the future.");
        if (!AttendanceDayStatuses.All.Contains(input.RequestedStatus))
            throw ApiException.BadRequest("Unknown attendance status.");

        // Marking somebody absent through a request makes no sense; absence is
        // what the system already assumes when nothing else is recorded.
        if (input.RequestedStatus == AttendanceDayStatuses.Absent)
            throw ApiException.BadRequest("Use a leave application, not an attendance request.");

        _ = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        var row = new HrAttendanceRequest
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            FromDate = input.FromDate,
            ToDate = input.ToDate,
            RequestedStatus = input.RequestedStatus,
            HalfDay = input.HalfDay && input.FromDate == input.ToDate,
            Reason = input.Reason,
            AttachmentUrl = input.AttachmentUrl,
            LeadId = input.LeadId,
            Status = HrRequestStatuses.PendingManager,
        };
        Db.HrAttendanceRequests.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(r => r.Employee).LoadAsync(ct);
        return Ok(ToAttendanceRequest(row));
    }

    /// <summary>Approving writes the attendance rows the request was asking for.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("attendance-requests/{id:int}/decide")]
    public async Task<ActionResult<HrAttendanceRequestDto>> DecideAttendanceRequest(
        int id, [FromQuery] bool approve, CancellationToken ct)
    {
        var row = await Db.HrAttendanceRequests.Include(r => r.Employee)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Attendance request");

        if (row.Status is HrRequestStatuses.Approved or HrRequestStatuses.Rejected)
            throw ApiException.BadRequest("That request has already been decided.");

        if (!approve)
        {
            row.Status = HrRequestStatuses.Rejected;
            await Db.SaveChangesAsync(ct);
            return Ok(ToAttendanceRequest(row));
        }

        var status = row.HalfDay ? AttendanceDayStatuses.HalfDay : row.RequestedStatus;

        var existing = await Db.HrAttendances
            .Where(a => a.EmployeeId == row.EmployeeId
                && a.WorkDate >= row.FromDate && a.WorkDate <= row.ToDate)
            .ToDictionaryAsync(a => a.WorkDate, ct);

        for (var day = row.FromDate; day <= row.ToDate; day = day.AddDays(1))
        {
            if (existing.TryGetValue(day, out var attendance))
            {
                attendance.Status = status;
                attendance.Source = $"Request #{row.Id}";
            }
            else
            {
                Db.HrAttendances.Add(new HrAttendance
                {
                    CompanyId = Db.Tenant.CompanyId,
                    EmployeeId = row.EmployeeId,
                    WorkDate = day,
                    Status = status,
                    Source = $"Request #{row.Id}",
                });
            }
        }

        row.Status = HrRequestStatuses.Approved;
        await Db.SaveChangesAsync(ct);
        return Ok(ToAttendanceRequest(row));
    }

    /* ================================================================== *
     * Mapping
     * ================================================================== */

    private static HrShiftAssignmentDto ToAssignment(HrShiftAssignment a) => new(
        a.Id, a.EmployeeId, a.Employee?.Name ?? "—",
        a.ShiftId, a.Shift?.Name ?? "—", a.Shift?.StartTime, a.Shift?.EndTime,
        a.FromDate, a.ToDate, a.LeadId, a.Notes, a.IsActive);

    private static HrShiftRequestDto ToRequest(HrShiftRequest r) => new(
        r.Id, r.EmployeeId, r.Employee?.Name ?? "—",
        r.ShiftId, r.Shift?.Name ?? "—",
        r.FromDate, r.ToDate, r.Reason, r.Status, r.ShiftAssignmentId, r.CreatedAt);

    private static HrAttendanceRequestDto ToAttendanceRequest(HrAttendanceRequest r) => new(
        r.Id, r.EmployeeId, r.Employee?.Name ?? "—",
        r.FromDate, r.ToDate, r.DayCount, r.RequestedStatus, r.HalfDay,
        r.Reason, r.AttachmentUrl, r.LeadId, r.Status, r.CreatedAt);
}
