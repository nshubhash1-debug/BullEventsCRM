using System.ComponentModel.DataAnnotations.Schema;

namespace BullEvents.Api.Models;

/* ====================================================================== *
 * Leave, properly
 *
 * What existed was a leave type, a balance row and a request. That works
 * until somebody asks the questions a leave register has to answer: where
 * did this balance come from, who granted it, what happens to it in April,
 * and why was this person allowed to book off during the wedding season.
 *
 * The shape here is the one those questions imply. An allocation is an
 * event — a grant, a carry-forward, a compensatory day earned, an
 * encashment taken — and the balance row is its running total, the same
 * ledger-and-total pattern the prop store uses. Nothing writes a balance
 * directly except the code that also writes the allocation explaining it.
 * ====================================================================== */

public static class LeaveAllocationSources
{
    /// <summary>The annual grant, from a policy assignment.</summary>
    public const string Policy = "Policy";

    /// <summary>Last period's unused days, brought forward within the cap.</summary>
    public const string CarryForward = "CarryForward";

    /// <summary>A day off earned by working a holiday or a weekly off.</summary>
    public const string Compensatory = "Compensatory";

    /// <summary>Days paid out instead of taken. A negative allocation.</summary>
    public const string Encashment = "Encashment";

    /// <summary>Somebody in HR granted or removed days by hand.</summary>
    public const string Manual = "Manual";

    /// <summary>Days that expired unused at the end of a period.</summary>
    public const string Lapsed = "Lapsed";

    public static readonly string[] All =
        [Policy, CarryForward, Compensatory, Encashment, Manual, Lapsed];
}

/// <summary>
/// The window allocations belong to — normally the financial year.
///
/// Leave is granted for a period rather than a calendar year because
/// carry-forward, encashment and lapsing all happen at its boundary, and an
/// Indian company's boundary is 1 April. Exactly one period may be open for a
/// given date; the seeder and the API both enforce it.
/// </summary>
public class HrLeavePeriod : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    /// <summary>Closed periods refuse new allocations and new leave.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Set when the period has been rolled over — carry-forward computed,
    /// the rest lapsed. Stops a second rollover doubling everybody's balance.
    /// </summary>
    public DateTime? RolledOverAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrLeaveAllocation> Allocations { get; set; } = new List<HrLeaveAllocation>();

    public bool Covers(DateOnly date) => date >= FromDate && date <= ToDate;
}

/// <summary>
/// A named bundle of entitlements — "Staff", "Site crew", "Probation".
///
/// Separate from the leave type because the same type is granted in different
/// quantities to different people: a designer gets 18 earned leave a year and
/// a helper on contract gets none, and neither fact belongs on the leave type
/// itself.
/// </summary>
public class HrLeavePolicy : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrLeavePolicyLine> Lines { get; set; } = new List<HrLeavePolicyLine>();
}

public class HrLeavePolicyLine : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int LeavePolicyId { get; set; }
    public HrLeavePolicy? LeavePolicy { get; set; }

    public int LeaveTypeId { get; set; }
    public HrLeaveType? LeaveType { get; set; }

    /// <summary>Days granted for a full period.</summary>
    public decimal AnnualAllocation { get; set; }
}

