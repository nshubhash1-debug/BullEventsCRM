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

/* ---------------- checklists ---------------- */

export interface HrChecklistTask {
  id: number;
  title: string;
  description: string | null;
  owner: string;
  /** Days from the joining or last working day. Negative is before it. */
  dueOffsetDays: number;
  isBlocking: boolean;
  sortOrder: number;
}

export interface HrChecklistTemplate {
  id: number;
  name: string;
  kind: string;
  departmentId: number | null;
  departmentName: string | null;
  collarType: string | null;
  notes: string | null;
  isActive: boolean;
  blockingTasks: number;
  tasks: HrChecklistTask[];
}

export interface HrChecklistItem {
  id: number;
  title: string;
  description: string | null;
  owner: string;
  dueOn: string | null;
  done: boolean;
  doneAt: string | null;
  isBlocking: boolean;
  isOverdue: boolean;
}

export interface HrChecklistRun {
  employeeId: number;
  employeeName: string;
  kind: string;
  tasksAdded: number;
  total: number;
  done: number;
  overdue: number;
  blocking: number;
  /** The sentence that matters: what is stopping them starting or leaving. */
  blockedBecause: string | null;
  items: HrChecklistItem[];
}

/* ---------------- grievances ---------------- */

export interface HrGrievance {
  id: number;
  employeeId: number;
  employeeName: string;
  category: string;
  subject: string;
  details: string | null;
  againstEmployeeId: number | null;
  assignedToEmployeeId: number | null;
  assignedToName: string | null;
  status: string;
  isConfidential: boolean;
  acknowledgedAt: string | null;
  resolvedAt: string | null;
  resolution: string | null;
  complainantSatisfied: boolean | null;
  attachmentUrl: string | null;
  ageInDays: number;
  createdAt: string;
  /** The law requires a constituted committee, not a line manager. */
  needsCommittee: boolean;
}

/* ---------------- skills ---------------- */

export interface HrSkill {
  id: number;
  name: string;
  category: string | null;
  requiresCertification: boolean;
  notes: string | null;
  isActive: boolean;
  peopleWithIt: number;
  averageProficiency: number | null;
}

export interface HrEmployeeSkill {
  id: number;
  employeeId: number;
  employeeName: string;
  skillId: number;
  skillName: string;
  category: string | null;
  proficiency: number;
  proficiencyLabel: string;
  yearsOfExperience: number;
  assessedOn: string | null;
  certificateNumber: string | null;
  certifiedUntil: string | null;
  certificateExpired: boolean;
  notes: string | null;
}

export interface HrSkillSearch {
  skillId: number;
  skillName: string;
  minimumProficiency: number;
  found: number;
  /** Reported rather than hidden — somebody has to decide whether to send them. */
  expiredCertificates: number;
  people: HrEmployeeSkill[];
}

/* ---------------- expenses ---------------- */

export interface HrExpenseType {
  id: number;
  name: string;
  description: string | null;
  perClaimLimit: number;
  monthlyLimit: number;
  requiresReceipt: boolean;
  receiptWaivedBelow: number;
  requiresTravelRequest: boolean;
  isActive: boolean;
}

export interface HrExpenseCheck {
  expenseClaimTypeId: number;
  typeName: string;
  amount: number;
  alreadyClaimedThisMonth: number;
  remainingThisMonth: number | null;
  allowed: boolean;
  problems: string[];
}

/* ---------------- travel ---------------- */

export interface HrTravelLeg {
  id: number;
  onDate: string;
  from: string;
  to: string;
  mode: string;
  estimatedCost: number;
  notes: string | null;
}

export interface HrTravel {
  id: number;
  employeeId: number;
  employeeName: string;
  purpose: string;
  leadId: number | null;
  fromDate: string;
  toDate: string;
  nights: number;
  destination: string | null;
  estimatedCost: number;
  advanceRequested: number;
  advancePaid: number;
  status: string;
  decisionNote: string | null;
  decidedAt: string | null;
  legs: HrTravelLeg[];
}

/* ---------------- timesheets ---------------- */

export interface HrTimesheetLine {
  id: number;
  onDate: string;
  leadId: number | null;
  activity: string;
  hours: number;
  isBillable: boolean;
  notes: string | null;
}

export interface HrTimesheet {
  id: number;
  employeeId: number;
  employeeName: string;
  weekStarting: string;
  status: string;
  totalHours: number;
  billableHours: number;
  billablePercent: number;
  submittedAt: string | null;
  approvedAt: string | null;
  decisionNote: string | null;
  lines: HrTimesheetLine[];
}

