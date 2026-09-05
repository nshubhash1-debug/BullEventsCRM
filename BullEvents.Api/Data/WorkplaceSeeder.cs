using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// The lists an events company works through: what happens on somebody's first
/// and last day, what its people can actually do, and what may be claimed back.
///
/// Refuses to run once a checklist exists. Nothing here is applied to anybody —
/// putting a separation checklist on a live employee would be an alarming thing
/// for a seeder to do.
/// </summary>
public static class WorkplaceSeeder
{
    private record Step(string Title, string Owner, int Offset, bool Blocking, string? Note);

    private record Checklist(string Name, string Kind, string? Collar, string Notes, Step[] Tasks);

    private static readonly Checklist[] Checklists =
    [
        new("Joining — office", ChecklistKinds.Onboarding, CollarTypes.White,
            "Designers, planners and support staff.",
        [
            new("Offer accepted and documents collected", ChecklistOwners.Hr, -7, true,
                "PAN, Aadhaar, bank details, previous employer relieving letter."),
            new("Laptop and email requested", ChecklistOwners.It, -5, false,
                "Ordered on the joining day it arrives in the second week."),
            new("Seat and access card", ChecklistOwners.Hr, -1, false, null),
            new("PF and ESI enrolment", ChecklistOwners.Hr, 1, true,
                "UAN and insurance number. Payroll files are rejected without them."),
            new("Salary structure assigned", ChecklistOwners.Finance, 1, true,
                "Nobody can be paid without one."),
            new("Leave policy assigned", ChecklistOwners.Hr, 2, false, null),
            new("Introduction to the team", ChecklistOwners.Manager, 1, false, null),
            new("Company policies read and acknowledged", ChecklistOwners.Employee, 5, false, null),
            new("Thirty-day check-in", ChecklistOwners.Manager, 30, false,
                "The conversation that catches a bad hire before probation ends."),
        ]),

        new("Joining — site crew", ChecklistKinds.Onboarding, CollarTypes.Production,
            "Carpenters, electricians, riggers, drivers and helpers.",
        [
            new("Documents and photograph collected", ChecklistOwners.Hr, -2, true,
                "Aadhaar and bank details. Many will not have a PAN — the higher TDS "
                + "rate applies until they do."),
            new("PF and ESI enrolment", ChecklistOwners.Hr, 1, true, null),
            new("Safety briefing", ChecklistOwners.Manager, 0, true,
                "Working at height, power and load-out. Nobody goes to site without it."),
            new("Boots, gloves and high-visibility issued", ChecklistOwners.Stores, 0, true, null),
            new("Tools issued and signed for", ChecklistOwners.Stores, 1, false, null),
            new("Salary structure assigned", ChecklistOwners.Finance, 1, true, null),
            new("Skills assessed", ChecklistOwners.Manager, 7, false,
                "So the roster knows what they can be sent to."),
        ]),

        new("Leaving — everybody", ChecklistKinds.Separation, null,
            "Applies to every exit. The collection list.",
        [
            new("Resignation acknowledged", ChecklistOwners.Hr, 0, false, null),
            new("Handover to a named person", ChecklistOwners.Manager, -7, true,
                "Named, not implied. An unnamed handover is one that did not happen."),
            new("Tools and equipment returned", ChecklistOwners.Stores, -1, true, null),
            new("Laptop, phone and access card returned", ChecklistOwners.It, -1, true, null),
            new("Advances and claims settled", ChecklistOwners.Finance, -3, true,
                "Anything outstanding comes off the full and final."),
            new("Exit interview", ChecklistOwners.Hr, -2, false,
                "Held before the last day, while they still care to be honest."),
            new("Full and final prepared", ChecklistOwners.Finance, 7, true, null),
            new("Relieving and experience letters issued", ChecklistOwners.Hr, 10, false, null),
            new("PF and ESI exit filed", ChecklistOwners.Hr, 15, false, null),
        ]),
    ];

    private record SkillSpec(string Name, string Category, bool Certified);

