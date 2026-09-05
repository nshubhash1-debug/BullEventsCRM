using System.ComponentModel.DataAnnotations.Schema;

namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Bull Group HRMS — people, time, pay and hiring.
 *
 * Shaped from the requirement workbook: company-wise data, employee lifecycle,
 * attendance that feeds payroll, leave with manager then HR, and Events-specific
 * deployment. Catalogue standing (User) stays the login; this is the HR record.
 * ------------------------------------------------------------------ */

public static class EmploymentStatuses
{
    public const string Active = "Active";
    public const string Probation = "Probation";
    public const string Confirmed = "Confirmed";
    public const string Resigned = "Resigned";
    public const string NoticePeriod = "NoticePeriod";
    public const string Relieved = "Relieved";
    public const string Terminated = "Terminated";

    public static readonly string[] All =
        [Active, Probation, Confirmed, Resigned, NoticePeriod, Relieved, Terminated];

    public static readonly string[] OnRolls = [Active, Probation, Confirmed, NoticePeriod];
}

public static class EmploymentTypes
{
    public const string Permanent = "Permanent";
    public const string Contract = "Contract";
    public const string Intern = "Intern";
    public const string Temporary = "Temporary";
    public const string BlueCollar = "BlueCollar";

    public static readonly string[] All =
        [Permanent, Contract, Intern, Temporary, BlueCollar];
}

public static class CollarTypes
{
    public const string White = "WhiteCollar";
    public const string Production = "BlueCollar";

    public static readonly string[] All = [White, Production];
}

public static class AttendanceDayStatuses
{
    public const string Present = "Present";
    public const string Absent = "Absent";
    public const string Late = "Late";
    public const string EarlyLeaving = "EarlyLeaving";
    public const string HalfDay = "HalfDay";
    public const string WorkFromHome = "WorkFromHome";
    public const string OnDuty = "OnDuty";
    public const string Holiday = "Holiday";
    public const string WeeklyOff = "WeeklyOff";
    public const string Leave = "Leave";

    public static readonly string[] All =
    [
        Present, Absent, Late, EarlyLeaving, HalfDay, WorkFromHome, OnDuty,
        Holiday, WeeklyOff, Leave,
    ];
}

public static class HrRequestStatuses
{
    public const string Draft = "Draft";
    public const string PendingManager = "PendingManager";
    public const string PendingHr = "PendingHr";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
        [Draft, PendingManager, PendingHr, Approved, Rejected, Cancelled];
}

public static class RecruitmentStages
{
    public const string Applied = "Applied";
    public const string Shortlisted = "Shortlisted";
    public const string Interview = "Interview";
    public const string Selected = "Selected";
    public const string Offered = "Offered";
    public const string Joined = "Joined";
    public const string Rejected = "Rejected";

    public static readonly string[] All =
        [Applied, Shortlisted, Interview, Selected, Offered, Joined, Rejected];
}

public static class PayrollRunStatuses
{
    public const string Draft = "Draft";
    public const string Locked = "Locked";
    public const string Processed = "Processed";
    public const string Reviewed = "Reviewed";

    public static readonly string[] All = [Draft, Locked, Processed, Reviewed];
}

