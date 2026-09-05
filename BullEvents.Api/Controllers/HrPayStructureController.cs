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
/// What a salary is made of, and the one-off money that sits beside it.
///
/// Components, the structures built out of them, who is on which structure,
/// additional pay for a single month, and advances recovered in instalments.
/// The statutory side stays where it was — this controller never computes a
/// contribution, it decides what the engine is given.
/// </summary>
[ApiController]
[Route("api/hr/pay")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrPayStructureController(AppDbContext db, SalaryStructureResolver resolver)
    : CrmControllerBase(db)
{
    /* ================================================================== *
     * Components
     * ================================================================== */

    [HttpGet("components")]
    public async Task<ActionResult<IReadOnlyList<HrSalaryComponentDto>>> Components(
        CancellationToken ct)
    {
        var rows = await Db.HrSalaryComponents.AsNoTracking()
            .OrderBy(c => c.ComponentType).ThenBy(c => c.SortOrder).ThenBy(c => c.Name)
            .ToListAsync(ct);

        var used = await Db.HrPayStructureLines.AsNoTracking()
            .GroupBy(l => l.SalaryComponentId)
            .Select(g => new { ComponentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ComponentId, x => x.Count, ct);

        return Ok(rows.Select(c => new HrSalaryComponentDto(
            c.Id, c.Name, c.Abbreviation, c.ComponentType, c.Calculation, c.Formula,
            c.AffectsPf, c.AffectsEsi, c.IsTaxable, c.IsHra, c.DependsOnPaymentDays,
            c.IsStatutory, c.SortOrder, c.IsActive, c.Notes,
            used.GetValueOrDefault(c.Id))).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("components")]
    public async Task<ActionResult<int>> SaveComponent(
        [FromBody] HrSalaryComponentInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A component needs a name.");

        var abbreviation = (input.Abbreviation ?? string.Empty).Trim().ToUpperInvariant();
        if (abbreviation.Length == 0)
            throw ApiException.BadRequest("A component needs an abbreviation — formulas use it.");
        if (!abbreviation.All(c => char.IsLetterOrDigit(c) || c == '_'))
            throw ApiException.BadRequest(
                "An abbreviation may only be letters, digits and underscores, "
                + "because it has to be usable in a formula.");
        if (string.Equals(abbreviation, "BASE", StringComparison.OrdinalIgnoreCase))
            throw ApiException.BadRequest("BASE is reserved for the assignment's base figure.");

        if (!SalaryComponentTypes.All.Contains(input.ComponentType))
            throw ApiException.BadRequest("A component is either an Earning or a Deduction.");
        if (!SalaryCalculations.All.Contains(input.Calculation))
            throw ApiException.BadRequest("Unknown calculation method.");

        // A formula is checked before it is stored. Finding out at four o'clock
        // on the last day of the month that a bracket was never closed is the
        // situation this avoids.
        if (input.Calculation == SalaryCalculations.Formula)
        {
            var known = await Db.HrSalaryComponents.AsNoTracking()
                .Where(c => c.Id != (input.Id ?? 0))
                .Select(c => c.Abbreviation)
                .ToListAsync(ct);
            known.Add("BASE");
            known.Add(abbreviation);

            var problem = SalaryFormula.Validate(input.Formula, known);
            if (problem is not null) throw ApiException.BadRequest(problem);
        }

        var clash = await Db.HrSalaryComponents
            .FirstOrDefaultAsync(c => c.Abbreviation == abbreviation && c.Id != (input.Id ?? 0), ct);
        if (clash is not null)
            throw ApiException.BadRequest($"{abbreviation} is already used by {clash.Name}.");

        var component = input.Id is int id and > 0
            ? await Db.HrSalaryComponents.FirstOrDefaultAsync(c => c.Id == id, ct)
                ?? throw ApiException.NotFound("Salary component")
            : new HrSalaryComponent { CompanyId = Db.Tenant.CompanyId };

        if (component.IsStatutory && component.Id > 0)
        {
            // The engine owns these. Letting somebody give provident fund a
            // formula would produce a payslip whose PF line disagreed with the
            // challan filed from the same run.
            component.Name = input.Name.Trim();
            component.SortOrder = input.SortOrder;
            component.IsActive = input.IsActive;
            component.Notes = input.Notes;
            await Db.SaveChangesAsync(ct);
            return Ok(component.Id);
        }

        component.Name = input.Name.Trim();
        component.Abbreviation = abbreviation;
        component.ComponentType = input.ComponentType;
        component.Calculation = input.Calculation;
        component.Formula = input.Formula;
        component.AffectsPf = input.AffectsPf;
        component.AffectsEsi = input.AffectsEsi;
        component.IsTaxable = input.IsTaxable;
        component.IsHra = input.IsHra;
        component.DependsOnPaymentDays = input.DependsOnPaymentDays;
        component.SortOrder = input.SortOrder;
        component.IsActive = input.IsActive;
        component.Notes = input.Notes;

        if (component.Id == 0) Db.HrSalaryComponents.Add(component);
        await Db.SaveChangesAsync(ct);
        return Ok(component.Id);
    }

    /// <summary>
    /// Try a formula against a base figure without saving anything.
    ///
    /// The form calls this as the formula is typed, so a mistake is a message
    /// under the field rather than a zero on two hundred payslips.
    /// </summary>
    [HttpPost("components/try-formula")]
    public async Task<ActionResult<HrFormulaTryDto>> TryFormula(
        [FromBody] HrFormulaTryInput input, CancellationToken ct)
    {
        var components = await Db.HrSalaryComponents.AsNoTracking()
            .Where(c => c.IsActive)
            .Select(c => new { c.Abbreviation, c.Calculation, c.Formula })
            .ToListAsync(ct);

        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["BASE"] = input.Base,
        };

        // Sample values so the answer means something: a percentage of base for
        // anything defined that way, and the base itself for anything fixed.
        foreach (var c in components)
        {
            values[c.Abbreviation] = c.Calculation == SalaryCalculations.PercentOfBase
                && decimal.TryParse(c.Formula, out var percent)
                ? Math.Round(input.Base * percent / 100m, 2)
                : 0m;
        }

        if (input.Values is not null)
        {
            foreach (var (abbreviation, amount) in input.Values) values[abbreviation] = amount;
        }

        try
        {
            var result = SalaryFormula.Evaluate(input.Formula ?? string.Empty, values);
            return Ok(new HrFormulaTryDto(
                Math.Round(result, 2, MidpointRounding.AwayFromZero), null,
                SalaryFormula.ReferencedNames(input.Formula).ToList()));
        }
        catch (SalaryFormula.FormulaException e)
        {
            return Ok(new HrFormulaTryDto(0m, e.Message,
                SalaryFormula.ReferencedNames(input.Formula).ToList()));
        }
    }

    /* ================================================================== *
     * Structures
     * ================================================================== */

    [HttpGet("structures")]
    public async Task<ActionResult<IReadOnlyList<HrPayStructureDto>>> Structures(
        CancellationToken ct)
    {
        var rows = await Db.HrPayStructures.AsNoTracking()
            .Include(s => s.Lines).ThenInclude(l => l.SalaryComponent)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var assigned = await Db.HrPayStructureAssignments.AsNoTracking()
            .GroupBy(a => a.PayStructureId)
            .Select(g => new { StructureId = g.Key, Count = g.Select(a => a.EmployeeId).Distinct().Count() })
            .ToDictionaryAsync(x => x.StructureId, x => x.Count, ct);

        return Ok(rows.Select(s => new HrPayStructureDto(
            s.Id, s.Name, s.Notes, s.IsActive, s.OvertimeRate,
            assigned.GetValueOrDefault(s.Id),
            s.Lines.OrderBy(l => l.SortOrder).Select(l => new HrPayStructureLineDto(
                l.Id, l.SalaryComponentId,
                l.SalaryComponent?.Name ?? "—",
                l.SalaryComponent?.Abbreviation ?? "—",
                l.SalaryComponent?.ComponentType ?? SalaryComponentTypes.Earning,
                l.SalaryComponent?.Calculation ?? SalaryCalculations.Fixed,
                l.Amount, l.Formula ?? l.SalaryComponent?.Formula, l.SortOrder)).ToList()))
            .ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("structures")]
    public async Task<ActionResult<int>> SaveStructure(
        [FromBody] HrPayStructureInput input, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
            throw ApiException.BadRequest("A structure needs a name.");
        if (input.Lines.Count == 0)
            throw ApiException.BadRequest("A structure with no components pays nothing.");

        var componentIds = input.Lines.Select(l => l.SalaryComponentId).ToList();
        var components = await Db.HrSalaryComponents.AsNoTracking()
            .Where(c => componentIds.Contains(c.Id))
            .ToListAsync(ct);

        if (components.Count != componentIds.Distinct().Count())
            throw ApiException.BadRequest("One of those components does not exist.");

        var earnings = components.Where(c =>
            c.ComponentType == SalaryComponentTypes.Earning && !c.IsStatutory).ToList();
        if (earnings.Count == 0)
            throw ApiException.BadRequest("A structure needs at least one earning.");
        if (!earnings.Any(c => c.AffectsPf))
        {
            throw ApiException.BadRequest(
                "No earning on this structure counts towards the provident fund wage. "
                + "Mark basic — or whatever plays its part — as affecting PF, or the "
                + "contribution will compute as nothing.");
        }

        var structure = input.Id is int id and > 0
            ? await Db.HrPayStructures.Include(s => s.Lines)
                .FirstOrDefaultAsync(s => s.Id == id, ct)
                ?? throw ApiException.NotFound("Pay structure")
            : new HrPayStructure { CompanyId = Db.Tenant.CompanyId };

        structure.Name = input.Name.Trim();
        structure.Notes = input.Notes;
        structure.IsActive = input.IsActive;
        structure.OvertimeRate = input.OvertimeRate;

        if (structure.Id == 0) Db.HrPayStructures.Add(structure);

        Db.HrPayStructureLines.RemoveRange(structure.Lines);
        structure.Lines.Clear();

        var order = 0;
        foreach (var line in input.Lines)
        {
            structure.Lines.Add(new HrPayStructureLine
            {
                CompanyId = Db.Tenant.CompanyId,
                SalaryComponentId = line.SalaryComponentId,
                Amount = line.Amount,
                Formula = line.Formula,
                SortOrder = line.SortOrder != 0 ? line.SortOrder : order,
            });
            order += 10;
        }

        await Db.SaveChangesAsync(ct);
        return Ok(structure.Id);
    }

    /// <summary>
    /// What a structure comes to on a given base, before anybody is on it.
    ///
    /// The screen that builds a structure calls this so the person writing the
    /// formulas can see the payslip they are producing.
    /// </summary>
    [HttpGet("structures/{id:int}/preview")]
    public async Task<ActionResult<HrStructurePreviewDto>> Preview(
        int id, [FromQuery] decimal @base, CancellationToken ct)
    {
        var structure = await Db.HrPayStructures.AsNoTracking()
            .Include(s => s.Lines).ThenInclude(l => l.SalaryComponent)
            .FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw ApiException.NotFound("Pay structure");

        var values = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["BASE"] = @base,
        };
        var lines = new List<HrStructurePreviewLineDto>();

        foreach (var line in structure.Lines
            .Where(l => l.SalaryComponent is { IsActive: true, IsStatutory: false })
            .OrderBy(l => l.SortOrder))
        {
            var component = line.SalaryComponent!;
            var formula = line.Formula ?? component.Formula;
            string? problem = null;
            var amount = 0m;

            try
            {
                amount = component.Calculation switch
                {
                    SalaryCalculations.Fixed => line.Amount,
                    SalaryCalculations.PercentOfBase => @base
                        * ((decimal.TryParse(formula, out var p) ? p : line.Amount) / 100m),
                    SalaryCalculations.Formula =>
                        SalaryFormula.Evaluate(formula ?? string.Empty, values),
                    _ => line.Amount,
                };
            }
            catch (SalaryFormula.FormulaException e)
            {
                problem = e.Message;
            }

            amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
            values[component.Abbreviation] = amount;

            lines.Add(new HrStructurePreviewLineDto(
                component.Name, component.Abbreviation, component.ComponentType,
                amount, formula, problem));
        }

        var gross = lines
            .Where(l => l.ComponentType == SalaryComponentTypes.Earning).Sum(l => l.Amount);
        var deductions = lines
            .Where(l => l.ComponentType == SalaryComponentTypes.Deduction).Sum(l => l.Amount);

        return Ok(new HrStructurePreviewDto(
            structure.Name, @base, gross, deductions, gross - deductions, lines));
    }

    /* ================================================================== *
     * Assignments
     * ================================================================== */

    [HttpGet("assignments")]
    public async Task<ActionResult<IReadOnlyList<HrPayAssignmentDto>>> Assignments(
        [FromQuery] int? employeeId, CancellationToken ct)
    {
        var query = Db.HrPayStructureAssignments.AsNoTracking()
            .Include(a => a.Employee).Include(a => a.PayStructure).AsQueryable();

        if (employeeId is int id) query = query.Where(a => a.EmployeeId == id);

        var rows = await query
            .OrderByDescending(a => a.EffectiveFrom).ThenBy(a => a.Employee!.Name)
            .Take(500).ToListAsync(ct);

        return Ok(rows.Select(a => new HrPayAssignmentDto(
            a.Id, a.EmployeeId, a.Employee?.Name ?? "—",
            a.PayStructureId, a.PayStructure?.Name ?? "—",
            a.Base, a.EffectiveFrom)).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("assignments")]
    public async Task<ActionResult<HrPayAssignmentDto>> Assign(
        [FromBody] HrPayAssignmentInput input, CancellationToken ct)
    {
        if (input.Base <= 0m)
            throw ApiException.BadRequest("The base figure has to be more than nothing.");

        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");
        var structure = await Db.HrPayStructures
            .FirstOrDefaultAsync(s => s.Id == input.PayStructureId && s.IsActive, ct)
            ?? throw ApiException.BadRequest("Unknown pay structure.");

        // Same employee, same date replaces — a correction made the same
        // morning should not leave two rows with the tie broken by insertion
        // order.
        var existing = await Db.HrPayStructureAssignments.FirstOrDefaultAsync(
            a => a.EmployeeId == employee.Id && a.EffectiveFrom == input.EffectiveFrom, ct);

        var assignment = existing ?? new HrPayStructureAssignment
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = employee.Id,
        };
        assignment.PayStructureId = structure.Id;
        assignment.Base = input.Base;
        assignment.EffectiveFrom = input.EffectiveFrom;

        if (existing is null) Db.HrPayStructureAssignments.Add(assignment);
        await Db.SaveChangesAsync(ct);

        return Ok(new HrPayAssignmentDto(
            assignment.Id, employee.Id, employee.Name,
            structure.Id, structure.Name, assignment.Base, assignment.EffectiveFrom));
    }

    /// <summary>What one employee is actually paid this month, component by component.</summary>
    [HttpGet("employees/{employeeId:int}/breakdown")]
    public async Task<ActionResult<HrPayBreakdownDto>> Breakdown(
        int employeeId, [FromQuery] DateOnly? asOf, CancellationToken ct)
    {
        var employee = await Db.HrEmployees.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == employeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        var date = asOf ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var salary = await resolver.ResolveAsync(employeeId, date, ct);

        if (salary is null)
        {
            return Ok(new HrPayBreakdownDto(
                employee.Id, employee.Name, date, false, 0m, 0m, 0m, 0m, []));
        }

        return Ok(new HrPayBreakdownDto(
            employee.Id, employee.Name, date, salary.FromLegacyStructure,
            salary.PfWageBase + salary.HraAmount + salary.OtherEarnings,
            salary.PfWageBase, salary.HraAmount, salary.NonStatutoryDeductions,
            salary.Components.OrderBy(c => c.SortOrder).Select(c => new HrComponentAmountDto(
                c.Name, c.Abbreviation, c.ComponentType, c.Amount,
                c.AffectsPf, c.IsHra, c.IsTaxable, c.DependsOnPaymentDays)).ToList()));
    }

    /* ================================================================== *
     * Additional salary
     * ================================================================== */

    [HttpGet("additional")]
    public async Task<ActionResult<IReadOnlyList<HrAdditionalSalaryDto>>> Additional(
        [FromQuery] int? year, [FromQuery] int? month, CancellationToken ct)
    {
        var query = Db.HrAdditionalSalaries.AsNoTracking()
            .Include(a => a.Employee).Include(a => a.SalaryComponent).AsQueryable();

        if (year is int y) query = query.Where(a => a.Year == y || a.IsRecurring);
        if (month is int m) query = query.Where(a => a.Month == m || a.IsRecurring);

        var rows = await query.OrderByDescending(a => a.Id).Take(300).ToListAsync(ct);
        return Ok(rows.Select(ToAdditional).ToList());
    }

    [PermissionAction(ObjectAction.Create)]
    [HttpPost("additional")]
    public async Task<ActionResult<HrAdditionalSalaryDto>> AddAdditional(
        [FromBody] HrAdditionalSalaryInput input, CancellationToken ct)
    {
        if (input.Amount <= 0m)
            throw ApiException.BadRequest("An amount of nothing changes nothing.");
        if (input.Month is < 1 or > 12)
            throw ApiException.BadRequest("Month must be 1–12.");

        _ = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");
        var component = await Db.HrSalaryComponents
            .FirstOrDefaultAsync(c => c.Id == input.SalaryComponentId && c.IsActive, ct)
            ?? throw ApiException.BadRequest("Unknown salary component.");

        if (component.IsStatutory)
        {
            throw ApiException.BadRequest(
                $"{component.Name} is computed by the payroll engine and cannot be "
                + "added by hand.");
        }

        if (input.IsRecurring && input.RecurringUntil is null)
        {
            throw ApiException.BadRequest(
                "A recurring addition needs an end date, or it is a salary increase "
                + "pretending to be a one-off.");
        }

        var row = new HrAdditionalSalary
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = input.EmployeeId,
            SalaryComponentId = component.Id,
            Amount = input.Amount,
            Year = input.Year,
            Month = input.Month,
            IsRecurring = input.IsRecurring,
            RecurringUntil = input.RecurringUntil,
            DependsOnPaymentDays = input.DependsOnPaymentDays,
            Reason = input.Reason,
            Status = HrRequestStatuses.Approved,
        };
        Db.HrAdditionalSalaries.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(a => a.Employee).LoadAsync(ct);
        await Db.Entry(row).Reference(a => a.SalaryComponent).LoadAsync(ct);
        return Ok(ToAdditional(row));
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("additional/{id:int}/cancel")]
    public async Task<IActionResult> CancelAdditional(int id, CancellationToken ct)
    {
        var row = await Db.HrAdditionalSalaries.FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Additional salary");

        if (row.PaidInPayrollRunId is not null)
            throw ApiException.BadRequest("That has already been paid. Reverse it with a deduction.");

        row.Status = HrRequestStatuses.Cancelled;
        await Db.SaveChangesAsync(ct);
        return NoContent();
    }

    private static HrAdditionalSalaryDto ToAdditional(HrAdditionalSalary a) => new(
        a.Id, a.EmployeeId, a.Employee?.Name ?? "—",
        a.SalaryComponentId, a.SalaryComponent?.Name ?? "—",
        a.SalaryComponent?.ComponentType ?? SalaryComponentTypes.Earning,
        a.Amount, a.Year, a.Month, a.IsRecurring, a.RecurringUntil,
        a.DependsOnPaymentDays, a.Reason, a.Status, a.PaidInPayrollRunId);

    /* ================================================================== *
     * Advances
     * ================================================================== */

    [HttpGet("advances")]
    public async Task<ActionResult<IReadOnlyList<HrAdvanceDto>>> Advances(
        [FromQuery] string? status, CancellationToken ct)
    {
        var query = Db.HrEmployeeAdvances.AsNoTracking()
            .Include(a => a.Employee).Include(a => a.Repayments).AsQueryable();

        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(a => a.Status == status);

        var rows = await query.OrderByDescending(a => a.Id).Take(300).ToListAsync(ct);
        return Ok(rows.Select(ToAdvance).ToList());
    }

    /// <summary>
    /// Advance money against future salary.
    ///
    /// The instalment is worked out here rather than asked for, so it always
    /// divides into the amount; the last one absorbs the rounding at recovery
    /// time so the total taken back comes to what was advanced.
    /// </summary>
    [PermissionAction(ObjectAction.Create)]
    [HttpPost("advances")]
    public async Task<ActionResult<HrAdvanceDto>> RequestAdvance(
        [FromBody] HrAdvanceInput input, CancellationToken ct)
    {
        if (input.Amount <= 0m)
            throw ApiException.BadRequest("An advance of nothing is not an advance.");
        if (input.Instalments is < 1 or > 36)
            throw ApiException.BadRequest("Recover it over 1 to 36 months.");
        if (input.RecoveryStartMonth is < 1 or > 12)
            throw ApiException.BadRequest("Month must be 1–12.");

        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        // An advance bigger than what is left of the pay after the statutory
        // deductions is one that cannot be recovered without the payslip going
        // negative, so the instalment is checked against the salary now.
        var salary = await resolver.ResolveAsync(
            employee.Id, DateOnly.FromDateTime(DateTime.UtcNow), ct);

        var instalment = Math.Round(input.Amount / input.Instalments, 2,
            MidpointRounding.AwayFromZero);

        if (salary is not null)
        {
            var monthlyGross = salary.PfWageBase + salary.HraAmount + salary.OtherEarnings;
            // Half the gross is the practical ceiling; past that an employee
            // takes home less than the statutory deductions leave them.
            if (monthlyGross > 0m && instalment > monthlyGross * 0.5m)
            {
                throw ApiException.BadRequest(
                    $"An instalment of {instalment:0.00} is more than half of "
                    + $"{employee.Name}'s monthly gross of {monthlyGross:0.00}. "
                    + "Spread it over more months.");
            }
        }

        var row = new HrEmployeeAdvance
        {
            CompanyId = Db.Tenant.CompanyId,
            EmployeeId = employee.Id,
            Amount = input.Amount,
            Instalments = input.Instalments,
            InstalmentAmount = instalment,
            RecoveryStartYear = input.RecoveryStartYear,
            RecoveryStartMonth = input.RecoveryStartMonth,
            Purpose = input.Purpose,
            Status = AdvanceStatuses.Requested,
        };
        Db.HrEmployeeAdvances.Add(row);
        await Db.SaveChangesAsync(ct);

        await Db.Entry(row).Reference(a => a.Employee).LoadAsync(ct);
        return Ok(ToAdvance(row));
    }

    /// <summary>
    /// Move an advance along: approve it, mark it paid, write it off.
    ///
    /// Recovery is not a step here — it happens on the payroll run, which is
    /// the only thing that knows whether a month has actually been paid.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("advances/{id:int}/status")]
    public async Task<ActionResult<HrAdvanceDto>> SetAdvanceStatus(
        int id, [FromQuery] string status, [FromQuery] string? reason, CancellationToken ct)
    {
        if (!AdvanceStatuses.All.Contains(status))
            throw ApiException.BadRequest("Unknown advance status.");

        var row = await Db.HrEmployeeAdvances
            .Include(a => a.Employee).Include(a => a.Repayments)
            .FirstOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiException.NotFound("Advance");

        if (row.Repayments.Count > 0 && status is AdvanceStatuses.Rejected)
        {
            throw ApiException.BadRequest(
                "Instalments have already been recovered against this advance. "
                + "Write it off instead of rejecting it.");
        }

        if (status == AdvanceStatuses.WrittenOff && string.IsNullOrWhiteSpace(reason))
            throw ApiException.BadRequest("Say why it is being written off.");

        row.Status = status;
        if (status == AdvanceStatuses.Paid) row.PaidOn ??= DateOnly.FromDateTime(DateTime.UtcNow);
        if (status == AdvanceStatuses.WrittenOff) row.WriteOffReason = reason;

        await Db.SaveChangesAsync(ct);
        return Ok(ToAdvance(row));
    }

    private static HrAdvanceDto ToAdvance(HrEmployeeAdvance a)
    {
        var recovered = a.Repayments.Sum(r => r.Amount);
        return new HrAdvanceDto(
            a.Id, a.EmployeeId, a.Employee?.Name ?? "—",
            a.Amount, a.Instalments, a.InstalmentAmount,
            a.RecoveryStartYear, a.RecoveryStartMonth,
            a.Purpose, a.Status, a.PaidOn, a.WriteOffReason,
            recovered, Math.Max(0m, a.Amount - recovered),
            a.Repayments.Count);
    }
}
