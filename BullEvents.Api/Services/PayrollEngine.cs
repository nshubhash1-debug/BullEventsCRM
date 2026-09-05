using BullEvents.Api.Models;

namespace BullEvents.Api.Services;

/* ------------------------------------------------------------------ *
 * Indian statutory payroll arithmetic.
 *
 * Pure functions over a rate set. Nothing here reads the database or knows
 * about an HTTP request, which is what makes a payslip reproducible: give it
 * the same wages and the same statutory rows and it returns the same numbers
 * in five years' time, which is the entire point of a payroll a person has
 * already shown their bank.
 * ------------------------------------------------------------------ */

/// <summary>What one employee earned in one month, before any statute is applied.</summary>
public record PayrollInput
{
    /// <summary>Basic plus dearness allowance. The PF and gratuity base.</summary>
    public decimal Basic { get; init; }

    public decimal Hra { get; init; }
    public decimal Allowances { get; init; }
    public decimal Incentive { get; init; }
    public decimal Bonus { get; init; }
    public decimal OvertimeAmount { get; init; }

    /// <summary>Deductions the company applies of its own accord — canteen, advances.</summary>
    public decimal OtherDeductions { get; init; }

    /// <summary>Days not paid for: absences plus unpaid leave.</summary>
    public decimal LopDays { get; init; }

    public int DaysInMonth { get; init; } = 30;

    /// <summary>Which month is being paid. Decides the professional tax row.</summary>
    public int Month { get; init; }
    public int Year { get; init; }

    /// <summary>The employee's work state, for professional tax.</summary>
    public string? PtState { get; init; }

    public string? Gender { get; init; }

    /* ---------------- for the tax projection ---------------- */

    /// <summary>Months left in the financial year, including this one.</summary>
    public int RemainingMonthsInYear { get; init; } = 1;

    /// <summary>Taxable salary already paid this financial year, before this month.</summary>
    public decimal YearToDateTaxableSalary { get; init; }

    /// <summary>Tax already deducted this financial year, before this month.</summary>
    public decimal YearToDateTaxDeducted { get; init; }

    /// <summary>Gross earnings, before anything is taken off.</summary>
    public decimal Gross => Basic + Hra + Allowances + Incentive + Bonus;
}

/// <summary>Everything the statute takes, computed and itemised.</summary>
public record PayrollResult
{
    public decimal Gross { get; init; }
    public decimal LopAmount { get; init; }

    /// <summary>Gross after loss of pay. What the statutory rates actually bite on.</summary>
    public decimal PayableGross { get; init; }

    public decimal PayableBasic { get; init; }

    /* ---------------- provident fund ---------------- */

    public decimal PfWage { get; init; }
    public decimal PfEmployee { get; init; }

    /// <summary>The employer's total, which is EPS plus EPF and nothing else.</summary>
    public decimal PfEmployer { get; init; }

    /// <summary>The pension slice of the employer's share.</summary>
    public decimal EpsEmployer { get; init; }

    /// <summary>The provident-fund slice of the employer's share.</summary>
    public decimal EpfEmployer { get; init; }

    public decimal Edli { get; init; }
    public decimal PfAdminCharges { get; init; }

    /* ---------------- other statutory ---------------- */

    public decimal EsiEmployee { get; init; }
    public decimal EsiEmployer { get; init; }
    public decimal ProfessionalTax { get; init; }
    public decimal LwfEmployee { get; init; }
    public decimal LwfEmployer { get; init; }

    /* ---------------- income tax ---------------- */

    /// <summary>Projected taxable income for the whole year, after exemptions.</summary>
    public decimal ProjectedAnnualTaxable { get; init; }

    /// <summary>Tax on that projection, cess and surcharge included.</summary>
    public decimal ProjectedAnnualTax { get; init; }

    /// <summary>This month's share of it.</summary>
    public decimal Tds { get; init; }

    /// <summary>The HRA actually exempted, for the payslip's own explanation.</summary>
    public decimal HraExemption { get; init; }

    /* ---------------- outcome ---------------- */

    public decimal TotalDeductions { get; init; }
    public decimal Net { get; init; }

    /// <summary>Gratuity earned this month, accrued rather than paid.</summary>
    public decimal GratuityAccrual { get; init; }

    /// <summary>What the employee costs the company, employer contributions included.</summary>
    public decimal CostToCompany { get; init; }
}

