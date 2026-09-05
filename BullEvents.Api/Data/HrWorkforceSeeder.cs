using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// A workforce for the demo tenant, shaped like an events company's.
///
/// <see cref="HrSeeder"/> only makes an employee for each login, which on a
/// fresh install is two people with no salary between them — a payroll module
/// with nothing to pay. This gives the tenant a floor: about thirty people
/// across the departments an events business actually has, on pay that
/// straddles every statutory threshold, so a payroll run exercises the rules
/// instead of returning zeros.
///
/// The spread is deliberate:
/// <list type="bullet">
/// <item>helpers and drivers under the ESI wage threshold, so ESI computes;</item>
/// <item>most of the crew under the PF wage ceiling, a few above it, so the
/// ceiling restriction is visible;</item>
/// <item>three states, so professional tax is not one flat number;</item>
/// <item>a handful earning enough to pay income tax, on both regimes.</item>
/// </list>
///
/// Demo data. It is guarded on the employee count, so a tenant that has
/// entered its own people is never touched.
/// </summary>
public static class HrWorkforceSeeder
{
    /// <summary>Below this many employees the tenant is assumed to be empty.</summary>
    private const int SeedBelow = 5;

    private record Person(
        string Name, string Dept, string Designation, string Location,
        decimal Basic, decimal Hra, decimal Allowances, string Collar,
        string EmploymentType, int MonthsOfService, decimal OvertimeRate = 0m);

