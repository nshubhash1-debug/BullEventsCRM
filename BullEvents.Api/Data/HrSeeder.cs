using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// Departments, a general shift, leave types and employee rows from existing users
/// so the HR app opens on a company that already has people.
/// </summary>
public static class HrSeeder
{
    public static async Task SeedAsync(AppDbContext db, int companyId)
    {
        db.Tenant.ResolveSystem(companyId);

        if (!await db.HrDepartments.AnyAsync())
        {
            db.HrDepartments.AddRange(
                new HrDepartment { CompanyId = companyId, Name = "Human Resources", Code = "HR", Location = "HQ" },
                new HrDepartment { CompanyId = companyId, Name = "Operations", Code = "OPS", Location = "HQ" },
                new HrDepartment { CompanyId = companyId, Name = "Production", Code = "PRD", Location = "Warehouse" },
                new HrDepartment { CompanyId = companyId, Name = "Sales", Code = "SAL", Location = "HQ" },
                new HrDepartment { CompanyId = companyId, Name = "Accounts", Code = "ACC", Location = "HQ" });
            await db.SaveChangesAsync();
        }

        if (!await db.HrShifts.AnyAsync())
        {
            db.HrShifts.Add(new HrShift
            {
                CompanyId = companyId,
                Name = "General",
                StartTime = new TimeSpan(9, 30, 0),
                EndTime = new TimeSpan(18, 30, 0),
                GraceMinutes = 15,
                WeeklyOff = "Sunday",
            });
            await db.SaveChangesAsync();
        }

        if (!await db.HrLeaveTypes.AnyAsync())
        {
            db.HrLeaveTypes.AddRange(
                new HrLeaveType
                {
                    CompanyId = companyId, Code = "CL", Name = "Casual leave",
                    MonthlyEntitlement = 1, Paid = true, CarryForward = false, ApprovalLevels = 1,
                },
                new HrLeaveType
                {
                    CompanyId = companyId, Code = "SL", Name = "Sick leave",
                    MonthlyEntitlement = 0.5m, Paid = true, CarryForward = false, ApprovalLevels = 1,
                },
                new HrLeaveType
                {
                    CompanyId = companyId, Code = "EL", Name = "Earned leave",
                    MonthlyEntitlement = 1.25m, Paid = true, CarryForward = true, ApprovalLevels = 2,
                },
                new HrLeaveType
                {
                    CompanyId = companyId, Code = "LWP", Name = "Leave without pay",
                    MonthlyEntitlement = 0, Paid = false, CarryForward = false, ApprovalLevels = 2,
                });
            await db.SaveChangesAsync();
        }

        if (!await db.HrEmployees.AnyAsync())
        {

        var shiftId = await db.HrShifts.Select(s => s.Id).FirstAsync();
        var salesDept = await db.HrDepartments.Where(d => d.Code == "SAL").Select(d => d.Id).FirstAsync();
        var hrDept = await db.HrDepartments.Where(d => d.Code == "HR").Select(d => d.Id).FirstAsync();
        var opsDept = await db.HrDepartments.Where(d => d.Code == "OPS").Select(d => d.Id).FirstAsync();
        var designationId = await db.Designations.OrderBy(d => d.Id).Select(d => (int?)d.Id).FirstOrDefaultAsync();
        var types = await db.HrLeaveTypes.ToListAsync();
        var year = DateTime.UtcNow.Year;

        var users = await db.Users.Where(u => u.IsActive).OrderBy(u => u.Id).Take(12).ToListAsync();
        var n = 1;
        HrEmployee? first = null;
        foreach (var user in users)
        {
            var dept = user.Role == Roles.Hr ? hrDept
                : user.Role is Roles.BackOffice or Roles.Mis ? opsDept
                : salesDept;
            var status = n <= 2 ? EmploymentStatuses.Probation : EmploymentStatuses.Confirmed;
            var employee = new HrEmployee
            {
                CompanyId = companyId,
                EmployeeCode = $"BE{n:0000}",
                Name = user.Name,
                Email = user.Email,
                Phone = null,
                UserId = user.Id,
                OwnerId = user.Id,
                DepartmentId = dept,
                DesignationId = designationId,
                ManagerEmployeeId = first?.Id,
                JoiningDate = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddMonths(-n), DateTimeKind.Utc),
                EmploymentType = EmploymentTypes.Permanent,
                Status = status,
                CollarType = n > 9 ? CollarTypes.Production : CollarTypes.White,
                ShiftId = shiftId,
                Location = "HQ",
            };
            db.HrEmployees.Add(employee);
            await db.SaveChangesAsync();
            first ??= employee;

            db.HrSalaryStructures.Add(new HrSalaryStructure
            {
                CompanyId = companyId,
                EmployeeId = employee.Id,
                EffectiveFrom = DateOnly.FromDateTime(employee.JoiningDate),
                Basic = 25_000 + n * 2_000,
                Hra = 10_000,
                Allowances = 5_000,
                Incentive = n % 3 == 0 ? 3_000 : 0,
                Deductions = 500,
            });

            foreach (var type in types)
            {
                db.HrLeaveBalances.Add(new HrLeaveBalance
                {
                    CompanyId = companyId,
                    EmployeeId = employee.Id,
                    LeaveTypeId = type.Id,
                    Year = year,
                    Opening = type.MonthlyEntitlement * 12,
                });
            }

            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            for (var i = 0; i < 7; i++)
            {
                var day = today.AddDays(-i);
                if (day.DayOfWeek == DayOfWeek.Sunday) continue;
                db.HrAttendances.Add(new HrAttendance
                {
                    CompanyId = companyId,
                    EmployeeId = employee.Id,
                    WorkDate = day,
                    InTime = new TimeSpan(9, 35, 0),
                    OutTime = new TimeSpan(18, 40, 0),
                    Status = i == 2 && n == 4 ? AttendanceDayStatuses.Absent : AttendanceDayStatuses.Present,
                    IsLate = i == 1,
                    Source = "Seed",
                });
            }

            n++;
        }

        await db.SaveChangesAsync();
        }