/// <summary>The statutory rows a run needs, gathered once and handed in.</summary>
public record PayrollRates
{
    public required HrStatutoryConfig Config { get; init; }
    public IReadOnlyList<HrProfessionalTaxSlab> PtSlabs { get; init; } = [];
    public IReadOnlyList<HrIncomeTaxSlab> TaxSlabs { get; init; } = [];
    public HrTaxRegimeConfig? RegimeConfig { get; init; }
    public HrEmployeeTaxProfile? TaxProfile { get; init; }

    /// <summary>Completed years of service, for the gratuity accrual.</summary>
    public decimal YearsOfService { get; init; }
}

public static class PayrollEngine
{
    /// <summary>
    /// Runs one month for one employee.
    ///
    /// Ordered the way the statute is: loss of pay first, because everything
    /// else is a rate on what is actually payable; then the contributions,
    /// which are deductible before tax; then the tax on what is left.
    /// </summary>
    public static PayrollResult Compute(PayrollInput input, PayrollRates rates)
    {
        var config = rates.Config;

        /* ---------------- loss of pay ---------------- */

        var days = Math.Max(1, input.DaysInMonth);
        var perDay = input.Gross / days;
        var lopAmount = Math.Round(perDay * Math.Max(0, input.LopDays), 2);

        var payableGross = Math.Max(0, input.Gross - lopAmount);

        // Basic is reduced in the same proportion, not by the same amount: PF
        // is a rate on basic, and taking the whole shortfall out of basic would
        // understate the contribution on a month with allowances in it.
        var basicShare = input.Gross == 0 ? 0 : input.Basic / input.Gross;
        var payableBasic = Math.Round(payableGross * basicShare, 2);

        /* ---------------- provident fund ---------------- */

        decimal pfEmployee = 0, pfEmployer = 0, eps = 0, epf = 0, edli = 0, pfAdmin = 0;
        var pfWage = payableBasic;

        if (config.PfEnabled)
        {
            var employeeWage = config.PfRestrictEmployeeToCeiling
                ? Math.Min(payableBasic, config.PfWageCeiling)
                : payableBasic;

            var employerWage = config.PfRestrictEmployerToCeiling
                ? Math.Min(payableBasic, config.PfWageCeiling)
                : payableBasic;

            pfWage = employeeWage;
            pfEmployee = Math.Round(employeeWage * config.PfEmployeeRate, 2);
            pfEmployer = Math.Round(employerWage * config.PfEmployerRate, 2);

            // The pension slice is capped at its own ceiling even when the rest
            // is contributed on full wages — so a ₹40,000 basic still sends only
            // ₹1,250 to EPS, and the balance of the employer's 12% to EPF.
            var epsWage = Math.Min(payableBasic, config.EpsWageCeiling);
            eps = Math.Round(epsWage * config.EpsRate, 2);
            eps = Math.Min(eps, pfEmployer);
            epf = Math.Round(pfEmployer - eps, 2);

            edli = Math.Round(Math.Min(payableBasic, config.EpsWageCeiling) * config.EdliRate, 2);
            pfAdmin = Math.Round(employerWage * config.PfAdminRate, 2);
        }

        /* ---------------- employees' state insurance ---------------- */

        decimal esiEmployee = 0, esiEmployer = 0;

        if (config.EsiEnabled && payableGross <= config.EsiWageThreshold)
        {
            // Rounded up to the rupee, which is what the ESI regulations say and
            // what the challan will otherwise disagree with by a few paise.
            esiEmployee = Math.Ceiling(payableGross * config.EsiEmployeeRate);
            esiEmployer = Math.Ceiling(payableGross * config.EsiEmployerRate);
        }

        /* ---------------- professional tax ---------------- */

        var pt = config.PtEnabled
            ? ProfessionalTaxFor(
                payableGross, input.PtState ?? config.PtDefaultState,
                input.Month, input.Gender, rates.PtSlabs)
            : 0m;

        /* ---------------- labour welfare fund ---------------- */

        decimal lwfEmployee = 0, lwfEmployer = 0;

        if (config.LwfEnabled && CollectsLwfIn(input.Month, config.LwfMonths))
        {
            lwfEmployee = config.LwfEmployeeAmount;
            lwfEmployer = config.LwfEmployerAmount;
        }

        /* ---------------- income tax ---------------- */

        var tax = config.TdsEnabled
            ? ComputeTax(input, rates, payableGross, payableBasic, pfEmployee, pt)
            : new TaxOutcome(0, 0, 0, 0);

        /* ---------------- what is left ---------------- */

        var deductions = pfEmployee + esiEmployee + pt + lwfEmployee
            + tax.MonthlyTds + input.OtherDeductions;

        var net = Math.Round(payableGross + input.OvertimeAmount - deductions, 2);

        /* ---------------- gratuity ---------------- */

        // Accrued from the first month rather than from the fifth year. The
        // liability builds as the service does; the five-year rule governs when
        // it becomes payable, not when it is earned.
        var gratuity = config.GratuityEnabled
            ? Math.Round(
                payableBasic * config.GratuityDaysPerYear
                / Math.Max(1, config.GratuityMonthDays) / 12m, 2)
            : 0m;

        return new PayrollResult
        {
            Gross = input.Gross,
            LopAmount = lopAmount,
            PayableGross = payableGross,
            PayableBasic = payableBasic,

            PfWage = pfWage,
            PfEmployee = pfEmployee,
            PfEmployer = pfEmployer,
            EpsEmployer = eps,
            EpfEmployer = epf,
            Edli = edli,
            PfAdminCharges = pfAdmin,

            EsiEmployee = esiEmployee,
            EsiEmployer = esiEmployer,
            ProfessionalTax = pt,
            LwfEmployee = lwfEmployee,
            LwfEmployer = lwfEmployer,

            ProjectedAnnualTaxable = tax.AnnualTaxable,
            ProjectedAnnualTax = tax.AnnualTax,
            Tds = tax.MonthlyTds,
            HraExemption = tax.HraExemption,

            TotalDeductions = Math.Round(deductions, 2),
            Net = Math.Max(0, net),

            GratuityAccrual = gratuity,
            CostToCompany = Math.Round(
                payableGross + input.OvertimeAmount + pfEmployer + edli + pfAdmin
                + esiEmployer + lwfEmployer + gratuity, 2),
        };
    }