/// <summary>
/// A policy applied to one employee for one period.
///
/// Kept as a record rather than a job that runs and forgets, because the
/// allocations it produced have to be traceable back to a decision somebody
/// made, and because applying the same assignment twice must be refused
/// rather than silently doubling the grant.
/// </summary>
public class HrLeavePolicyAssignment : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public int LeavePolicyId { get; set; }
    public HrLeavePolicy? LeavePolicy { get; set; }

    public int LeavePeriodId { get; set; }
    public HrLeavePeriod? LeavePeriod { get; set; }

    /// <summary>
    /// When the employee joined mid-period, the grant is pro-rated from here.
    /// Null grants the full year.
    /// </summary>
    public DateOnly? EffectiveFrom { get; set; }

    /// <summary>Set once the allocations have been written. Guards a re-run.</summary>
    public DateTime? AppliedAt { get; set; }

    /// <summary>Total days actually granted, after pro-rating.</summary>
    public decimal DaysAllocated { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>
/// One movement of leave entitlement. Append-only.
///
/// The balance is the sum of these; nothing subtracts from a balance without
/// writing the row that says why. Days may be negative — an encashment and a
/// lapse both take entitlement away.
/// </summary>
public class HrLeaveAllocation : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public int LeaveTypeId { get; set; }
    public HrLeaveType? LeaveType { get; set; }

    public int LeavePeriodId { get; set; }
    public HrLeavePeriod? LeavePeriod { get; set; }

    /// <summary>A value from <see cref="LeaveAllocationSources"/>.</summary>
    public string Source { get; set; } = LeaveAllocationSources.Manual;

    /// <summary>Negative for an encashment or a lapse.</summary>
    public decimal Days { get; set; }

    /// <summary>The assignment, compensatory request or encashment behind it.</summary>
    public int? SourceRecordId { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>
/// A day worked when it should not have been, claimed back as a day off.
///
/// This is the single most-used request in an events company: the whole crew
/// works Sundays through the season, and comp-off is what they are paid back
/// in. Approval creates a compensatory allocation, which is why the request
/// carries the date worked — the allocation has to be able to name it.
/// </summary>
public class HrCompensatoryRequest : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    /// <summary>The holiday or weekly off that was worked.</summary>
    public DateOnly WorkedOn { get; set; }

    /// <summary>Which type the earned day lands in. Usually a comp-off type.</summary>
    public int LeaveTypeId { get; set; }
    public HrLeaveType? LeaveType { get; set; }

    /// <summary>Half a day for a short call-out, one for a full day.</summary>
    public decimal Days { get; set; } = 1m;

    public string? Reason { get; set; }

    /// <summary>The event or site worked, when there was one.</summary>
    public int? LeadId { get; set; }

    public string Status { get; set; } = HrRequestStatuses.PendingManager;

    /// <summary>The allocation approval produced, so it can be undone.</summary>
    public int? AllocationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>
/// Unused days paid out instead of carried.
///
/// The amount is computed from the salary at the time and frozen here, because
/// a payout reprinted after a raise must still show what was paid.
/// </summary>
public class HrLeaveEncashment : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public int LeaveTypeId { get; set; }
    public HrLeaveType? LeaveType { get; set; }

    public int LeavePeriodId { get; set; }
    public HrLeavePeriod? LeavePeriod { get; set; }

    public decimal Days { get; set; }

    /// <summary>Basic ÷ 30 at the time of the payout.</summary>
    public decimal PerDayAmount { get; set; }

    public decimal Amount { get; set; }

    public string Status { get; set; } = HrRequestStatuses.PendingHr;

    /// <summary>Set when the amount has been carried onto a payslip.</summary>
    public int? PaidInPayrollRunId { get; set; }

    public int? AllocationId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>
/// Dates on which leave is refused.
///
/// An events company's reason for existing is that it turns up on the day, and
/// the day is known months ahead. This is how November gets closed.
/// </summary>
public class HrLeaveBlockDate : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    /// <summary>Null blocks the whole company.</summary>
    public int? DepartmentId { get; set; }
    public HrDepartment? Department { get; set; }

    /// <summary>
    /// Whether HR can approve through it anyway. A hard block is for the days
    /// the company genuinely cannot cover; a soft one is a warning.
    /// </summary>
    public bool AllowOverride { get; set; } = true;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public bool Covers(DateOnly date) => date >= FromDate && date <= ToDate;
}

/* ====================================================================== *
 * Shifts and attendance requests
 * ====================================================================== */

/// <summary>
/// Which shift somebody is on, and between which dates.
///
/// The employee record carries a default shift, which is right for an office
/// and useless for a crew that works nights in season. An assignment overrides
/// it for a window, and the roster is the set of assignments for a day.
/// </summary>
public class HrShiftAssignment : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public int ShiftId { get; set; }
    public HrShift? Shift { get; set; }

    public DateOnly FromDate { get; set; }

    /// <summary>Null runs until somebody ends it.</summary>
    public DateOnly? ToDate { get; set; }

    /// <summary>The event this rostering was for, when it was for one.</summary>
    public int? LeadId { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public bool Covers(DateOnly date) =>
        IsActive && date >= FromDate && (ToDate is null || date <= ToDate);

    /// <summary>Whether two assignments fight over the same days.</summary>
    public bool Overlaps(DateOnly from, DateOnly? to) =>
        FromDate <= (to ?? DateOnly.MaxValue) && (ToDate ?? DateOnly.MaxValue) >= from;
}

/// <summary>
/// An employee asking to be put on a different shift for a while.
///
/// Approval creates the assignment. Kept as its own record so the ask survives
/// the decision — a refused request is a thing a manager has to be able to
/// point at later.
/// </summary>
public class HrShiftRequest : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public int ShiftId { get; set; }
    public HrShift? Shift { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly? ToDate { get; set; }

    public string? Reason { get; set; }
    public string Status { get; set; } = HrRequestStatuses.PendingManager;

    /// <summary>The assignment approval created.</summary>
    public int? ShiftAssignmentId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>
/// Attendance somebody is asking to have recorded after the fact.
///
/// Distinct from a correction, which argues with an existing row. This one
/// covers the days there is no row for at all: on site all week, at a client's
/// office, out at a venue recce — the punch machine never saw them and
/// payroll would otherwise read the silence as absence.
/// </summary>
public class HrAttendanceRequest : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    /// <summary>What the days should be marked as — a value from <see cref="AttendanceDayStatuses"/>.</summary>
    public string RequestedStatus { get; set; } = AttendanceDayStatuses.OnDuty;

    /// <summary>Half day applies to a single-day request.</summary>
    public bool HalfDay { get; set; }

    public string? Reason { get; set; }
    public string? AttachmentUrl { get; set; }

    /// <summary>The event they were at, when there was one.</summary>
    public int? LeadId { get; set; }

    public string Status { get; set; } = HrRequestStatuses.PendingManager;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    [NotMapped]
    public int DayCount => ToDate.DayNumber - FromDate.DayNumber + 1;
}
