using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>
/// The one place leave entitlement moves.
///
/// Every grant, carry-forward, compensatory day, encashment and lapse goes
/// through <see cref="AllocateAsync"/>, which writes both the allocation that
/// explains the movement and the balance row that totals it. Nothing else
/// touches a balance, so a balance can always be reconciled against the rows
/// behind it — the same discipline the prop store's stock ledger keeps.
/// </summary>
public class LeaveLedger(AppDbContext db)
{
    /// <summary>
    /// The period a date falls in.
    ///
    /// Leave belongs to a period, not a calendar year: an events company runs
    /// April to March, so a day taken in February belongs to the year that
    /// started the previous April. Reading it from the date rather than from
    /// <c>DateTime.UtcNow.Year</c> is the difference between a February leave
    /// coming off the right balance and coming off next year's.
    /// </summary>
    public async Task<HrLeavePeriod?> PeriodForAsync(DateOnly date, CancellationToken ct = default)
    {
        return await db.HrLeavePeriods
            .Where(p => p.IsActive && p.FromDate <= date && p.ToDate >= date)
            .OrderByDescending(p => p.FromDate)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>The period covering today, which is what most screens want.</summary>
    public Task<HrLeavePeriod?> CurrentPeriodAsync(CancellationToken ct = default) =>
        PeriodForAsync(DateOnly.FromDateTime(DateTime.UtcNow), ct);

    /// <summary>
    /// Move entitlement, and record why.
    ///
    /// <paramref name="days"/> may be negative — an encashment and a lapse both
    /// take days away. The balance row is created if this is the employee's
    /// first movement in the period.
    /// </summary>
    public async Task<HrLeaveAllocation> AllocateAsync(
        int employeeId,
        int leaveTypeId,
        HrLeavePeriod period,
        string source,
        decimal days,
        int? sourceRecordId = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (!LeaveAllocationSources.All.Contains(source))
            throw ApiException.BadRequest($"Unknown allocation source '{source}'.");

        var allocation = new HrLeaveAllocation
        {
            CompanyId = db.Tenant.CompanyId,
            EmployeeId = employeeId,
            LeaveTypeId = leaveTypeId,
            LeavePeriodId = period.Id,
            Source = source,
            Days = days,
            SourceRecordId = sourceRecordId,
            Notes = notes,
        };
        db.HrLeaveAllocations.Add(allocation);

        var balance = await BalanceRowAsync(employeeId, leaveTypeId, period, ct);

        // Opening is what crossed the period boundary; everything earned inside
        // the period is accrual. Keeping them apart is what lets a balance
        // screen say "8 carried, 18 earned" rather than one number nobody can
        // take apart.
        if (source == LeaveAllocationSources.CarryForward) balance.Opening += days;
        else balance.Accrued += days;

        return allocation;
    }

    /// <summary>
    /// Record days consumed by an approved leave.
    ///
    /// Taken is kept separate from a negative accrual so the register can show
    /// what was granted and what was used, which are different questions.
    /// </summary>
    public async Task ConsumeAsync(
        int employeeId, int leaveTypeId, HrLeavePeriod period, decimal days,
        CancellationToken ct = default)
    {
        var balance = await BalanceRowAsync(employeeId, leaveTypeId, period, ct);
        balance.Taken += days;
    }

    private async Task<HrLeaveBalance> BalanceRowAsync(
        int employeeId, int leaveTypeId, HrLeavePeriod period, CancellationToken ct)
    {
        // The row is addressed by the period's starting year, which is what the
        // existing unique index is on. One period per starting year is the only
        // arrangement that makes sense anyway.
        var year = period.FromDate.Year;

        var balance = await db.HrLeaveBalances.FirstOrDefaultAsync(
            b => b.EmployeeId == employeeId && b.LeaveTypeId == leaveTypeId && b.Year == year, ct);

        if (balance is null)
        {
            balance = new HrLeaveBalance
            {
                CompanyId = db.Tenant.CompanyId,
                EmployeeId = employeeId,
                LeaveTypeId = leaveTypeId,
                Year = year,
            };
            db.HrLeaveBalances.Add(balance);
        }

        return balance;
    }

    /// <summary>
    /// Whether an employee has the days they are asking for.
    ///
    /// Returns null when the request is fine, and the reason when it is not.
    /// A type that allows a negative balance never refuses — loss of pay is
    /// how that case is settled, at payroll, not here.
    /// </summary>
    public async Task<string?> ShortfallAsync(
        int employeeId, HrLeaveType type, HrLeavePeriod period, decimal days,
        CancellationToken ct = default)
    {
        if (type.AllowNegativeBalance || !type.Paid) return null;

        var year = period.FromDate.Year;
        var balance = await db.HrLeaveBalances.AsNoTracking().FirstOrDefaultAsync(
            b => b.EmployeeId == employeeId && b.LeaveTypeId == type.Id && b.Year == year, ct);

        var available = balance?.Closing ?? 0m;
        if (available >= days) return null;

        return $"Only {available:0.##} day{(available == 1m ? "" : "s")} of {type.Name} "
            + $"available; {days:0.##} requested.";
    }

    /// <summary>
    /// The blocks standing in the way of a date range, worst first.
    ///
    /// A hard block cannot be approved through by anybody; a soft one is a
    /// warning the approver is shown and may accept.
    /// </summary>
    public async Task<List<HrLeaveBlockDate>> BlocksForAsync(
        DateOnly from, DateOnly to, int? departmentId, CancellationToken ct = default)
    {
        var blocks = await db.HrLeaveBlockDates.AsNoTracking()
            .Where(b => b.IsActive
                && b.FromDate <= to && b.ToDate >= from
                && (b.DepartmentId == null || b.DepartmentId == departmentId))
            .ToListAsync(ct);

        return [.. blocks.OrderBy(b => b.AllowOverride).ThenBy(b => b.FromDate)];
    }

    /// <summary>
    /// Close a period: carry what may be carried, lapse the rest.
    ///
    /// Written as two allocations rather than one net movement, because the
    /// register has to be able to show a company how many days it let expire.
    /// Refuses a second run — a rollover applied twice would double every
    /// opening balance, and there is no way to tell from the totals that it
    /// happened.
    /// </summary>
    public async Task<(int Carried, int Lapsed, decimal DaysCarried, decimal DaysLapsed)>
        RollOverAsync(HrLeavePeriod closing, HrLeavePeriod opening, CancellationToken ct = default)
    {
        if (closing.RolledOverAt is not null)
            throw ApiException.BadRequest($"{closing.Name} has already been rolled over.");
        if (opening.Id == closing.Id)
            throw ApiException.BadRequest("A period cannot roll over into itself.");

        var types = await db.HrLeaveTypes.AsNoTracking().ToDictionaryAsync(t => t.Id, ct);
        var balances = await db.HrLeaveBalances.AsNoTracking()
            .Where(b => b.Year == closing.FromDate.Year)
            .ToListAsync(ct);

        var carried = 0;
        var lapsed = 0;
        var daysCarried = 0m;
        var daysLapsed = 0m;

        foreach (var balance in balances)
        {
            if (!types.TryGetValue(balance.LeaveTypeId, out var type)) continue;

            var closingBalance = balance.Closing;
            if (closingBalance <= 0m) continue;

            var carryable = type.CarryForward
                ? type.MaxCarryForward > 0m
                    ? Math.Min(closingBalance, type.MaxCarryForward)
                    : closingBalance
                : 0m;

            // The next period's own ceiling can bite before the carry cap does.
            if (type.MaxBalance > 0m) carryable = Math.Min(carryable, type.MaxBalance);

            var expiring = closingBalance - carryable;

            if (carryable > 0m)
            {
                await AllocateAsync(balance.EmployeeId, balance.LeaveTypeId, opening,
                    LeaveAllocationSources.CarryForward, carryable, closing.Id,
                    $"Carried from {closing.Name}", ct);
                carried++;
                daysCarried += carryable;
            }

            if (expiring > 0m)
            {
                // Booked against the closing period, not the opening one — the
                // days expired there, and that is where the register must
                // show them.
                await AllocateAsync(balance.EmployeeId, balance.LeaveTypeId, closing,
                    LeaveAllocationSources.Lapsed, -expiring, closing.Id,
                    $"Lapsed at the end of {closing.Name}", ct);
                lapsed++;
                daysLapsed += expiring;
            }
        }

        var period = await db.HrLeavePeriods.FirstAsync(p => p.Id == closing.Id, ct);
        period.RolledOverAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return (carried, lapsed, daysCarried, daysLapsed);
    }

    /// <summary>
    /// Apply a policy to an employee, pro-rated if they joined mid-period.
    ///
    /// Pro-rating is by whole months remaining rather than days, which is what
    /// every Indian HR policy this has been checked against actually says, and
    /// is kinder than the day count in the month somebody joins.
    /// </summary>
    public async Task<decimal> ApplyPolicyAsync(
        HrLeavePolicyAssignment assignment, CancellationToken ct = default)
    {
        if (assignment.AppliedAt is not null)
            throw ApiException.BadRequest("This assignment has already been applied.");

        var period = await db.HrLeavePeriods.FirstOrDefaultAsync(
            p => p.Id == assignment.LeavePeriodId, ct)
            ?? throw ApiException.BadRequest("Unknown leave period.");

        var lines = await db.HrLeavePolicyLines.AsNoTracking()
            .Where(l => l.LeavePolicyId == assignment.LeavePolicyId)
            .ToListAsync(ct);

        if (lines.Count == 0)
            throw ApiException.BadRequest("That policy grants nothing — add a leave type to it first.");

        var start = assignment.EffectiveFrom ?? period.FromDate;
        if (start < period.FromDate) start = period.FromDate;

        var totalMonths = MonthsBetween(period.FromDate, period.ToDate);
        var remaining = MonthsBetween(start, period.ToDate);
        var share = totalMonths <= 0 ? 1m : Math.Min(1m, (decimal)remaining / totalMonths);

        var granted = 0m;
        foreach (var line in lines)
        {
            var days = Math.Round(line.AnnualAllocation * share * 2m, MidpointRounding.AwayFromZero) / 2m;
            if (days <= 0m) continue;

            await AllocateAsync(assignment.EmployeeId, line.LeaveTypeId, period,
                LeaveAllocationSources.Policy, days, assignment.Id,
                share < 1m ? $"Pro-rated for {remaining} of {totalMonths} months" : null, ct);
            granted += days;
        }

        var tracked = await db.HrLeavePolicyAssignments.FirstAsync(a => a.Id == assignment.Id, ct);
        tracked.AppliedAt = DateTime.UtcNow;
        tracked.DaysAllocated = granted;

        await db.SaveChangesAsync(ct);
        return granted;
    }

    /// <summary>Whole months in a range, counting the month of the start date.</summary>
    private static int MonthsBetween(DateOnly from, DateOnly to)
    {
        if (to < from) return 0;
        return ((to.Year - from.Year) * 12) + to.Month - from.Month + 1;
    }
}
