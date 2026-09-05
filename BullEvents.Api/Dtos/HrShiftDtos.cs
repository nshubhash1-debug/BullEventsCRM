namespace BullEvents.Api.Dtos;

/* ---------------- assignments ---------------- */

public record HrShiftAssignmentDto(
    int Id, int EmployeeId, string EmployeeName,
    int ShiftId, string ShiftName, TimeSpan? StartTime, TimeSpan? EndTime,
    DateOnly FromDate, DateOnly? ToDate,
    int? LeadId, string? Notes, bool IsActive);

public record HrShiftAssignmentInput(
    int? Id, int EmployeeId, int ShiftId,
    DateOnly FromDate, DateOnly? ToDate, int? LeadId, string? Notes);

/// <summary>Roster a group onto one shift — the whole crew for an event week.</summary>
public record HrBulkShiftInput(
    IReadOnlyList<int> EmployeeIds, int ShiftId,
    DateOnly FromDate, DateOnly? ToDate, int? LeadId, string? Notes);

public record HrRosterResultDto(int Assigned, IReadOnlyList<string> AlreadyAssigned);

/* ---------------- the roster ---------------- */

public record HrRosterRowDto(
    int EmployeeId, string EmployeeName, string EmployeeCode, string Department,
    int? ShiftId, string ShiftName, TimeSpan? StartTime, TimeSpan? EndTime,
    /// <summary>False when this is only the employee's default shift, not a decision.</summary>
    bool OnAssignment,
    int? LeadId,
    /// <summary>Working, Leave, Holiday or Weekly off.</summary>
    string State);

public record HrRosterDto(
    DateOnly OnDate, bool IsHoliday,
    int Working, int OnLeave, int OnAssignment,
    IReadOnlyList<HrRosterRowDto> Rows);

/* ---------------- shift requests ---------------- */

public record HrShiftRequestDto(
    int Id, int EmployeeId, string EmployeeName,
    int ShiftId, string ShiftName,
    DateOnly FromDate, DateOnly? ToDate, string? Reason,
    string Status, int? ShiftAssignmentId, DateTime CreatedAt);

public record HrShiftRequestInput(
    int EmployeeId, int ShiftId, DateOnly FromDate, DateOnly? ToDate, string? Reason);

/* ---------------- attendance requests ---------------- */

public record HrAttendanceRequestDto(
    int Id, int EmployeeId, string EmployeeName,
    DateOnly FromDate, DateOnly ToDate, int DayCount,
    string RequestedStatus, bool HalfDay,
    string? Reason, string? AttachmentUrl, int? LeadId,
    string Status, DateTime CreatedAt);

public record HrAttendanceRequestInput(
    int EmployeeId, DateOnly FromDate, DateOnly ToDate,
    string RequestedStatus, bool HalfDay,
    string? Reason, string? AttachmentUrl, int? LeadId);
