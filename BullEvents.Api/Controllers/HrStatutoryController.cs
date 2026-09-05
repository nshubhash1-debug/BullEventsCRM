using System.Text;
using BullEvents.Api.Data;
using BullEvents.Api.Dtos;
using BullEvents.Api.Infrastructure;
using BullEvents.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BullEvents.Api.Controllers;

/// <summary>
/// The statutory side of payroll: the rates it runs on, and the returns it
/// produces.
///
/// Separate from <see cref="HrOpsController"/> because these are two different
/// audiences. Payroll operations belong to whoever runs the month; these belong
/// to whoever files with the EPFO, the ESIC, the state and the department — a
/// compliance job with its own deadlines, done from the same numbers but a week
/// later and by somebody else.
///
/// Every register here is derived from payslips that already exist. Nothing on
/// this controller computes a contribution; if a figure is wrong, it is wrong on
/// the slip, and reprocessing the run is the fix.
/// </summary>
[ApiController]
[Route("api/hr/statutory")]
[Authorize]
[RequireModule(Modules.Hr)]
[SecuredBy(SecuredObjects.Employee)]
public class HrStatutoryController(AppDbContext db) : CrmControllerBase(db)
{
    /* ================================================================== *
     * Setup — the rates
     * ================================================================== */

    /// <summary>
    /// The rate set in force, with the slabs that go with it.
    ///
    /// Returns the config effective on or before today, so a company that has
    /// already entered next April's rates still sees this month's.
    /// </summary>
    [HttpGet("config")]
    public async Task<ActionResult<HrStatutoryConfigDto>> Config(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var config = await Db.HrStatutoryConfigs.AsNoTracking()
            .Where(c => c.EffectiveFrom <= today)
            .OrderByDescending(c => c.EffectiveFrom).ThenByDescending(c => c.Id)
            .FirstOrDefaultAsync(ct);

        if (config is null) return Ok(null);

        var financialYear = today.Month >= 4 ? today.Year : today.Year - 1;

        var pt = await Db.HrProfessionalTaxSlabs.AsNoTracking()
            .Where(s => s.IsActive && s.EffectiveFrom <= today)
            .OrderBy(s => s.State).ThenBy(s => s.FromAmount).ThenBy(s => s.Month)
            .ToListAsync(ct);

        var slabs = await Db.HrIncomeTaxSlabs.AsNoTracking()
            .Where(s => s.FinancialYear == financialYear)
            .OrderBy(s => s.Regime).ThenBy(s => s.SortOrder)
            .ToListAsync(ct);

        var regimes = await Db.HrTaxRegimeConfigs.AsNoTracking()
            .Where(r => r.FinancialYear == financialYear)
            .OrderBy(r => r.Regime)
            .ToListAsync(ct);

        return Ok(new HrStatutoryConfigDto(
            config.Id, config.EffectiveFrom, config.Label,
            config.PfEnabled, config.PfEmployeeRate, config.PfEmployerRate,
            config.PfWageCeiling, config.PfRestrictEmployeeToCeiling,
            config.PfRestrictEmployerToCeiling, config.EpsRate, config.EpsWageCeiling,
            config.EdliRate, config.PfAdminRate,
            config.EsiEnabled, config.EsiEmployeeRate, config.EsiEmployerRate,
            config.EsiWageThreshold,
            config.PtEnabled, config.PtDefaultState,
            config.TdsEnabled, config.CessRate,
            config.LwfEnabled, config.LwfEmployeeAmount, config.LwfEmployerAmount,
            config.LwfMonths,
            config.GratuityEnabled, config.GratuityDaysPerYear, config.GratuityMonthDays,
            config.GratuityEligibleYears, config.GratuityCeiling,
            config.Notes,
            financialYear,
            pt.Select(s => new HrPtSlabDto(
                s.Id, s.State, s.FromAmount, s.ToAmount, s.Amount, s.Month, s.Gender,
                s.EffectiveFrom, s.IsActive)).ToList(),
            slabs.Select(s => new HrIncomeTaxSlabDto(
                s.Id, s.FinancialYear, s.Regime, s.FromAmount, s.ToAmount, s.Rate,
                s.SortOrder)).ToList(),
            regimes.Select(r => new HrTaxRegimeDto(
                r.Id, r.FinancialYear, r.Regime, r.StandardDeduction,
                r.RebateIncomeCeiling, r.RebateMaximum, r.AllowsChapterViaDeductions,
                r.AllowsHraExemption, r.SurchargeBands)).ToList()));
    }

