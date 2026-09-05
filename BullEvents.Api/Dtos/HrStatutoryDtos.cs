namespace BullEvents.Api.Dtos;

/* ====================================================================== *
 * Setup — the rates payroll runs on
 * ====================================================================== */

/// <summary>
/// The rate set in force, with the slabs that belong to it.
///
/// Rates are carried as fractions, the way they are stored and computed —
/// 0.12, not 12. The UI multiplies for display; nothing on the wire does, so
/// there is one representation to be wrong about instead of two.
/// </summary>
public record HrStatutoryConfigDto(
    int Id,
    DateOnly EffectiveFrom,
    string Label,

    bool PfEnabled,
    decimal PfEmployeeRate,
    decimal PfEmployerRate,
    decimal PfWageCeiling,
    bool PfRestrictEmployeeToCeiling,
    bool PfRestrictEmployerToCeiling,
    decimal EpsRate,
    decimal EpsWageCeiling,
    decimal EdliRate,
    decimal PfAdminRate,

    bool EsiEnabled,
    decimal EsiEmployeeRate,
    decimal EsiEmployerRate,
    decimal EsiWageThreshold,

    bool PtEnabled,
    string? PtDefaultState,

    bool TdsEnabled,
    decimal CessRate,

    bool LwfEnabled,
    decimal LwfEmployeeAmount,
    decimal LwfEmployerAmount,
    string? LwfMonths,

    bool GratuityEnabled,
    decimal GratuityDaysPerYear,
    decimal GratuityMonthDays,
    decimal GratuityEligibleYears,
    decimal GratuityCeiling,

    string? Notes,

    /// <summary>The year the slabs below belong to — 2026 means FY 2026-27.</summary>
    int FinancialYear,
    IReadOnlyList<HrPtSlabDto> ProfessionalTaxSlabs,
    IReadOnlyList<HrIncomeTaxSlabDto> IncomeTaxSlabs,
    IReadOnlyList<HrTaxRegimeDto> Regimes);

public record HrStatutoryConfigInput(
    /// <summary>Null starts the new rates today. A revision is dated, never retroactive by accident.</summary>
    DateOnly? EffectiveFrom,
    string? Label,

    bool PfEnabled,
    decimal PfEmployeeRate,
    decimal PfEmployerRate,
    decimal PfWageCeiling,
    bool PfRestrictEmployeeToCeiling,
    bool PfRestrictEmployerToCeiling,
    decimal EpsRate,
    decimal EpsWageCeiling,
    decimal EdliRate,
    decimal PfAdminRate,

    bool EsiEnabled,
    decimal EsiEmployeeRate,
    decimal EsiEmployerRate,
    decimal EsiWageThreshold,

    bool PtEnabled,
    string? PtDefaultState,

    bool TdsEnabled,
    decimal CessRate,

    bool LwfEnabled,
    decimal LwfEmployeeAmount,
    decimal LwfEmployerAmount,
    string? LwfMonths,

    bool GratuityEnabled,
    decimal GratuityDaysPerYear,
    decimal GratuityMonthDays,
    decimal GratuityEligibleYears,
    decimal GratuityCeiling,

    string? Notes);

public record HrPtSlabDto(
    int Id, string State, decimal FromAmount, decimal? ToAmount, decimal Amount,
    /// <summary>Set only on a month-specific row, like Maharashtra's February.</summary>
    int? Month,
    string? Gender, DateOnly EffectiveFrom, bool IsActive);

public record HrIncomeTaxSlabDto(
    int Id, int FinancialYear, string Regime, decimal FromAmount, decimal? ToAmount,
    decimal Rate, int SortOrder);

public record HrTaxRegimeDto(
    int Id, int FinancialYear, string Regime, decimal StandardDeduction,
    decimal RebateIncomeCeiling, decimal RebateMaximum,
    bool AllowsChapterViaDeductions, bool AllowsHraExemption, string? SurchargeBands);

/* ====================================================================== *
 * Setup — one employee's tax position
 * ====================================================================== */

public record HrTaxProfileDto(
    int Id, int EmployeeId, string EmployeeName, int FinancialYear, string Regime,
    decimal AnnualRentPaid, bool RentsInMetro, string? LandlordPan,
    decimal Section80C, decimal Section80Ccd1B, decimal Section80D,
    decimal HousingLoanInterest, decimal Section80Tta, decimal OtherDeductions,
    /// <summary>What the caps actually allow of the declarations above.</summary>
    decimal TotalChapterVia,
    decimal OtherIncome, decimal PreviousEmployerSalary, decimal PreviousEmployerTds,
    bool ProofsSubmitted, DateOnly? ProofsSubmittedOn, string? Notes);

public record HrTaxProfileInput(
    int EmployeeId, int FinancialYear, string Regime,
    decimal AnnualRentPaid, bool RentsInMetro, string? LandlordPan,
    decimal Section80C, decimal Section80Ccd1B, decimal Section80D,
    decimal HousingLoanInterest, decimal Section80Tta, decimal OtherDeductions,
    decimal OtherIncome, decimal PreviousEmployerSalary, decimal PreviousEmployerTds,
    bool ProofsSubmitted, string? Notes);

/* ====================================================================== *
 * Returns — what has to be remitted
 * ====================================================================== */

/// <summary>
/// A month's remittances, totalled by the authority each is paid to.
///
/// The three <c>Missing…</c> counts are the ones that stop a filing rather than
/// merely look untidy: the EPFO rejects a line without a UAN, the ESIC one
/// without an insurance number, and a TDS return without a PAN attracts the
/// higher deduction rate.
/// </summary>
public record HrStatutorySummaryDto(
    int RunId, int Year, int Month, string Status, int SlipCount,
    /// <summary>The 15th of the following month — the PF and ESI deadline.</summary>
    DateOnly RemittanceDueOn,

    int PfMembers,
    decimal PfEmployee,
    decimal Eps,
    decimal Epf,
    decimal Edli,
    decimal PfAdmin,
    decimal PfTotal,

    int EsiMembers,
    decimal EsiEmployee,
    decimal EsiEmployer,
    decimal EsiTotal,

    decimal PtTotal,
    IReadOnlyList<HrPtStateTotalDto> PtByState,

    decimal LwfTotal,

    int TdsMembers,
    decimal TdsTotal,

    decimal GrossTotal,
    decimal NetTotal,
    decimal CostToCompany,

    int MissingUan,
    int MissingEsiIp,
    int MissingPan);

public record HrPtStateTotalDto(string State, int Employees, decimal Amount);

/* ====================================================================== *
 * Form 16 Part B
 * ====================================================================== */

/// <summary>
/// A year's salary and tax for one employee, from the payslips issued.
///
/// This is the annexure, not the certificate: Part A carries the TRACES
/// challan identifiers and comes from the department once the quarterly
/// returns are filed.
/// </summary>
public record HrForm16Dto(
    int EmployeeId, string EmployeeName, string EmployeeCode, string? Pan,
    int FinancialYear, string Regime,
    int MonthsPaid,
    decimal GrossSalary,
    decimal HraExemption,
    decimal StandardDeduction,
    decimal ProfessionalTax,
    decimal ChapterViaDeductions,
    decimal HousingLoanInterest,
    decimal TaxableIncome,
    decimal TaxDeducted,
    decimal ProvidentFundEmployee,
    decimal ProjectedAnnualTax);