        await SeedAdvancedAsync(db, companyId);
    }

    static async Task SeedAdvancedAsync(AppDbContext db, int companyId)
    {
        if (await db.HrHolidays.AnyAsync()) return;

        var year = DateTime.UtcNow.Year;
        db.HrHolidays.AddRange(
            new HrHoliday { CompanyId = companyId, Name = "Republic Day", OnDate = new DateOnly(year, 1, 26) },
            new HrHoliday { CompanyId = companyId, Name = "Holi", OnDate = new DateOnly(year, 3, 14) },
            new HrHoliday { CompanyId = companyId, Name = "Independence Day", OnDate = new DateOnly(year, 8, 15) },
            new HrHoliday { CompanyId = companyId, Name = "Gandhi Jayanti", OnDate = new DateOnly(year, 10, 2) },
            new HrHoliday { CompanyId = companyId, Name = "Diwali", OnDate = new DateOnly(year, 11, 8) },
            new HrHoliday { CompanyId = companyId, Name = "Christmas", OnDate = new DateOnly(year, 12, 25) });

        db.HrPolicies.AddRange(
            new HrPolicy
            {
                CompanyId = companyId, Title = "Leave & attendance policy", Category = "Time",
                Body = "Apply leave in the HR app. Casual leave is manager-approved. Earned leave needs HR as a second step. Geo check-in is the source of truth for payroll days.",
                EffectiveFrom = new DateOnly(year, 1, 1),
            },
            new HrPolicy
            {
                CompanyId = companyId, Title = "POSH", Category = "Conduct",
                Body = "The workplace is a safe space. Raise a confidential ticket under category POSH — it routes only to Head HR.",
                EffectiveFrom = new DateOnly(year, 1, 1),
            },
            new HrPolicy
            {
                CompanyId = companyId, Title = "Expense & travel", Category = "Pay",
                Body = "Claims need a bill. Manager then Accounts. Event-site travel uses the Event crew module for OT, not this form.",
                EffectiveFrom = new DateOnly(year, 1, 1),
            });

        db.HrAnnouncements.Add(new HrAnnouncement
        {
            CompanyId = companyId,
            Title = "HR command centre is live",
            Body = "Check-in from Attendance, goals live under Performance, and HR tickets no longer go to a shared inbox. Open My HR for self-service.",
            PinUntil = DateTime.UtcNow.AddMonths(1),
            Audience = "All",
        });

        var people = await db.HrEmployees.OrderBy(e => e.Id).Take(4).ToListAsync();
        if (people.Count == 0)
        {
            await db.SaveChangesAsync();
            return;
        }

        var first = people[0];
        db.HrExpenseClaims.Add(new HrExpenseClaim
        {
            CompanyId = companyId,
            EmployeeId = first.Id,
            ClaimDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-2)),
            Category = "Travel",
            Amount = 1850,
            Description = "Site visit — auto + parking",
            Status = HrRequestStatuses.PendingManager,
        });
        db.HrHelpdeskTickets.Add(new HrHelpdeskTicket
        {
            CompanyId = companyId,
            EmployeeId = first.Id,
            Subject = "UAN not reflecting in EPFO",
            Category = "Statutory",
            Priority = "High",
            Body = "Joined last month — UAN still shows previous employer.",
            Status = HrTicketStatuses.Open,
        });

        var cycle = new HrAppraisalCycle
        {
            CompanyId = companyId,
            Name = $"{year} H2 review",
            FromDate = new DateOnly(year, 7, 1),
            ToDate = new DateOnly(year, 12, 31),
        };
        db.HrAppraisalCycles.Add(cycle);
        await db.SaveChangesAsync();

        db.HrGoals.Add(new HrGoal
        {
            CompanyId = companyId,
            EmployeeId = first.Id,
            CycleId = cycle.Id,
            Title = "Close 12 qualified venue enquiries",
            Kra = "Revenue",
            Target = "12 closed-won or handed to operations",
            Weight = 40,
            Progress = 35,
        });
        db.HrAppraisals.Add(new HrAppraisal
        {
            CompanyId = companyId,
            CycleId = cycle.Id,
            EmployeeId = first.Id,
            SelfScore = 4,
            Status = "Submitted",
        });

        db.HrTrainings.Add(new HrTraining
        {
            CompanyId = companyId,
            Title = "On-site safety & guest handling",
            Trainer = "Ops Head",
            FromDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(10)),
            ToDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(11)),
            Venue = "HQ auditorium",
        });
        db.HrLifecycleEvents.Add(new HrLifecycleEvent
        {
            CompanyId = companyId,
            EmployeeId = first.Id,
            Kind = HrLifecycleKinds.Confirmation,
            EffectiveOn = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(20)),
            FromValue = EmploymentStatuses.Probation,
            ToValue = EmploymentStatuses.Confirmed,
            Notes = "Probation review — no PIP.",
        });

        if (!await db.HrVacancies.AnyAsync())
        {
            db.HrVacancies.Add(new HrVacancy
            {
                CompanyId = companyId,
                Position = "Event floor supervisor",
                Location = "Mumbai",
                Experience = "3–5 years",
                Status = "Open",
                JobDescription = "Own guest flow, vendor check-in and late-night close.",
            });
            await db.SaveChangesAsync();
        }

        var vacancyId = await db.HrVacancies.Select(v => (int?)v.Id).FirstOrDefaultAsync();
        if (!await db.HrCandidates.AnyAsync() && vacancyId is int vid)
        {
            var candidate = new HrCandidate
            {
                CompanyId = companyId,
                VacancyId = vid,
                Name = "Ananya Mehta",
                Phone = "9876500123",
                Email = "ananya.mehta@example.com",
                Source = "Naukri",
                Experience = "4 years banquet",
                Stage = RecruitmentStages.Interview,
            };
            db.HrCandidates.Add(candidate);
            await db.SaveChangesAsync();
            db.HrInterviews.Add(new HrInterview
            {
                CompanyId = companyId,
                CandidateId = candidate.Id,
                ScheduledAt = DateTime.UtcNow.AddDays(2).AddHours(4),
                Panel = "Head HR + Ops",
                Mode = "InPerson",
                Status = "Scheduled",
            });
        }

        await db.SaveChangesAsync();
    }
}
