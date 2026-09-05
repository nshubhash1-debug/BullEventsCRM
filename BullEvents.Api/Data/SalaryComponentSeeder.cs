using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Data;

/// <summary>
/// The components an Indian payroll is built out of, and two structures made
/// of them.
///
/// Refuses to run once a component exists, so a company that has designed its
/// own pay never has it rewritten. Nobody is assigned to a structure here —
/// that would silently change what people are paid. The existing
/// Basic/HRA/Allowances records keep running until somebody moves an employee
/// across deliberately.
///
/// The flags are the part worth reading. Whether an allowance counts towards
/// the provident fund wage decides an employer's largest recurring liability,
/// and the position on special allowances has moved with the case law — these
/// are a defensible starting point, not a ruling.
/// </summary>
public static class SalaryComponentSeeder
{
    private record Spec(
        string Name, string Abbr, string Type, string Calculation, string? Formula,
        bool Pf, bool Esi, bool Taxable, bool Hra, bool ProRated, bool Statutory, int Order);

    private static readonly Spec[] Components =
    [
        /* ---------------- earnings ---------------- */

        new("Basic", "BASIC", SalaryComponentTypes.Earning,
            SalaryCalculations.PercentOfBase, "50",
            Pf: true, Esi: true, Taxable: true, Hra: false, ProRated: true,
            Statutory: false, 10),

        new("Dearness allowance", "DA", SalaryComponentTypes.Earning,
            SalaryCalculations.Fixed, null,
            // Dearness allowance is part of the provident fund wage by statute,
            // not by choice — this one is not a setting anybody should change.
            Pf: true, Esi: true, Taxable: true, Hra: false, ProRated: true,
            Statutory: false, 20),

        new("House rent allowance", "HRA", SalaryComponentTypes.Earning,
            SalaryCalculations.Formula, "BASIC * 0.4",
            Pf: false, Esi: true, Taxable: true, Hra: true, ProRated: true,
            Statutory: false, 30),

        new("Conveyance allowance", "CONV", SalaryComponentTypes.Earning,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: true, Taxable: true, Hra: false, ProRated: true,
            Statutory: false, 40),

        new("Special allowance", "SPL", SalaryComponentTypes.Earning,
            // The balancing figure: whatever the base has left after everything
            // else has taken its share. Written as a formula so a raise to the
            // base flows through without anybody recomputing it by hand.
            SalaryCalculations.Formula, "max(0, BASE - BASIC - DA - HRA - CONV)",
            Pf: false, Esi: true, Taxable: true, Hra: false, ProRated: true,
            Statutory: false, 50),

        new("Site allowance", "SITE", SalaryComponentTypes.Earning,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: true, Taxable: true, Hra: false, ProRated: true,
            Statutory: false, 60),

        new("Night shift allowance", "NIGHT", SalaryComponentTypes.Earning,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: true, Taxable: true, Hra: false, ProRated: true,
            Statutory: false, 70),

        new("Performance bonus", "PBONUS", SalaryComponentTypes.Earning,
            SalaryCalculations.Fixed, null,
            // A bonus is earned by the month's work as a whole; a day of unpaid
            // leave does not reduce it.
            Pf: false, Esi: true, Taxable: true, Hra: false, ProRated: false,
            Statutory: false, 80),

        new("Arrears", "ARREARS", SalaryComponentTypes.Earning,
            SalaryCalculations.Fixed, null,
            Pf: true, Esi: true, Taxable: true, Hra: false, ProRated: false,
            Statutory: false, 85),

        new("Reimbursement", "REIMB", SalaryComponentTypes.Earning,
            SalaryCalculations.Fixed, null,
            // Money being returned, not earned: not pay, not taxable, and not
            // reduced by loss of pay.
            Pf: false, Esi: false, Taxable: false, Hra: false, ProRated: false,
            Statutory: false, 90),

        /* ---------------- deductions the company applies ---------------- */

        new("Canteen", "CANTEEN", SalaryComponentTypes.Deduction,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: false, Taxable: false, Hra: false, ProRated: false,
            Statutory: false, 100),

        new("Salary advance recovery", "ADVREC", SalaryComponentTypes.Deduction,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: false, Taxable: false, Hra: false, ProRated: false,
            Statutory: false, 110),

        new("Damage or loss recovery", "DAMAGE", SalaryComponentTypes.Deduction,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: false, Taxable: false, Hra: false, ProRated: false,
            Statutory: false, 120),

        /* ---------------- statutory, computed by the engine ---------------- */

        new("Provident fund", "PF", SalaryComponentTypes.Deduction,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: false, Taxable: false, Hra: false, ProRated: false,
            Statutory: true, 200),
        new("ESI", "ESI", SalaryComponentTypes.Deduction,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: false, Taxable: false, Hra: false, ProRated: false,
            Statutory: true, 210),
        new("Professional tax", "PT", SalaryComponentTypes.Deduction,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: false, Taxable: false, Hra: false, ProRated: false,
            Statutory: true, 220),
        new("Income tax", "TDS", SalaryComponentTypes.Deduction,
            SalaryCalculations.Fixed, null,
            Pf: false, Esi: false, Taxable: false, Hra: false, ProRated: false,
            Statutory: true, 230),
    ];