    /// <summary>
    /// Pay is written as its components rather than a gross, because that is
    /// what the statute works on: PF follows basic, HRA is what section 10(13A)
    /// exempts against, and only their sum is the gross.
    /// </summary>
    private static readonly Person[] People =
    [
        /* ---------------- leadership ---------------- */
        new("Rohit Malhotra", "SAL", "Director — Events", "Maharashtra",
            120_000, 48_000, 32_000, CollarTypes.White, EmploymentTypes.Permanent, 96),
        new("Ananya Desai", "OPS", "Head of Operations", "Maharashtra",
            85_000, 34_000, 21_000, CollarTypes.White, EmploymentTypes.Permanent, 72),
        new("Vikram Nair", "ACC", "Finance Controller", "Maharashtra",
            78_000, 31_200, 19_800, CollarTypes.White, EmploymentTypes.Permanent, 60),

        /* ---------------- sales and client servicing ---------------- */
        new("Sneha Kulkarni", "SAL", "Senior Wedding Planner", "Maharashtra",
            48_000, 19_200, 12_800, CollarTypes.White, EmploymentTypes.Permanent, 44),
        new("Arjun Mehta", "SAL", "Wedding Planner", "Maharashtra",
            32_000, 12_800, 8_200, CollarTypes.White, EmploymentTypes.Permanent, 30),
        new("Ritika Joshi", "SAL", "Client Servicing Executive", "Karnataka",
            22_000, 8_800, 5_200, CollarTypes.White, EmploymentTypes.Permanent, 20),
        new("Karan Bhatia", "SAL", "Corporate Events Manager", "Karnataka",
            45_000, 18_000, 12_000, CollarTypes.White, EmploymentTypes.Permanent, 38),
        new("Pooja Rane", "SAL", "Business Development Executive", "Maharashtra",
            19_000, 7_600, 4_400, CollarTypes.White, EmploymentTypes.Permanent, 14),

        /* ---------------- design and décor ---------------- */
        new("Meera Iyer", "OPS", "Lead Décor Designer", "Maharashtra",
            52_000, 20_800, 13_200, CollarTypes.White, EmploymentTypes.Permanent, 50),
        new("Tanvi Shah", "OPS", "Décor Designer", "Maharashtra",
            28_000, 11_200, 7_800, CollarTypes.White, EmploymentTypes.Permanent, 26),
        new("Imran Qureshi", "OPS", "Floral Designer", "Maharashtra",
            18_500, 7_400, 4_100, CollarTypes.White, EmploymentTypes.Permanent, 18),
        new("Nikhil Rao", "OPS", "3D Visualiser", "Karnataka",
            26_000, 10_400, 6_600, CollarTypes.White, EmploymentTypes.Permanent, 22),

        /* ---------------- production and site crew ---------------- */
        new("Sanjay Pawar", "PRD", "Production Manager", "Maharashtra",
            42_000, 16_800, 11_200, CollarTypes.White, EmploymentTypes.Permanent, 55),
        new("Ramesh Yadav", "PRD", "Site Supervisor", "Maharashtra",
            16_000, 6_400, 3_600, CollarTypes.Production, EmploymentTypes.Permanent, 40, 120m),
        new("Dinesh Kamble", "PRD", "Site Supervisor", "Maharashtra",
            15_000, 6_000, 3_400, CollarTypes.Production, EmploymentTypes.Permanent, 34, 120m),
        new("Salim Ansari", "PRD", "Carpenter", "Maharashtra",
            11_000, 4_400, 2_600, CollarTypes.Production, EmploymentTypes.Permanent, 28, 95m),
        new("Prakash Gupta", "PRD", "Carpenter", "Maharashtra",
            10_500, 4_200, 2_500, CollarTypes.Production, EmploymentTypes.Permanent, 24, 95m),
        new("Vijay Sawant", "PRD", "Electrician", "Maharashtra",
            12_000, 4_800, 2_800, CollarTypes.Production, EmploymentTypes.Permanent, 32, 105m),
        new("Mohan Bhoir", "PRD", "Lighting Technician", "Maharashtra",
            11_500, 4_600, 2_700, CollarTypes.Production, EmploymentTypes.Permanent, 20, 105m),
        new("Suresh Jadhav", "PRD", "Rigger", "Maharashtra",
            10_000, 4_000, 2_400, CollarTypes.Production, EmploymentTypes.Permanent, 16, 90m),
        new("Ganesh More", "PRD", "Helper", "Maharashtra",
            8_000, 3_200, 1_900, CollarTypes.Production, EmploymentTypes.Permanent, 12, 75m),
        new("Rakesh Chavan", "PRD", "Helper", "Maharashtra",
            8_000, 3_200, 1_900, CollarTypes.Production, EmploymentTypes.Permanent, 9, 75m),
        new("Ashok Patil", "PRD", "Helper", "Maharashtra",
            7_800, 3_120, 1_880, CollarTypes.Production, EmploymentTypes.Contract, 7, 75m),

        /* ---------------- stores and logistics ---------------- */
        new("Deepak Shinde", "PRD", "Stores In-charge", "Maharashtra",
            20_000, 8_000, 5_000, CollarTypes.Production, EmploymentTypes.Permanent, 46),
        new("Naresh Kumar", "PRD", "Storekeeper", "Maharashtra",
            12_500, 5_000, 2_900, CollarTypes.Production, EmploymentTypes.Permanent, 26),
        new("Iqbal Shaikh", "PRD", "Driver", "Maharashtra",
            11_000, 4_400, 2_600, CollarTypes.Production, EmploymentTypes.Permanent, 30, 85m),
        new("Balu Gaikwad", "PRD", "Driver", "Maharashtra",
            10_800, 4_320, 2_580, CollarTypes.Production, EmploymentTypes.Permanent, 18, 85m),

        /* ---------------- support functions ---------------- */
        new("Shalini Verma", "HR", "HR Manager", "Maharashtra",
            46_000, 18_400, 12_100, CollarTypes.White, EmploymentTypes.Permanent, 52),
        new("Farah Khan", "HR", "HR Executive", "Maharashtra",
            21_000, 8_400, 5_100, CollarTypes.White, EmploymentTypes.Permanent, 15),
        new("Girish Menon", "ACC", "Accounts Executive", "Karnataka",
            24_000, 9_600, 6_000, CollarTypes.White, EmploymentTypes.Permanent, 28),
        new("Divya Pillai", "ACC", "Accounts Assistant", "Karnataka",
            15_500, 6_200, 3_800, CollarTypes.White, EmploymentTypes.Permanent, 11),
    ];