    /// <summary>
    /// What this business actually needs to know about its people before it
    /// staffs a site.
    /// </summary>
    private static readonly SkillSpec[] Skills =
    [
        new("Working at height", "Site safety", true),
        new("Rigging and trussing", "Site safety", true),
        new("Electrical — temporary power", "Technical", true),
        new("Goods vehicle driving", "Logistics", true),
        new("Forklift operation", "Logistics", true),
        new("First aid", "Site safety", true),

        new("Carpentry and fabrication", "Craft", false),
        new("Floral arrangement", "Craft", false),
        new("Draping and fabric work", "Craft", false),
        new("Mandap construction", "Craft", false),
        new("Stage and set building", "Craft", false),
        new("Lighting design", "Technical", false),
        new("Sound and AV", "Technical", false),

        new("3D visualisation", "Design", false),
        new("AutoCAD", "Design", false),
        new("Design costing", "Design", false),

        new("Client handling", "Client-facing", false),
        new("Vendor negotiation", "Client-facing", false),
        new("Crew supervision", "Leadership", false),
        new("Event coordination", "Leadership", false),
    ];

    private record ExpenseSpec(
        string Name, decimal PerClaim, decimal Monthly, bool Receipt,
        decimal WaivedBelow, bool NeedsTravel, string Description);

