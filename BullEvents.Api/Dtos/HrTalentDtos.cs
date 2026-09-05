namespace BullEvents.Api.Dtos;

/* ---------------- staffing plan ---------------- */

public record HrStaffingPlanLineDto(
    int Id, int? DepartmentId, string? DepartmentName, string Position,
    int Headcount, decimal BudgetPerHead, string? Notes,
    /// <summary>Approved requisitions already drawn against this line.</summary>
    int Approved,
    int Remaining);

public record HrStaffingPlanDto(
    int Id, string Name, DateOnly FromDate, DateOnly ToDate, string Status, string? Notes,
    bool IsCurrent, int TotalHeadcount, decimal TotalBudget,
    IReadOnlyList<HrStaffingPlanLineDto> Lines);

public record HrStaffingPlanLineInput(
    int? DepartmentId, string Position, int Headcount, decimal BudgetPerHead, string? Notes);

public record HrStaffingPlanInput(
    int? Id, string Name, DateOnly FromDate, DateOnly ToDate, string Status, string? Notes,
    IReadOnlyList<HrStaffingPlanLineInput> Lines);

/* ---------------- requisitions ---------------- */

public record HrRequisitionDto(
    int Id, string Position, int? DepartmentId, string? DepartmentName, int Headcount,
    bool IsReplacement, int? ReplacingEmployeeId,
    int? RequestedByEmployeeId, string? RequestedByName,
    DateOnly? RequiredBy, string? Justification, string? JobDescription,
    decimal? SalaryMin, decimal? SalaryMax, string? Location,
    int? StaffingPlanId, string? StaffingPlanName,
    string Status, string? DecisionNote, DateTime? DecidedAt, int? VacancyId,
    /// <summary>Set when the ask does not fit the plan. Guidance, not a refusal.</summary>
    string? PlanWarning);

public record HrRequisitionInput(
    string Position, int? DepartmentId, int? DesignationId, int Headcount,
    bool IsReplacement, int? ReplacingEmployeeId, int? RequestedByEmployeeId,
    DateOnly? RequiredBy, string? Justification, string? JobDescription,
    decimal? SalaryMin, decimal? SalaryMax, string? Location);

/* ---------------- interview rounds ---------------- */

public record HrInterviewSkillDto(int Id, string Name, decimal Weight, int SortOrder);

public record HrInterviewRoundDto(
    int Id, string Name, int SortOrder, decimal PassingScore, string? Notes, bool IsActive,
    IReadOnlyList<HrInterviewSkillDto> Skills);

public record HrInterviewSkillInput(string Name, decimal Weight);

public record HrInterviewRoundInput(
    int? Id, string Name, int SortOrder, decimal PassingScore, string? Notes, bool IsActive,
    IReadOnlyList<HrInterviewSkillInput> Skills);

/* ---------------- scorecards ---------------- */

public record HrSkillRatingDto(string SkillName, decimal Rating, decimal Weight, string? Comment);

public record HrInterviewFeedbackDto(
    int Id, int? PanellistEmployeeId, string PanellistName, decimal Score,
    string? Recommendation, string? Strengths, string? Concerns,
    IReadOnlyList<HrSkillRatingDto> Ratings);

/// <summary>
/// A panel's view of one interview.
///
/// <see cref="Spread"/> is the distance between the highest and lowest
/// panellist. It is the number worth looking at — three fours and a set of
/// five, three, four average the same and are not the same candidate.
/// </summary>
public record HrInterviewPanelDto(
    int InterviewId, string CandidateName,
    int? InterviewRoundId, string? RoundName, decimal? PassingScore,
    int PanellistCount, decimal? AverageScore, decimal? Spread,
    IReadOnlyList<HrInterviewFeedbackDto> Feedback);

public record HrSkillRatingInput(
    int? InterviewSkillId, string? SkillName, decimal Rating, decimal Weight, string? Comment);

public record HrInterviewFeedbackInput(
    int? PanellistEmployeeId, string? PanellistName,
    string? Recommendation, string? Strengths, string? Concerns,
    IReadOnlyList<HrSkillRatingInput> Ratings);

/* ---------------- offers ---------------- */

public record HrJobOfferDto(
    int Id, int CandidateId, string CandidateName, string Position,
    decimal AnnualCtc, DateOnly OfferDate, DateOnly? ValidUntil,
    DateOnly? ProposedJoiningDate,
    string Status, string? OutcomeReason, DateTime? RespondedAt, string? Terms,
    int Revision,
    /// <summary>Sent, and past its validity without an answer.</summary>
    bool HasLapsed);