export interface HrActivityHours {
  activity: string;
  hours: number;
}

export interface HrEventHours {
  leadId: number;
  people: number;
  totalHours: number;
  billableHours: number;
  byActivity: HrActivityHours[];
}

export const CHECKLIST_OWNERS = [
  "Employee", "Manager", "HR", "IT", "Finance", "Stores",
] as const;

export const GRIEVANCE_CATEGORIES = [
  "Pay", "WorkingConditions", "Safety", "Harassment",
  "Discrimination", "Management", "Other",
] as const;

export const TRAVEL_MODES = ["Train", "Flight", "Bus", "Road"] as const;

export const workplaceApi = {
  checklists: (kind?: string) =>
    get<HrChecklistTemplate[]>(`/api/hr/workplace/checklists${qs({ kind })}`),
  saveChecklist: (body: Record<string, unknown>) =>
    post<number>("/api/hr/workplace/checklists", body),
  applyChecklist: (body: Record<string, unknown>) =>
    post<HrChecklistRun>("/api/hr/workplace/checklists/apply", body),
  employeeChecklist: (employeeId: number, kind?: string) =>
    get<HrChecklistRun>(
      `/api/hr/workplace/checklists/employee/${employeeId}${qs({ kind })}`
    ),

  grievances: (status?: string) =>
    get<HrGrievance[]>(`/api/hr/workplace/grievances${qs({ status })}`),
  raiseGrievance: (body: Record<string, unknown>) =>
    post<HrGrievance>("/api/hr/workplace/grievances", body),
  assignGrievance: (id: number, toEmployeeId: number, committee: boolean) =>
    post<HrGrievance>(
      `/api/hr/workplace/grievances/${id}/assign${qs({ toEmployeeId, committee })}`
    ),
  resolveGrievance: (id: number, body: Record<string, unknown>) =>
    post<HrGrievance>(`/api/hr/workplace/grievances/${id}/resolve`, body),

  skills: () => get<HrSkill[]>("/api/hr/workplace/skills"),
  saveSkill: (body: Record<string, unknown>) =>
    post<number>("/api/hr/workplace/skills", body),
  setEmployeeSkill: (body: Record<string, unknown>) =>
    post<HrEmployeeSkill>("/api/hr/workplace/employee-skills", body),
  employeeSkills: (employeeId: number) =>
    get<HrEmployeeSkill[]>(`/api/hr/workplace/employee-skills/${employeeId}`),
  whoCan: (skillId: number, minimumProficiency: number, onlyCertified: boolean) =>
    get<HrSkillSearch>(
      `/api/hr/workplace/skills/who-can${qs({ skillId, minimumProficiency, onlyCertified })}`
    ),

  expenseTypes: () => get<HrExpenseType[]>("/api/hr/workplace/expense-types"),
  saveExpenseType: (body: Record<string, unknown>) =>
    post<number>("/api/hr/workplace/expense-types", body),
  checkClaim: (
    typeId: number,
    employeeId: number,
    amount: number,
    hasReceipt: boolean,
    travelRequestId?: number
  ) =>
    get<HrExpenseCheck>(
      `/api/hr/workplace/expense-types/${typeId}/check` +
        qs({ employeeId, amount, hasReceipt, travelRequestId })
    ),

  travel: (status?: string) => get<HrTravel[]>(`/api/hr/workplace/travel${qs({ status })}`),
  requestTravel: (body: Record<string, unknown>) =>
    post<HrTravel>("/api/hr/workplace/travel", body),
  decideTravel: (id: number, approve: boolean, advancePaid?: number, note?: string) =>
    post<HrTravel>(
      `/api/hr/workplace/travel/${id}/decide${qs({ approve, advancePaid, note })}`
    ),

  timesheets: (employeeId?: number, status?: string) =>
    get<HrTimesheet[]>(`/api/hr/workplace/timesheets${qs({ employeeId, status })}`),
  saveTimesheet: (body: Record<string, unknown>) =>
    post<HrTimesheet>("/api/hr/workplace/timesheets", body),
  decideTimesheet: (id: number, approve: boolean, note?: string) =>
    post<HrTimesheet>(`/api/hr/workplace/timesheets/${id}/decide${qs({ approve, note })}`),
  hoursByEvent: (from?: string, to?: string) =>
    get<HrEventHours[]>(`/api/hr/workplace/timesheets/by-event${qs({ from, to })}`),
};