    public static async Task SeedAsync(AppDbContext db)
    {
        if (await db.HrSalaryComponents.IgnoreQueryFilters().AnyAsync()) return;

        var company = await db.Companies.OrderBy(c => c.Id).FirstOrDefaultAsync();
        if (company is null) return;

        foreach (var spec in Components)
        {
            db.HrSalaryComponents.Add(new HrSalaryComponent
            {
                CompanyId = company.Id,
                Name = spec.Name,
                Abbreviation = spec.Abbr,
                ComponentType = spec.Type,
                Calculation = spec.Calculation,
                Formula = spec.Formula,
                AffectsPf = spec.Pf,
                AffectsEsi = spec.Esi,
                IsTaxable = spec.Taxable,
                IsHra = spec.Hra,
                DependsOnPaymentDays = spec.ProRated,
                IsStatutory = spec.Statutory,
                SortOrder = spec.Order,
            });
        }

        await db.SaveChangesAsync();

        var byAbbr = await db.HrSalaryComponents.ToDictionaryAsync(c => c.Abbreviation, c => c.Id);

        /* ---------------- two structures ---------------- */

        var office = new HrPayStructure
        {
            CompanyId = company.Id,
            Name = "Office — monthly gross",
            Notes = "BASE is the monthly gross. Basic is half of it, house rent 40% of "
                + "basic, and special allowance takes up the slack.",
        };

        var crew = new HrPayStructure
        {
            CompanyId = company.Id,
            Name = "Site crew — monthly gross",
            Notes = "BASE is the monthly gross. A higher basic, because the crew's "
                + "provident fund and gratuity are the point of it, plus a site "
                + "allowance. Overtime is paid at the hourly rate below.",
            OvertimeRate = 95m,
        };

        db.HrPayStructures.AddRange(office, crew);
        await db.SaveChangesAsync();

        void Line(HrPayStructure structure, string abbr, decimal amount, string? formula, int order)
        {
            if (!byAbbr.TryGetValue(abbr, out var componentId)) return;
            db.HrPayStructureLines.Add(new HrPayStructureLine
            {
                CompanyId = company.Id,
                PayStructureId = structure.Id,
                SalaryComponentId = componentId,
                Amount = amount,
                Formula = formula,
                SortOrder = order,
            });
        }

        Line(office, "BASIC", 0m, "50", 10);
        Line(office, "HRA", 0m, "BASIC * 0.4", 20);
        Line(office, "CONV", 1_600m, null, 30);
        Line(office, "SPL", 0m, "max(0, BASE - BASIC - HRA - CONV)", 40);

        Line(crew, "BASIC", 0m, "60", 10);
        Line(crew, "HRA", 0m, "BASIC * 0.4", 20);
        Line(crew, "SITE", 1_000m, null, 30);
        Line(crew, "SPL", 0m, "max(0, BASE - BASIC - HRA - SITE)", 40);

        await db.SaveChangesAsync();
    }
}
