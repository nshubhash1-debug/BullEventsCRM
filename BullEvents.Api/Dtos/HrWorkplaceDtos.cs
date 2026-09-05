namespace BullEvents.Api.Dtos;

/* ---------------- checklists ---------------- */

public record HrChecklistTaskDto(
    int Id, string Title, string? Description, string Owner,
    /// <summary>Days from the joining or last working day. Negative is before it.</summary>
    int DueOffsetDays,
    bool IsBlocking, int SortOrder);

public record HrChecklistTemplateDto(
    int Id, string Name, string Kind,
    int? DepartmentId, string? DepartmentName, string? CollarType,
    string? Notes, bool IsActive, int BlockingTasks,
    IReadOnlyList<HrChecklistTaskDto> Tasks);

public record HrChecklistTaskInput(
    string Title, string? Description, string Owner, int DueOffsetDays, bool IsBlocking);

public record HrChecklistTemplateInput(
    int? Id, string Name, string Kind, int? DepartmentId, string? CollarType,
    string? Notes, bool IsActive,
    IReadOnlyList<HrChecklistTaskInput> Tasks);

public record HrApplyChecklistInput(
    int EmployeeId, string Kind,
    /// <summary>Null picks the template that fits the person most closely.</summary>
    int? ChecklistTemplateId,
    /// <summary>Null uses the joining date, or today for a leaver.</summary>
    DateOnly? AnchorDate);

public record HrChecklistItemDto(
    int Id, string Title, string? Description, string Owner,
    DateOnly? DueOn, bool Done, DateTime? DoneAt, bool IsBlocking, bool IsOverdue);

/// <summary>
/// Somebody's checklist, and the one sentence that matters: whether anything
/// is stopping them starting, or being relieved.
/// </summary>
public record HrChecklistRunDto(
    int EmployeeId, string EmployeeName, string Kind, int TasksAdded,
    int Total, int Done, int Overdue, int Blocking,
    string? BlockedBecause,
    IReadOnlyList<HrChecklistItemDto> Items);

/* ---------------- grievances ---------------- */

public record HrGrievanceDto(
    int Id, int EmployeeId, string EmployeeName, string Category,
    string Subject, string? Details,
    int? AgainstEmployeeId, int? AssignedToEmployeeId, string? AssignedToName,
    string Status, bool IsConfidential,
    DateTime? AcknowledgedAt, DateTime? ResolvedAt,
    string? Resolution, bool? ComplainantSatisfied, string? AttachmentUrl,
    int AgeInDays, DateTime CreatedAt,
    /// <summary>Whether the law requires a constituted committee rather than a manager.</summary>
    bool NeedsCommittee);

public record HrGrievanceInput(
    int EmployeeId, string Category, string Subject, string? Details,
    int? AgainstEmployeeId, bool IsConfidential, string? AttachmentUrl);

public record HrGrievanceResolveInput(
    string Resolution, bool? ComplainantSatisfied, bool Withdrawn);

/* ---------------- skills ---------------- */

public record HrSkillDto(
    int Id, string Name, string? Category, bool RequiresCertification,
    string? Notes, bool IsActive,
    int PeopleWithIt, decimal? AverageProficiency);

public record HrSkillInput(
    int? Id, string Name, string? Category, bool RequiresCertification,
    string? Notes, bool IsActive);

public record HrEmployeeSkillDto(
    int Id, int EmployeeId, string EmployeeName,
    int SkillId, string SkillName, string? Category,
    int Proficiency, string ProficiencyLabel,
    int YearsOfExperience, DateOnly? AssessedOn,
    string? CertificateNumber, DateOnly? CertifiedUntil,
    bool CertificateExpired, string? Notes);

public record HrEmployeeSkillInput(
    int EmployeeId, int SkillId, int Proficiency, int YearsOfExperience,
    int? AssessedByEmployeeId, string? CertificateNumber, DateOnly? CertifiedUntil,
    string? Notes);

/// <summary>
/// Who can do a thing — the question asked before every job.
///
/// <see cref="ExpiredCertificates"/> counts people whose certificate has run
/// out; they are reported rather than hidden, because somebody has to decide
/// whether to send them.
/// </summary>
public record HrSkillSearchDto(
    int SkillId, string SkillName, int MinimumProficiency,
    int Found, int ExpiredCertificates,
    IReadOnlyList<HrEmployeeSkillDto> People);

/* ---------------- expenses ---------------- */

public record HrExpenseTypeDto(
    int Id, string Name, string? Description,
    decimal PerClaimLimit, decimal MonthlyLimit,
    bool RequiresReceipt, decimal ReceiptWaivedBelow, bool RequiresTravelRequest,
    bool IsActive);

public record HrExpenseTypeInput(
    int? Id, string Name, string? Description,
    decimal PerClaimLimit, decimal MonthlyLimit,
    bool RequiresReceipt, decimal ReceiptWaivedBelow, bool RequiresTravelRequest,
    bool IsActive);

/// <summary>What a claim would run into, answered while it is still being typed.</summary>
public record HrExpenseCheckDto(
    int ExpenseClaimTypeId, string TypeName,
    decimal Amount, decimal AlreadyClaimedThisMonth, decimal? RemainingThisMonth,
    bool Allowed, IReadOnlyList<string> Problems);

/* ---------------- travel ---------------- */

public record HrTravelLegDto(
    int Id, DateOnly OnDate, string From, string To, string Mode,
    decimal EstimatedCost, string? Notes);

public record HrTravelDto(
    int Id, int EmployeeId, string EmployeeName, string Purpose, int? LeadId,
    DateOnly FromDate, DateOnly ToDate, int Nights, string? Destination,
    decimal EstimatedCost, decimal AdvanceRequested, decimal AdvancePaid,
    string Status, string? DecisionNote, DateTime? DecidedAt,
    IReadOnlyList<HrTravelLegDto> Legs);

public record HrTravelLegInput(
    DateOnly OnDate, string From, string To, string Mode,
    decimal EstimatedCost, string? Notes);

public record HrTravelInput(
    int EmployeeId, string Purpose, int? LeadId,
    DateOnly FromDate, DateOnly ToDate, string? Destination,
    decimal EstimatedCost, decimal AdvanceRequested,
    IReadOnlyList<HrTravelLegInput>? Legs);

/* ---------------- timesheets ---------------- */

public record HrTimesheetLineDto(
    int Id, DateOnly OnDate, int? LeadId, string Activity,
    decimal Hours, bool IsBillable, string? Notes);

public record HrTimesheetDto(
    int Id, int EmployeeId, string EmployeeName, DateOnly WeekStarting,
    string Status, decimal TotalHours, decimal BillableHours,
    /// <summary>What share of the week could be charged to an event.</summary>
    decimal BillablePercent,
    DateTime? SubmittedAt, DateTime? ApprovedAt, string? DecisionNote,
    IReadOnlyList<HrTimesheetLineDto> Lines);

public record HrTimesheetLineInput(
    DateOnly OnDate, int? LeadId, string Activity, decimal Hours,
    bool IsBillable, string? Notes);

public record HrTimesheetInput(
    int EmployeeId, DateOnly WeekStarting, bool Submit,
    IReadOnlyList<HrTimesheetLineInput> Lines);

public record HrActivityHoursDto(string Activity, decimal Hours);

/// <summary>
/// Hours charged to one event.
///
/// The number that says whether it made money — attendance shows the crew were
/// at work, this shows the days they spent on this job in particular.
/// </summary>
public record HrEventHoursDto(
    int LeadId, int People, decimal TotalHours, decimal BillableHours,
    IReadOnlyList<HrActivityHoursDto> ByActivity);