    public static async Task SeedAsync(AppDbContext db, int companyId)
    {
        if (await db.HrEmployees.CountAsync() >= SeedBelow) return;

        var shiftId = await db.HrShifts.Select(s => (int?)s.Id).FirstOrDefaultAsync();
        var departments = await db.HrDepartments.ToDictionaryAsync(d => d.Code, d => d.Id);
        if (departments.Count == 0) return;

        // Not every install has a Finance department; fall back rather than fail.
        int Dept(string code) =>
            departments.TryGetValue(code, out var id) ? id
            : departments.TryGetValue("OPS", out var ops) ? ops
            : departments.Values.First();

        // The designation catalogue is still the property business's — Managing
        // Director and Sales Manager, with nothing a wedding company employs.
        // Creating the missing titles here rather than leaving thirty people
        // with a blank one: an employee with no designation is a hole in the org
        // chart, and every screen that shows a job title shows a dash.
        var designations = await db.Designations.ToListAsync();
        var levels = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["Director — Events"] = 1,
            ["Head of Operations"] = 2,
            ["Finance Controller"] = 2,
            ["Production Manager"] = 3,
            ["Corporate Events Manager"] = 3,
            ["HR Manager"] = 3,
            ["Lead Décor Designer"] = 3,
            ["Senior Wedding Planner"] = 3,
            ["Stores In-charge"] = 4,
            ["Site Supervisor"] = 4,
            ["Wedding Planner"] = 4,
            ["Décor Designer"] = 4,
        };

