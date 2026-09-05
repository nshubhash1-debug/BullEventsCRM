namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * India statutory payroll.
 *
 * The payroll run used to carry its rates as literals — 12%, ₹15,000, 0.75%,
 * ₹21,000 — sitting inside a controller method. Every one of those numbers is
 * set by statute and every one of them moves: the PF ceiling has been ₹6,500
 * and ₹15,000 in living memory, ESI's threshold has moved twice, and the tax
 * slabs move most Februaries. A rate in a method body means a code change and a
 * deploy to follow the law, and it means last year's payslips silently
 * recompute on this year's numbers the moment somebody reruns a month.
 *
 * So the rates live in effective-dated rows, and a payroll run reads whichever
 * row was in force for the month it is paying. Reprocessing March next year
 * gets March's law.
 *
 * What is modelled here is the statutory floor an Indian employer cannot skip:
 * provident fund with its pension split, employees' state insurance,
 * state-wise professional tax, income tax deducted at source, the labour
 * welfare fund, and gratuity accrual.
 * ------------------------------------------------------------------ */

/// <summary>
/// Which tax regime an employee has elected for a financial year.
///
/// Not a company setting: the choice is the employee's, it is made afresh each
/// year, and payroll has to deduct on whichever one they picked.
/// </summary>
public static class TaxRegimes
{
    /// <summary>Slabs with almost no deductions. The default since FY 2023-24.</summary>
    public const string New = "New";

    /// <summary>Lower exemption limit, but 80C, 80D, HRA and the rest are allowed.</summary>
    public const string Old = "Old";

    public static readonly string[] All = [New, Old];
}

/// <summary>
/// The states whose professional tax this system knows about.
///
/// Professional tax is levied by the state, not the centre, and a handful of
/// states do not levy it at all. Stored as a plain string rather than an enum
/// so a state that starts levying — or one this list has missed — is a data
/// row rather than a deploy.
/// </summary>
public static class PtStates
{
    public const string Maharashtra = "Maharashtra";
    public const string Karnataka = "Karnataka";
    public const string WestBengal = "West Bengal";
    public const string TamilNadu = "Tamil Nadu";
    public const string AndhraPradesh = "Andhra Pradesh";
    public const string Telangana = "Telangana";
    public const string Gujarat = "Gujarat";
    public const string MadhyaPradesh = "Madhya Pradesh";
    public const string Kerala = "Kerala";
    public const string Odisha = "Odisha";
    public const string Assam = "Assam";
    public const string Bihar = "Bihar";
    public const string Jharkhand = "Jharkhand";
    public const string Meghalaya = "Meghalaya";
    public const string Tripura = "Tripura";
    public const string Puducherry = "Puducherry";
    public const string Sikkim = "Sikkim";
    public const string Nagaland = "Nagaland";
    public const string Manipur = "Manipur";
    public const string Mizoram = "Mizoram";

    /// <summary>States that levy no professional tax at all.</summary>
    public static readonly string[] NotLevied =
    [
        "Delhi", "Haryana", "Punjab", "Rajasthan", "Uttar Pradesh",
        "Uttarakhand", "Himachal Pradesh", "Goa", "Chandigarh",
        "Jammu and Kashmir", "Arunachal Pradesh", "Chhattisgarh",
    ];

    public static readonly string[] All =
    [
        Maharashtra, Karnataka, WestBengal, TamilNadu, AndhraPradesh, Telangana,
        Gujarat, MadhyaPradesh, Kerala, Odisha, Assam, Bihar, Jharkhand,
        Meghalaya, Tripura, Puducherry, Sikkim, Nagaland, Manipur, Mizoram,
    ];
}

