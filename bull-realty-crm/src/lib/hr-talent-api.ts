import { apiRequest } from "@/lib/api";

function get<T>(path: string) {
  return apiRequest<T>(path, { method: "GET", auth: true });
}

function post<T>(path: string, body?: unknown) {
  return apiRequest<T>(path, {
    method: "POST",
    body: JSON.stringify(body ?? {}),
    auth: true,
  });
}

/** Query strings carry reasons and notes, which carry spaces. */
function qs(params: Record<string, string | number | boolean | null | undefined>) {
  const search = new URLSearchParams();
  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined && value !== "") {
      search.set(key, String(value));
    }
  }
  return search.size ? `?${search}` : "";
}

/* ---------------- staffing plan ---------------- */

export interface HrStaffingPlanLine {
  id: number;
  departmentId: number | null;
  departmentName: string | null;
  position: string;
  headcount: number;
  budgetPerHead: number;
  notes: string | null;
  /** Approved requisitions already drawn against this line. */
  approved: number;
  remaining: number;
}

export interface HrStaffingPlan {
  id: number;
  name: string;
  fromDate: string;
  toDate: string;
  status: string;
  notes: string | null;
  isCurrent: boolean;
  totalHeadcount: number;
  totalBudget: number;
  lines: HrStaffingPlanLine[];
}

/* ---------------- requisitions ---------------- */

export interface HrRequisition {
  id: number;
  position: string;
  departmentId: number | null;
  departmentName: string | null;
  headcount: number;
  isReplacement: boolean;
  replacingEmployeeId: number | null;
  requestedByEmployeeId: number | null;
  requestedByName: string | null;
  requiredBy: string | null;
  justification: string | null;
  jobDescription: string | null;
  salaryMin: number | null;
  salaryMax: number | null;
  location: string | null;
  staffingPlanId: number | null;
  staffingPlanName: string | null;
  status: string;
  decisionNote: string | null;
  decidedAt: string | null;
  vacancyId: number | null;
  /** Set when the ask does not fit the plan. Guidance, not a refusal. */
  planWarning: string | null;
}

/* ---------------- interview rounds and scorecards ---------------- */

export interface HrInterviewSkill {
  id: number;
  name: string;
  weight: number;
  sortOrder: number;
}

export interface HrInterviewRound {
  id: number;
  name: string;
  sortOrder: number;
  passingScore: number;
  notes: string | null;
  isActive: boolean;
  skills: HrInterviewSkill[];
}

export interface HrSkillRating {
  skillName: string;
  rating: number;
  weight: number;
  comment: string | null;
}

export interface HrInterviewFeedback {
  id: number;
  panellistEmployeeId: number | null;
  panellistName: string;
  score: number;
  recommendation: string | null;
  strengths: string | null;
  concerns: string | null;
  ratings: HrSkillRating[];
}

export interface HrInterviewPanel {
  interviewId: number;
  candidateName: string;
  interviewRoundId: number | null;
  roundName: string | null;
  passingScore: number | null;
  panellistCount: number;
  averageScore: number | null;
  /** Highest panellist less lowest — the number worth looking at. */
  spread: number | null;
  feedback: HrInterviewFeedback[];
}

/* ---------------- offers and referrals ---------------- */

export interface HrJobOffer {
  id: number;
  candidateId: number;
  candidateName: string;
  position: string;
  annualCtc: number;
  offerDate: string;
  validUntil: string | null;
  proposedJoiningDate: string | null;
  status: string;
  outcomeReason: string | null;
  respondedAt: string | null;
  terms: string | null;
  revision: number;
  hasLapsed: boolean;
}

export interface HrReferral {
  id: number;
  referrerEmployeeId: number;
  referrerName: string;
  candidateName: string;
  phone: string | null;
  email: string | null;
  position: string | null;
  notes: string | null;
  candidateId: number | null;
  vacancyId: number | null;
  status: string;
  bonusAmount: number;
  retentionMonths: number;
  hiredOn: string | null;
  bonusDueOn: string | null;
  bonusAdditionalSalaryId: number | null;
  /** The retention period is served and the bonus has not been raised. */
  bonusPayable: boolean;
}

/* ---------------- performance ---------------- */

export interface HrTemplateKra {
  id: number;
  title: string;
  description: string | null;
  weight: number;
  sortOrder: number;
}

export interface HrAppraisalTemplate {
  id: number;
  name: string;
  notes: string | null;
  isActive: boolean;
  totalWeight: number;
  appraisalsUsing: number;
  kras: HrTemplateKra[];
}

export interface HrAppraisalKra {
  id: number;
  title: string;
  weight: number;
  selfScore: number | null;
  managerScore: number | null;
  selfComment: string | null;
  managerComment: string | null;
  /** Manager less self. The conversation the appraisal is for. */
  gap: number | null;
}

export interface HrAppraisalDetail {
  id: number;
  cycleId: number;
  cycleName: string;
  employeeId: number;
  employeeName: string;
  appraisalTemplateId: number | null;
  templateName: string | null;
  selfScore: number | null;
  managerScore: number | null;
  finalScore: number | null;
  rating: string | null;
  comments: string | null;
  status: string;
  feedbackCount: number;
  feedbackAverage: number | null;
  kras: HrAppraisalKra[];
}

export interface HrFeedback {
  id: number;
  employeeId: number;
  employeeName: string;
  appraisalCycleId: number | null;
  /** Null when given anonymously. */
  givenByEmployeeId: number | null;
  givenByName: string;
  relationship: string;
  rating: number | null;
  whatWorksWell: string | null;
  whatCouldImprove: string | null;
  isAnonymous: boolean;
  sharedWithEmployee: boolean;
  createdAt: string;
}