public class HrDepartment : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Code { get; set; }
    public string? Location { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>The HR master — one row per person on the rolls.</summary>
public class HrEmployee : ITenantScoped, IAuditable, ISoftDeletable, IOwnedRecord
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? PhotoUrl { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public string? EmergencyContactName { get; set; }
    public string? EmergencyContactPhone { get; set; }

    public int? DepartmentId { get; set; }
    public HrDepartment? Department { get; set; }
    public int? DesignationId { get; set; }
    public Designation? Designation { get; set; }
    public int? ManagerEmployeeId { get; set; }
    public HrEmployee? Manager { get; set; }

    public int? BranchId { get; set; }
    public Branch? Branch { get; set; }
    public string? Location { get; set; }

    public DateTime JoiningDate { get; set; } = DateTime.UtcNow.Date;
    public string EmploymentType { get; set; } = EmploymentTypes.Permanent;
    public string Status { get; set; } = EmploymentStatuses.Probation;
    public string CollarType { get; set; } = CollarTypes.White;

    /// <summary>CRM login this person uses for ESS, if they have one.</summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Sharing owner — the linked user, else the creator.</summary>
    public int? OwnerId { get; set; }

    public int? ShiftId { get; set; }
    public HrShift? Shift { get; set; }

    public string? BankAccount { get; set; }
    public string? Ifsc { get; set; }
    public string? Pan { get; set; }
    public string? Aadhaar { get; set; }
    public string? Uan { get; set; }
    public string? EsicIp { get; set; }

    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int? DeletedById { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrEmployee> Reports { get; set; } = new List<HrEmployee>();
}

public class HrEmployeeDocument : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ReminderDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrShift : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int GraceMinutes { get; set; } = 15;
    public string WeeklyOff { get; set; } = "Sunday";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrAttendance : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public DateOnly WorkDate { get; set; }
    public TimeSpan? InTime { get; set; }
    public TimeSpan? OutTime { get; set; }
    public string Status { get; set; } = AttendanceDayStatuses.Present;
    public bool IsLate { get; set; }
    public bool LeftEarly { get; set; }
    public string? Source { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrAttendanceCorrection : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public DateOnly WorkDate { get; set; }
    public string Reason { get; set; } = string.Empty;
    public TimeSpan? CorrectInTime { get; set; }
    public TimeSpan? CorrectOutTime { get; set; }
    public string? AttachmentUrl { get; set; }
    public string Status { get; set; } = HrRequestStatuses.PendingManager;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrLeaveType : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal MonthlyEntitlement { get; set; }
    public bool Paid { get; set; } = true;
    public bool CarryForward { get; set; }
    public int ApprovalLevels { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    /* ---------------- how a period ends ---------------- */
    //
    // Carry-forward without a cap is how a company wakes up owing somebody
    // eleven months of leave. The two ceilings below are what stop that, and
    // they are on the type because they differ by type: earned leave carries,
    // sick leave lapses, casual leave usually does both badly.

    /// <summary>The most that may cross into the next period. Zero means no cap.</summary>
    public decimal MaxCarryForward { get; set; }

    /// <summary>The most that may stand at once, however it was earned. Zero means no cap.</summary>
    public decimal MaxBalance { get; set; }

    /// <summary>Whether unused days may be paid out instead of carried.</summary>
    public bool AllowEncashment { get; set; }

    /// <summary>
    /// Whether this is the bucket compensatory days land in.
    ///
    /// An events company works most Sundays in season, so this is the type
    /// that actually moves; it is flagged rather than matched on its name.
    /// </summary>
    public bool IsCompensatory { get; set; }

    /// <summary>Whether an employee may go negative on it. Rarely wanted.</summary>
    public bool AllowNegativeBalance { get; set; }

    /// <summary>Days after which a compensatory day earned expires. Zero never expires.</summary>
    public int CompensatoryExpiryDays { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrLeaveBalance : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public int LeaveTypeId { get; set; }
    public HrLeaveType? LeaveType { get; set; }
    public int Year { get; set; }
    public decimal Opening { get; set; }
    public decimal Accrued { get; set; }
    public decimal Taken { get; set; }
    [NotMapped]
    public decimal Closing => Opening + Accrued - Taken;
}

public class HrLeaveRequest : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public int LeaveTypeId { get; set; }
    public HrLeaveType? LeaveType { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public bool HalfDay { get; set; }
    public string? Reason { get; set; }
    public string? AttachmentUrl { get; set; }
    public string Status { get; set; } = HrRequestStatuses.PendingManager;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrSalaryStructure : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public decimal Basic { get; set; }
    public decimal Hra { get; set; }
    public decimal Allowances { get; set; }
    public decimal Incentive { get; set; }
    public decimal Bonus { get; set; }
    public decimal OvertimeRate { get; set; }
    public decimal Deductions { get; set; }
    [NotMapped]
    public decimal Gross => Basic + Hra + Allowances + Incentive + Bonus;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrSalaryRevision : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public decimal OldGross { get; set; }
    public decimal NewGross { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrPayrollRun : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public string Status { get; set; } = PayrollRunStatuses.Draft;
    public DateTime? AttendanceLockedAt { get; set; }
    public DateTime? ProcessedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
    public ICollection<HrPayslip> Slips { get; set; } = new List<HrPayslip>();
}

public class HrPayslip : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int PayrollRunId { get; set; }
    public HrPayrollRun? Run { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public decimal Gross { get; set; }
    public decimal LopDays { get; set; }
    public decimal LopAmount { get; set; }
    public decimal OtherDeductions { get; set; }
    public decimal PfEmployee { get; set; }
    public decimal PfEmployer { get; set; }
    public decimal EsicEmployee { get; set; }
    public decimal EsicEmployer { get; set; }
    public decimal Incentive { get; set; }
    public decimal OvertimeAmount { get; set; }
    public decimal Net { get; set; }

    /* ---------------- statutory detail ---------------- */
    //
    // Everything below is what a payslip has to be able to explain and what the
    // monthly returns are filed from. It was absent while the run carried its
    // rates as literals, and a payslip that states a PF figure it cannot break
    // down is one an employee cannot check.

    /// <summary>Basic after loss of pay — the wage PF was actually taken on.</summary>
    public decimal PfWage { get; set; }

    /// <summary>The pension slice of the employer's contribution. Capped at the EPS ceiling.</summary>
    public decimal EpsEmployer { get; set; }

    /// <summary>The provident-fund slice — the employer's total less the pension slice.</summary>
    public decimal EpfEmployer { get; set; }

    /// <summary>Employees' Deposit Linked Insurance, borne by the employer.</summary>
    public decimal Edli { get; set; }

    /// <summary>EPF administration charges, borne by the employer.</summary>
    public decimal PfAdminCharges { get; set; }

    /// <summary>State professional tax for this month.</summary>
    public decimal ProfessionalTax { get; set; }

    public decimal LwfEmployee { get; set; }
    public decimal LwfEmployer { get; set; }

    /// <summary>Income tax deducted this month, from the year's projection.</summary>
    public decimal Tds { get; set; }

    /// <summary>The projection it came from, kept so the payslip can show its working.</summary>
    public decimal ProjectedAnnualTaxable { get; set; }
    public decimal ProjectedAnnualTax { get; set; }

    /// <summary>House rent exempted under section 10(13A), where the regime allows it.</summary>
    public decimal HraExemption { get; set; }

    /// <summary>Which regime this month was deducted under.</summary>
    public string? TaxRegime { get; set; }

    /// <summary>Gratuity earned this month. A liability, not a payment.</summary>
    public decimal GratuityAccrual { get; set; }

    /// <summary>Gross plus every employer contribution — what the month really cost.</summary>
    public decimal CostToCompany { get; set; }

    /// <summary>Everything taken off, so the payslip does not have to re-add them.</summary>
    public decimal TotalDeductions { get; set; }

    /// <summary>Gross after loss of pay. What the statutory rates were applied to.</summary>
    public decimal PayableGross { get; set; }

    /// <summary>
    /// What the totals above are made of.
    ///
    /// Written when the run is processed and never recomputed, so a slip
    /// reprinted a year later shows the components that were paid rather than
    /// the ones that exist now.
    /// </summary>
    public ICollection<HrPayslipLine> Lines { get; set; } = new List<HrPayslipLine>();
}

public class HrVacancy : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Position { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public HrDepartment? Department { get; set; }
    public string? Location { get; set; }
    public string? Experience { get; set; }
    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? JobDescription { get; set; }
    public int? HiringManagerEmployeeId { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrCandidate : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int? VacancyId { get; set; }
    public HrVacancy? Vacancy { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? CvUrl { get; set; }
    public string? Source { get; set; }
    public string? Experience { get; set; }
    public string? InterviewNotes { get; set; }
    public string Stage { get; set; } = RecruitmentStages.Applied;
    public DateTime? OfferJoiningDate { get; set; }
    public decimal? OfferSalary { get; set; }
    public int? ConvertedEmployeeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrOnboardingItem : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string Title { get; set; } = string.Empty;
    public bool Done { get; set; }
    public DateTime? DoneAt { get; set; }

    /* ---------------- what a template puts on it ---------------- */
    //
    // A title and a tick is enough for the second employee and useless by the
    // twentieth: nobody knows whose job it is, when it should have happened, or
    // whether the person can start without it.

    /// <summary>Onboarding or Separation — see <see cref="ChecklistKinds"/>.</summary>
    public string Kind { get; set; } = ChecklistKinds.Onboarding;

    /// <summary>Whose job it is — see <see cref="ChecklistOwners"/>.</summary>
    public string Owner { get; set; } = ChecklistOwners.Hr;

    public string? Description { get; set; }

    /// <summary>When it should have happened, from the joining or last working day.</summary>
    public DateOnly? DueOn { get; set; }

    /// <summary>Whether the person cannot be marked started or relieved without it.</summary>
    public bool IsBlocking { get; set; }

    /// <summary>The template task this came from, when it came from one.</summary>
    public int? ChecklistTaskId { get; set; }

    public int SortOrder { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>Not done and past its date.</summary>
    public bool IsOverdue(DateOnly today) => !Done && DueOn is DateOnly due && today > due;
}

public class HrLetter : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public static class HrLetterKinds
{
    public static readonly string[] All =
    [
        "Offer", "Appointment", "Confirmation", "Increment", "Promotion",
        "Transfer", "Warning", "Experience", "Relieving",
    ];
}

public class HrResignation : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public DateOnly ResignationDate { get; set; }
    public int NoticeDays { get; set; } = 30;
    public DateOnly LastWorkingDay { get; set; }
    public string? Reason { get; set; }
    public string Status { get; set; } = HrRequestStatuses.PendingManager;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrFullAndFinal : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public int? ResignationId { get; set; }
    public decimal SalaryDue { get; set; }
    public decimal LopAmount { get; set; }
    public decimal LeaveEncashment { get; set; }
    public decimal Deductions { get; set; }
    public decimal AssetsRecovered { get; set; }
    public decimal Payable { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrEventDeployment : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateOnly EventDate { get; set; }
    public string? Venue { get; set; }
    public string RoleOnSite { get; set; } = string.Empty;
    public string? ShiftName { get; set; }
    public TimeSpan? ReportingTime { get; set; }
    public TimeSpan? ClosingTime { get; set; }
    public string AttendanceStatus { get; set; } = AttendanceDayStatuses.Present;
    public decimal OvertimeHours { get; set; }
    public decimal IncentiveAmount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrAssetIssue : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string AssetType { get; set; } = string.Empty;
    public string? SerialNo { get; set; }
    public DateOnly IssueDate { get; set; }
    public string Condition { get; set; } = "Good";
    public DateOnly? ReturnDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}
