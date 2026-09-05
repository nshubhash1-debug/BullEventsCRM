using BullEvents.Api.Data;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Services;

/// <summary>One component, worked out for one employee for one month.</summary>
public record ResolvedComponent(
    int? ComponentId,
    string Name,
    string Abbreviation,
    string ComponentType,
    decimal Amount,
    bool AffectsPf,
    bool AffectsEsi,
    bool IsTaxable,
    bool IsHra,
    bool DependsOnPaymentDays,
    int SortOrder);

/// <summary>
/// What an employee's pay is made of this month, and the totals the statutory
/// engine needs.
///
/// The four totals are the bridge to <see cref="PayrollEngine"/>, which knows
/// about ceilings and slabs but not about components. <see cref="PfWageBase"/>
/// is the sum of the components flagged as counting towards the provident fund
/// wage — not "basic", because a company may pay a dearness allowance that also
/// counts, and calling it basic would have been a guess.
/// </summary>
public record ResolvedSalary(
    IReadOnlyList<ResolvedComponent> Components,
    decimal PfWageBase,
    decimal HraAmount,
    decimal OtherEarnings,
    decimal NonStatutoryDeductions,
    /// <summary>
    /// The variable part of this month's pay, kept apart so the payslip's own
    /// incentive column keeps meaning what it always did.
    /// </summary>
    decimal IncentiveAmount,
    decimal OvertimeRate,
    /// <summary>Set when this came from the older Basic/HRA/Allowances structure.</summary>
    bool FromLegacyStructure);

/// <summary>
/// Turns a pay structure into amounts.
///
/// Components are computed in dependency order, so a house rent allowance
/// written as <c>BASIC * 0.4</c> sees a basic that has already been worked out.
/// A formula that refers to something not yet computed reads zero rather than
/// failing, and a cycle is broken rather than hung — a payroll run must
/// produce a payslip even when somebody has written a silly formula, and the
/// wrong number is easier to notice than a run that will not start.
/// </summary>
public class SalaryStructureResolver(AppDbContext db)
{
    /// <summary>
    /// Resolve one employee's pay for a month.
    ///
    /// Falls back to the older Basic/HRA/Allowances structure when the employee
    /// has no component-based assignment, so the two can coexist while a company
    /// moves across. A tenant that never sets up components keeps working
    /// exactly as it did.
    /// </summary>
    public async Task<ResolvedSalary?> ResolveAsync(
        int employeeId, DateOnly periodEnd, CancellationToken ct = default)
    {
        var assignment = await db.HrPayStructureAssignments.AsNoTracking()
            .Where(a => a.EmployeeId == employeeId && a.EffectiveFrom <= periodEnd)
            .OrderByDescending(a => a.EffectiveFrom).ThenByDescending(a => a.Id)
            .FirstOrDefaultAsync(ct);

        if (assignment is null) return await LegacyAsync(employeeId, periodEnd, ct);

        var structure = await db.HrPayStructures.AsNoTracking()
            .Include(s => s.Lines).ThenInclude(l => l.SalaryComponent)
            .FirstOrDefaultAsync(s => s.Id == assignment.PayStructureId, ct);

        if (structure is null) return await LegacyAsync(employeeId, periodEnd, ct);

        // Statutory components carry no amount of their own — the engine fills
        // them in later — so they are dropped here rather than resolved to zero
        // and then overwritten.
        var lines = structure.Lines
            .Where(l => l.SalaryComponent is { IsActive: true, IsStatutory: false })
            .OrderBy(l => l.SortOrder).ThenBy(l => l.SalaryComponent!.SortOrder)
            .ToList();

        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["BASE"] = assignment.Base,
        };

        var resolved = new List<ResolvedComponent>();

        foreach (var line in Order(lines))
        {
            var component = line.SalaryComponent!;
            var formula = line.Formula ?? component.Formula;

            var amount = component.Calculation switch
            {
                SalaryCalculations.Fixed => line.Amount,
                SalaryCalculations.PercentOfBase => assignment.Base
                    * (Percent(formula, line.Amount) / 100m),
                SalaryCalculations.Formula => Safely(formula, values),
                _ => line.Amount,
            };

            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);

