namespace BullEvents.Api.Models;

/* ====================================================================== *
 * Joining and leaving, from a checklist somebody wrote down
 *
 * An onboarding item existed already — a title and a tick, created by hand
 * for each new joiner. That works for the second employee and stops working
 * at the twentieth, because the list is only as good as whoever remembered
 * it that morning. A template is the same list every time, with an owner
 * against each task and a date it is due relative to the joining day.
 * ====================================================================== */

public static class ChecklistKinds
{
    public const string Onboarding = "Onboarding";
    public const string Separation = "Separation";

    public static readonly string[] All = [Onboarding, Separation];
}

public static class ChecklistOwners
{
    /// <summary>The joiner or leaver themselves.</summary>
    public const string Employee = "Employee";
    public const string Manager = "Manager";
    public const string Hr = "HR";
    public const string It = "IT";
    public const string Finance = "Finance";
    public const string Stores = "Stores";

    public static readonly string[] All = [Employee, Manager, Hr, It, Finance, Stores];
}

/// <summary>
/// A named checklist for joining or leaving.
///
/// Separate templates per kind because the two lists are opposites — one
/// hands things out and the other collects them back — and a single list with
/// half its rows irrelevant is a list people learn to skim.
/// </summary>
public class HrChecklistTemplate : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>A value from <see cref="ChecklistKinds"/>.</summary>
    public string Kind { get; set; } = ChecklistKinds.Onboarding;

    /// <summary>Null applies to everybody; otherwise only this department.</summary>
    public int? DepartmentId { get; set; }
    public HrDepartment? Department { get; set; }

    /// <summary>Null applies to both; otherwise White or Production.</summary>
    public string? CollarType { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrChecklistTask> Tasks { get; set; } = new List<HrChecklistTask>();

    /// <summary>Whether this template is the one for a given person.</summary>
    public bool AppliesTo(int? departmentId, string? collarType) =>
        IsActive
        && (DepartmentId is null || DepartmentId == departmentId)
        && (CollarType is null || CollarType == collarType);
}

public class HrChecklistTask : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int ChecklistTemplateId { get; set; }
    public HrChecklistTemplate? ChecklistTemplate { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>A value from <see cref="ChecklistOwners"/>.</summary>
    public string Owner { get; set; } = ChecklistOwners.Hr;

    /// <summary>
    /// Days from the joining or last working day. Negative is before it.
    ///
    /// A laptop ordered on the joining day arrives in the second week, which is
    /// why the useful tasks have negative offsets.
    /// </summary>
    public int DueOffsetDays { get; set; }

    /// <summary>Whether the person cannot be marked started or relieved without it.</summary>
    public bool IsBlocking { get; set; }

    public int SortOrder { get; set; }
}

/* ====================================================================== *
 * Grievances
 * ====================================================================== */

public static class GrievanceStatuses
{
    public const string Raised = "Raised";
    public const string Acknowledged = "Acknowledged";
    public const string Investigating = "Investigating";
    public const string Resolved = "Resolved";
    public const string Withdrawn = "Withdrawn";
    public const string Escalated = "Escalated";

    public static readonly string[] All =
        [Raised, Acknowledged, Investigating, Resolved, Withdrawn, Escalated];

    /// <summary>States in which the clock is still running.</summary>
    public static readonly string[] Open =
        [Raised, Acknowledged, Investigating, Escalated];
}

public static class GrievanceCategories
{
    public const string Pay = "Pay";
    public const string WorkingConditions = "WorkingConditions";
    public const string Safety = "Safety";
    public const string Harassment = "Harassment";
    public const string Discrimination = "Discrimination";
    public const string Management = "Management";
    public const string Other = "Other";

    public static readonly string[] All =
        [Pay, WorkingConditions, Safety, Harassment, Discrimination, Management, Other];

    /// <summary>
    /// Categories the law requires be handled by a constituted committee, not a
    /// line manager. Routing one of these to the person complained about is the
    /// failure mode worth designing against.
    /// </summary>
    public static readonly string[] RequireCommittee = [Harassment, Discrimination];
}