/// <summary>
/// The statutory rates in force for one company from one date.
///
/// Effective-dated rather than mutable. Editing last year's row in place would
/// change what last year's payslips say the moment anybody reran a month, and a
/// payslip is a document an employee has already filed with their bank.
/// </summary>
public class HrStatutoryConfig : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>First month this row applies to. The run picks the latest row at or before it.</summary>
    public DateOnly EffectiveFrom { get; set; }

    public string Label { get; set; } = string.Empty;

    /* ---------------- provident fund ---------------- */

    public bool PfEnabled { get; set; } = true;

    /// <summary>Employee's share, as a fraction. Statutory 12%.</summary>
    public decimal PfEmployeeRate { get; set; } = 0.12m;

    /// <summary>Employer's total share. Also 12%, but it splits — see below.</summary>
    public decimal PfEmployerRate { get; set; } = 0.12m;

    /// <summary>
    /// Monthly wage above which PF is optional. ₹15,000 since 2014.
    ///
    /// Applied to basic plus DA, not to gross — quietly the most commonly
    /// mis-implemented rule in Indian payroll.
    /// </summary>
    public decimal PfWageCeiling { get; set; } = 15_000m;

    /// <summary>
    /// Whether contributions are capped at the ceiling or taken on full wages.
    ///
    /// Both are lawful and both are common: many employers restrict to the
    /// ceiling, others contribute on the whole basic. The employee's own share
    /// can differ from the employer's, which is why this is one flag per side.
    /// </summary>
    public bool PfRestrictEmployeeToCeiling { get; set; } = true;
    public bool PfRestrictEmployerToCeiling { get; set; } = true;

    /// <summary>
    /// The pension slice of the employer's 12%. Statutory 8.33%, and capped at
    /// the ceiling wage even when the rest is not — so ₹1,250 a month at most.
    ///
    /// The old code set the employer's contribution equal to the employee's and
    /// stopped there, which reports the right total and the wrong split. EPS and
    /// EPF are different funds with different withdrawal rules, and the ECR
    /// return wants them apart.
    /// </summary>
    public decimal EpsRate { get; set; } = 0.0833m;
    public decimal EpsWageCeiling { get; set; } = 15_000m;

    /// <summary>Employees' Deposit Linked Insurance — 0.5% of the ceiling wage.</summary>
    public decimal EdliRate { get; set; } = 0.005m;

    /// <summary>EPF administration charges, borne by the employer. 0.5%.</summary>
    public decimal PfAdminRate { get; set; } = 0.005m;

    /* ---------------- employees' state insurance ---------------- */

    public bool EsiEnabled { get; set; } = true;

    /// <summary>Employee's share of gross. 0.75%.</summary>
    public decimal EsiEmployeeRate { get; set; } = 0.0075m;

    /// <summary>Employer's share of gross. 3.25%.</summary>
    public decimal EsiEmployerRate { get; set; } = 0.0325m;

    /// <summary>Gross above which ESI does not apply. ₹21,000.</summary>
    public decimal EsiWageThreshold { get; set; } = 21_000m;

    /* ---------------- professional tax ---------------- */

    public bool PtEnabled { get; set; } = true;

    /// <summary>
    /// The state whose slabs apply when an employee's own work state is blank.
    ///
    /// Per employee rather than per company in principle — a company with a
    /// Mumbai office and a Bengaluru one deducts under two different acts — so
    /// this is only the fallback.
    /// </summary>
    public string? PtDefaultState { get; set; }

    /* ---------------- income tax ---------------- */

    public bool TdsEnabled { get; set; } = true;

    /// <summary>Health and education cess on the tax figure. 4%.</summary>
    public decimal CessRate { get; set; } = 0.04m;

    /* ---------------- labour welfare fund ---------------- */

    public bool LwfEnabled { get; set; }

    /// <summary>A flat amount, not a rate. Varies by state and is usually tiny.</summary>
    public decimal LwfEmployeeAmount { get; set; }
    public decimal LwfEmployerAmount { get; set; }

    /// <summary>
    /// Months the LWF is collected in, comma separated — "6,12".
    ///
    /// Most states collect it half-yearly or yearly rather than monthly, and
    /// deducting it every month would be twelve times the law.
    /// </summary>
    public string? LwfMonths { get; set; }

    /* ---------------- gratuity ---------------- */

    public bool GratuityEnabled { get; set; } = true;

    /// <summary>Days of wage earned per completed year. Statutory 15.</summary>
    public decimal GratuityDaysPerYear { get; set; } = 15m;

    /// <summary>Divisor turning a month's wage into a day's. Statutory 26.</summary>
    public decimal GratuityMonthDays { get; set; } = 26m;

    /// <summary>Years of service before any gratuity is payable. Statutory 5.</summary>
    public decimal GratuityEligibleYears { get; set; } = 5m;

    /// <summary>The lifetime exemption ceiling. ₹20 lakh.</summary>
    public decimal GratuityCeiling { get; set; } = 2_000_000m;

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>Whether this row governs a given pay month.</summary>
    public bool AppliesTo(DateOnly month) => EffectiveFrom <= month;
}