/* ---------------- training ---------------- */

export interface HrEnrolmentOutcome {
  id: number;
  employeeId: number;
  employeeName: string;
  status: string;
  result: string;
  score: number | null;
  trainerRemarks: string | null;
  gaveFeedback: boolean;
}

export interface HrTrainingFeedback {
  id: number;
  employeeId: number;
  employeeName: string;
  rating: number;
  wouldRecommend: boolean;
  comments: string | null;
  createdAt: string;
}

export interface HrTrainingOutcome {
  id: number;
  title: string;
  trainer: string | null;
  fromDate: string;
  toDate: string;
  status: string;
  enrolled: number;
  passed: number;
  failed: number;
  absent: number;
  averageRating: number | null;
  recommendPercent: number | null;
  enrolments: HrEnrolmentOutcome[];
  feedback: HrTrainingFeedback[];
}

export const TRAINING_RESULTS = [
  "Pending", "Passed", "Failed", "Attended", "Absent",
] as const;

export const OFFER_STATUSES = [
  "Draft", "Sent", "Accepted", "Declined", "Withdrawn", "Lapsed",
] as const;

export const FEEDBACK_RELATIONSHIPS = ["Manager", "Peer", "Report", "Other"] as const;

export const talentApi = {
  staffingPlans: () => get<HrStaffingPlan[]>("/api/hr/talent/staffing-plans"),
  saveStaffingPlan: (body: Record<string, unknown>) =>
    post<number>("/api/hr/talent/staffing-plans", body),

  requisitions: (status?: string) =>
    get<HrRequisition[]>(`/api/hr/talent/requisitions${qs({ status })}`),
  raise: (body: Record<string, unknown>) =>
    post<HrRequisition>("/api/hr/talent/requisitions", body),
  decideRequisition: (id: number, approve: boolean, note?: string) =>
    post<HrRequisition>(`/api/hr/talent/requisitions/${id}/decide${qs({ approve, note })}`),

  rounds: () => get<HrInterviewRound[]>("/api/hr/talent/interview-rounds"),
  saveRound: (body: Record<string, unknown>) =>
    post<number>("/api/hr/talent/interview-rounds", body),

  panel: (interviewId: number) =>
    get<HrInterviewPanel>(`/api/hr/talent/interviews/${interviewId}/feedback`),
  giveInterviewFeedback: (interviewId: number, body: Record<string, unknown>) =>
    post<HrInterviewFeedback>(`/api/hr/talent/interviews/${interviewId}/feedback`, body),

  offers: (status?: string) => get<HrJobOffer[]>(`/api/hr/talent/offers${qs({ status })}`),
  makeOffer: (body: Record<string, unknown>) =>
    post<HrJobOffer>("/api/hr/talent/offers", body),
  setOfferStatus: (id: number, status: string, reason?: string) =>
    post<HrJobOffer>(`/api/hr/talent/offers/${id}/status${qs({ status, reason })}`),

  referrals: (status?: string) =>
    get<HrReferral[]>(`/api/hr/talent/referrals${qs({ status })}`),
  refer: (body: Record<string, unknown>) =>
    post<HrReferral>("/api/hr/talent/referrals", body),
  referralToCandidate: (id: number) =>
    post<HrReferral>(`/api/hr/talent/referrals/${id}/to-candidate`),
  referralHired: (id: number, on?: string) =>
    post<HrReferral>(`/api/hr/talent/referrals/${id}/hired${qs({ on })}`),
  payReferralBonus: (id: number, year: number, month: number) =>
    post<HrReferral>(`/api/hr/talent/referrals/${id}/pay-bonus${qs({ year, month })}`),

  templates: () => get<HrAppraisalTemplate[]>("/api/hr/performance/templates"),
  saveTemplate: (body: Record<string, unknown>) =>
    post<number>("/api/hr/performance/templates", body),

  startAppraisal: (body: Record<string, unknown>) =>
    post<HrAppraisalDetail>("/api/hr/performance/appraisals", body),
  appraisal: (id: number) =>
    get<HrAppraisalDetail>(`/api/hr/performance/appraisals/${id}`),
  score: (id: number, asManager: boolean, body: Record<string, unknown>) =>
    post<HrAppraisalDetail>(
      `/api/hr/performance/appraisals/${id}/score${qs({ asManager })}`,
      body
    ),
  closeAppraisal: (id: number, rating?: string) =>
    post<HrAppraisalDetail>(`/api/hr/performance/appraisals/${id}/close${qs({ rating })}`),

  feedback: (employeeId?: number, cycleId?: number, includeUnshared = false) =>
    get<HrFeedback[]>(
      `/api/hr/performance/feedback${qs({ employeeId, cycleId, includeUnshared })}`
    ),
  giveFeedback: (body: Record<string, unknown>) =>
    post<HrFeedback>("/api/hr/performance/feedback", body),
  shareFeedback: (employeeId: number, cycleId?: number) =>
    post<number>(`/api/hr/performance/feedback/share${qs({ employeeId, cycleId })}`),

  trainingOutcome: (trainingId: number) =>
    get<HrTrainingOutcome>(`/api/hr/performance/trainings/${trainingId}/outcome`),
  recordResult: (enrolmentId: number, body: Record<string, unknown>) =>
    post(`/api/hr/performance/enrolments/${enrolmentId}/result`, body),
  trainingFeedback: (trainingId: number, body: Record<string, unknown>) =>
    post<HrTrainingFeedback>(`/api/hr/performance/trainings/${trainingId}/feedback`, body),
};
