using BullEvents.Api.Models;
using BullEvents.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// The leave year, the policies that fill it, and the season nobody may book off.
///
/// Refuses to run once a period exists, so a company that has set up its own
/// leave year is never touched. What it writes is what an events business
/// actually runs: two policies, because a designer on the payroll and a helper
/// on contract are not owed the same leave, and a blocked November, because
/// that is when every wedding in the country happens.
/// </summary>
public static class LeaveSeeder
{
    /// <summary>
    /// Leave types the allocation model needs that the base seeder does not
    /// create — chiefly compensatory off, which is the one an events crew
    /// actually earns.
    /// </summary>
    private record TypeSpec(
        string Code, string Name, decimal Monthly, bool Paid, bool CarryForward,
        decimal MaxCarryForward, bool AllowEncashment, bool IsCompensatory,
        int CompensatoryExpiryDays);

    private static readonly TypeSpec[] Types =
    [
        new("COMP", "Compensatory off", 0m, Paid: true, CarryForward: false,
            MaxCarryForward: 0m, AllowEncashment: false, IsCompensatory: true,
            // A comp-off nobody takes within the quarter is one the company has
            // quietly turned into an unfunded liability.
            CompensatoryExpiryDays: 90),
    ];

    public static async Task SeedAsync(AppDbContext db, LeaveLedger ledger)
    {
        if (await db.HrLeavePeriods.IgnoreQueryFilters().AnyAsync()) return;

        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startYear = today.Month >= 4 ? today.Year : today.Year - 1;

        /* ---------------- the leave year ---------------- */

        var current = new HrLeavePeriod
        {
            CompanyId = company.Id,
            Name = $"FY {startYear}-{(startYear + 1) % 100:D2}",
            FromDate = new DateOnly(startYear, 4, 1),
            ToDate = new DateOnly(startYear + 1, 3, 31),
        };
        // Next year exists from the start so a rollover has somewhere to go
        // without somebody having to remember to create it in March.
        var next = new HrLeavePeriod
        {
            CompanyId = company.Id,
            Name = $"FY {startYear + 1}-{(startYear + 2) % 100:D2}",
            FromDate = new DateOnly(startYear + 1, 4, 1),
            ToDate = new DateOnly(startYear + 2, 3, 31),
        };
        db.HrLeavePeriods.AddRange(current, next);
        await db.SaveChangesAsync();

        /* ---------------- period-end rules on the existing types ---------------- */

        var existing = await db.HrLeaveTypes.ToListAsync();

        foreach (var type in existing)
        {
            switch (type.Code)
            {
                case "EL":  // earned leave carries, within a cap, and may be paid out
                    type.CarryForward = true;
                    type.MaxCarryForward = 30m;
                    type.MaxBalance = 45m;
                    type.AllowEncashment = true;
                    break;
                case "SL":  // sick leave lapses; carrying it encourages hoarding
                    type.CarryForward = false;
                    break;
                case "CL":  // casual leave lapses too, by almost every policy
                    type.CarryForward = false;
                    break;
                case "LWP": // loss of pay has no balance to run out of
                    type.AllowNegativeBalance = true;
                    break;
            }
        }

        foreach (var spec in Types)
        {
            if (existing.Any(t => t.Code == spec.Code)) continue;

            db.HrLeaveTypes.Add(new HrLeaveType
            {
                CompanyId = company.Id,
                Code = spec.Code,
                Name = spec.Name,
                MonthlyEntitlement = spec.Monthly,
                Paid = spec.Paid,
                CarryForward = spec.CarryForward,
                MaxCarryForward = spec.MaxCarryForward,
                AllowEncashment = spec.AllowEncashment,
                IsCompensatory = spec.IsCompensatory,
                CompensatoryExpiryDays = spec.CompensatoryExpiryDays,
                ApprovalLevels = 1,
            });
        }

        await db.SaveChangesAsync();

        var types = await db.HrLeaveTypes.ToDictionaryAsync(t => t.Code, t => t.Id);

        /* ---------------- policies ---------------- */

        var staff = new HrLeavePolicy
        {
            CompanyId = company.Id,
            Name = "Staff",
            Notes = "Salaried employees on the office roll.",
        };
        var crew = new HrLeavePolicy
        {
            CompanyId = company.Id,
            Name = "Site crew",
            Notes = "Production and site staff. Less earned leave, "
                + "because most of what they take back is compensatory off.",
        };
        db.HrLeavePolicies.AddRange(staff, crew);
        await db.SaveChangesAsync();

        void Line(HrLeavePolicy policy, string code, decimal days)
        {
            if (!types.TryGetValue(code, out var typeId)) return;
            db.HrLeavePolicyLines.Add(new HrLeavePolicyLine
            {
                CompanyId = company.Id,
                LeavePolicyId = policy.Id,
                LeaveTypeId = typeId,
                AnnualAllocation = days,
            });
        }

        Line(staff, "EL", 18m);
        Line(staff, "CL", 8m);
        Line(staff, "SL", 8m);

        Line(crew, "EL", 12m);
        Line(crew, "CL", 6m);
        Line(crew, "SL", 6m);

        await db.SaveChangesAsync();

        /* ---------------- the season ---------------- */

        // November through mid-December is the wedding season. Nothing else in
        // the year is worth blocking, and blocking more than this makes the
        // list something people learn to ignore.
        db.HrLeaveBlockDates.AddRange(
            new HrLeaveBlockDate
            {
                CompanyId = company.Id,
                FromDate = new DateOnly(startYear, 11, 1),
                ToDate = new DateOnly(startYear, 12, 15),
                Reason = "Wedding season — every crew is committed",
                AllowOverride = false,
            },
            new HrLeaveBlockDate
            {
                CompanyId = company.Id,
                FromDate = new DateOnly(startYear + 1, 1, 15),
                ToDate = new DateOnly(startYear + 1, 2, 28),
                Reason = "Second wedding season — leave needs a good reason",
                AllowOverride = true,
            });

        await db.SaveChangesAsync();

        /* ---------------- grant the year ---------------- */

        var employees = await db.HrEmployees
            .Where(e => EmploymentStatuses.OnRolls.Contains(e.Status))
            .Select(e => new { e.Id, e.CollarType, e.JoiningDate })
            .ToListAsync();

        foreach (var employee in employees)
        {
            var policy = employee.CollarType == CollarTypes.Production ? crew : staff;
            var joined = DateOnly.FromDateTime(employee.JoiningDate);

            var assignment = new HrLeavePolicyAssignment
            {
                CompanyId = company.Id,
                EmployeeId = employee.Id,
                LeavePolicyId = policy.Id,
                LeavePeriodId = current.Id,
                EffectiveFrom = joined > current.FromDate ? joined : null,
            };
            db.HrLeavePolicyAssignments.Add(assignment);
            await db.SaveChangesAsync();

            await ledger.ApplyPolicyAsync(assignment);
        }
    }
}