    /* ------------------------------------------------------------------ *
     * Professional tax
     * ------------------------------------------------------------------ */

    /// <summary>
    /// The state's flat deduction for this month's gross.
    ///
    /// A row naming this month wins over a general one, which is how
    /// Maharashtra's February — ₹300 rather than ₹200, to bring the year to
    /// ₹2,500 — is expressed as data instead of as a special case here.
    /// </summary>
    private static decimal ProfessionalTaxFor(
        decimal monthlyGross,
        string? state,
        int month,
        string? gender,
        IReadOnlyList<HrProfessionalTaxSlab> slabs)
    {
        if (string.IsNullOrWhiteSpace(state) || slabs.Count == 0) return 0m;

        var candidates = slabs
            .Where(s => s.IsActive)
            .Where(s => string.Equals(s.State, state, StringComparison.OrdinalIgnoreCase))
            .Where(s => s.Gender is null
                || string.Equals(s.Gender, gender, StringComparison.OrdinalIgnoreCase))
            .Where(s => s.Covers(monthlyGross))
            .ToList();

        if (candidates.Count == 0) return 0m;

        var monthSpecific = candidates.FirstOrDefault(s => s.Month == month);
        return (monthSpecific ?? candidates.First(s => s.Month is null or 0)).Amount;
    }

    private static bool CollectsLwfIn(int month, string? months)
    {
        if (string.IsNullOrWhiteSpace(months)) return true;

        return months
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Any(m => int.TryParse(m, out var value) && value == month);
    }

    /* ------------------------------------------------------------------ *
     * Income tax
     * ------------------------------------------------------------------ */

    private record TaxOutcome(
        decimal AnnualTaxable, decimal AnnualTax, decimal MonthlyTds, decimal HraExemption);

