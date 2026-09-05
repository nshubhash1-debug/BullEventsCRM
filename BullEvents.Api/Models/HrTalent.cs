namespace BullEvents.Api.Models;

/* ====================================================================== *
 * Recruitment, with the decisions in it
 *
 * What existed was a vacancy, a candidate and an interview — enough to
 * track a pipeline and nothing more. Missing was every place a decision
 * gets made: who agreed the headcount, who asked for the role, what each
 * panellist actually thought, what was offered, and who brought the person
 * in. Those are the records somebody asks for six months later, and a
 * pipeline that cannot answer them is a list, not a process.
 * ====================================================================== */

public static class RequisitionStatuses
{
    public const string Draft = "Draft";
    public const string PendingApproval = "PendingApproval";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Filled = "Filled";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All =
        [Draft, PendingApproval, Approved, Rejected, Filled, Cancelled];
}

public static class OfferStatuses
{
    public const string Draft = "Draft";
    public const string Sent = "Sent";
    public const string Accepted = "Accepted";
    public const string Declined = "Declined";
    public const string Withdrawn = "Withdrawn";
    public const string Lapsed = "Lapsed";

    public static readonly string[] All =
        [Draft, Sent, Accepted, Declined, Withdrawn, Lapsed];
}

public static class ReferralStatuses
{
    public const string Submitted = "Submitted";
    public const string InProcess = "InProcess";
    public const string Hired = "Hired";
    public const string BonusDue = "BonusDue";
    public const string BonusPaid = "BonusPaid";
    public const string NotHired = "NotHired";

    public static readonly string[] All =
        [Submitted, InProcess, Hired, BonusDue, BonusPaid, NotHired];
}

/// <summary>
/// Headcount somebody has agreed to pay for, by department and role.
///
/// An events company hires in waves — forty crew before the season and
/// nobody in March — and the difference between a plan and a wish is that a
/// plan has a number and a budget against it. Requisitions are checked
/// against this, so a manager asking for a fifth designer when four were
/// agreed finds out at the point of asking rather than at the offer.
/// </summary>
public class HrStaffingPlan : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;
    public DateOnly FromDate { get; set; }
    public DateOnly ToDate { get; set; }

    public string Status { get; set; } = "Draft";
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrStaffingPlanLine> Lines { get; set; } = new List<HrStaffingPlanLine>();

    public bool Covers(DateOnly date) => date >= FromDate && date <= ToDate;
}

public class HrStaffingPlanLine : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int StaffingPlanId { get; set; }
    public HrStaffingPlan? StaffingPlan { get; set; }

    public int? DepartmentId { get; set; }
    public HrDepartment? Department { get; set; }

    /// <summary>The role, as a title rather than a designation record — plans
    /// are written before the designation list catches up.</summary>
    public string Position { get; set; } = string.Empty;

    /// <summary>How many of them the plan allows.</summary>
    public int Headcount { get; set; }

    /// <summary>What one of them is budgeted to cost a year.</summary>
    public decimal BudgetPerHead { get; set; }

    public string? Notes { get; set; }
}

/// <summary>
/// A manager asking for a role to be opened.
///
/// Separate from the vacancy because the ask and the advertisement are
/// different things with different owners: a requisition is refused or
/// approved, and a vacancy only exists once it has been approved. Keeping
/// them as one record would mean an unapproved role sitting on the careers
/// page.
/// </summary>
public class HrJobRequisition : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Position { get; set; } = string.Empty;

    public int? DepartmentId { get; set; }
    public HrDepartment? Department { get; set; }

    public int? DesignationId { get; set; }

    /// <summary>How many people are being asked for.</summary>
    public int Headcount { get; set; } = 1;

    /// <summary>
    /// Whether this replaces somebody who left, or adds to the team.
    ///
    /// A replacement does not consume plan headcount — the post was already
    /// budgeted, and counting it twice is how a plan appears breached when it
    /// is not.
    /// </summary>
    public bool IsReplacement { get; set; }

    /// <summary>Who is being replaced, when it is a replacement.</summary>
    public int? ReplacingEmployeeId { get; set; }

    public int? RequestedByEmployeeId { get; set; }
    public HrEmployee? RequestedByEmployee { get; set; }

    public DateOnly? RequiredBy { get; set; }
    public string? Justification { get; set; }
    public string? JobDescription { get; set; }

    public decimal? SalaryMin { get; set; }
    public decimal? SalaryMax { get; set; }
    public string? Location { get; set; }

    /// <summary>The plan this was checked against, when there was one.</summary>
    public int? StaffingPlanId { get; set; }
    public HrStaffingPlan? StaffingPlan { get; set; }

    public string Status { get; set; } = RequisitionStatuses.PendingApproval;
    public string? DecisionNote { get; set; }
    public DateTime? DecidedAt { get; set; }

    /// <summary>The vacancy approval opened.</summary>
    public int? VacancyId { get; set; }
    public HrVacancy? Vacancy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/// <summary>
