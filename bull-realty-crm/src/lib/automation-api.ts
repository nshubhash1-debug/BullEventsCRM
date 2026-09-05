import { apiRequest } from "@/lib/api";

/* ------------------------------------------------------------------ *
 * The automation client.
 *
 * Kept out of `api.ts` because these are the rules that run by themselves —
 * routing, duplicate checks, SLA clocks, approvals and scheduled work — and
 * folding six administrative surfaces into the file every screen already
 * imports would make it a place nobody can find anything in.
 * ------------------------------------------------------------------ */

const base = "/api/admin/automation";

function get<T>(path: string) {
  return apiRequest<T>(`${base}${path}`, { method: "GET", auth: true });
}

function post<T>(path: string, body: unknown) {
  return apiRequest<T>(`${base}${path}`, {
    method: "POST",
    body: JSON.stringify(body),
    auth: true,
  });
}

function put<T>(path: string, body: unknown) {
  return apiRequest<T>(`${base}${path}`, {
    method: "PUT",
    body: JSON.stringify(body),
    auth: true,
  });
}

function del<T>(path: string) {
  return apiRequest<T>(`${base}${path}`, { method: "DELETE", auth: true });
}

/* ---------------- shared ---------------- */

export interface NamedOption {
  value: string;
  label: string;
}

export interface AutomationReference {
  roles: NamedOption[];
  users: NamedOption[];
  branches: NamedOption[];
  businessHours: NamedOption[];
  objects: string[];
  strategies: string[];
  duplicateActions: string[];
  escalationActions: string[];
  approverKinds: string[];
  jobKinds: string[];
}

/* ---------------- business hours ---------------- */

export interface DayWindow {
  /** Minutes past midnight. -1 means closed that day. */
  open: number;
  close: number;
}

export interface Holiday {
  id: number;
  date: string;
  name: string;
  isRecurring: boolean;
}

export interface BusinessHours {
  id: number;
  name: string;
  branchId: number | null;
  branchName: string | null;
  timeZoneId: string;
  /** Seven windows, Sunday first. */
  days: DayWindow[];
  isDefault: boolean;
  isActive: boolean;
  holidays: Holiday[];
  openNow: boolean;
}

export interface SaveBusinessHours {
  name: string;
  branchId?: number | null;
  timeZoneId: string;
  days: DayWindow[];
  isDefault: boolean;
  isActive: boolean;
}

/* ---------------- assignment ---------------- */

export interface AssignmentRule {
  id: number;
  name: string;
  description: string | null;
  object: string;
  sortOrder: number;
  criteriaField: string | null;
  criteriaOperator: string | null;
  criteriaValue: string | null;
  strategy: string;
  poolUserIds: string | null;
  poolRoleKey: string | null;
  poolRoleName: string | null;
  fixedUserId: number | null;
  fixedUserName: string | null;
  /** How many people the strategy currently has to choose from. */
  poolSize: number;
  isActive: boolean;
}

export interface SaveAssignmentRule {
  name: string;
  description?: string | null;
  object: string;
  criteriaField?: string | null;
  criteriaOperator?: string | null;
  criteriaValue?: string | null;
  strategy: string;
  poolUserIds?: string | null;
  poolRoleKey?: string | null;
  fixedUserId?: number | null;
  isActive: boolean;
}

/* ---------------- duplicates ---------------- */

export interface DuplicateRule {
  id: number;
  name: string;
  object: string;
  matchFields: string;
  action: string;
  acrossOwners: boolean;
  isActive: boolean;
}

export interface SaveDuplicateRule {
  name: string;
  object: string;
  matchFields: string;
  action: string;
  acrossOwners: boolean;
  isActive: boolean;
}

/* ---------------- escalation ---------------- */

export interface EscalationRule {
  id: number;
  name: string;
  object: string;
  criteriaField: string | null;
  criteriaOperator: string | null;
  criteriaValue: string | null;
  startsFrom: string;
  targetMinutes: number;
  businessHoursId: number | null;
  businessHoursName: string | null;
  action: string;
  reassignToUserId: number | null;
  reassignToUserName: string | null;
  isActive: boolean;
  firedCount: number;
}

export interface SaveEscalationRule {
  name: string;
  object: string;
  criteriaField?: string | null;
  criteriaOperator?: string | null;
  criteriaValue?: string | null;
  startsFrom: string;
  targetMinutes: number;
  businessHoursId?: number | null;
  action: string;
  reassignToUserId?: number | null;
  isActive: boolean;
}

export interface SweepResult {
  checked: number;
  breached: number;
  acted: number;
  notes: string[];
}

/* ---------------- approvals ---------------- */

export interface ApprovalStep {
  id: number;
  sortOrder: number;
  name: string;
  approverKind: string;
  approverRoleKey: string | null;
  approverRoleName: string | null;
  approverUserId: number | null;
  approverUserName: string | null;
}

export interface ApprovalProcess {
  id: number;
  name: string;
  description: string | null;
  object: string;
  criteriaField: string | null;
  criteriaOperator: string | null;
  criteriaValue: string | null;
  lockRecord: boolean;
  isActive: boolean;
  steps: ApprovalStep[];
}

export interface SaveApprovalStep {
  name: string;
  approverKind: string;
  approverRoleKey?: string | null;
  approverUserId?: number | null;
}

