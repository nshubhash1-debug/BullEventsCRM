namespace BullEvents.Api.Dtos;

public record HrDepartmentDto(int Id, string Name, string? Code, string? Location, bool IsActive);

public record HrDepartmentInput(string Name, string? Code, string? Location, bool IsActive = true);

public record HrShiftDto(
    int Id, string Name, TimeSpan StartTime, TimeSpan EndTime, int GraceMinutes,
    string WeeklyOff, bool IsActive);

public record HrShiftInput(
    string Name, TimeSpan StartTime, TimeSpan EndTime, int GraceMinutes, string WeeklyOff,
    bool IsActive = true);

public record HrEmployeeDto(
    int Id,
    string EmployeeCode,
    string Name,
    string? PhotoUrl,
    DateTime? DateOfBirth,
    string? Phone,
    string? Email,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    int? DepartmentId,
    string DepartmentName,
    int? DesignationId,
    string DesignationName,
    int? ManagerEmployeeId,
    string? ManagerName,
    int? BranchId,
    string? Location,
    DateTime JoiningDate,
    string EmploymentType,
    string Status,
    string CollarType,
    int? UserId,
    int? ShiftId,
    string? ShiftName,
    string? Pan,
    string? Uan,
    string? EsicIp,
    DateTime CreatedAt);

public record HrEmployeeInput(
    string EmployeeCode,
    string Name,
    DateTime? DateOfBirth,
    string? Phone,
    string? Email,
    string? Address,
    string? EmergencyContactName,
    string? EmergencyContactPhone,
    int? DepartmentId,
    int? DesignationId,
    int? ManagerEmployeeId,
    int? BranchId,
    string? Location,
    DateTime JoiningDate,
    string EmploymentType,
    string Status,
    string CollarType,
    int? UserId,
    int? ShiftId,
    string? PhotoUrl,
    string? BankAccount,
    string? Ifsc,
    string? Pan,
    string? Aadhaar,
    string? Uan,
    string? EsicIp);

public record HrDocumentDto(
    int Id, int EmployeeId, string DocumentType, string FileName, string? Url,
    DateTime? ExpiryDate, DateTime? ReminderDate);

public record HrDocumentInput(
    string DocumentType, string FileName, string? Url, DateTime? ExpiryDate, DateTime? ReminderDate);

public record HrAttendanceDto(
    int Id, int EmployeeId, string EmployeeName, DateOnly WorkDate,
    TimeSpan? InTime, TimeSpan? OutTime, string Status, bool IsLate, bool LeftEarly,
    string? Notes);

public record HrAttendanceInput(
    int EmployeeId, DateOnly WorkDate, TimeSpan? InTime, TimeSpan? OutTime,
    string Status, string? Notes);

public record HrCorrectionDto(
    int Id, int EmployeeId, string EmployeeName, DateOnly WorkDate, string Reason,
    TimeSpan? CorrectInTime, TimeSpan? CorrectOutTime, string Status);

public record HrCorrectionInput(
    int EmployeeId, DateOnly WorkDate, string Reason, TimeSpan? CorrectInTime,
    TimeSpan? CorrectOutTime, string? AttachmentUrl);

public record HrLeaveTypeDto(
    int Id, string Code, string Name, decimal MonthlyEntitlement, bool Paid,
    bool CarryForward, int ApprovalLevels, bool IsActive);

public record HrLeaveTypeInput(
    string Code, string Name, decimal MonthlyEntitlement, bool Paid, bool CarryForward,
    int ApprovalLevels, bool IsActive = true);

public record HrLeaveBalanceDto(
    int LeaveTypeId, string LeaveType, decimal Opening, decimal Accrued, decimal Taken,
    decimal Closing);

public record HrLeaveRequestDto(
    int Id, int EmployeeId, string EmployeeName, int LeaveTypeId, string LeaveType,
    DateOnly FromDate, DateOnly ToDate, bool HalfDay, string? Reason, string Status);

public record HrLeaveRequestInput(
    int EmployeeId, int LeaveTypeId, DateOnly FromDate, DateOnly ToDate, bool HalfDay,
    string? Reason, string? AttachmentUrl);

public record HrSalaryDto(
    int Id, int EmployeeId, DateOnly EffectiveFrom, decimal Basic, decimal Hra,
    decimal Allowances, decimal Incentive, decimal Bonus, decimal OvertimeRate,
    decimal Deductions, decimal Gross);

public record HrSalaryInput(
    int EmployeeId, DateOnly EffectiveFrom, decimal Basic, decimal Hra, decimal Allowances,
    decimal Incentive, decimal Bonus, decimal OvertimeRate, decimal Deductions);