/// A named stage of interviewing, and what it is meant to find out.
///
/// A round without a list of what it assesses is a conversation, and two
/// panellists come out of it having judged different things. The skills are
/// what the feedback form is built from.
/// </summary>
public class HrInterviewRound : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Where it sits in the sequence.</summary>
    public int SortOrder { get; set; }

    /// <summary>The average rating below which a candidate does not go on.</summary>
    public decimal PassingScore { get; set; } = 3m;

    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrInterviewSkill> Skills { get; set; } = new List<HrInterviewSkill>();
}

public class HrInterviewSkill : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int InterviewRoundId { get; set; }
    public HrInterviewRound? InterviewRound { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>What it counts for in the round's average. Weights need not sum to anything.</summary>
    public decimal Weight { get; set; } = 1m;

    public int SortOrder { get; set; }
}

/// <summary>
/// One panellist's view of one interview.
///
/// Per panellist rather than one score on the interview, because the useful
/// signal in a panel is the disagreement — three people who all said four and
/// three people who said five, three and four are not the same candidate, and
/// an averaged number hides which one you are looking at.
/// </summary>
public class HrInterviewFeedback : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int InterviewId { get; set; }
    public HrInterview? Interview { get; set; }

    /// <summary>The panellist, when they are on the payroll.</summary>
    public int? PanellistEmployeeId { get; set; }
    public HrEmployee? PanellistEmployee { get; set; }

    /// <summary>Their name, for a panellist who is not an employee.</summary>
    public string PanellistName { get; set; } = string.Empty;

    /// <summary>The weighted average of the skill ratings.</summary>
    public decimal Score { get; set; }

    /// <summary>Hire, No hire, or Hire with reservations.</summary>
    public string? Recommendation { get; set; }

    public string? Strengths { get; set; }
    public string? Concerns { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    public ICollection<HrInterviewSkillRating> Ratings { get; set; } =
        new List<HrInterviewSkillRating>();
}

public class HrInterviewSkillRating : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int InterviewFeedbackId { get; set; }
    public HrInterviewFeedback? InterviewFeedback { get; set; }

    public int? InterviewSkillId { get; set; }

    /// <summary>Copied, so a renamed skill does not rewrite an old scorecard.</summary>
    public string SkillName { get; set; } = string.Empty;

    /// <summary>One to five.</summary>
    public decimal Rating { get; set; }

    public decimal Weight { get; set; } = 1m;
    public string? Comment { get; set; }
}

/// <summary>
/// The offer itself, with its terms and its own life.
///
/// The candidate record carried a salary and a joining date, which is enough
/// until an offer is revised, declined, or lapses unanswered. Those are three
/// different outcomes and a pipeline that cannot tell them apart cannot say
/// why it is not filling roles.
/// </summary>
public class HrJobOffer : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int CandidateId { get; set; }
    public HrCandidate? Candidate { get; set; }

    public string Position { get; set; } = string.Empty;
    public int? DepartmentId { get; set; }
    public int? DesignationId { get; set; }

    /// <summary>Annual cost to company as offered.</summary>
    public decimal AnnualCtc { get; set; }

    /// <summary>The structure the offer was built on, when one was used.</summary>
    public int? PayStructureId { get; set; }

    public DateOnly OfferDate { get; set; }
    public DateOnly? ValidUntil { get; set; }
    public DateOnly? ProposedJoiningDate { get; set; }

    public string Status { get; set; } = OfferStatuses.Draft;

    /// <summary>Why it was declined or withdrawn. The most useful field here.</summary>
    public string? OutcomeReason { get; set; }

    public DateTime? RespondedAt { get; set; }
    public string? Terms { get; set; }

    /// <summary>Which revision this is. A second offer to the same candidate is a fact worth keeping.</summary>
    public int Revision { get; set; } = 1;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }

    /// <summary>Whether the offer has run out of time without an answer.</summary>
    public bool HasLapsed(DateOnly today) =>
        Status == OfferStatuses.Sent && ValidUntil is DateOnly until && today > until;
}

/// <summary>
/// An employee putting somebody forward.
///
/// How most blue-collar hiring in India actually happens: the site supervisor
/// knows six carpenters. The bonus is held until the referred person has
/// stayed long enough to be worth it, which is the only version of a referral
/// scheme that does not become a way to churn friends through payroll.
/// </summary>
public class HrEmployeeReferral : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int ReferrerEmployeeId { get; set; }
    public HrEmployee? ReferrerEmployee { get; set; }

    public string CandidateName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Position { get; set; }
    public string? Notes { get; set; }

    /// <summary>The candidate record this became, once one was created.</summary>
    public int? CandidateId { get; set; }
    public HrCandidate? Candidate { get; set; }

    public int? VacancyId { get; set; }

    public string Status { get; set; } = ReferralStatuses.Submitted;

    public decimal BonusAmount { get; set; }

    /// <summary>
    /// Months the referred person must stay before the bonus is due.
    ///
    /// Zero pays on joining, which is what a company does when it is desperate
    /// and regrets when the referral leaves in six weeks.
    /// </summary>
    public int RetentionMonths { get; set; } = 3;

    public DateOnly? HiredOn { get; set; }
    public DateOnly? BonusDueOn { get; set; }

    /// <summary>The additional salary row the bonus was paid through.</summary>
    public int? BonusAdditionalSalaryId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/* ====================================================================== *
 * Performance
 * ====================================================================== */

