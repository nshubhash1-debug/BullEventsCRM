namespace BullEvents.Api.Dtos;

/* ---------------- periods ---------------- */

public record HrLeavePeriodDto(
    int Id, string Name, DateOnly FromDate, DateOnly ToDate, bool IsActive,
    DateTime? RolledOverAt,
    /// <summary>Whether today falls inside it — the period screens default to.</summary>
    bool IsCurrent,
    int EmployeesAllocated, decimal DaysAllocated);

public record HrLeavePeriodInput(
    int? Id, string Name, DateOnly FromDate, DateOnly ToDate, bool IsActive);

/// <summary>
/// What a roll over did.
///
/// The counts are of balances — one employee with earned, casual and sick
/// leave is three of them. Counting employees would hide the fact that most
/// people have some leave carried and some lapsed at the same time.
/// </summary>
public record HrRollOverResultDto(
    string ClosedPeriod, string OpenedPeriod,
    int BalancesCarried, int BalancesLapsed,
    decimal DaysCarried, decimal DaysLapsed);

/* ---------------- policies ---------------- */

public record HrLeavePolicyLineDto(
    int Id, int LeaveTypeId, string LeaveTypeName, decimal AnnualAllocation);

public record HrLeavePolicyDto(
    int Id, string Name, string? Notes, bool IsActive,
    int EmployeesAssigned, decimal TotalDays,
    IReadOnlyList<HrLeavePolicyLineDto> Lines);

public record HrLeavePolicyLineInput(int LeaveTypeId, decimal AnnualAllocation);

public record HrLeavePolicyInput(
    int? Id, string Name, string? Notes, bool IsActive,
    IReadOnlyList<HrLeavePolicyLineInput> Lines);

/* ---------------- assignments ---------------- */

public record HrLeaveAssignmentDto(
    int Id, int EmployeeId, string EmployeeName,
    int LeavePolicyId, string PolicyName,
    int LeavePeriodId, string PeriodName,
    DateOnly? EffectiveFrom, DateTime? AppliedAt, decimal DaysAllocated);

/// <summary>
/// Grant a policy to people for a period.
///
/// An empty employee list means everyone on the rolls, which is what the
/// start-of-year run actually is.
/// </summary>
public record HrLeaveAssignInput(
    int LeavePolicyId, int LeavePeriodId, IReadOnlyList<int>? EmployeeIds);

public record HrGrantResultDto(int Granted, int Skipped, decimal TotalDays);

/* ---------------- the register ---------------- */

public record HrLeaveTypeBriefDto(
    int Id, string Code, string Name, bool Paid, bool CarryForward,
    decimal MaxCarryForward, bool AllowEncashment, bool IsCompensatory);

public record HrLeaveBalanceCellDto(
    int LeaveTypeId, decimal Opening, decimal Accrued, decimal Taken, decimal Closing);

public record HrLeaveRegisterRowDto(
    int EmployeeId, string EmployeeName, decimal TotalClosing,
    IReadOnlyList<HrLeaveBalanceCellDto> Balances);

public record HrLeaveRegisterDto(
    int? PeriodId, string? PeriodName,
    IReadOnlyList<HrLeaveTypeBriefDto> LeaveTypes,
    IReadOnlyList<HrLeaveRegisterRowDto> Rows);

public record HrLeaveAllocationDto(
    int Id, int LeaveTypeId, string LeaveTypeName,
    int LeavePeriodId, string PeriodName,
    string Source, decimal Days, string? Notes, DateTime CreatedAt);

public record HrLeaveAdjustInput(
    int EmployeeId, int LeaveTypeId, int LeavePeriodId, decimal Days, string Notes);

/* ---------------- compensatory off ---------------- */

public record HrCompensatoryDto(
    int Id, int EmployeeId, string EmployeeName, DateOnly WorkedOn,
    int LeaveTypeId, string LeaveTypeName, decimal Days, string? Reason,
    int? LeadId, string Status, int? AllocationId, DateTime CreatedAt);

public record HrCompensatoryInput(
    int EmployeeId, DateOnly WorkedOn, int LeaveTypeId, decimal Days,
    string? Reason, int? LeadId);

/* ---------------- encashment ---------------- */

public record HrEncashmentDto(
    int Id, int EmployeeId, string EmployeeName,
    int LeaveTypeId, string LeaveTypeName,
    int LeavePeriodId, string PeriodName,
    decimal Days, decimal PerDayAmount, decimal Amount,
    string Status, int? PaidInPayrollRunId, DateTime CreatedAt);

public record HrEncashmentInput(
    int EmployeeId, int LeaveTypeId, int LeavePeriodId, decimal Days);

/* ---------------- block dates ---------------- */

public record HrLeaveBlockDto(
    int Id, DateOnly FromDate, DateOnly ToDate, string Reason,
    int? DepartmentId, string? DepartmentName, bool AllowOverride, bool IsActive,
    int DayCount);

public record HrLeaveBlockInput(
    int? Id, DateOnly FromDate, DateOnly ToDate, string Reason,
    int? DepartmentId, bool AllowOverride, bool IsActive);

/// <summary>
/// What a leave application would cost, before it is made.
///
/// <see cref="CountedDays"/> excludes holidays and the weekly off inside the
/// range — the single most common source of a wrong leave balance.
/// </summary>
public record HrLeaveCheckDto(
    decimal CountedDays, int SkippedDays,
    decimal CurrentBalance, decimal BalanceAfter,
    string? Shortfall, string? PeriodName,
    IReadOnlyList<HrLeaveBlockDto> Blocks);

/// <summary>
/// What a balance rebuild found and fixed.
///
/// <see cref="BalancesOrphaned"/> counts rows removed because nothing was left
/// behind them — the case a reconciliation is usually run for.
/// </summary>
public record HrRebuildResultDto(
    int Periods, int BalancesExamined, int BalancesCorrected, int BalancesOrphaned);
