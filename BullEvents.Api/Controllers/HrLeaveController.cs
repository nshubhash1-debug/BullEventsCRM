using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// Leave, from where the days come from to where they go.
///
/// The existing leave endpoints on <see cref="HrOpsController"/> apply and
/// approve; this one is everything that makes those two operations mean
/// something — the period days belong to, the policy that grants them, the
/// compensatory days an events crew earns working Sundays, the payout for
/// what is left, and the dates on which none of it may be taken.
/// </summary>
[ApiController]
[Route("api/hr/leave")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrLeaveController(AppDbContext db, LeaveLedger ledger) : CrmControllerBase(db)
{
    /* ================================================================== *
     * Periods
     * ================================================================== */

    [HttpGet("periods")]
    public async Task<ActionResult<IReadOnlyList<HrLeavePeriodDto>>> Periods(CancellationToken ct)
    {
        var rows = await Db.HrLeavePeriods.AsNoTracking()
            .OrderByDescending(p => p.FromDate).ThenByDescending(p => p.Id)
            .ToListAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var counts = await Db.HrLeaveAllocations.AsNoTracking()
            .GroupBy(a => a.LeavePeriodId)
            .Select(g => new { PeriodId = g.Key, Employees = g.Select(a => a.EmployeeId).Distinct().Count(), Days = g.Sum(a => a.Days) })
            .ToDictionaryAsync(x => x.PeriodId, ct);

        return Ok(rows.Select(p => new HrLeavePeriodDto(
            p.Id, p.Name, p.FromDate, p.ToDate, p.IsActive, p.RolledOverAt,
            p.Covers(today),
            counts.TryGetValue(p.Id, out var c) ? c.Employees : 0,
            counts.TryGetValue(p.Id, out var d) ? d.Days : 0m)).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("periods")]
    public async Task<ActionResult<HrLeavePeriodDto>> SavePeriod(
        [FromBody] HrLeavePeriodInput input, CancellationToken ct)
    {
        if (input.ToDate <= input.FromDate)
            throw ApiException.BadRequest("A period has to end after it starts.");

        // Overlapping periods would make "which period does this date belong
        // to" ambiguous, and every balance in the system is addressed that way.
        var clash = await Db.HrLeavePeriods
            .Where(p => p.Id != input.Id && p.IsActive
                && p.FromDate <= input.ToDate && p.ToDate >= input.FromDate)
            .FirstOrDefaultAsync(ct);
        if (clash is not null)
            throw ApiException.BadRequest($"These dates overlap {clash.Name}.");

        var period = input.Id is int id and > 0
            ? await Db.HrLeavePeriods.FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw ApiException.NotFound("Leave period")
            : new HrLeavePeriod { CompanyId = Db.Tenant.CompanyId };

        period.Name = input.Name.Trim();
        period.FromDate = input.FromDate;
        period.ToDate = input.ToDate;
        period.IsActive = input.IsActive;

        if (period.Id == 0) Db.HrLeavePeriods.Add(period);
        await Db.SaveChangesAsync(ct);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return Ok(new HrLeavePeriodDto(period.Id, period.Name, period.FromDate, period.ToDate,
            period.IsActive, period.RolledOverAt, period.Covers(today), 0, 0m));
    }

    /// <summary>
    /// Close a period and open the next: carry what may be carried, lapse the
    /// rest. Refuses a second run, because it would double every opening.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("periods/{id:int}/roll-over")]
    public async Task<ActionResult<HrRollOverResultDto>> RollOver(
        int id, [FromQuery] int intoPeriodId, CancellationToken ct)
    {
        var closing = await Db.HrLeavePeriods.FirstOrDefaultAsync(p => p.Id == id, ct)
            ?? throw ApiException.NotFound("Leave period");
        var opening = await Db.HrLeavePeriods.FirstOrDefaultAsync(p => p.Id == intoPeriodId, ct)
            ?? throw ApiException.NotFound("Target leave period");

        var (carried, lapsed, daysCarried, daysLapsed) =
            await ledger.RollOverAsync(closing, opening, ct);

        return Ok(new HrRollOverResultDto(
            closing.Name, opening.Name, carried, lapsed, daysCarried, daysLapsed));
    }

    /* ================================================================== *
     * Policies
     * ================================================================== */

    [HttpGet("policies")]
    public async Task<ActionResult<IReadOnlyList<HrLeavePolicyDto>>> Policies(CancellationToken ct)
    {
        var rows = await Db.HrLeavePolicies.AsNoTracking()
            .Include(p => p.Lines).ThenInclude(l => l.LeaveType)
            .OrderBy(p => p.Name)
            .ToListAsync(ct);

        var assigned = await Db.HrLeavePolicyAssignments.AsNoTracking()
            .GroupBy(a => a.LeavePolicyId)
            .Select(g => new { PolicyId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.PolicyId, x => x.Count, ct);

        return Ok(rows.Select(p => new HrLeavePolicyDto(
            p.Id, p.Name, p.Notes, p.IsActive,
            assigned.GetValueOrDefault(p.Id),
            p.Lines.Sum(l => l.AnnualAllocation),
            p.Lines.OrderBy(l => l.LeaveType?.Name).Select(l => new HrLeavePolicyLineDto(
                l.Id, l.LeaveTypeId, l.LeaveType?.Name ?? "—", l.AnnualAllocation)).ToList()))
            .ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("policies")]
    public async Task<ActionResult<int>> SavePolicy(
        [FromBody] HrLeavePolicyInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A policy needs a name.");

        var policy = input.Id is int id and > 0
            ? await Db.HrLeavePolicies.Include(p => p.Lines)
                .FirstOrDefaultAsync(p => p.Id == id, ct)
                ?? throw ApiException.NotFound("Leave policy")
            : new HrLeavePolicy { CompanyId = Db.Tenant.CompanyId };

        policy.Name = input.Name.Trim();
        policy.Notes = input.Notes;
        policy.IsActive = input.IsActive;

        if (policy.Id == 0) Db.HrLeavePolicies.Add(policy);

        // Lines are replaced wholesale. A policy is a small, closed list and
        // diffing it would buy nothing but a chance to get it wrong.
        Db.HrLeavePolicyLines.RemoveRange(policy.Lines);
        policy.Lines.Clear();

        foreach (var line in input.Lines.Where(l => l.AnnualAllocation > 0m))
        {
            policy.Lines.Add(new HrLeavePolicyLine
            {
                CompanyId = Db.Tenant.CompanyId,
                LeaveTypeId = line.LeaveTypeId,
                AnnualAllocation = line.AnnualAllocation,
            });
        }

        await Db.SaveChangesAsync(ct);
        return Ok(policy.Id);
    }

    /* ================================================================== *
     * Assignments — the grant itself
     * ================================================================== */

    [HttpGet("assignments")]
    public async Task<ActionResult<IReadOnlyList<HrLeaveAssignmentDto>>> Assignments(
        [FromQuery] int? periodId, CancellationToken ct)
    {
        var query = Db.HrLeavePolicyAssignments.AsNoTracking()
            .Include(a => a.Employee).Include(a => a.LeavePolicy).Include(a => a.LeavePeriod)
            .AsQueryable();

        if (periodId is int id) query = query.Where(a => a.LeavePeriodId == id);

        var rows = await query.OrderBy(a => a.Employee!.Name).ThenBy(a => a.Id).ToListAsync(ct);

        return Ok(rows.Select(a => new HrLeaveAssignmentDto(
            a.Id, a.EmployeeId, a.Employee?.Name ?? "—",
            a.LeavePolicyId, a.LeavePolicy?.Name ?? "—",
            a.LeavePeriodId, a.LeavePeriod?.Name ?? "—",
            a.EffectiveFrom, a.AppliedAt, a.DaysAllocated)).ToList());
    }

    /// <summary>
    /// Grant a policy to a set of employees for a period.
    ///
    /// Takes a list because this is a start-of-year job done for everybody at
    /// once, and doing it one employee at a time through the UI is how half a
    /// company ends up with no leave. Employees who already have an assignment
    /// for the period are skipped rather than failing the batch.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("assignments")]
    public async Task<ActionResult<HrGrantResultDto>> Assign(
        [FromBody] HrLeaveAssignInput input, CancellationToken ct)
    {
        var period = await Db.HrLeavePeriods.FirstOrDefaultAsync(p => p.Id == input.LeavePeriodId, ct)
            ?? throw ApiException.NotFound("Leave period");
        if (!period.IsActive)
            throw ApiException.BadRequest($"{period.Name} is closed.");

        var policy = await Db.HrLeavePolicies.FirstOrDefaultAsync(p => p.Id == input.LeavePolicyId, ct)
            ?? throw ApiException.NotFound("Leave policy");

        var employeeIds = input.EmployeeIds?.Distinct().ToList() ?? [];
        if (employeeIds.Count == 0)
        {
            // Nobody named means everybody on the rolls — the usual case in April.
            employeeIds = await Db.HrEmployees
                .Where(e => EmploymentStatuses.OnRolls.Contains(e.Status))
                .Select(e => e.Id)
                .ToListAsync(ct);
        }

        var existing = await Db.HrLeavePolicyAssignments
            .Where(a => a.LeavePeriodId == period.Id && employeeIds.Contains(a.EmployeeId))
            .Select(a => a.EmployeeId)
            .ToListAsync(ct);

        var joiningDates = await Db.HrEmployees
            .Where(e => employeeIds.Contains(e.Id))
            .ToDictionaryAsync(e => e.Id, e => DateOnly.FromDateTime(e.JoiningDate), ct);

        var granted = 0;
        var skipped = 0;
        var totalDays = 0m;

        foreach (var employeeId in employeeIds)
        {
            if (existing.Contains(employeeId)) { skipped++; continue; }
            if (!joiningDates.TryGetValue(employeeId, out var joined)) { skipped++; continue; }

            var assignment = new HrLeavePolicyAssignment
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employeeId,
                LeavePolicyId = policy.Id,
                LeavePeriodId = period.Id,
                // Somebody who joined after the period opened gets the balance
                // of the year, not the whole of it.
                EffectiveFrom = joined > period.FromDate ? joined : null,
            };
            Db.HrLeavePolicyAssignments.Add(assignment);
            await Db.SaveChangesAsync(ct);

            totalDays += await ledger.ApplyPolicyAsync(assignment, ct);
            granted++;
        }

        return Ok(new HrGrantResultDto(granted, skipped, totalDays));
    }

    /* ================================================================== *
     * The register
     * ================================================================== */

    /// <summary>
    /// Every employee's standing balance for a period, by leave type.
    ///
    /// This is the screen HR is asked for when somebody says "how many days do
    /// I have left" — and the one payroll checks before approving an
    /// encashment.
    /// </summary>
    [HttpGet("balances")]
    public async Task<ActionResult<HrLeaveRegisterDto>> Balances(
        [FromQuery] int? periodId, CancellationToken ct)
    {
        var period = periodId is int id
            ? await Db.HrLeavePeriods.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct)
            : await ledger.CurrentPeriodAsync(ct);

        if (period is null)
            return Ok(new HrLeaveRegisterDto(null, null, [], []));

        var year = period.FromDate.Year;

        var types = await Db.HrLeaveTypes.AsNoTracking()
            .Where(t => t.IsActive).OrderBy(t => t.Name).ToListAsync(ct);

        var balances = await Db.HrLeaveBalances.AsNoTracking()
            .Include(b => b.Employee)
            .Where(b => b.Year == year)
            .ToListAsync(ct);

        var rows = balances
            .Where(b => b.Employee is not null)
            .GroupBy(b => new { b.EmployeeId, Name = b.Employee!.Name })
            .OrderBy(g => g.Key.Name)
            .Select(g => new HrLeaveRegisterRowDto(
                g.Key.EmployeeId, g.Key.Name,
                g.Sum(b => b.Closing),
                g.OrderBy(b => b.LeaveTypeId).Select(b => new HrLeaveBalanceCellDto(
                    b.LeaveTypeId, b.Opening, b.Accrued, b.Taken, b.Closing)).ToList()))
            .ToList();

        return Ok(new HrLeaveRegisterDto(
            period.Id, period.Name,
            types.Select(t => new HrLeaveTypeBriefDto(
                t.Id, t.Code, t.Name, t.Paid, t.CarryForward, t.MaxCarryForward,
                t.AllowEncashment, t.IsCompensatory)).ToList(),
            rows));
    }

    /// <summary>Every movement behind one employee's balance, newest first.</summary>
    [HttpGet("allocations")]
    public async Task<ActionResult<IReadOnlyList<HrLeaveAllocationDto>>> Allocations(
        [FromQuery] int employeeId, [FromQuery] int? periodId, CancellationToken ct)
    {
        var query = Db.HrLeaveAllocations.AsNoTracking()
            .Include(a => a.LeaveType).Include(a => a.LeavePeriod)
            .Where(a => a.EmployeeId == employeeId);

        if (periodId is int id) query = query.Where(a => a.LeavePeriodId == id);

        var rows = await query.OrderByDescending(a => a.Id).Take(300).ToListAsync(ct);

        return Ok(rows.Select(a => new HrLeaveAllocationDto(
            a.Id, a.LeaveTypeId, a.LeaveType?.Name ?? "—",
            a.LeavePeriodId, a.LeavePeriod?.Name ?? "—",
            a.Source, a.Days, a.Notes, a.CreatedAt)).ToList());
    }

    /// <summary>A hand-made adjustment, when the policy did not fit.</summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("allocations")]
    public async Task<ActionResult<HrLeaveAllocationDto>> Adjust(
        [FromBody] HrLeaveAdjustInput input, CancellationToken ct)
    {
        if (input.Days == 0m)
            throw ApiException.BadRequest("An adjustment of nothing changes nothing.");
        if (string.IsNullOrWhiteSpace(input.Notes))
            throw ApiException.BadRequest("Say why — a hand adjustment without a reason "
                + "is one nobody can defend at an audit.");

        var period = await Db.HrLeavePeriods.FirstOrDefaultAsync(p => p.Id == input.LeavePeriodId, ct)
            ?? throw ApiException.NotFound("Leave period");

        var allocation = await ledger.AllocateAsync(
            input.EmployeeId, input.LeaveTypeId, period,
            LeaveAllocationSources.Manual, input.Days, null, input.Notes.Trim(), ct);

        await Db.SaveChangesAsync(ct);
        await Db.Entry(allocation).Reference(a => a.LeaveType).LoadAsync(ct);

        return Ok(new HrLeaveAllocationDto(
            allocation.Id, allocation.LeaveTypeId, allocation.LeaveType?.Name ?? "—",
            period.Id, period.Name, allocation.Source, allocation.Days,
            allocation.Notes, allocation.CreatedAt));
    }

    /// <summary>
    /// Rebuild every balance from the allocations behind it.
    ///
    /// The balance rows are a running total, and a running total can drift —
    /// a restore from backup, a hand-edited row, a half-finished import. The
    /// allocations and the approved leave are the record; this recomputes the
    /// totals from them and reports what moved, which is also the cheapest
    /// proof that the two have not diverged.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("rebuild-balances")]
    public async Task<ActionResult<HrRebuildResultDto>> RebuildBalances(
        [FromQuery] int? periodId, CancellationToken ct)
    {
        var periods = periodId is int id
            ? await Db.HrLeavePeriods.Where(p => p.Id == id).ToListAsync(ct)
            : await Db.HrLeavePeriods.ToListAsync(ct);

        if (periods.Count == 0) throw ApiException.NotFound("Leave period");

        var corrected = 0;
        var examined = 0;
        var orphaned = 0;

        foreach (var period in periods)
        {
            var year = period.FromDate.Year;

            var allocations = await Db.HrLeaveAllocations.AsNoTracking()
                .Where(a => a.LeavePeriodId == period.Id)
                .ToListAsync(ct);

            // Leave taken is counted from the approved applications that start
            // inside the period, which is the same rule the approval uses.
            var taken = await Db.HrLeaveRequests.AsNoTracking()
                .Where(r => r.Status == HrRequestStatuses.Approved
                    && r.FromDate >= period.FromDate && r.FromDate <= period.ToDate)
                .ToListAsync(ct);

            var holidays = await Db.HrHolidays.AsNoTracking()
                .Where(h => h.OnDate >= period.FromDate && h.OnDate <= period.ToDate)
                .Select(h => h.OnDate).ToListAsync(ct);

            var weeklyOffs = await Db.HrEmployees.AsNoTracking()
                .Include(e => e.Shift)
                .ToDictionaryAsync(e => e.Id, e => e.Shift?.WeeklyOff, ct);

            var keys = allocations.Select(a => (a.EmployeeId, a.LeaveTypeId))
                .Concat(taken.Select(r => (r.EmployeeId, r.LeaveTypeId)))
                .Distinct()
                .ToList();

            var rows = await Db.HrLeaveBalances
                .Where(b => b.Year == year)
                .ToDictionaryAsync(b => (b.EmployeeId, b.LeaveTypeId), ct);

            foreach (var key in keys)
            {
                examined++;

                var mine = allocations.Where(a =>
                    a.EmployeeId == key.EmployeeId && a.LeaveTypeId == key.LeaveTypeId).ToList();

                var opening = mine
                    .Where(a => a.Source == LeaveAllocationSources.CarryForward)
                    .Sum(a => a.Days);
                var accrued = mine
                    .Where(a => a.Source != LeaveAllocationSources.CarryForward)
                    .Sum(a => a.Days);

                var used = 0m;
                foreach (var request in taken.Where(r =>
                    r.EmployeeId == key.EmployeeId && r.LeaveTypeId == key.LeaveTypeId))
                {
                    used += CountLeaveDays(request, weeklyOffs.GetValueOrDefault(key.EmployeeId),
                        holidays);
                }

                if (!rows.TryGetValue(key, out var row))
                {
                    row = new HrLeaveBalance
                    {
                        CompanyId = Db.Tenant.CompanyId,
                        EmployeeId = key.EmployeeId,
                        LeaveTypeId = key.LeaveTypeId,
                        Year = year,
                    };
                    Db.HrLeaveBalances.Add(row);
                }

                if (row.Opening != opening || row.Accrued != accrued || row.Taken != used)
                {
                    row.Opening = opening;
                    row.Accrued = accrued;
                    row.Taken = used;
                    corrected++;
                }
            }

            // A balance with nothing behind it is the case this endpoint exists
            // for — a restored backup, a reversed roll over, an import that was
            // undone. Recomputing only the rows that still have allocations
            // would leave exactly those untouched, which is to say it would
            // leave the wrong ones wrong.
            var live = keys.ToHashSet();
            foreach (var (key, row) in rows)
            {
                if (live.Contains(key)) continue;

                examined++;
                orphaned++;
                Db.HrLeaveBalances.Remove(row);
            }
        }

        await Db.SaveChangesAsync(ct);
        return Ok(new HrRebuildResultDto(periods.Count, examined, corrected, orphaned));
    }

    /// <summary>Days an approved leave actually consumed, holidays excluded.</summary>
    private static decimal CountLeaveDays(
        HrLeaveRequest request, string? weeklyOff, IReadOnlyCollection<DateOnly> holidays)
    {
        var counted = 0m;
        for (var day = request.FromDate; day <= request.ToDate; day = day.AddDays(1))
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

        if (request.HalfDay && counted > 0m) counted = counted == 1m ? 0.5m : counted - 0.5m;
        return counted;
    }

    /* ================================================================== *
     * Compensatory off
     * ================================================================== */

    [HttpGet("compensatory")]
    public async Task<ActionResult<IReadOnlyList<HrCompensatoryDto>>> Compensatory(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrCompensatoryRequests.AsNoTracking()
            .Include(r => r.Employee).Include(r => r.LeaveType).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(r => r.Status == status);

        var rows = await query.OrderByDescending(r => r.WorkedOn).ThenByDescending(r => r.Id)
            .Take(300).ToListAsync(ct);

        return Ok(rows.Select(ToCompensatory).ToList());
    }

    /// <summary>
    /// Claim back a day worked when it should not have been.
    ///
    /// The date is checked against the roster rather than taken on trust: a
    /// compensatory day is only earned if the day really was a holiday or a
    /// weekly off, and an events company where every Sunday is worked would
    /// otherwise quietly double everybody's leave.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("compensatory")]
    public async Task<ActionResult<HrCompensatoryDto>> ClaimCompensatory(
        [FromBody] HrCompensatoryInput input, CancellationToken ct)
    {
        var employee = await Db.HrEmployees.Include(e => e.Shift)
            .FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        if (input.WorkedOn > DateOnly.FromDateTime(DateTime.UtcNow))
            throw ApiException.BadRequest("That day has not happened yet.");

        var isHoliday = await Db.HrHolidays.AnyAsync(h => h.OnDate == input.WorkedOn, ct);
        var weeklyOff = employee.Shift?.WeeklyOff;
        var isWeeklyOff = !string.IsNullOrWhiteSpace(weeklyOff)
            && string.Equals(input.WorkedOn.DayOfWeek.ToString(), weeklyOff,
                StringComparison.OrdinalIgnoreCase);

        if (!isHoliday && !isWeeklyOff)
            throw ApiException.BadRequest(
                $"{input.WorkedOn:d MMM yyyy} was a working day — there is nothing to claim back.");

        var already = await Db.HrCompensatoryRequests.AnyAsync(
            r => r.EmployeeId == input.EmployeeId && r.WorkedOn == input.WorkedOn
                && r.Status != HrRequestStatuses.Rejected
                && r.Status != HrRequestStatuses.Cancelled, ct);
        if (already)
            throw ApiException.BadRequest("That day has already been claimed.");

        var type = await Db.HrLeaveTypes.FirstOrDefaultAsync(
            t => t.Id == input.LeaveTypeId && t.IsActive, ct)
            ?? throw ApiException.BadRequest("Unknown leave type.");

        var row = new HrCompensatoryRequest
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = employee.Id,
            WorkedOn = input.WorkedOn,
            LeaveTypeId = type.Id,
            Days = input.Days is > 0m and <= 1m ? input.Days : 1m,
            Reason = input.Reason,
            LeadId = input.LeadId,
            Status = HrRequestStatuses.PendingManager,
        };
        Db.HrCompensatoryRequests.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(r => r.Employee).LoadAsync(ct);
        await Db.Entry(row).Reference(r => r.LeaveType).LoadAsync(ct);
        return Ok(ToCompensatory(row));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("compensatory/{id:int}/decide")]
    public async Task<ActionResult<HrCompensatoryDto>> DecideCompensatory(
        int id, [FromQuery] bool approve, CancellationToken ct)
    {
        var row = await Db.HrCompensatoryRequests
            .Include(r => r.Employee).Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw ApiException.NotFound("Compensatory request");

        if (row.Status is HrRequestStatuses.Approved or HrRequestStatuses.Rejected)
            throw ApiException.BadRequest("That request has already been decided.");

        if (!approve)
        {
            row.Status = HrRequestStatuses.Rejected;
            await Db.SaveChangesAsync(ct);
            return Ok(ToCompensatory(row));
        }

        // The day is earned into the period the work happened in, not today's —
        // a claim filed in April for a Sunday in March belongs to March.
        var period = await ledger.PeriodForAsync(row.WorkedOn, ct)
            ?? await ledger.CurrentPeriodAsync(ct)
            ?? throw ApiException.BadRequest(
                "No leave period covers that date. Set one up before approving.");

        var allocation = await ledger.AllocateAsync(
            row.EmployeeId, row.LeaveTypeId, period,
            LeaveAllocationSources.Compensatory, row.Days, row.Id,
            $"Worked {row.WorkedOn:d MMM yyyy}", ct);

        row.Status = HrRequestStatuses.Approved;
        await Db.SaveChangesAsync(ct);

        row.AllocationId = allocation.Id;
        await Db.SaveChangesAsync(ct);

        return Ok(ToCompensatory(row));
    }

    /* ================================================================== *
     * Encashment
     * ================================================================== */

    [HttpGet("encashments")]
    public async Task<ActionResult<IReadOnlyList<HrEncashmentDto>>> Encashments(
        CancellationToken ct)
    {
        var rows = await Db.HrLeaveEncashments.AsNoTracking()
            .Include(e => e.Employee).Include(e => e.LeaveType).Include(e => e.LeavePeriod)
            .OrderByDescending(e => e.Id).Take(200).ToListAsync(ct);

        return Ok(rows.Select(ToEncashment).ToList());
    }

    /// <summary>
    /// Pay out unused days.
    ///
    /// The per-day rate is basic ÷ 30 at the time of the payout and is frozen
    /// on the record, because an encashment reprinted after a raise has to
    /// still show what was actually paid.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("encashments")]
    public async Task<ActionResult<HrEncashmentDto>> Encash(
        [FromBody] HrEncashmentInput input, CancellationToken ct)
    {
        var type = await Db.HrLeaveTypes.FirstOrDefaultAsync(t => t.Id == input.LeaveTypeId, ct)
            ?? throw ApiException.BadRequest("Unknown leave type.");
        if (!type.AllowEncashment)
            throw ApiException.BadRequest($"{type.Name} cannot be encashed.");
        if (input.Days <= 0m)
            throw ApiException.BadRequest("Days must be more than nothing.");

        var period = await Db.HrLeavePeriods.FirstOrDefaultAsync(p => p.Id == input.LeavePeriodId, ct)
            ?? throw ApiException.NotFound("Leave period");

        var shortfall = await ledger.ShortfallAsync(input.EmployeeId, type, period, input.Days, ct);
        if (shortfall is not null) throw ApiException.BadRequest(shortfall);

        var structure = await Db.HrSalaryStructures.AsNoTracking()
            .Where(s => s.EmployeeId == input.EmployeeId)
            .OrderByDescending(s => s.EffectiveFrom).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct)
            ?? throw ApiException.BadRequest(
                "That employee has no salary structure, so there is no rate to pay out at.");

        var perDay = Math.Round(structure.Basic / 30m, 2);

        var row = new HrLeaveEncashment
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            LeaveTypeId = type.Id,
            LeavePeriodId = period.Id,
            Days = input.Days,
            PerDayAmount = perDay,
            Amount = Math.Round(perDay * input.Days, 2),
            Status = HrRequestStatuses.PendingHr,
        };
        Db.HrLeaveEncashments.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(e => e.Employee).LoadAsync(ct);
        await Db.Entry(row).Reference(e => e.LeaveType).LoadAsync(ct);
        await Db.Entry(row).Reference(e => e.LeavePeriod).LoadAsync(ct);
        return Ok(ToEncashment(row));
    }

    /// <summary>
    /// Approving takes the days off the balance; rejecting leaves them alone.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("encashments/{id:int}/decide")]
    public async Task<ActionResult<HrEncashmentDto>> DecideEncashment(
        int id, [FromQuery] bool approve, CancellationToken ct)
    {
        var row = await Db.HrLeaveEncashments
            .Include(e => e.Employee).Include(e => e.LeaveType).Include(e => e.LeavePeriod)
            .FirstOrDefaultAsync(e => e.Id == id, ct)
            ?? throw ApiException.NotFound("Encashment");

        if (row.Status is HrRequestStatuses.Approved or HrRequestStatuses.Rejected)
            throw ApiException.BadRequest("That encashment has already been decided.");

        if (!approve)
        {
            row.Status = HrRequestStatuses.Rejected;
            await Db.SaveChangesAsync(ct);
            return Ok(ToEncashment(row));
        }

        var period = row.LeavePeriod
            ?? await Db.HrLeavePeriods.FirstAsync(p => p.Id == row.LeavePeriodId, ct);

        // Re-checked at approval, not just at request: the balance may have
        // been spent on actual leave in between.
        var type = row.LeaveType ?? await Db.HrLeaveTypes.FirstAsync(t => t.Id == row.LeaveTypeId, ct);
        var shortfall = await ledger.ShortfallAsync(row.EmployeeId, type, period, row.Days, ct);
        if (shortfall is not null) throw ApiException.BadRequest(shortfall);

        var allocation = await ledger.AllocateAsync(
            row.EmployeeId, row.LeaveTypeId, period,
            LeaveAllocationSources.Encashment, -row.Days, row.Id,
            $"Encashed at {row.PerDayAmount:0.00} a day", ct);

        row.Status = HrRequestStatuses.Approved;
        await Db.SaveChangesAsync(ct);

        row.AllocationId = allocation.Id;
        await Db.SaveChangesAsync(ct);

        return Ok(ToEncashment(row));
    }

    /* ================================================================== *
     * Block dates
     * ================================================================== */

    [HttpGet("block-dates")]
    public async Task<ActionResult<IReadOnlyList<HrLeaveBlockDto>>> BlockDates(CancellationToken ct)
    {
        var rows = await Db.HrLeaveBlockDates.AsNoTracking()
            .Include(b => b.Department)
            .OrderByDescending(b => b.FromDate).ToListAsync(ct);

        return Ok(rows.Select(b => new HrLeaveBlockDto(
            b.Id, b.FromDate, b.ToDate, b.Reason, b.DepartmentId,
            b.Department?.Name, b.AllowOverride, b.IsActive,
            b.ToDate.DayNumber - b.FromDate.DayNumber + 1)).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("block-dates")]
    public async Task<ActionResult<int>> SaveBlockDate(
        [FromBody] HrLeaveBlockInput input, CancellationToken ct)
    {
        if (input.ToDate < input.FromDate)
            throw ApiException.BadRequest("A block has to end on or after it starts.");
        if (string.IsNullOrWhiteSpace(input.Reason))
            throw ApiException.BadRequest("Say why the dates are blocked — people will ask.");

        var row = input.Id is int id and > 0
            ? await Db.HrLeaveBlockDates.FirstOrDefaultAsync(b => b.Id == id, ct)
                ?? throw ApiException.NotFound("Block date")
            : new HrLeaveBlockDate { CompanyId = Db.Tenant.CompanyId };

        row.FromDate = input.FromDate;
        row.ToDate = input.ToDate;
        row.Reason = input.Reason.Trim();
        row.DepartmentId = input.DepartmentId;
        row.AllowOverride = input.AllowOverride;
        row.IsActive = input.IsActive;

        if (row.Id == 0) Db.HrLeaveBlockDates.Add(row);
        await Db.SaveChangesAsync(ct);
        return Ok(row.Id);
    }

    /// <summary>
    /// What would happen if this leave were applied for — balance, blocks, and
    /// the days that actually count.
    ///
    /// The apply screen calls this as the dates are picked, so an employee
    /// finds out about the November block before writing a reason, not after.
    /// </summary>
    [HttpGet("check")]
    public async Task<ActionResult<HrLeaveCheckDto>> Check(
        [FromQuery] int employeeId, [FromQuery] int leaveTypeId,
        [FromQuery] DateOnly fromDate, [FromQuery] DateOnly toDate,
        [FromQuery] bool halfDay, CancellationToken ct)
    {
        if (toDate < fromDate)
            throw ApiException.BadRequest("To date must be on or after from date.");

        var employee = await Db.HrEmployees.AsNoTracking().Include(e => e.Shift)
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        var type = await Db.HrLeaveTypes.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == leaveTypeId, ct)
            ?? throw ApiException.BadRequest("Unknown leave type.");

        var holidays = await Db.HrHolidays.AsNoTracking()
            .Where(h => h.OnDate >= fromDate && h.OnDate <= toDate)
            .Select(h => h.OnDate)
            .ToListAsync(ct);

        // Holidays and the weekly off inside a leave are not leave. Counting
        // them is the single most common way a leave balance goes wrong.
        var weeklyOff = employee.Shift?.WeeklyOff;
        var counted = 0m;
        var skipped = 0;
        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            if (holidays.Contains(day)) { skipped++; continue; }
            if (!string.IsNullOrWhiteSpace(weeklyOff)
                && string.Equals(day.DayOfWeek.ToString(), weeklyOff,
                    StringComparison.OrdinalIgnoreCase))
            {
                skipped++;
                continue;
            }
            counted++;
        }

        if (halfDay && counted > 0m) counted = counted == 1m ? 0.5m : counted - 0.5m;

        var period = await ledger.PeriodForAsync(fromDate, ct);
        var shortfall = period is null
            ? "No leave period covers those dates."
            : await ledger.ShortfallAsync(employeeId, type, period, counted, ct);

        var blocks = await ledger.BlocksForAsync(fromDate, toDate, employee.DepartmentId, ct);

        var year = period?.FromDate.Year ?? fromDate.Year;
        var balance = await Db.HrLeaveBalances.AsNoTracking().FirstOrDefaultAsync(
            b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year, ct);

        return Ok(new HrLeaveCheckDto(
            counted, skipped,
            balance?.Closing ?? 0m,
            (balance?.Closing ?? 0m) - counted,
            shortfall,
            period?.Name,
            blocks.Select(b => new HrLeaveBlockDto(
                b.Id, b.FromDate, b.ToDate, b.Reason, b.DepartmentId, null,
                b.AllowOverride, b.IsActive,
                b.ToDate.DayNumber - b.FromDate.DayNumber + 1)).ToList()));
    }

    /* ================================================================== *
     * Mapping
     * ================================================================== */

    private static HrCompensatoryDto ToCompensatory(HrCompensatoryRequest r) => new(
        r.Id, r.EmployeeId, r.Employee?.Name ?? "—", r.WorkedOn,
        r.LeaveTypeId, r.LeaveType?.Name ?? "—", r.Days, r.Reason, r.LeadId,
        r.Status, r.AllocationId, r.CreatedAt);

    private static HrEncashmentDto ToEncashment(HrLeaveEncashment e) => new(
        e.Id, e.EmployeeId, e.Employee?.Name ?? "—",
        e.LeaveTypeId, e.LeaveType?.Name ?? "—",
        e.LeavePeriodId, e.LeavePeriod?.Name ?? "—",
        e.Days, e.PerDayAmount, e.Amount, e.Status, e.PaidInPayrollRunId, e.CreatedAt);
}