            values[component.Abbreviation] = amount;
            resolved.Add(new ResolvedComponent(
                component.Id, component.Name, component.Abbreviation, component.ComponentType,
                amount, component.AffectsPf, component.AffectsEsi, component.IsTaxable,
                component.IsHra, component.DependsOnPaymentDays,
                line.SortOrder * 1000 + component.SortOrder));
        }

        var earnings = resolved.Where(c => c.ComponentType == SalaryComponentTypes.Earning).ToList();

        return new ResolvedSalary(
            resolved,
            PfWageBase: earnings.Where(c => c.AffectsPf).Sum(c => c.Amount),
            HraAmount: earnings.Where(c => c.IsHra).Sum(c => c.Amount),
            OtherEarnings: earnings.Where(c => !c.AffectsPf && !c.IsHra).Sum(c => c.Amount),
            NonStatutoryDeductions: resolved
                .Where(c => c.ComponentType == SalaryComponentTypes.Deduction)
                .Sum(c => c.Amount),
            // On a component structure the variable part arrives as an
            // additional salary rather than as a line on the structure, so
            // there is nothing to report here.
            IncentiveAmount: 0m,
            OvertimeRate: structure.OvertimeRate,
            FromLegacyStructure: false);
    }

    /// <summary>
    /// The older five-column structure, presented as components.
    ///
    /// So that a payslip looks the same whichever a company is on, and so the
    /// rest of the payroll code has one shape to handle rather than two.
    /// </summary>
    private async Task<ResolvedSalary?> LegacyAsync(
        int employeeId, DateOnly periodEnd, CancellationToken ct)
    {
        var structure = await db.HrSalaryStructures.AsNoTracking()
            .Where(s => s.EmployeeId == employeeId && s.EffectiveFrom <= periodEnd)
            .OrderByDescending(s => s.EffectiveFrom).ThenByDescending(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (structure is null) return null;

        var components = new List<ResolvedComponent>();

        void Add(string name, string abbr, decimal amount, string type, bool pf, bool hra, int order)
        {
            if (amount == 0m) return;
            components.Add(new ResolvedComponent(
                null, name, abbr, type, amount, pf, AffectsEsi: true, IsTaxable: true,
                IsHra: hra, DependsOnPaymentDays: true, order));
        }

        Add("Basic", "BASIC", structure.Basic, SalaryComponentTypes.Earning,
            pf: true, hra: false, 10);
        Add("House rent allowance", "HRA", structure.Hra, SalaryComponentTypes.Earning,
            pf: false, hra: true, 20);
        Add("Other allowances", "ALLOW", structure.Allowances, SalaryComponentTypes.Earning,
            pf: false, hra: false, 30);
        Add("Incentive", "INC", structure.Incentive, SalaryComponentTypes.Earning,
            pf: false, hra: false, 40);
        Add("Bonus", "BONUS", structure.Bonus, SalaryComponentTypes.Earning,
            pf: false, hra: false, 50);
        Add("Other deductions", "OTHDED", structure.Deductions, SalaryComponentTypes.Deduction,
            pf: false, hra: false, 200);

        return new ResolvedSalary(
            components,
            PfWageBase: structure.Basic,
            HraAmount: structure.Hra,
            OtherEarnings: structure.Allowances + structure.Incentive + structure.Bonus,
            NonStatutoryDeductions: structure.Deductions,
            IncentiveAmount: structure.Incentive,
            OvertimeRate: structure.OvertimeRate,
            FromLegacyStructure: true);
    }

    /// <summary>
    /// Sort lines so a formula sees the components it refers to.
    ///
    /// A depth-first walk over the references, with anything already visited
    /// treated as settled — which breaks a cycle by evaluating one side of it
    /// as zero instead of recursing forever.
    /// </summary>
    private static List<HrPayStructureLine> Order(List<HrPayStructureLine> lines)
    {
        var byAbbr = lines
            .Where(l => l.SalaryComponent is not null)
            .GroupBy(l => l.SalaryComponent!.Abbreviation, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        var ordered = new List<HrPayStructureLine>();
        var visited = new HashSet<int>();
        var inProgress = new HashSet<int>();

        void Visit(HrPayStructureLine line)
        {
            if (!visited.Add(line.Id)) return;
            if (!inProgress.Add(line.Id)) return;

            var formula = line.Formula ?? line.SalaryComponent?.Formula;
            foreach (var name in SalaryFormula.ReferencedNames(formula))
            {
                if (byAbbr.TryGetValue(name, out var dependency) && dependency.Id != line.Id)
                {
                    Visit(dependency);
                }
            }

            inProgress.Remove(line.Id);
            ordered.Add(line);
        }

        foreach (var line in lines) Visit(line);
        return ordered;
    }

    /// <summary>
    /// A formula that will not parse yields nothing rather than stopping the run.
    ///
    /// A payroll of two hundred people should not fail because one component on
    /// one structure has a typo in it; a zero on a payslip is noticed, a run
    /// that refuses to start at four o'clock on the last day of the month is a
    /// different kind of problem.
    /// </summary>
    private static decimal Safely(string? formula, IReadOnlyDictionary<string, decimal> values)
    {
        try
        {
            return SalaryFormula.Evaluate(formula ?? string.Empty, values);
        }
        catch (SalaryFormula.FormulaException)
        {
            return 0m;
        }
    }

    /// <summary>A percentage held either in the formula or, failing that, the amount.</summary>
    private static decimal Percent(string? formula, decimal fallback)
    {
        if (string.IsNullOrWhiteSpace(formula)) return fallback;
        return decimal.TryParse(formula, out var parsed) ? parsed : fallback;
    }
}