/// <summary>
/// A named set of weighted responsibilities.
///
/// The existing goal record carried a KRA as free text, which means a
/// designer and a driver are appraised on whatever their manager happened to
/// type. A template is the same set of questions asked of everybody in a
/// role, which is the only way two appraisals can be compared.
/// </summary>
public class HrAppraisalTemplate : ITenantScoped, IAuditable
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

    public ICollection<HrAppraisalTemplateKra> Kras { get; set; } =
        new List<HrAppraisalTemplateKra>();
}

public class HrAppraisalTemplateKra : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int AppraisalTemplateId { get; set; }
    public HrAppraisalTemplate? AppraisalTemplate { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    /// <summary>Percentage of the appraisal. The template's weights must come to 100.</summary>
    public decimal Weight { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>
/// One responsibility, scored, on one appraisal.
///
/// Self and manager scores sit side by side rather than one overwriting the
/// other, because the gap between them is the conversation the appraisal
/// exists to have.
/// </summary>
public class HrAppraisalKra : ITenantScoped
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int AppraisalId { get; set; }
    public HrAppraisal? Appraisal { get; set; }

    /// <summary>Copied from the template, so a later edit does not rewrite history.</summary>
    public string Title { get; set; } = string.Empty;
    public decimal Weight { get; set; }

    /// <summary>One to five, or null while it is still unanswered.</summary>
    public decimal? SelfScore { get; set; }
    public decimal? ManagerScore { get; set; }

    public string? SelfComment { get; set; }
    public string? ManagerComment { get; set; }

    public int SortOrder { get; set; }
}

public static class FeedbackRelationships
{
    public const string Manager = "Manager";
    public const string Peer = "Peer";
    public const string Report = "Report";
    public const string Other = "Other";

    public static readonly string[] All = [Manager, Peer, Report, Other];
}

/// <summary>
/// What somebody else thinks, gathered around an appraisal.
///
/// Kept apart from the appraisal's own scores because it is not the manager's
/// judgement and should not be averaged into it. Whether the subject sees who
/// said what is a policy choice, which is why the flag is on the record rather
/// than assumed.
/// </summary>
public class HrPerformanceFeedback : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int? AppraisalCycleId { get; set; }
    public HrAppraisalCycle? AppraisalCycle { get; set; }

    /// <summary>Who the feedback is about.</summary>
    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    /// <summary>Who gave it.</summary>
    public int GivenByEmployeeId { get; set; }
    public HrEmployee? GivenByEmployee { get; set; }

    /// <summary>A value from <see cref="FeedbackRelationships"/>.</summary>
    public string Relationship { get; set; } = FeedbackRelationships.Peer;

    /// <summary>One to five.</summary>
    public decimal? Rating { get; set; }

    public string? WhatWorksWell { get; set; }
    public string? WhatCouldImprove { get; set; }

    /// <summary>Whether the subject may see who wrote it.</summary>
    public bool IsAnonymous { get; set; } = true;

    /// <summary>Whether the subject may see it at all yet.</summary>
    public bool SharedWithEmployee { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}

/* ====================================================================== *
 * Training
 * ====================================================================== */

public static class TrainingResults
{
    public const string Pending = "Pending";
    public const string Passed = "Passed";
    public const string Failed = "Failed";
    public const string Attended = "Attended";
    public const string Absent = "Absent";

    public static readonly string[] All = [Pending, Passed, Failed, Attended, Absent];
}

/// <summary>
/// What an attendee thought of a training.
///
/// Separate from the enrolment's score, which is what the trainer thought of
/// the attendee. Both are worth having and they answer opposite questions: one
/// says whether the person learned, the other whether the course was worth
/// running again.
/// </summary>
public class HrTrainingFeedback : ITenantScoped, IAuditable
{
    public int Id { get; set; }
    public int CompanyId { get; set; }

    public int TrainingId { get; set; }
    public HrTraining? Training { get; set; }

    public int EmployeeId { get; set; }
    public HrEmployee? Employee { get; set; }

    /// <summary>One to five.</summary>
    public decimal Rating { get; set; }

    /// <summary>Whether they would send a colleague.</summary>
    public bool WouldRecommend { get; set; } = true;

    public string? Comments { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int? CreatedById { get; set; }
    public int? UpdatedById { get; set; }
}