public record HrRevisionInput(int EmployeeId, decimal NewGross, DateOnly EffectiveFrom, string? Reason);

public record HrPayrollRunDto(
    int Id, int Year, int Month, string Status, DateTime? AttendanceLockedAt,
    DateTime? ProcessedAt, int SlipCount, decimal NetTotal);

/// <summary>
/// A payslip, itemised the way it has to be able to explain itself.
///
/// The employer contributions are carried alongside the employee's because
/// they are what the monthly returns are filed from, and because cost to
/// company means nothing without them.
/// </summary>
public record HrPayslipDto(
    int Id, int PayrollRunId, int EmployeeId, string EmployeeName, decimal Gross,
    decimal LopDays, decimal LopAmount, decimal OtherDeductions, decimal PfEmployee,
    decimal EsicEmployee, decimal Incentive, decimal OvertimeAmount, decimal Net,

    /* ---------------- statutory detail ---------------- */

    decimal PayableGross,
    decimal PfWage,
    decimal PfEmployer,
    /// <summary>The pension slice of the employer's contribution.</summary>
    decimal EpsEmployer,
    /// <summary>The provident-fund slice — the employer's total less the pension.</summary>
    decimal EpfEmployer,
    decimal Edli,
    decimal PfAdminCharges,
    decimal EsicEmployer,
    decimal ProfessionalTax,
    decimal LwfEmployee,
    decimal LwfEmployer,
    decimal Tds,
    /// <summary>The year's projection this month's TDS was a twelfth of.</summary>
    decimal ProjectedAnnualTaxable,
    decimal ProjectedAnnualTax,
    decimal HraExemption,
    string? TaxRegime,
    decimal GratuityAccrual,
    /// <summary>Gross plus every employer contribution.</summary>
    decimal CostToCompany,
    decimal TotalDeductions);

public record HrVacancyDto(
    int Id, string Position, int? DepartmentId, string? DepartmentName, string? Location,
    string? Experience, decimal? SalaryMin, decimal? SalaryMax, string? JobDescription,
    string Status);

public record HrVacancyInput(
    string Position, int? DepartmentId, string? Location, string? Experience,
    decimal? SalaryMin, decimal? SalaryMax, string? JobDescription,
    int? HiringManagerEmployeeId, string Status = "Open");

public record HrCandidateDto(
    int Id, int? VacancyId, string Name, string? Phone, string? Email, string? Source,
    string? Experience, string Stage, DateTime? OfferJoiningDate, decimal? OfferSalary,
    int? ConvertedEmployeeId);

public record HrCandidateInput(
    int? VacancyId, string Name, string? Phone, string? Email, string? CvUrl,
    string? Source, string? Experience, string? InterviewNotes, string Stage,
    DateTime? OfferJoiningDate, decimal? OfferSalary);

public record HrOnboardingDto(int Id, string Title, bool Done, DateTime? DoneAt);

public record HrLetterDto(int Id, int EmployeeId, string Kind, string Body, DateTime CreatedAt);

public record HrLetterInput(int EmployeeId, string Kind);

public record HrResignationDto(
    int Id, int EmployeeId, string EmployeeName, DateOnly ResignationDate, int NoticeDays,
    DateOnly LastWorkingDay, string? Reason, string Status);

public record HrResignationInput(
    int EmployeeId, DateOnly ResignationDate, int NoticeDays, DateOnly LastWorkingDay,
    string? Reason);

public record HrFnfDto(
    int Id, int EmployeeId, decimal SalaryDue, decimal LopAmount, decimal LeaveEncashment,
    decimal Deductions, decimal AssetsRecovered, decimal Payable, string Status);

public record HrFnfInput(
    int EmployeeId, int? ResignationId, decimal SalaryDue, decimal LopAmount,
    decimal LeaveEncashment, decimal Deductions, decimal AssetsRecovered);

public record HrDeploymentDto(
    int Id, int EmployeeId, string EmployeeName, string EventName, DateOnly EventDate,
    string? Venue, string RoleOnSite, string? ShiftName, TimeSpan? ReportingTime,
    TimeSpan? ClosingTime, string AttendanceStatus, decimal OvertimeHours,
    decimal IncentiveAmount);

public record HrDeploymentInput(
    int EmployeeId, string EventName, DateOnly EventDate, string? Venue, string RoleOnSite,
    string? ShiftName, TimeSpan? ReportingTime, TimeSpan? ClosingTime,
    string AttendanceStatus, decimal OvertimeHours, decimal IncentiveAmount);

