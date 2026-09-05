namespace BullEvents.Api.Models;

/* ------------------------------------------------------------------ *
 * Advanced HR — feature map aligned with Horilla HR and Frappe HR
 * (org, geo attendance, PMS, expenses, helpdesk, policies, holidays,
 * training, interviews, employee lifecycle). Implemented natively on
 * this stack; those products cannot be loaded as Python/Django apps here.
 * ------------------------------------------------------------------ */

public static class HrPunchKinds
{
    public const string In = "In";
    public const string Out = "Out";
    public static readonly string[] All = [In, Out];
}

public static class HrTicketStatuses
{
    public const string Open = "Open";
    public const string InProgress = "InProgress";
    public const string Waiting = "Waiting";
    public const string Resolved = "Resolved";
    public const string Closed = "Closed";
    public static readonly string[] All = [Open, InProgress, Waiting, Resolved, Closed];
}

public static class HrLifecycleKinds
{
    public const string Confirmation = "Confirmation";
    public const string Promotion = "Promotion";
    public const string Transfer = "Transfer";
    public const string Increment = "Increment";
    public const string Demotion = "Demotion";
    public static readonly string[] All =
        [Confirmation, Promotion, Transfer, Increment, Demotion];
}

public class HrHoliday : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly OnDate { get; set; }
    public bool Optional { get; set; }
    public string? Locations { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrPolicy : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Body { get; set; } = string.Empty;
    public DateOnly EffectiveFrom { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public string? AttachmentUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrAnnouncement : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public DateTime? PinUntil { get; set; }
    public string Audience { get; set; } = "All";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrPunch : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public DateTime At { get; set; } = DateTime.UtcNow;
    public string Kind { get; set; } = HrPunchKinds.In;
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? Address { get; set; }
    public string? Device { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrExpenseClaim : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public DateOnly ClaimDate { get; set; }

    /// <summary>Kept as free text for the claims raised before types existed.</summary>
    public string Category { get; set; } = "Travel";

    /// <summary>The type, and with it the limits and the receipt rule.</summary>
    public int? ExpenseClaimTypeId { get; set; }
    public HrExpenseClaimType? ExpenseClaimType { get; set; }

    /// <summary>The trip this was spent on, when it was spent on one.</summary>
    public int? TravelRequestId { get; set; }
    public HrTravelRequest? TravelRequest { get; set; }

    public decimal Amount { get; set; }

    /// <summary>What was actually allowed, which may be less than was claimed.</summary>
    public decimal? ApprovedAmount { get; set; }

    /// <summary>Why less was allowed. Required when it is.</summary>
    public string? ReductionReason { get; set; }

    public string? Description { get; set; }
    public string? BillUrl { get; set; }
    public string Status { get; set; } = HrRequestStatuses.PendingManager;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrHelpdeskTicket : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string Category { get; set; } = "General";
    public string Priority { get; set; } = "Medium";
    public string Body { get; set; } = string.Empty;
    public string Status { get; set; } = HrTicketStatuses.Open;
    public int? AssignedToEmployeeId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrAppraisalCycle : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string Status { get; set; } = "Open";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrGoal : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public int? CycleId { get; set; }
    public HrAppraisalCycle? Cycle { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Kra { get; set; }
    public string? Target { get; set; }
    public decimal Weight { get; set; } = 100;
    public decimal Progress { get; set; }
    public string Status { get; set; } = "InProgress";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrAppraisal : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int CycleId { get; set; }
    public HrAppraisalCycle? Cycle { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    /// <summary>The responsibilities this appraisal scores.</summary>
    public ICollection<HrAppraisalKra> Kras { get; set; } = new List<HrAppraisalKra>();

    /// <summary>The template the responsibilities were taken from.</summary>
    public int? AppraisalTemplateId { get; set; }
    public HrAppraisalTemplate? AppraisalTemplate { get; set; }

    public decimal? SelfScore { get; set; }
    public decimal? ManagerScore { get; set; }

    /// <summary>
    /// The weighted manager score across the responsibilities.
    ///
    /// Stored rather than derived because the weights are copied onto each row
    /// at the time, and a template edited afterwards must not silently restate
    /// last year's rating.
    /// </summary>
    public decimal? FinalScore { get; set; }

    public string? Rating { get; set; }
    public string? Comments { get; set; }
    public string Status { get; set; } = "Draft";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrInterview : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int CandidateId { get; set; }
    public HrCandidate? Candidate { get; set; }

    /// <summary>
    /// Which stage this is, and so which skills the panel is asked to rate.
    ///
    /// Null for interviews booked before rounds existed; those keep the single
    /// score they were given.
    /// </summary>
    public int? InterviewRoundId { get; set; }
    public HrInterviewRound? InterviewRound { get; set; }

    public DateTime ScheduledAt { get; set; }
    public string? Panel { get; set; }
    public string Mode { get; set; } = "InPerson";
    public decimal? Score { get; set; }
    public string? Recommendation { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = "Scheduled";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrTraining : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Trainer { get; set; }
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }
    public string? Venue { get; set; }
    public string Status { get; set; } = "Scheduled";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrTrainingEnrolment : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int TrainingId { get; set; }
    public HrTraining? Training { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string Status { get; set; } = "Enrolled";

    /// <summary>What the trainer made of them — a value from <see cref="TrainingResults"/>.</summary>
    public string Result { get; set; } = TrainingResults.Pending;

    public decimal? Score { get; set; }

    /// <summary>Notes from the trainer, on this attendee.</summary>
    public string? TrainerRemarks { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

public class HrLifecycleEvent : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }
    public string Kind { get; set; } = HrLifecycleKinds.Promotion;
    public DateOnly EffectiveOn { get; set; }
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Notes { get; set; }
    public string Status { get; set; } = HrRequestStatuses.PendingHr;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}