/// <summary>
/// A complaint, and what was done about it.
///
/// The audit trail matters more than the record: a company asked to show it
/// took a complaint seriously has to be able to show when it was acknowledged,
/// who looked into it and what came of it. So the dates are separate fields
/// rather than a status somebody moved.
/// </summary>
public class HrGrievance : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    /// <summary>A value from <see cref="GrievanceCategories"/>.</summary>
    public string Category { get; set; } = GrievanceCategories.Other;

    public string Subject { get; set; } = string.Empty;
    public string? Details { get; set; }

    /// <summary>Who it is about, when it is about somebody.</summary>
    public int? AgainstEmployeeId { get; set; }

    public int? AssignedToEmployeeId { get; set; }
    public HrEmployee? AssignedToEmployee { get; set; }

    public string Status { get; set; } = GrievanceStatuses.Raised;

    /// <summary>
    /// Whether only the assignee and HR may read it.
    ///
    /// On by default. A grievance that anybody in HR can browse is one nobody
    /// raises twice.
    /// </summary>
    public bool IsConfidential { get; set; } = true;

    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    /// <summary>What was actually done. Required to resolve.</summary>
    public string? Resolution { get; set; }

    /// <summary>What the person who raised it made of the outcome.</summary>
    public bool? ComplainantSatisfied { get; set; }

    public string? AttachmentUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>Days it has been open, or days it took to close.</summary>
    public int AgeInDays =>
        (int)((ResolvedAt ?? DateTime.UtcNow) - CreatedAt).TotalDays;
}

/* ====================================================================== *
 * Skills
 *
 * For an office this is a nice-to-have. For an events company it is how a
 * site gets staffed: the question before every job is who can rig at height,
 * who can drive the tempo, who has actually done a mandap. Answering it from
 * memory is how a crew arrives without an electrician.
 * ====================================================================== */

public static class SkillProficiencies
{
    public const int Aware = 1;
    public const int Working = 2;
    public const int Competent = 3;
    public const int Advanced = 4;
    public const int Expert = 5;

    public static string Describe(int level) => level switch
    {
        1 => "Aware",
        2 => "Working",
        3 => "Competent",
        4 => "Advanced",
        5 => "Expert",
        _ => "—",
    };
}

public class HrSkill : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>A grouping — Rigging, Design, Driving, Client-facing.</summary>
    public string? Category { get; set; }

    /// <summary>
    /// Whether holding this skill requires a certificate that can expire.
    ///
    /// Working at height and driving a goods vehicle both do, and an expired
    /// one is worse than none because everybody assumes it is valid.
    /// </summary>
    public bool RequiresCertification { get; set; }

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrEmployeeSkill : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public int SkillId { get; set; }
    public HrSkill? Skill { get; set; }

    /// <summary>One to five. See <see cref="SkillProficiencies"/>.</summary>
    public int Proficiency { get; set; } = SkillProficiencies.Working;

    public int YearsOfExperience { get; set; }

    /// <summary>Who said so — a manager, a trainer, or nobody yet.</summary>
    public int? AssessedByEmployeeId { get; set; }
    public DateOnly? AssessedOn { get; set; }

    public string? CertificateNumber { get; set; }
    public DateOnly? CertifiedUntil { get; set; }

    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>Whether a required certificate has run out.</summary>
    public bool CertificateExpired(DateOnly today) =>
        CertifiedUntil is DateOnly until && today > until;
}

/* ====================================================================== *
 * Expenses and travel
 * ====================================================================== */