    private static readonly ExpenseSpec[] ExpenseTypes =
    [
        new("Local conveyance", 1_500m, 8_000m, true, 300m, false,
            "Autos and taxis within the city. Small fares do not need a bill."),
        new("Outstation travel", 0m, 0m, true, 0m, true,
            "Train, flight and bus for a trip. Has to hang off an approved travel request."),
        new("Accommodation", 4_000m, 0m, true, 0m, true,
            "A night away on a destination event."),
        new("Meals on site", 500m, 12_000m, true, 200m, false,
            "Crew food on a load-in or event day."),
        new("Site materials", 10_000m, 0m, true, 0m, false,
            "Bought at the venue because something ran out. Not a substitute for the godown."),
        new("Client entertainment", 5_000m, 25_000m, true, 0m, false,
            "Meetings and tastings with a client."),
        new("Mobile and internet", 1_000m, 1_000m, false, 0m, false,
            "Monthly reimbursement against the plan."),
        new("Medical", 0m, 0m, true, 0m, false,
            "Treatment not covered by insurance. Approved case by case."),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        // Each set guards itself rather than all three hanging off one table.
        // A seeder whose guard is a single table cannot survive somebody having
        // cleared one of the others — it re-runs and collides on a unique index,
        // which takes the whole application down at startup.
        var seedChecklists = !await db.HrChecklistTemplates.IgnoreQueryFilters().AnyAsync();
        var seedSkills = !await db.HrSkills.IgnoreQueryFilters().AnyAsync();
        var seedExpenseTypes = !await db.HrExpenseClaimTypes.IgnoreQueryFilters().AnyAsync();

        /* ---------------- checklists ---------------- */

        foreach (var checklist in seedChecklists ? Checklists : [])
        {
            var template = new HrChecklistTemplate
            {
                CompanyId = company.Id,
                Name = checklist.Name,
                Kind = checklist.Kind,
                CollarType = checklist.Collar,
                Notes = checklist.Notes,
            };

            var order = 0;
            foreach (var step in checklist.Tasks)
            {
                template.Tasks.Add(new HrChecklistTask
                {
                    CompanyId = company.Id,
                    Title = step.Title,
                    Description = step.Note,
                    Owner = step.Owner,
                    DueOffsetDays = step.Offset,
                    IsBlocking = step.Blocking,
                    SortOrder = order,
                });
                order += 10;
            }

            db.HrChecklistTemplates.Add(template);
        }

        /* ---------------- skills ---------------- */

        foreach (var skill in seedSkills ? Skills : [])
        {
            db.HrSkills.Add(new HrSkill
            {
                CompanyId = company.Id,
                Name = skill.Name,
                Category = skill.Category,
                RequiresCertification = skill.Certified,
            });
        }

        /* ---------------- expense types ---------------- */

        foreach (var type in seedExpenseTypes ? ExpenseTypes : [])
        {
            db.HrExpenseClaimTypes.Add(new HrExpenseClaimType
            {
                CompanyId = company.Id,
                Name = type.Name,
                Description = type.Description,
                PerClaimLimit = type.PerClaim,
                MonthlyLimit = type.Monthly,
                RequiresReceipt = type.Receipt,
                ReceiptWaivedBelow = type.WaivedBelow,
                RequiresTravelRequest = type.NeedsTravel,
            });
        }

        await db.SaveChangesAsync();

        /* ---------------- a starting skill map ---------------- */
        //
        // Enough for the roster to be answerable on day one. Assessed from the
        // designation, which is a defensible guess and flagged as unassessed —
        // nobody has actually watched these people work.

        // Only when nobody has a skill yet. A company that has assessed its
        // people must never have those assessments overwritten by a guess made
        // from a job title.
        if (await db.HrEmployeeSkills.IgnoreQueryFilters().AnyAsync()) return;

        var skills = await db.HrSkills.ToDictionaryAsync(s => s.Name, s => s.Id);
        var employees = await db.HrEmployees
            .Include(e => e.Designation)
            .Where(e => EmploymentStatuses.OnRolls.Contains(e.Status))
            .ToListAsync();

        void Give(HrEmployee employee, string skillName, int proficiency, int years)
        {
            if (!skills.TryGetValue(skillName, out var skillId)) return;
            db.HrEmployeeSkills.Add(new HrEmployeeSkill
            {
                CompanyId = company.Id,
                EmployeeId = employee.Id,
                SkillId = skillId,
                Proficiency = proficiency,
                YearsOfExperience = years,
                Notes = "From the designation. Not yet assessed.",
            });
        }

        foreach (var employee in employees)
        {
            var title = employee.Designation?.Name ?? string.Empty;
            var years = Math.Clamp(
                (int)((DateTime.UtcNow - employee.JoiningDate).TotalDays / 365), 0, 30);

            if (title.Contains("Carpenter", StringComparison.OrdinalIgnoreCase))
            {
                Give(employee, "Carpentry and fabrication", 4, years);
                Give(employee, "Stage and set building", 3, years);
            }
            else if (title.Contains("Electrician", StringComparison.OrdinalIgnoreCase))
            {
                Give(employee, "Sound and AV", 2, years);
            }
            else if (title.Contains("Lighting", StringComparison.OrdinalIgnoreCase))
            {
                Give(employee, "Lighting design", 4, years);
            }
            else if (title.Contains("Rigger", StringComparison.OrdinalIgnoreCase))
            {
                Give(employee, "Stage and set building", 3, years);
            }
            else if (title.Contains("Driver", StringComparison.OrdinalIgnoreCase))
            {
                // Nothing here — the licence is a certified skill and inventing
                // a certificate number would be worse than leaving it blank.
            }
            else if (title.Contains("Floral", StringComparison.OrdinalIgnoreCase))
            {
                Give(employee, "Floral arrangement", 4, years);
                Give(employee, "Draping and fabric work", 3, years);
            }
            else if (title.Contains("Designer", StringComparison.OrdinalIgnoreCase))
            {
                Give(employee, "3D visualisation", 3, years);
                Give(employee, "AutoCAD", 3, years);
                Give(employee, "Design costing", 3, years);
            }
            else if (title.Contains("Visualiser", StringComparison.OrdinalIgnoreCase))
            {
                Give(employee, "3D visualisation", 4, years);
            }
            else if (title.Contains("Supervisor", StringComparison.OrdinalIgnoreCase)
                || title.Contains("Production Manager", StringComparison.OrdinalIgnoreCase))
            {
                Give(employee, "Crew supervision", 4, years);
                Give(employee, "Event coordination", 3, years);
            }
            else if (title.Contains("Planner", StringComparison.OrdinalIgnoreCase)
                || title.Contains("Client Servicing", StringComparison.OrdinalIgnoreCase))
            {
                Give(employee, "Client handling", 4, years);
                Give(employee, "Event coordination", 4, years);
                Give(employee, "Vendor negotiation", 3, years);
            }
        }

        await db.SaveChangesAsync();
    }
}