public record HrAssetDto(
    int Id, int EmployeeId, string AssetType, string? SerialNo, DateOnly IssueDate,
    string Condition, DateOnly? ReturnDate);

public record HrAssetInput(
    int EmployeeId, string AssetType, string? SerialNo, DateOnly IssueDate, string Condition);

public record HrDashboardDto(
    int Headcount,
    int PresentToday,
    int AbsentToday,
    int OnLeaveToday,
    int WfhToday,
    int LateToday,
    int JoinersThisMonth,
    int ExitsThisMonth,
    int PendingApprovals,
    int PendingExpenses,
    int OpenTickets,
    int ActiveGoals,
    int InterviewsThisWeek,
    int HolidaysUpcoming);

public record HrOrgNodeDto(
    int Id, string EmployeeCode, string Name, string Title, string Department,
    IReadOnlyList<HrOrgNodeDto> Children);

public record HrPunchDto(
    int Id, int EmployeeId, string EmployeeName, DateTime At, string Kind,
    double? Latitude, double? Longitude, string? Address);

public record HrPunchInput(
    int? EmployeeId, string Kind, double? Latitude, double? Longitude, string? Address, string? Device);

public record HrHolidayDto(int Id, string Name, DateOnly OnDate, bool Optional, string? Locations);
public record HrHolidayInput(string Name, DateOnly OnDate, bool Optional, string? Locations);

public record HrPolicyDto(int Id, string Title, string Category, string Body, DateOnly EffectiveFrom);
public record HrPolicyInput(string Title, string Category, string Body, DateOnly EffectiveFrom, string? AttachmentUrl);

public record HrAnnouncementDto(int Id, string Title, string Body, DateTime? PinUntil, string Audience);
public record HrAnnouncementInput(string Title, string Body, DateTime? PinUntil, string Audience);

public record HrExpenseDto(
    int Id, int EmployeeId, string EmployeeName, DateOnly ClaimDate, string Category,
    decimal Amount, string? Description, string Status);
public record HrExpenseInput(int EmployeeId, DateOnly ClaimDate, string Category, decimal Amount, string? Description, string? BillUrl);

public record HrTicketDto(
    int Id, int EmployeeId, string EmployeeName, string Subject, string Category,
    string Priority, string Body, string Status, int? AssignedToEmployeeId);
public record HrTicketInput(int EmployeeId, string Subject, string Category, string Priority, string Body);

public record HrCycleDto(int Id, string Name, DateOnly FromDate, DateOnly ToDate, string Status);
public record HrCycleInput(string Name, DateOnly FromDate, DateOnly ToDate);

public record HrGoalDto(
    int Id, int EmployeeId, string EmployeeName, int? CycleId, string Title, string? Kra,
    string? Target, decimal Weight, decimal Progress, string Status);
public record HrGoalInput(
    int EmployeeId, int? CycleId, string Title, string? Kra, string? Target, decimal Weight);

public record HrAppraisalDto(
    int Id, int CycleId, int EmployeeId, string EmployeeName, decimal? SelfScore,
    decimal? ManagerScore, string? Rating, string? Comments, string Status);
public record HrAppraisalInput(int CycleId, int EmployeeId, decimal? SelfScore, decimal? ManagerScore, string? Rating, string? Comments);

public record HrInterviewDto(
    int Id, int CandidateId, string CandidateName, DateTime ScheduledAt, string? Panel,
    string Mode, decimal? Score, string? Recommendation, string Status, string? Notes);
public record HrInterviewInput(
    int CandidateId, DateTime ScheduledAt, string? Panel, string Mode, decimal? Score,
    string? Recommendation, string? Notes);

public record HrTrainingDto(
    int Id, string Title, string? Trainer, DateOnly FromDate, DateOnly ToDate, string? Venue, string Status, int Enrolments);
public record HrTrainingInput(string Title, string? Trainer, DateOnly FromDate, DateOnly ToDate, string? Venue);

public record HrLifecycleDto(
    int Id, int EmployeeId, string EmployeeName, string Kind, DateOnly EffectiveOn,
    string? FromValue, string? ToValue, string? Notes, string Status);
public record HrLifecycleInput(
    int EmployeeId, string Kind, DateOnly EffectiveOn, string? FromValue, string? ToValue, string? Notes);

public record HrMeDto(
    HrEmployeeDto Employee,
    IReadOnlyList<HrLeaveBalanceDto> Balances,
    IReadOnlyList<HrPayslipDto> RecentPayslips);