/// <summary>
/// One band of a state's professional tax schedule.
///
/// Slabs are flat amounts against a monthly gross band, not percentages, and
/// they differ per state in both the bands and the money. Maharashtra's
/// February is the awkward one — ₹300 instead of ₹200, to make the year total
/// ₹2,500 — which is what <see cref="Month"/> exists for.
/// </summary>
public class HrProfessionalTaxSlab : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string State { get; set; } = string.Empty;

    /// <summary>Monthly gross this band starts at, inclusive.</summary>
    public decimal FromAmount { get; set; }

    /// <summary>Where the band ends, inclusive. Null is the open top band.</summary>
    public decimal? ToAmount { get; set; }

    /// <summary>The flat monthly deduction for this band.</summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Applies only in this calendar month. Null applies to every month.
    ///
    /// A month-specific row wins over the general one, which is how
    /// Maharashtra's February surcharge is expressed without a special case in
    /// the engine.
    /// </summary>
    public int? Month { get; set; }

    /// <summary>Male, Female, or null for both — a few states still differentiate.</summary>
    public string? Gender { get; set; }

    public DateOnly EffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>Whether a monthly gross falls in this band.</summary>
    public bool Covers(decimal monthlyGross) =>
        monthlyGross >= FromAmount && (ToAmount is null || monthlyGross <= ToAmount);
}

