namespace BullEvents.Api.Dtos;

/* ---------------- components ---------------- */

public record HrSalaryComponentDto(
    int Id, string Name, string Abbreviation, string ComponentType, string Calculation,
    string? Formula,
    bool AffectsPf, bool AffectsEsi, bool IsTaxable, bool IsHra,
    bool DependsOnPaymentDays, bool IsStatutory,
    int SortOrder, bool IsActive, string? Notes,
    /// <summary>How many structures use it — what an edit would reach.</summary>
    int UsedInStructures);

public record HrSalaryComponentInput(
    int? Id, string Name, string? Abbreviation, string ComponentType, string Calculation,
    string? Formula,
    bool AffectsPf, bool AffectsEsi, bool IsTaxable, bool IsHra,
    bool DependsOnPaymentDays,
    int SortOrder, bool IsActive, string? Notes);

/// <summary>Try a formula before saving it.</summary>
public record HrFormulaTryInput(
    string? Formula, decimal Base,
    /// <summary>Values to stand in for named components, when the defaults will not do.</summary>
    IReadOnlyDictionary<string, decimal>? Values);

public record HrFormulaTryDto(
    decimal Result, string? Error, IReadOnlyList<string> References);

/* ---------------- structures ---------------- */

public record HrPayStructureLineDto(
    int Id, int SalaryComponentId, string ComponentName, string Abbreviation,
    string ComponentType, string Calculation,
    decimal Amount, string? Formula, int SortOrder);

public record HrPayStructureDto(
    int Id, string Name, string? Notes, bool IsActive, decimal OvertimeRate,
    int EmployeesAssigned,
    IReadOnlyList<HrPayStructureLineDto> Lines);

public record HrPayStructureLineInput(
    int SalaryComponentId, decimal Amount, string? Formula, int SortOrder);

public record HrPayStructureInput(
    int? Id, string Name, string? Notes, bool IsActive, decimal OvertimeRate,
    IReadOnlyList<HrPayStructureLineInput> Lines);

public record HrStructurePreviewLineDto(
    string Name, string Abbreviation, string ComponentType, decimal Amount,
    string? Formula,
    /// <summary>Set when the formula would not evaluate — shown against the line.</summary>
    string? Error);

public record HrStructurePreviewDto(
    string StructureName, decimal Base,
    decimal Gross, decimal Deductions, decimal Net,
    IReadOnlyList<HrStructurePreviewLineDto> Lines);

/* ---------------- assignments ---------------- */

public record HrPayAssignmentDto(
    int Id, int EmployeeId, string EmployeeName,
    int PayStructureId, string StructureName,
    decimal Base, DateOnly EffectiveFrom);

public record HrPayAssignmentInput(
    int EmployeeId, int PayStructureId, decimal Base, DateOnly EffectiveFrom);

public record HrComponentAmountDto(
    string Name, string Abbreviation, string ComponentType, decimal Amount,
    bool AffectsPf, bool IsHra, bool IsTaxable, bool DependsOnPaymentDays);

/// <summary>
/// What one employee is paid, itemised.
///
/// <see cref="FromLegacyStructure"/> says the figures came from the older
/// Basic/HRA/Allowances record rather than a component structure — worth
/// showing, because it tells whoever is looking why there is nothing to edit
/// on the components screen.
/// </summary>
public record HrPayBreakdownDto(
    int EmployeeId, string EmployeeName, DateOnly AsOf, bool FromLegacyStructure,
    decimal Gross, decimal PfWageBase, decimal Hra, decimal Deductions,
    IReadOnlyList<HrComponentAmountDto> Components);

/* ---------------- additional salary ---------------- */

public record HrAdditionalSalaryDto(
    int Id, int EmployeeId, string EmployeeName,
    int SalaryComponentId, string ComponentName, string ComponentType,
    decimal Amount, int Year, int Month,
    bool IsRecurring, DateOnly? RecurringUntil, bool DependsOnPaymentDays,
    string? Reason, string Status, int? PaidInPayrollRunId);

public record HrAdditionalSalaryInput(
    int EmployeeId, int SalaryComponentId, decimal Amount, int Year, int Month,
    bool IsRecurring, DateOnly? RecurringUntil, bool DependsOnPaymentDays, string? Reason);

/* ---------------- advances ---------------- */

public record HrAdvanceDto(
    int Id, int EmployeeId, string EmployeeName,
    decimal Amount, int Instalments, decimal InstalmentAmount,
    int RecoveryStartYear, int RecoveryStartMonth,
    string? Purpose, string Status, DateOnly? PaidOn, string? WriteOffReason,
    decimal Recovered, decimal Outstanding, int InstalmentsTaken);

public record HrAdvanceInput(
    int EmployeeId, decimal Amount, int Instalments,
    int RecoveryStartYear, int RecoveryStartMonth, string? Purpose);

/* ---------------- payslip lines ---------------- */

public record HrPayslipLineDto(
    string Name, string Abbreviation, string ComponentType, decimal Amount,
    bool IsStatutory, int SortOrder);