export interface SaveApprovalProcess {
  name: string;
  description?: string | null;
  object: string;
  criteriaField?: string | null;
  criteriaOperator?: string | null;
  criteriaValue?: string | null;
  lockRecord: boolean;
  isActive: boolean;
  steps: SaveApprovalStep[];
}

/* ---------------- jobs ---------------- */

export interface ScheduledJob {
  id: number;
  name: string;
  kind: string;
  cron: string;
  /** The cron expression as a sentence, so nobody has to decode it. */
  schedule: string;
  isActive: boolean;
  lastRunAt: string | null;
  nextRunAt: string | null;
  lastOutcome: string | null;
  lastMessage: string | null;
  lastDurationMs: number;
  runCount: number;
  failureCount: number;
}

export interface SaveScheduledJob {
  name: string;
  kind: string;
  cron: string;
  isActive: boolean;
}

/* ------------------------------------------------------------------ *
 * Calls
 * ------------------------------------------------------------------ */

export const automationApi = {
  reference: () => get<AutomationReference>("/reference"),

  businessHours: () => get<BusinessHours[]>("/business-hours"),
  createBusinessHours: (body: SaveBusinessHours) =>
    post<BusinessHours>("/business-hours", body),
  updateBusinessHours: (id: number, body: SaveBusinessHours) =>
    put<BusinessHours>(`/business-hours/${id}`, body),
  deleteBusinessHours: (id: number) => del<void>(`/business-hours/${id}`),
  addHoliday: (id: number, body: { name: string; date: string; isRecurring: boolean }) =>
    post<Holiday>(`/business-hours/${id}/holidays`, body),
  deleteHoliday: (id: number) => del<void>(`/holidays/${id}`),

  assignmentRules: () => get<AssignmentRule[]>("/assignment-rules"),
  createAssignmentRule: (body: SaveAssignmentRule) =>
    post<AssignmentRule>("/assignment-rules", body),
  updateAssignmentRule: (id: number, body: SaveAssignmentRule) =>
    put<AssignmentRule>(`/assignment-rules/${id}`, body),
  reorderAssignmentRules: (orderedIds: number[]) =>
    put<void>("/assignment-rules/order", orderedIds),
  deleteAssignmentRule: (id: number) => del<void>(`/assignment-rules/${id}`),

  duplicateRules: () => get<DuplicateRule[]>("/duplicate-rules"),
  createDuplicateRule: (body: SaveDuplicateRule) =>
    post<DuplicateRule>("/duplicate-rules", body),
  updateDuplicateRule: (id: number, body: SaveDuplicateRule) =>
    put<DuplicateRule>(`/duplicate-rules/${id}`, body),
  deleteDuplicateRule: (id: number) => del<void>(`/duplicate-rules/${id}`),

  escalationRules: () => get<EscalationRule[]>("/escalation-rules"),
  createEscalationRule: (body: SaveEscalationRule) =>
    post<EscalationRule>("/escalation-rules", body),
  updateEscalationRule: (id: number, body: SaveEscalationRule) =>
    put<EscalationRule>(`/escalation-rules/${id}`, body),
  deleteEscalationRule: (id: number) => del<void>(`/escalation-rules/${id}`),
  runSweep: () => post<SweepResult>("/escalation-rules/run", {}),

  approvalProcesses: () => get<ApprovalProcess[]>("/approval-processes"),
  createApprovalProcess: (body: SaveApprovalProcess) =>
    post<ApprovalProcess>("/approval-processes", body),
  updateApprovalProcess: (id: number, body: SaveApprovalProcess) =>
    put<ApprovalProcess>(`/approval-processes/${id}`, body),
  deleteApprovalProcess: (id: number) => del<void>(`/approval-processes/${id}`),

  jobs: () => get<ScheduledJob[]>("/jobs"),
  createJob: (body: SaveScheduledJob) => post<ScheduledJob>("/jobs", body),
  updateJob: (id: number, body: SaveScheduledJob) =>
    put<ScheduledJob>(`/jobs/${id}`, body),
  deleteJob: (id: number) => del<void>(`/jobs/${id}`),
  runJob: (id: number) => post<ScheduledJob>(`/jobs/${id}/run`, {}),
};

/* ------------------------------------------------------------------ *
 * Formatting
 * ------------------------------------------------------------------ */

/** Minutes past midnight as "09:30". */
export function clock(minutes: number) {
  return `${String(Math.floor(minutes / 60)).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`;
}

export function toMinutes(value: string) {
  const [h, m] = value.split(":").map(Number);
  return Number.isNaN(h) || Number.isNaN(m) ? -1 : h * 60 + m;
}

/**
 * A duration in the words somebody would use — "4 hours", not "240 minutes".
 *
 * SLA targets are set in hours and days far more often than in minutes, and a
 * screen that insists on the stored unit makes its reader do the arithmetic.
 */
export function duration(minutes: number) {
  if (minutes < 60) return `${minutes} min`;
  if (minutes < 1440) {
    const hours = minutes / 60;
    return `${Number.isInteger(hours) ? hours : hours.toFixed(1)} h`;
  }

  const days = minutes / 1440;
  return `${Number.isInteger(days) ? days : days.toFixed(1)} d`;
}

export const DAY_NAMES = ["Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat"];