    /// <summary>
    /// Revise the rates.
    ///
    /// Writes a new effective-dated row rather than editing the old one, so a
    /// run reprocessed for an earlier month still computes on the rates that
    /// applied then. Editing in place would silently rewrite history.
    /// </summary>
    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("config")]
    public async Task<ActionResult<int>> SaveConfig(
        [FromBody] HrStatutoryConfigInput input, CancellationToken ct)
    {
        if (input.PfEmployeeRate is < 0m or > 1m || input.PfEmployerRate is < 0m or > 1m)
            throw ApiException.BadRequest("Contribution rates are fractions — 0.12 for 12%.");
        if (input.EsiEmployeeRate is < 0m or > 1m || input.EsiEmployerRate is < 0m or > 1m)
            throw ApiException.BadRequest("Contribution rates are fractions — 0.0075 for 0.75%.");

        var effective = input.EffectiveFrom ?? DateOnly.FromDateTime(DateTime.UtcNow);

        // A second revision on the same date replaces the first — otherwise a
        // typo corrected the same morning would leave two rows and the tie
        // broken by insertion order.
        var existing = await Db.HrStatutoryConfigs
            .FirstOrDefaultAsync(c => c.EffectiveFrom == effective, ct);

        var config = existing ?? new HrStatutoryConfig { CompanyId = Db.Tenant.CompanyId };
        config.EffectiveFrom = effective;
        config.Label = input.Label?.Trim() is { Length: > 0 } label
            ? label
            : $"Rates from {effective:d MMM yyyy}";
        config.PfEnabled = input.PfEnabled;
        config.PfEmployeeRate = input.PfEmployeeRate;
        config.PfEmployerRate = input.PfEmployerRate;
        config.PfWageCeiling = input.PfWageCeiling;
        config.PfRestrictEmployeeToCeiling = input.PfRestrictEmployeeToCeiling;
        config.PfRestrictEmployerToCeiling = input.PfRestrictEmployerToCeiling;
        config.EpsRate = input.EpsRate;
        config.EpsWageCeiling = input.EpsWageCeiling;
        config.EdliRate = input.EdliRate;
        config.PfAdminRate = input.PfAdminRate;
        config.EsiEnabled = input.EsiEnabled;
        config.EsiEmployeeRate = input.EsiEmployeeRate;
        config.EsiEmployerRate = input.EsiEmployerRate;
        config.EsiWageThreshold = input.EsiWageThreshold;
        config.PtEnabled = input.PtEnabled;
        config.PtDefaultState = input.PtDefaultState;
        config.TdsEnabled = input.TdsEnabled;
        config.CessRate = input.CessRate;
        config.LwfEnabled = input.LwfEnabled;
        config.LwfEmployeeAmount = input.LwfEmployeeAmount;
        config.LwfEmployerAmount = input.LwfEmployerAmount;
        config.LwfMonths = input.LwfMonths;
        config.GratuityEnabled = input.GratuityEnabled;
        config.GratuityDaysPerYear = input.GratuityDaysPerYear;
        config.GratuityMonthDays = input.GratuityMonthDays;
        config.GratuityEligibleYears = input.GratuityEligibleYears;
        config.GratuityCeiling = input.GratuityCeiling;
        config.Notes = input.Notes;

        if (existing is null) Db.HrStatutoryConfigs.Add(config);
        await Db.SaveChangesAsync(ct);
        return Ok(config.Id);
    }

    /* ================================================================== *
     * Setup — one employee's tax position
     * ================================================================== */

    [HttpGet("tax-profiles")]
    public async Task<ActionResult<IReadOnlyList<HrTaxProfileDto>>> TaxProfiles(
        [FromQuery] int? financialYear, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fy = financialYear ?? (today.Month >= 4 ? today.Year : today.Year - 1);

        var rows = await Db.HrEmployeeTaxProfiles.AsNoTracking()
            .Include(p => p.Employee)
            .Where(p => p.FinancialYear == fy)
            .OrderBy(p => p.Employee!.Name).ThenBy(p => p.Id)
            .ToListAsync(ct);

        return Ok(rows.Select(ToProfile).ToList());
    }

    [PermissionAction(ObjectAction.Edit)]
    [HttpPost("tax-profiles")]
    public async Task<ActionResult<HrTaxProfileDto>> SaveTaxProfile(
        [FromBody] HrTaxProfileInput input, CancellationToken ct)
    {
        if (input.Regime != TaxRegimes.New && input.Regime != TaxRegimes.Old)
            throw ApiException.BadRequest("Regime must be New or Old.");

        var employee = await Db.HrEmployees.FirstOrDefaultAsync(e => e.Id == input.EmployeeId, ct)
            ?? throw ApiException.NotFound("Employee");

        var profile = await Db.HrEmployeeTaxProfiles
            .FirstOrDefaultAsync(p => p.EmployeeId == input.EmployeeId
                && p.FinancialYear == input.FinancialYear, ct);

        if (profile is null)
        {
            profile = new HrEmployeeTaxProfile
            {
                CompanyId = Db.Tenant.CompanyId,
                EmployeeId = employee.Id,
                FinancialYear = input.FinancialYear,
            };
            Db.HrEmployeeTaxProfiles.Add(profile);
        }

        profile.Regime = input.Regime;
        profile.AnnualRentPaid = input.AnnualRentPaid;
        profile.RentsInMetro = input.RentsInMetro;
        profile.LandlordPan = input.LandlordPan;
        profile.Section80C = input.Section80C;
        profile.Section80Ccd1B = input.Section80Ccd1B;
        profile.Section80D = input.Section80D;
        profile.HousingLoanInterest = input.HousingLoanInterest;
        profile.Section80Tta = input.Section80Tta;
        profile.OtherDeductions = input.OtherDeductions;
        profile.OtherIncome = input.OtherIncome;
        profile.PreviousEmployerSalary = input.PreviousEmployerSalary;
        profile.PreviousEmployerTds = input.PreviousEmployerTds;
        profile.ProofsSubmitted = input.ProofsSubmitted;
        profile.ProofsSubmittedOn = input.ProofsSubmitted
            ? profile.ProofsSubmittedOn ?? DateOnly.FromDateTime(DateTime.UtcNow)
            : null;
        profile.Notes = input.Notes;

        await Db.SaveChangesAsync(ct);
        await Db.Entry(profile).Reference(p => p.Employee).LoadAsync(ct);
        return Ok(ToProfile(profile));
    }

    private static HrTaxProfileDto ToProfile(HrEmployeeTaxProfile p) => new(
        p.Id, p.EmployeeId, p.Employee?.Name ?? "—", p.FinancialYear, p.Regime,
        p.AnnualRentPaid, p.RentsInMetro, p.LandlordPan,
        p.Section80C, p.Section80Ccd1B, p.Section80D, p.HousingLoanInterest,
        p.Section80Tta, p.OtherDeductions, p.TotalChapterVia,
        p.OtherIncome, p.PreviousEmployerSalary, p.PreviousEmployerTds,
        p.ProofsSubmitted, p.ProofsSubmittedOn, p.Notes);

    /* ================================================================== *
     * Returns — what has to be remitted, and to whom
     * ================================================================== */

    /// <summary>
    /// The month's remittances, totalled by authority.
    ///
    /// This is the page a compliance clerk works from: four challans, four
    /// deadlines, and the headcount behind each. The professional tax breaks
    /// out by state because it is filed state by state.
    /// </summary>
    [HttpGet("runs/{runId:int}/summary")]
    public async Task<ActionResult<HrStatutorySummaryDto>> Summary(int runId, CancellationToken ct)
    {
        var run = await Db.HrPayrollRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw ApiException.NotFound("Payroll run");

        var slips = await Db.HrPayslips.AsNoTracking()
            .Include(s => s.Employee)
            .Where(s => s.PayrollRunId == runId)
            .ToListAsync(ct);

        var defaultState = await DefaultPtStateAsync(ct);

        var pt = slips
            .Where(s => s.ProfessionalTax > 0m)
            .GroupBy(s => s.Employee?.Location is { Length: > 0 } loc
                && PtStates.All.Contains(loc) ? loc : defaultState ?? "—")
            .Select(g => new HrPtStateTotalDto(g.Key, g.Count(), g.Sum(s => s.ProfessionalTax)))
            .OrderByDescending(x => x.Amount)
            .ToList();

        // The 15th of the following month for PF and ESI; the deadline the
        // clerk is actually working against, so it is stated rather than left
        // to be remembered.
        var due = new DateOnly(run.Year, run.Month, 1).AddMonths(1).AddDays(14);

        return Ok(new HrStatutorySummaryDto(
            run.Id, run.Year, run.Month, run.Status, slips.Count,
            due,

            PfMembers: slips.Count(s => s.PfEmployee > 0m || s.PfEmployer > 0m),
            PfEmployee: slips.Sum(s => s.PfEmployee),
            Eps: slips.Sum(s => s.EpsEmployer),
            Epf: slips.Sum(s => s.EpfEmployer),
            Edli: slips.Sum(s => s.Edli),
            PfAdmin: slips.Sum(s => s.PfAdminCharges),
            PfTotal: slips.Sum(s => s.PfEmployee + s.PfEmployer + s.Edli + s.PfAdminCharges),

            EsiMembers: slips.Count(s => s.EsicEmployee > 0m || s.EsicEmployer > 0m),
            EsiEmployee: slips.Sum(s => s.EsicEmployee),
            EsiEmployer: slips.Sum(s => s.EsicEmployer),
            EsiTotal: slips.Sum(s => s.EsicEmployee + s.EsicEmployer),

            PtTotal: slips.Sum(s => s.ProfessionalTax),
            PtByState: pt,

            LwfTotal: slips.Sum(s => s.LwfEmployee + s.LwfEmployer),

            TdsMembers: slips.Count(s => s.Tds > 0m),
            TdsTotal: slips.Sum(s => s.Tds),

            GrossTotal: slips.Sum(s => s.Gross),
            NetTotal: slips.Sum(s => s.Net),
            CostToCompany: slips.Sum(s => s.CostToCompany),

            MissingUan: slips.Count(s => s.PfEmployee > 0m
                && string.IsNullOrWhiteSpace(s.Employee!.Uan)),
            MissingEsiIp: slips.Count(s => s.EsicEmployee > 0m
                && string.IsNullOrWhiteSpace(s.Employee!.EsicIp)),
            MissingPan: slips.Count(s => s.Tds > 0m
                && string.IsNullOrWhiteSpace(s.Employee!.Pan))));
    }

    /// <summary>
    /// The EPFO electronic challan-cum-return, in the format the portal takes:
    /// eleven <c>#~#</c>-delimited fields a line, one line a member, no header.
    ///
    /// Everything is whole rupees; the portal rejects paise. Wages round down,
    /// contributions round to nearest, and the two employer shares are made to
    /// reconcile rather than rounded apart.
    /// </summary>
    [HttpGet("runs/{runId:int}/pf-ecr.txt")]
    public async Task<IActionResult> PfEcr(int runId, CancellationToken ct)
    {
        var (run, slips) = await RunWithSlipsAsync(runId, ct);
        var sb = new StringBuilder();

        foreach (var s in slips.Where(x => x.PfEmployee > 0m || x.PfEmployer > 0m))
        {
            var gross = Floor(s.PayableGross);
            var epfWage = Floor(s.PfWage);
            // EPS and EDLI stop at their own ceiling; the wage reported has to
            // be the capped one or the portal recomputes a different figure.
            var epsWage = Math.Min(epfWage, EpsWageCeiling);

            // The pension share rounds on its own and the provident-fund share
            // is then what is left of the employer's contribution — never
            // rounded independently. 1,249.50 and 550.50 each taken to the
            // nearest rupee come to 1,799 against an employer contribution of
            // 1,800, and a challan a rupee short of its own total is one the
            // portal returns.
            var employee = Rupees(s.PfEmployee);
            var employer = Rupees(s.PfEmployer);
            var eps = Math.Min(Rupees(s.EpsEmployer), employer);
            var epf = employer - eps;

            sb.Append(string.Join("#~#",
                Field(s.Employee?.Uan),
                Field(s.Employee?.Name),
                gross.ToString("0"),
                epfWage.ToString("0"),
                epsWage.ToString("0"),
                epsWage.ToString("0"),
                employee.ToString("0"),
                eps.ToString("0"),
                epf.ToString("0"),
                s.LopDays.ToString("0"),
                "0"));
            sb.Append('\n');
        }

        var name = $"ecr-{run.Year}-{run.Month:00}.txt";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/plain", name);
    }

    /// <summary>
    /// The ESIC monthly contribution return, as the portal's upload template:
    /// insurance number, name, days, wages, contribution.
    /// </summary>
    [HttpGet("runs/{runId:int}/esi.csv")]
    public async Task<IActionResult> EsiReturn(int runId, CancellationToken ct)
    {
        var (run, slips) = await RunWithSlipsAsync(runId, ct);
        var daysInMonth = DateTime.DaysInMonth(run.Year, run.Month);

        var sb = new StringBuilder();
        sb.AppendLine("IP Number,IP Name,No of Days,Total Monthly Wages,"
            + "Reason Code for Zero workings days,Last Working Day");

        foreach (var s in slips.Where(x => x.EsicEmployee > 0m || x.EsicEmployer > 0m))
        {
            var paidDays = Math.Max(0m, daysInMonth - s.LopDays);
            sb.AppendLine(string.Join(',',
                Csv(s.Employee?.EsicIp),
                Csv(s.Employee?.Name),
                paidDays.ToString("0"),
                Floor(s.PayableGross).ToString("0"),
                paidDays == 0m ? "2" : "0",
                ""));
        }

        var name = $"esi-{run.Year}-{run.Month:00}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", name);
    }

    /// <summary>
    /// The professional tax register, ordered by state — one filing per state
    /// the company employs in.
    /// </summary>
    [HttpGet("runs/{runId:int}/pt.csv")]
    public async Task<IActionResult> PtRegister(int runId, CancellationToken ct)
    {
        var (run, slips) = await RunWithSlipsAsync(runId, ct);
        var defaultState = await DefaultPtStateAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("State,Employee Code,Name,Payable Gross,Professional Tax");

        var rows = slips
            .Where(s => s.ProfessionalTax > 0m)
            .Select(s => (
                State: s.Employee?.Location is { Length: > 0 } loc
                    && PtStates.All.Contains(loc) ? loc : defaultState ?? "—",
                Slip: s))
            .OrderBy(x => x.State).ThenBy(x => x.Slip.Employee?.Name);

        foreach (var (state, s) in rows)
        {
            sb.AppendLine(string.Join(',',
                Csv(state), Csv(s.Employee?.EmployeeCode), Csv(s.Employee?.Name),
                s.PayableGross.ToString("0.00"), s.ProfessionalTax.ToString("0.00")));
        }

        var name = $"professional-tax-{run.Year}-{run.Month:00}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", name);
    }

    /// <summary>
    /// The salary TDS register behind a quarter's Form 24Q — deductee-wise, with
    /// the projection each month's deduction was taken from.
    /// </summary>
    [HttpGet("runs/{runId:int}/tds.csv")]
    public async Task<IActionResult> TdsRegister(int runId, CancellationToken ct)
    {
        var (run, slips) = await RunWithSlipsAsync(runId, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Employee Code,Name,PAN,Regime,Payable Gross,"
            + "Projected Annual Taxable,Projected Annual Tax,TDS Deducted");

        foreach (var s in slips.Where(x => x.Tds > 0m).OrderBy(x => x.Employee?.Name))
        {
            sb.AppendLine(string.Join(',',
                Csv(s.Employee?.EmployeeCode), Csv(s.Employee?.Name),
                Csv(s.Employee?.Pan), Csv(s.TaxRegime),
                s.PayableGross.ToString("0.00"),
                s.ProjectedAnnualTaxable.ToString("0.00"),
                s.ProjectedAnnualTax.ToString("0.00"),
                s.Tds.ToString("0.00")));
        }

        var name = $"tds-{run.Year}-{run.Month:00}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", name);
    }

    /// <summary>
    /// The full salary register — every slip, every column, one row an employee.
    /// What an auditor asks for and what the accounts entry is passed from.
    /// </summary>
    [HttpGet("runs/{runId:int}/register.csv")]
    public async Task<IActionResult> SalaryRegister(int runId, CancellationToken ct)
    {
        var (run, slips) = await RunWithSlipsAsync(runId, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Code,Name,Gross,LOP Days,LOP Amount,Payable Gross,Incentive,"
            + "Overtime,PF Wage,PF Employee,PF Employer,EPS,EPF,EDLI,PF Admin,"
            + "ESI Employee,ESI Employer,Professional Tax,LWF Employee,LWF Employer,"
            + "TDS,Other Deductions,Total Deductions,Net,Gratuity Accrual,CTC");

        foreach (var s in slips.OrderBy(x => x.Employee?.Name))
        {
            sb.AppendLine(string.Join(',',
                Csv(s.Employee?.EmployeeCode), Csv(s.Employee?.Name),
                M(s.Gross), s.LopDays.ToString("0.##"), M(s.LopAmount), M(s.PayableGross),
                M(s.Incentive), M(s.OvertimeAmount),
                M(s.PfWage), M(s.PfEmployee), M(s.PfEmployer), M(s.EpsEmployer),
                M(s.EpfEmployer), M(s.Edli), M(s.PfAdminCharges),
                M(s.EsicEmployee), M(s.EsicEmployer), M(s.ProfessionalTax),
                M(s.LwfEmployee), M(s.LwfEmployer), M(s.Tds), M(s.OtherDeductions),
                M(s.TotalDeductions), M(s.Net), M(s.GratuityAccrual), M(s.CostToCompany)));
        }

        var name = $"salary-register-{run.Year}-{run.Month:00}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", name);
    }

    /// <summary>
    /// The bank advice for a month — one line per employee, ready to upload.
    ///
    /// Anybody with no bank account or no IFSC is left out rather than written
    /// with blanks: a bank rejects the whole file for one bad row, and finding
    /// out from the bank is worse than finding out here. The count that was
    /// skipped is in the header comment so nobody uploads a short file without
    /// noticing.
    /// </summary>
    [HttpGet("runs/{runId:int}/bank-advice.csv")]
    public async Task<IActionResult> BankAdvice(int runId, CancellationToken ct)
    {
        var (run, slips) = await RunWithSlipsAsync(runId, ct);

        var payable = slips.Where(s => s.Net > 0m).ToList();
        var ready = payable.Where(s =>
            !string.IsNullOrWhiteSpace(s.Employee?.BankAccount)
            && !string.IsNullOrWhiteSpace(s.Employee?.Ifsc)).ToList();
        var missing = payable.Count - ready.Count;

        var sb = new StringBuilder();
        sb.AppendLine($"# Salary for {run.Month:00}/{run.Year}. "
            + $"{ready.Count} payments, {ready.Sum(s => s.Net):0.00} total."
            + (missing > 0
                ? $" {missing} employee(s) left out for want of a bank account or IFSC."
                : ""));
        sb.AppendLine("Beneficiary Name,Account Number,IFSC,Amount,Narration");

        foreach (var slip in ready.OrderBy(s => s.Employee!.Name))
        {
            sb.AppendLine(string.Join(',',
                Csv(slip.Employee!.Name),
                Csv(slip.Employee.BankAccount),
                Csv(slip.Employee.Ifsc),
                slip.Net.ToString("0.00"),
                Csv($"Salary {run.Month:00}/{run.Year}")));
        }

        var name = $"bank-advice-{run.Year}-{run.Month:00}.csv";
        return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", name);
    }

    /// <summary>
    /// Form 16 Part B, for a financial year, per employee.
    ///
    /// Built from the payslips actually issued rather than from the structure,
    /// so it states what was paid and deducted rather than what was supposed to
    /// be. Part A — the TRACES certificate with the challan identifiers — comes
    /// from the department after the quarterly returns are filed and cannot be
    /// produced here; this is the annexure that goes with it.
    /// </summary>
    [HttpGet("form16")]
    public async Task<ActionResult<IReadOnlyList<HrForm16Dto>>> Form16(
        [FromQuery] int? financialYear, [FromQuery] int? employeeId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fy = financialYear ?? (today.Month >= 4 ? today.Year : today.Year - 1);

        // April of the starting year through March of the next.
        var runs = await Db.HrPayrollRuns.AsNoTracking()
            .Where(r => (r.Year == fy && r.Month >= 4) || (r.Year == fy + 1 && r.Month <= 3))
            .Select(r => r.Id)
            .ToListAsync(ct);

        if (runs.Count == 0) return Ok(Array.Empty<HrForm16Dto>());

        var query = Db.HrPayslips.AsNoTracking().Include(s => s.Employee)
            .Where(s => runs.Contains(s.PayrollRunId));
        if (employeeId is int id) query = query.Where(s => s.EmployeeId == id);

        var slips = await query.ToListAsync(ct);

        var profiles = await Db.HrEmployeeTaxProfiles.AsNoTracking()
            .Where(p => p.FinancialYear == fy)
            .ToDictionaryAsync(p => p.EmployeeId, ct);

        var regimes = await Db.HrTaxRegimeConfigs.AsNoTracking()
            .Where(r => r.FinancialYear == fy)
            .ToListAsync(ct);

        var rows = slips
            .Where(s => s.Employee is not null)
            .GroupBy(s => s.EmployeeId)
            .Select(g =>
            {
                var employee = g.First().Employee!;
                var profile = profiles.GetValueOrDefault(g.Key);
                var regime = profile?.Regime
                    ?? g.OrderByDescending(s => s.Id).First().TaxRegime
                    ?? TaxRegimes.New;
                var regimeConfig = regimes.FirstOrDefault(r => r.Regime == regime);

                var gross = g.Sum(s => s.PayableGross);
                var hraExempt = g.Sum(s => s.HraExemption);
                var professionalTax = g.Sum(s => s.ProfessionalTax);
                var standardDeduction = regimeConfig?.StandardDeduction ?? 0m;
                var chapterVia = regimeConfig?.AllowsChapterViaDeductions == true
                    ? profile?.TotalChapterVia ?? 0m
                    : 0m;
                var housingInterest = regimeConfig?.AllowsChapterViaDeductions == true
                    ? Math.Min(profile?.HousingLoanInterest ?? 0m, 200_000m)
                    : 0m;

                var taxable = Math.Max(0m,
                    gross - hraExempt - standardDeduction - professionalTax
                        - chapterVia - housingInterest);

                return new HrForm16Dto(
                    employee.Id, employee.Name, employee.EmployeeCode, employee.Pan,
                    fy, regime,
                    MonthsPaid: g.Count(),
                    GrossSalary: gross,
                    HraExemption: hraExempt,
                    StandardDeduction: standardDeduction,
                    ProfessionalTax: professionalTax,
                    ChapterViaDeductions: chapterVia,
                    HousingLoanInterest: housingInterest,
                    TaxableIncome: taxable,
                    TaxDeducted: g.Sum(s => s.Tds),
                    ProvidentFundEmployee: g.Sum(s => s.PfEmployee),
                    // The last month's projection is the best estimate of the
                    // year's liability the system holds.
                    ProjectedAnnualTax: g.OrderByDescending(s => s.Id)
                        .First().ProjectedAnnualTax);
            })
            .OrderBy(r => r.EmployeeName)
            .ToList();

        return Ok(rows);
    }

    /* ================================================================== *
     * Helpers
     * ================================================================== */

    private async Task<(HrPayrollRun Run, List<HrPayslip> Slips)> RunWithSlipsAsync(
        int runId, CancellationToken ct)
    {
        var run = await Db.HrPayrollRuns.AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == runId, ct)
            ?? throw ApiException.NotFound("Payroll run");

        var slips = await Db.HrPayslips.AsNoTracking()
            .Include(s => s.Employee)
            .Where(s => s.PayrollRunId == runId)
            .ToListAsync(ct);

        return (run, slips);
    }

    /// <summary>The state professional tax falls back to when an employee has no location.</summary>
    private async Task<string?> DefaultPtStateAsync(CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        return await Db.HrStatutoryConfigs.AsNoTracking()
            .Where(c => c.EffectiveFrom <= today)
            .OrderByDescending(c => c.EffectiveFrom).ThenByDescending(c => c.Id)
            .Select(c => c.PtDefaultState)
            .FirstOrDefaultAsync(ct);
    }

    /// <summary>
    /// The EPS wage ceiling the ECR reports against.
    ///
    /// A literal, unlike everywhere else, because the ECR's layout is defined
    /// against the statutory ceiling: the file states an EPS wage and the portal
    /// recomputes from it, so it has to be what the EPFO expects rather than
    /// whatever a tenant has configured.
    /// </summary>
    private const decimal EpsWageCeiling = 15_000m;

    /// <summary>Wages round down; a rounding must never report pay nobody had.</summary>
    private static decimal Floor(decimal value) => Math.Floor(value);

    /// <summary>Whole rupees, nearest — how a contribution is actually remitted.</summary>
    private static decimal Rupees(decimal value) =>
        Math.Round(value, 0, MidpointRounding.AwayFromZero);

    private static string M(decimal value) => value.ToString("0.00");

    /// <summary>The ECR is delimited, so a name carrying the delimiter would split a row.</summary>
    private static string Field(string? value) =>
        (value ?? string.Empty).Replace("#~#", " ").Replace('\n', ' ').Trim();

    private static string Csv(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        return value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? $"\"{value.Replace("\"", "\"\"")}\""
            : value;
    }
}