        foreach (var title in People.Select(p => p.Designation).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (designations.Any(d => string.Equals(d.Name, title, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var designation = new Designation
            {
                CompanyId = companyId,
                Name = title,
                // Five is the catalogue's own default for an individual
                // contributor; the ones that outrank it are named above.
                Level = levels.TryGetValue(title, out var level) ? level : 5,
                Description = "Added with the events workforce.",
            };
            db.Designations.Add(designation);
            designations.Add(designation);
        }

        await db.SaveChangesAsync();
        var leaveTypes = await db.HrLeaveTypes.ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var financialYear = today.Month >= 4 ? today.Year : today.Year - 1;

        // Continue the existing numbering rather than colliding with it.
        var lastCode = await db.HrEmployees
            .Select(e => e.EmployeeCode)
            .ToListAsync();
        var next = lastCode
            .Select(c => int.TryParse(c.TrimStart('B', 'E'), out var n) ? n : 0)
            .DefaultIfEmpty(0)
            .Max() + 1;

        var managerId = await db.HrEmployees.OrderBy(e => e.Id)
            .Select(e => (int?)e.Id).FirstOrDefaultAsync();

        var created = new List<(HrEmployee Employee, Person Source)>();

        foreach (var person in People)
        {
            var joining = DateTime.SpecifyKind(
                DateTime.UtcNow.Date.AddMonths(-person.MonthsOfService), DateTimeKind.Utc);

            var designationId = designations
                .FirstOrDefault(d => string.Equals(d.Name, person.Designation,
                    StringComparison.OrdinalIgnoreCase))?.Id;

            var employee = new HrEmployee
            {
                CompanyId = companyId,
                EmployeeCode = $"BE{next:0000}",
                Name = person.Name,
                Email = $"{Slug(person.Name)}@bulleventsglobal.com",
                Phone = $"98{(70_00_00_00 + next * 137):00000000}",
                DepartmentId = Dept(person.Dept),
                DesignationId = designationId,
                ManagerEmployeeId = managerId,
                JoiningDate = joining,
                EmploymentType = person.EmploymentType,
                // Under six months is still on probation; that is what decides
                // notice period and confirmation, so it is derived rather than
                // sprinkled at random.
                Status = person.MonthsOfService < 6
                    ? EmploymentStatuses.Probation
                    : EmploymentStatuses.Confirmed,
                CollarType = person.Collar,
                ShiftId = shiftId,
                Location = person.Location,
                // The identifiers the statutory returns are rejected without.
                Pan = $"ABCPZ{1000 + next:0000}{(char)('A' + next % 26)}",
                Uan = $"1010{(next * 7919 + 1_000_000):0000000}",
                EsicIp = $"31{(next * 104729 + 10_000_000):00000000}",
                BankAccount = $"5011{(next * 8191 + 100_000_000):000000000}",
                Ifsc = "HDFC0000123",
            };

            db.HrEmployees.Add(employee);
            created.Add((employee, person));
            next++;
        }

        await db.SaveChangesAsync();

        foreach (var (employee, person) in created)
        {
            db.HrSalaryStructures.Add(new HrSalaryStructure
            {
                CompanyId = companyId,
                EmployeeId = employee.Id,
                EffectiveFrom = DateOnly.FromDateTime(employee.JoiningDate),
                Basic = person.Basic,
                Hra = person.Hra,
                Allowances = person.Allowances,
                Incentive = 0,
                Bonus = 0,
                OvertimeRate = person.OvertimeRate,
                Deductions = 0,
            });

            foreach (var type in leaveTypes)
            {
                db.HrLeaveBalances.Add(new HrLeaveBalance
                {
                    CompanyId = companyId,
                    EmployeeId = employee.Id,
                    LeaveTypeId = type.Id,
                    Year = today.Year,
                    Opening = type.MonthlyEntitlement * 12,
                });
            }
        }

        await db.SaveChangesAsync();

        await SeedTaxProfilesAsync(db, companyId, created, financialYear);
        await SeedAttendanceAsync(db, companyId, created, today);
    }

    /// <summary>
    /// Tax declarations for the people who earn enough to have one.
    ///
    /// Only the higher earners get a profile, and only some of them elect the
    /// old regime — which is the realistic picture, and it means a payroll run
    /// exercises both regimes rather than one.
    /// </summary>
    private static async Task SeedTaxProfilesAsync(
        AppDbContext db, int companyId,
        List<(HrEmployee Employee, Person Source)> created, int financialYear)
    {
        var index = 0;
        foreach (var (employee, person) in created)
        {
            var annualGross = (person.Basic + person.Hra + person.Allowances) * 12;
            index++;

            // Below roughly the rebate ceiling there is no tax to plan for and
            // no reason anyone would have filed a declaration.
            if (annualGross < 900_000m) continue;

            // Every third of them has done the arithmetic and moved.
            var old = index % 3 == 0;

            db.HrEmployeeTaxProfiles.Add(new HrEmployeeTaxProfile
            {
                CompanyId = companyId,
                EmployeeId = employee.Id,
                FinancialYear = financialYear,
                Regime = old ? TaxRegimes.Old : TaxRegimes.New,
                AnnualRentPaid = old ? person.Hra * 12 * 1.2m : 0m,
                RentsInMetro = old && person.Location == PtStates.Maharashtra,
                Section80C = old ? 150_000m : 0m,
                Section80Ccd1B = old ? 50_000m : 0m,
                Section80D = old ? 25_000m : 0m,
                Section80Tta = old ? 10_000m : 0m,
                ProofsSubmitted = false,
            });
        }

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// A month of attendance, with enough absence to make loss of pay real.
    ///
    /// Payroll that never meets an absent day is payroll that has never had its
    /// loss-of-pay arithmetic checked, so two people are given one each.
    /// </summary>
    private static async Task SeedAttendanceAsync(
        AppDbContext db, int companyId,
        List<(HrEmployee Employee, Person Source)> created, DateOnly today)
    {
        var index = 0;
        foreach (var (employee, _) in created)
        {
            index++;
            for (var back = 1; back <= 30; back++)
            {
                var day = today.AddDays(-back);
                if (day.DayOfWeek == DayOfWeek.Sunday) continue;
                if (day < DateOnly.FromDateTime(employee.JoiningDate)) continue;

                // Two people, one absence each — enough to exercise LOP without
                // making the demo look like nobody turns up.
                var absent = (index == 14 && back == 9) || (index == 21 && back == 4);

                db.HrAttendances.Add(new HrAttendance
                {
                    CompanyId = companyId,
                    EmployeeId = employee.Id,
                    WorkDate = day,
                    InTime = absent ? null : new TimeSpan(9, 35, 0),
                    OutTime = absent ? null : new TimeSpan(18, 40, 0),
                    Status = absent
                        ? AttendanceDayStatuses.Absent
                        : AttendanceDayStatuses.Present,
                    IsLate = !absent && back % 7 == 0,
                    Source = "Seed",
                });
            }
        }

        await db.SaveChangesAsync();
    }

    private static string Slug(string name) =>
        name.ToLowerInvariant().Replace(' ', '.');
}