public record HrJobOfferInput(
    int CandidateId, string? Position, int? DepartmentId, int? DesignationId,
    decimal AnnualCtc, int? PayStructureId,
    DateOnly OfferDate, DateOnly? ValidUntil, DateOnly? ProposedJoiningDate,
    string? Terms);

/* ---------------- referrals ---------------- */

public record HrReferralDto(
    int Id, int ReferrerEmployeeId, string ReferrerName,
    string CandidateName, string? Phone, string? Email, string? Position, string? Notes,
    int? CandidateId, int? VacancyId, string Status,
    decimal BonusAmount, int RetentionMonths,
    DateOnly? HiredOn, DateOnly? BonusDueOn, int? BonusAdditionalSalaryId,
    /// <summary>The retention period is served and the bonus has not been raised.</summary>
    bool BonusPayable);

public record HrReferralInput(
    int ReferrerEmployeeId, string CandidateName, string? Phone, string? Email,
    string? Position, string? Notes, int? VacancyId,
    decimal BonusAmount, int RetentionMonths);

/* ---------------- appraisal templates ---------------- */

public record HrTemplateKraDto(
    int Id, string Title, string? Description, decimal Weight, int SortOrder);

public record HrAppraisalTemplateDto(
    int Id, string Name, string? Notes, bool IsActive,
    decimal TotalWeight, int AppraisalsUsing,
    IReadOnlyList<HrTemplateKraDto> Kras);

public record HrTemplateKraInput(string Title, string? Description, decimal Weight);

public record HrAppraisalTemplateInput(
    int? Id, string Name, string? Notes, bool IsActive,
    IReadOnlyList<HrTemplateKraInput> Kras);

/* ---------------- appraisals ---------------- */

public record HrAppraisalKraDto(
    int Id, string Title, decimal Weight,
    decimal? SelfScore, decimal? ManagerScore,
    string? SelfComment, string? ManagerComment,
    /// <summary>Manager less self. The conversation the appraisal is for.</summary>
    decimal? Gap);

public record HrAppraisalDetailDto(
    int Id, int CycleId, string CycleName,
    int EmployeeId, string EmployeeName,
    int? AppraisalTemplateId, string? TemplateName,
    decimal? SelfScore, decimal? ManagerScore, decimal? FinalScore,
    string? Rating, string? Comments, string Status,
    int FeedbackCount, decimal? FeedbackAverage,
    IReadOnlyList<HrAppraisalKraDto> Kras);

public record HrStartAppraisalInput(int CycleId, int EmployeeId, int AppraisalTemplateId);

public record HrAppraisalKraScoreInput(int AppraisalKraId, decimal Score, string? Comment);

public record HrAppraisalScoreInput(
    IReadOnlyList<HrAppraisalKraScoreInput> Scores, string? Comments);

/* ---------------- feedback ---------------- */

public record HrFeedbackDto(
    int Id, int EmployeeId, string EmployeeName, int? AppraisalCycleId,
    /// <summary>Null when the feedback was given anonymously.</summary>
    int? GivenByEmployeeId,
    string GivenByName,
    string Relationship, decimal? Rating,
    string? WhatWorksWell, string? WhatCouldImprove,
    bool IsAnonymous, bool SharedWithEmployee, DateTime CreatedAt);

public record HrFeedbackInput(
    int EmployeeId, int GivenByEmployeeId, int? AppraisalCycleId,
    string Relationship, decimal? Rating,
    string? WhatWorksWell, string? WhatCouldImprove, bool IsAnonymous);

/* ---------------- training outcomes ---------------- */

public record HrEnrolmentOutcomeDto(
    int Id, int EmployeeId, string EmployeeName,
    string Status, string Result, decimal? Score, string? TrainerRemarks,
    bool GaveFeedback);

public record HrTrainingFeedbackDto(
    int Id, int EmployeeId, string EmployeeName,
    decimal Rating, bool WouldRecommend, string? Comments, DateTime CreatedAt);

/// <summary>
/// Both halves of the question a training raises: what the trainer made of the
/// attendees, and what the attendees made of the training.
/// </summary>
public record HrTrainingOutcomeDto(
    int Id, string Title, string? Trainer,
    DateOnly FromDate, DateOnly ToDate, string Status,
    int Enrolled, int Passed, int Failed, int Absent,
    decimal? AverageRating,
    /// <summary>Percentage who would send a colleague.</summary>
    decimal? RecommendPercent,
    IReadOnlyList<HrEnrolmentOutcomeDto> Enrolments,
    IReadOnlyList<HrTrainingFeedbackDto> Feedback);

public record HrTrainingResultInput(string Result, decimal? Score, string? TrainerRemarks);

public record HrTrainingFeedbackInput(
    int EmployeeId, decimal Rating, bool WouldRecommend, string? Comments);