/// <summary>
/// One slab of the income tax schedule, for one regime in one financial year.
///
/// Held as rows rather than a switch statement because they change most
/// Februaries, and because the same engine has to be able to compute last
/// year's tax on last year's slabs when somebody reissues a Form 16.
/// </summary>
public class HrIncomeTaxSlab : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    /// <summary>The financial year this belongs to, as its starting year — 2026 is FY 2026-27.</summary>
    public int FinancialYear { get; set; }

    /// <summary>A value from <see cref="TaxRegimes"/>.</summary>
    public string Regime { get; set; } = TaxRegimes.New;

    public decimal FromAmount { get; set; }

    /// <summary>Null is the top slab.</summary>
    public decimal? ToAmount { get; set; }

    /// <summary>Marginal rate on the part of income inside this slab.</summary>
    public decimal Rate { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>
/// The regime-level constants that sit beside a year's slabs.
///
/// Separate from the slab rows because they are one-per-regime-per-year rather
/// than one-per-band: the standard deduction, the section 87A rebate and the
/// surcharge thresholds are not slabs and modelling them as such would have
/// meant a slab row that means something different from all the others.
/// </summary>
public class HrTaxRegimeConfig : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int FinancialYear { get; set; }
    public string Regime { get; set; } = TaxRegimes.New;

    /// <summary>Flat deduction from salary income. ₹75,000 new, ₹50,000 old.</summary>
    public decimal StandardDeduction { get; set; }

    /// <summary>Taxable income at or below which the 87A rebate wipes the tax out.</summary>
    public decimal RebateIncomeCeiling { get; set; }

    /// <summary>The most the rebate can wipe out.</summary>
    public decimal RebateMaximum { get; set; }

    /// <summary>Whether Chapter VI-A deductions — 80C, 80D and the rest — are allowed.</summary>
    public bool AllowsChapterViaDeductions { get; set; }

    /// <summary>Whether house rent allowance can be exempted under section 10(13A).</summary>
    public bool AllowsHraExemption { get; set; }

    /// <summary>Surcharge bands, as "threshold:rate" pairs — "5000000:0.10,10000000:0.15".</summary>
    public string? SurchargeBands { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>The surcharge rate on a taxable income, from the stored bands.</summary>
    public decimal SurchargeFor(decimal taxableIncome)
    {
        if (string.IsNullOrWhiteSpace(SurchargeBands)) return 0m;

        var rate = 0m;
        foreach (var band in SurchargeBands.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = band.Split(':');
            if (parts.Length != 2) continue;
            if (!decimal.TryParse(parts[0], out var threshold)) continue;
            if (!decimal.TryParse(parts[1], out var bandRate)) continue;

            // Bands are cumulative thresholds, so the highest one cleared wins.
            if (taxableIncome > threshold && bandRate > rate) rate = bandRate;
        }

        return rate;
    }
}

/// <summary>
/// One employee's tax position for one financial year.
///
/// The regime is elected annually and the declarations are what the employee
/// tells payroll they will invest, which is what TDS is computed on until
/// proofs arrive. Both are per year, which is why this is not fields on the
/// employee record.
/// </summary>
public class HrEmployeeTaxProfile : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public int FinancialYear { get; set; }
    public string Regime { get; set; } = TaxRegimes.New;

    /* ---------------- exemptions ---------------- */

    /// <summary>Annual rent paid, for the section 10(13A) exemption.</summary>
    public decimal AnnualRentPaid { get; set; }

    /// <summary>
    /// Whether the rented home is in a metro.
    ///
    /// Decides the 50%-versus-40%-of-basic leg of the HRA exemption, which is
    /// usually the leg that binds.
    /// </summary>
    public bool RentsInMetro { get; set; }

    public string? LandlordPan { get; set; }

    /* ---------------- Chapter VI-A ---------------- */

    /// <summary>80C — provident fund, insurance, ELSS, tuition. Capped at ₹1.5 lakh.</summary>
    public decimal Section80C { get; set; }

    /// <summary>80CCD(1B) — the extra NPS deduction, ₹50,000.</summary>
    public decimal Section80Ccd1B { get; set; }

    /// <summary>80D — medical insurance premium.</summary>
    public decimal Section80D { get; set; }

    /// <summary>24(b) — interest on a housing loan for a self-occupied property.</summary>
    public decimal HousingLoanInterest { get; set; }

    /// <summary>80TTA / 80TTB — interest on savings.</summary>
    public decimal Section80Tta { get; set; }

    /// <summary>Anything else declared, lumped rather than modelled one section at a time.</summary>
    public decimal OtherDeductions { get; set; }

    /* ---------------- other income and prior employment ---------------- */

    /// <summary>Income from elsewhere the employee has asked to be considered.</summary>
    public decimal OtherIncome { get; set; }

    /// <summary>Salary already drawn this year at a previous employer.</summary>
    public decimal PreviousEmployerSalary { get; set; }

    /// <summary>Tax already deducted there. Reduces what is left to deduct here.</summary>
    public decimal PreviousEmployerTds { get; set; }

    /* ---------------- proofs ---------------- */

    /// <summary>Whether the declared investments have been evidenced.</summary>
    public bool ProofsSubmitted { get; set; }
    public DateOnly? ProofsSubmittedOn { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>
    /// Everything claimed under Chapter VI-A, with each section's own cap applied.
    ///
    /// Capped here rather than at entry so an employee can declare what they
    /// actually invested and payroll still deducts what the law allows.
    /// </summary>
    public decimal TotalChapterVia =>
        Math.Min(Section80C, 150_000m)
        + Math.Min(Section80Ccd1B, 50_000m)
        + Math.Min(Section80D, 100_000m)
        + Math.Min(Section80Tta, 50_000m)
        + OtherDeductions;
}