    /// <summary>
    /// This month's TDS, from a projection of the whole year.
    ///
    /// Salary TDS is not a rate on a month. The employer estimates what the
    /// employee will earn over the year, works out the tax on it, subtracts what
    /// has already been deducted, and spreads the remainder over the months that
    /// are left. That is why a raise in October changes every payslip after it
    /// and not just its own.
    /// </summary>
    private static TaxOutcome ComputeTax(
        PayrollInput input,
        PayrollRates rates,
        decimal payableGross,
        decimal payableBasic,
        decimal pfEmployee,
        decimal professionalTax)
    {
        var regime = rates.RegimeConfig;
        if (regime is null || rates.TaxSlabs.Count == 0) return new TaxOutcome(0, 0, 0, 0);

        var profile = rates.TaxProfile;
        var months = Math.Max(1, input.RemainingMonthsInYear);

        // The year's salary: what has been paid, plus this month, plus the same
        // again for each month still to come.
        var annualSalary = input.YearToDateTaxableSalary
            + payableGross
            + (payableGross * (months - 1))
            + (profile?.PreviousEmployerSalary ?? 0)
            + (profile?.OtherIncome ?? 0);

        /* ---------------- exemptions ---------------- */

        var hraExemption = 0m;

        if (regime.AllowsHraExemption && profile is not null && profile.AnnualRentPaid > 0)
        {
            var annualBasic = payableBasic * 12m;
            var annualHra = input.Hra * 12m;

            // The least of three, which is what section 10(13A) says: the
            // allowance itself, rent over a tenth of basic, and half or four
            // tenths of basic depending on the city.
            hraExemption = Math.Min(
                annualHra,
                Math.Min(
                    Math.Max(0, profile.AnnualRentPaid - (annualBasic * 0.10m)),
                    annualBasic * (profile.RentsInMetro ? 0.50m : 0.40m)));
        }

        var taxable = annualSalary - hraExemption - regime.StandardDeduction;

        if (regime.AllowsChapterViaDeductions && profile is not null)
        {
            // Employee provident fund counts towards 80C whether or not it was
            // declared, so it is added rather than assumed to be in the figure.
            var eightyC = Math.Min(profile.Section80C + (pfEmployee * 12m), 150_000m);

            taxable -= eightyC
                + Math.Min(profile.Section80Ccd1B, 50_000m)
                + Math.Min(profile.Section80D, 100_000m)
                + Math.Min(profile.Section80Tta, 50_000m)
                + Math.Min(profile.HousingLoanInterest, 200_000m)
                + profile.OtherDeductions;
        }

        // Professional tax is deductible from salary income under both regimes.
        taxable -= professionalTax * 12m;
        taxable = Math.Max(0, Math.Round(taxable, 0));

        /* ---------------- the slabs ---------------- */

        var tax = 0m;
        foreach (var slab in rates.TaxSlabs.OrderBy(s => s.FromAmount))
        {
            if (taxable <= slab.FromAmount) continue;

            var upper = slab.ToAmount ?? taxable;
            var inBand = Math.Min(taxable, upper) - slab.FromAmount;
            if (inBand > 0) tax += inBand * slab.Rate;
        }

        /* ---------------- rebate, surcharge, cess ---------------- */

        if (taxable <= regime.RebateIncomeCeiling)
        {
            tax = Math.Max(0, tax - regime.RebateMaximum);
        }

        var surcharge = tax * regime.SurchargeFor(taxable);
        var withSurcharge = tax + surcharge;
        var annualTax = Math.Round(withSurcharge * (1 + rates.Config.CessRate), 0);

        /* ---------------- spread what is left ---------------- */

        var alreadyPaid = input.YearToDateTaxDeducted + (profile?.PreviousEmployerTds ?? 0);
        var outstanding = Math.Max(0, annualTax - alreadyPaid);
        var monthly = Math.Round(outstanding / months, 2);

        return new TaxOutcome(taxable, annualTax, monthly, Math.Round(hraExemption, 0));
    }

    /* ------------------------------------------------------------------ *
     * Gratuity
     * ------------------------------------------------------------------ */

    /// <summary>
    /// What is actually payable on separation.
    ///
    /// Fifteen days' wage for every completed year, on the last drawn basic,
    /// with a part-year past six months counting as a whole one. Nothing is
    /// payable before the qualifying period, however much has accrued.
    /// </summary>
    public static decimal GratuityPayable(
        decimal lastDrawnBasic, decimal yearsOfService, HrStatutoryConfig config)
    {
        if (!config.GratuityEnabled) return 0m;
        if (yearsOfService < config.GratuityEligibleYears) return 0m;

        var whole = Math.Floor(yearsOfService);
        if (yearsOfService - whole > 0.5m) whole += 1;

        var payable = lastDrawnBasic
            * config.GratuityDaysPerYear
            / Math.Max(1, config.GratuityMonthDays)
            * whole;

        return Math.Round(Math.Min(payable, config.GratuityCeiling), 2);
    }
}