/// <summary>
/// A category of claim, with the rules that go with it.
///
/// The claim already carried a category as free text, which means the limit
/// lives in somebody's head and is applied differently by each approver.
/// </summary>
public class HrExpenseClaimType : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>The most that may be claimed on one bill. Zero means no limit.</summary>
    public decimal PerClaimLimit { get; set; }

    /// <summary>The most that may be claimed in a month. Zero means no limit.</summary>
    public decimal MonthlyLimit { get; set; }

    /// <summary>Whether a bill has to be attached.</summary>
    public bool RequiresReceipt { get; set; } = true;

    /// <summary>Below this a receipt is not insisted on. Zero insists always.</summary>
    public decimal ReceiptWaivedBelow { get; set; }

    /// <summary>Whether the claim has to hang off an approved travel request.</summary>
    public bool RequiresTravelRequest { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public static class TravelStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
        [Draft, PendingApproval, Approved, Rejected, Completed, Cancelled];
}

/// <summary>
/// Somebody going somewhere for work.
///
/// A destination wedding takes a crew of fifteen to Udaipur for four days,
/// and the difference between that being planned and being improvised is this
/// record: who is going, when, what it is expected to cost, and how much they
/// were given up front.
/// </summary>
public class HrTravelRequest : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    public string Purpose { get; set; } = string.Empty;

    /// <summary>The event this is for, when it is for one.</summary>
    public int? LeadId { get; set; }

    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public string? Destination { get; set; }

    public decimal EstimatedCost { get; set; }

    /// <summary>Money handed over before departure, recovered against the claims.</summary>
    public decimal AdvanceRequested { get; set; }
    public decimal AdvancePaid { get; set; }

    public string Status { get; set; } = TravelStatuses.PendingApproval;
    public string? DecisionNote { get; set; }
    public DateTime? DecidedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrTravelLeg> Legs { get; set; } = new List<HrTravelLeg>();

    public int Nights => Math.Max(0, ToDate.DayNumber - FromDate.DayNumber);
}

public class HrTravelLeg : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int TravelRequestId { get; set; }
    public HrTravelRequest? TravelRequest { get; set; }

    public DateOnly OnDate { get; set; }
    public string From { get; set; } = string.Empty;
    public string To { get; set; } = string.Empty;

    /// <summary>Train, Flight, Bus, Road — how they are getting there.</summary>
    public string Mode { get; set; } = "Train";

    public decimal EstimatedCost { get; set; }
    public string? Notes { get; set; }
    public int SortOrder { get; set; }
}

/* ====================================================================== *
 * Timesheets
 * ====================================================================== */

public static class TimesheetStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly string[] All = [Draft, Submitted, Approved, Rejected];
}

/// <summary>
/// A week of somebody's hours, against the events they went to.
///
/// Attendance says they were at work; a timesheet says which job to charge it
/// to. For an events company those are different questions and only the second
/// one tells you whether an event made money — a wedding that took four crew
/// three days longer than quoted looks fine on attendance and terrible here.
/// </summary>
public class HrTimesheet : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    /// <summary>The Monday the week starts on.</summary>
    public DateOnly WeekStarting { get; set; }

    public string Status { get; set; } = TimesheetStatuses.Draft;

    /// <summary>Totalled from the lines when they are written.</summary>
    public decimal TotalHours { get; set; }

    /// <summary>The part that can be charged to an event.</summary>
    public decimal BillableHours { get; set; }

    public DateTime? SubmittedAt { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public string? DecisionNote { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrTimesheetLine> Lines { get; set; } = new List<HrTimesheetLine>();
}

public class HrTimesheetLine : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int TimesheetId { get; set; }
    public HrTimesheet? Timesheet { get; set; }

    public DateOnly OnDate { get; set; }

    /// <summary>The event the hours belong to. Null is overhead.</summary>
    public int? LeadId { get; set; }

    /// <summary>Load-in, Rehearsal, Event day, Load-out, Workshop, Office.</summary>
    public string Activity { get; set; } = string.Empty;

    public decimal Hours { get; set; }

    /// <summary>
    /// Whether it can be charged to the event.
    ///
    /// Travel to a venue usually is; a design revision after the client changed
    /// their mind usually is not, and telling them apart is the whole reason a
    /// timesheet is kept.
    /// </summary>
    public bool IsBillable { get; set; } = true;

    public string? Notes { get; set; }
}
