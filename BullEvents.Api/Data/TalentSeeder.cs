using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// The scaffolding hiring and appraisals hang off: interview rounds with
/// something to assess, appraisal templates with weighted responsibilities, and
/// a season's staffing plan.
///
/// Refuses to run once a round exists, so a company that has designed its own
/// process is never overwritten. Nothing here creates a requisition, an offer
/// or an appraisal — those are decisions, and inventing them would put words in
/// somebody's mouth.
/// </summary>
public static class TalentSeeder
{
    private record Round(string Name, int Order, decimal Passing, string[] Skills);

    /// <summary>
    /// Three rounds, because an events company hires two very different kinds
    /// of person and the crew round is not the designer round.
    /// </summary>
    private static readonly Round[] Rounds =
    [
        new("Screening", 10, 3m,
            ["Relevant experience", "Communication", "Availability", "Salary fit"]),

        new("Craft — design", 20, 3.5m,
            ["Portfolio quality", "Understanding of a brief", "Material knowledge",
             "Working to a budget", "Software"]),

        new("Craft — site", 20, 3m,
            ["Hands-on skill", "Site safety", "Working at height", "Stamina and reliability",
             "Taking instruction"]),

        new("Client fit", 30, 3.5m,
            ["Presence with a client", "Composure under pressure", "Judgement",
             "Ownership"]),

        new("Final — leadership", 40, 4m,
            ["Track record", "Team building", "Commercial sense", "Culture"]),
    ];

    private record Kra(string Title, decimal Weight, string Description);

    private static readonly (string Name, string Notes, Kra[] Kras)[] Templates =
    [
        ("Design and creative",
         "Designers and visualisers. Weighted towards the work itself.",
         [
             new("Design quality", 30m,
                 "Does the work stand up — on the drawing and on the day."),
             new("Delivering to the brief", 20m,
                 "What the client asked for, not what would have been more fun to make."),
             new("Working to budget", 20m,
                 "Designs that can be built for the money quoted."),
             new("Turnaround", 15m, "Drawings and revisions when they were promised."),
             new("Working with the crew", 15m,
                 "Whether the people building it can read and reach what was drawn."),
         ]),

        ("Site and production",
         "Supervisors, technicians and crew. Weighted towards execution and safety.",
         [
             new("Execution on the day", 30m,
                 "Set up on time, to the drawing, without the client noticing the effort."),
             new("Safety", 25m,
                 "Rigging, power and working at height. Nothing about this is negotiable."),
             new("Care of equipment", 20m,
                 "What comes back to the godown, and in what state."),
             new("Reliability", 15m, "Turning up, on the day, in season."),
             new("Teamwork", 10m, "Whether the rest of the crew wants them on the job."),
         ]),

        ("Client servicing and sales",
         "Planners and account managers. Weighted towards the relationship and the margin.",
         [
             new("Client relationships", 25m,
                 "Repeat business and referrals, not just satisfaction on the day."),
             new("Conversion", 25m, "Enquiries turned into signed events."),
             new("Margin discipline", 20m,
                 "Events that made what the quotation said they would."),
             new("Coordination", 20m,
                 "Whether design, production and the client stayed in step."),
             new("Paperwork", 10m,
                 "Quotations, contracts and collections, on time and correct."),
         ]),

        ("Support functions",
         "Finance, HR, stores and administration.",
         [
             new("Accuracy", 30m, "Work that does not have to be checked twice."),
             new("Timeliness", 25m, "Deadlines that are statutory or contractual."),
             new("Process improvement", 20m,
                 "Whether the job is done better than it was a year ago."),
             new("Service to the business", 15m,
                 "Whether the people who depend on this function can get on with theirs."),
             new("Compliance", 10m, "Nothing filed late, nothing filed wrong."),
         ]),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.HrInterviewRounds.IgnoreQueryFilters().AnyAsync()) return;

        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        /* ---------------- interview rounds ---------------- */

        foreach (var round in Rounds)
        {
            var row = new HrInterviewRound
            {
                CompanyId = company.Id,
                Name = round.Name,
                SortOrder = round.Order,
                PassingScore = round.Passing,
            };

            var order = 0;
            foreach (var skill in round.Skills)
            {
                row.Skills.Add(new HrInterviewSkill
                {
                    CompanyId = company.Id,
                    Name = skill,
                    Weight = 1m,
                    SortOrder = order,
                });
                order += 10;
            }

            db.HrInterviewRounds.Add(row);
        }

        /* ---------------- appraisal templates ---------------- */

        foreach (var (name, notes, kras) in Templates)
        {
            var template = new HrAppraisalTemplate
            {
                CompanyId = company.Id,
                Name = name,
                Notes = notes,
            };

            var order = 0;
            foreach (var kra in kras)
            {
                template.Kras.Add(new HrAppraisalTemplateKra
                {
                    CompanyId = company.Id,
                    Title = kra.Title,
                    Description = kra.Description,
                    Weight = kra.Weight,
                    SortOrder = order,
                });
                order += 10;
            }

            db.HrAppraisalTemplates.Add(template);
        }

        await db.SaveChangesAsync();

        /* ---------------- the season's plan ---------------- */

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var startYear = today.Month >= 4 ? today.Year : today.Year - 1;

        var departments = await db.HrDepartments.ToDictionaryAsync(d => d.Code, d => d.Id);
        int? Dept(string code) => departments.TryGetValue(code, out var id) ? id : null;

        var plan = new HrStaffingPlan
        {
            CompanyId = company.Id,
            Name = $"Season {startYear}-{(startYear + 1) % 100:D2}",
            FromDate = new DateOnly(startYear, 4, 1),
            ToDate = new DateOnly(startYear + 1, 3, 31),
            Status = "Approved",
            Notes = "Headcount agreed for the wedding season. Replacements do not "
                + "draw against it — the post was already budgeted.",
        };

        void Line(string code, string position, int headcount, decimal budget)
        {
            plan.Lines.Add(new HrStaffingPlanLine
            {
                CompanyId = company.Id,
                DepartmentId = Dept(code),
                Position = position,
                Headcount = headcount,
                BudgetPerHead = budget,
            });
        }

        // Weighted to the crew, because that is what a season actually needs.
        Line("PRD", "Helper", 12, 156_000m);
        Line("PRD", "Carpenter", 4, 216_000m);
        Line("PRD", "Electrician", 2, 235_000m);
        Line("PRD", "Site Supervisor", 2, 312_000m);
        Line("PRD", "Driver", 2, 216_000m);
        Line("OPS", "Décor Designer", 2, 564_000m);
        Line("OPS", "Floral Designer", 1, 360_000m);
        Line("SAL", "Wedding Planner", 2, 636_000m);
        Line("SAL", "Client Servicing Executive", 2, 432_000m);
        Line("ACC", "Accounts Assistant", 1, 306_000m);

        db.HrStaffingPlans.Add(plan);
        await db.SaveChangesAsync();
    }
}
