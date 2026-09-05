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

/* ====================================================================== *
 * Leave — allocated rather than assumed
 * ====================================================================== */

export interface HrLeavePeriod {
  id: number;
  name: string;
  fromDate: string;
  toDate: string;
  isActive: boolean;
  rolledOverAt: string | null;
  isCurrent: boolean;
  employeesAllocated: number;
  daysAllocated: number;
}

export interface HrLeavePolicyLine {
  id: number;
  leaveTypeId: number;
  leaveTypeName: string;
  annualAllocation: number;
}

export interface HrLeavePolicy {
  id: number;
  name: string;
  notes: string | null;
  isActive: boolean;
  employeesAssigned: number;
  totalDays: number;
  lines: HrLeavePolicyLine[];
}

export interface HrLeaveTypeBrief {
  id: number;
  code: string;
  name: string;
  paid: boolean;
  carryForward: boolean;
  maxCarryForward: number;
  allowEncashment: boolean;
  isCompensatory: boolean;
}

export interface HrLeaveBalanceCell {
  leaveTypeId: number;
  opening: number;
  accrued: number;
  taken: number;
  closing: number;
}

export interface HrLeaveRegisterRow {
  employeeId: number;
  employeeName: string;
  totalClosing: number;
  balances: HrLeaveBalanceCell[];
}

export interface HrLeaveRegister {
  periodId: number | null;
  periodName: string | null;
  leaveTypes: HrLeaveTypeBrief[];
  rows: HrLeaveRegisterRow[];
}

export interface HrLeaveAllocation {
  id: number;
  leaveTypeId: number;
  leaveTypeName: string;
  leavePeriodId: number;
  periodName: string;
  /** Policy, CarryForward, Compensatory, Encashment, Manual or Lapsed. */
  source: string;
  days: number;
  notes: string | null;
  createdAt: string;
}

export interface HrCompensatoryRequest {
  id: number;
  employeeId: number;
  employeeName: string;
  workedOn: string;
  leaveTypeId: number;
  leaveTypeName: string;
  days: number;
  reason: string | null;
  leadId: number | null;
  status: string;
  allocationId: number | null;
  createdAt: string;
}

export interface HrEncashment {
  id: number;
  employeeId: number;
  employeeName: string;
  leaveTypeId: number;
  leaveTypeName: string;
  leavePeriodId: number;
  periodName: string;
  days: number;
  perDayAmount: number;
  amount: number;
  status: string;
  paidInPayrollRunId: number | null;
  createdAt: string;
}

export interface HrLeaveBlock {
  id: number;
  fromDate: string;
  toDate: string;
  reason: string;
  departmentId: number | null;
  departmentName: string | null;
  /** False means nobody, HR included, can approve through it. */
  allowOverride: boolean;
  isActive: boolean;
  dayCount: number;
}

/** What a leave would cost, answered while the dates are still being picked. */
export interface HrLeaveCheck {
  countedDays: number;
  skippedDays: number;
  currentBalance: number;
  balanceAfter: number;
  shortfall: string | null;
  periodName: string | null;
  blocks: HrLeaveBlock[];
}

export interface HrRollOverResult {
  closedPeriod: string;
  openedPeriod: string;
  balancesCarried: number;
  balancesLapsed: number;
  daysCarried: number;
  daysLapsed: number;
}

export interface HrGrantResult {
  granted: number;
  skipped: number;
  totalDays: number;
}

export interface HrRebuildResult {
  periods: number;
  balancesExamined: number;
  balancesCorrected: number;
  balancesOrphaned: number;
}

/* ====================================================================== *
 * Shifts and the roster
 * ====================================================================== */

export interface HrShiftAssignment {
  id: number;
  employeeId: number;
  employeeName: string;
  shiftId: number;
  shiftName: string;
  startTime: string | null;
  endTime: string | null;
  fromDate: string;
  toDate: string | null;
  leadId: number | null;
  notes: string | null;
  isActive: boolean;
}

export interface HrRosterRow {
  employeeId: number;
  employeeName: string;
  employeeCode: string;
  department: string;
  shiftId: number | null;
  shiftName: string;
  startTime: string | null;
  endTime: string | null;
  /** False when this is only the default shift, not a decision somebody made. */
  onAssignment: boolean;
  leadId: number | null;
  /** Working, Leave, Holiday or Weekly off. */
  state: string;
}

export interface HrRoster {
  onDate: string;
  isHoliday: boolean;
  working: number;
  onLeave: number;
  onAssignment: number;
  rows: HrRosterRow[];
}

export interface HrShiftRequest {
  id: number;
  employeeId: number;
  employeeName: string;
  shiftId: number;
  shiftName: string;
  fromDate: string;
  toDate: string | null;
  reason: string | null;
  status: string;
  shiftAssignmentId: number | null;
  createdAt: string;
}

export interface HrAttendanceRequest {
  id: number;
  employeeId: number;
  employeeName: string;
  fromDate: string;
  toDate: string;
  dayCount: number;
  requestedStatus: string;
  halfDay: boolean;
  reason: string | null;
  attachmentUrl: string | null;
  leadId: number | null;
  status: string;
  createdAt: string;
}

/* ====================================================================== *
 * Client
 * ====================================================================== */

export const leaveApi = {
  periods: () => get<HrLeavePeriod[]>("/api/hr/leave/periods"),
  savePeriod: (body: Record<string, unknown>) =>
    post<HrLeavePeriod>("/api/hr/leave/periods", body),
  rollOver: (id: number, intoPeriodId: number) =>
    post<HrRollOverResult>(`/api/hr/leave/periods/${id}/roll-over?intoPeriodId=${intoPeriodId}`),

  policies: () => get<HrLeavePolicy[]>("/api/hr/leave/policies"),
  savePolicy: (body: Record<string, unknown>) => post<number>("/api/hr/leave/policies", body),
  assign: (body: Record<string, unknown>) =>
    post<HrGrantResult>("/api/hr/leave/assignments", body),

  register: (periodId?: number) =>
    get<HrLeaveRegister>(`/api/hr/leave/balances${periodId ? `?periodId=${periodId}` : ""}`),
  allocations: (employeeId: number, periodId?: number) =>
    get<HrLeaveAllocation[]>(
      `/api/hr/leave/allocations?employeeId=${employeeId}` +
        (periodId ? `&periodId=${periodId}` : "")
    ),
  adjust: (body: Record<string, unknown>) =>
    post<HrLeaveAllocation>("/api/hr/leave/allocations", body),
  rebuild: () => post<HrRebuildResult>("/api/hr/leave/rebuild-balances"),

  compensatory: (status?: string) =>
    get<HrCompensatoryRequest[]>(
      `/api/hr/leave/compensatory${status ? `?status=${status}` : ""}`
    ),
  claimCompensatory: (body: Record<string, unknown>) =>
    post<HrCompensatoryRequest>("/api/hr/leave/compensatory", body),
  decideCompensatory: (id: number, approve: boolean) =>
    post<HrCompensatoryRequest>(`/api/hr/leave/compensatory/${id}/decide?approve=${approve}`),

  encashments: () => get<HrEncashment[]>("/api/hr/leave/encashments"),
  encash: (body: Record<string, unknown>) =>
    post<HrEncashment>("/api/hr/leave/encashments", body),
  decideEncashment: (id: number, approve: boolean) =>
    post<HrEncashment>(`/api/hr/leave/encashments/${id}/decide?approve=${approve}`),

  blocks: () => get<HrLeaveBlock[]>("/api/hr/leave/block-dates"),
  saveBlock: (body: Record<string, unknown>) => post<number>("/api/hr/leave/block-dates", body),

  /** Called as the dates are picked, so a block is known before a reason is written. */
  check: (
    employeeId: number,
    leaveTypeId: number,
    fromDate: string,
    toDate: string,
    halfDay: boolean
  ) =>
    get<HrLeaveCheck>(
      `/api/hr/leave/check?employeeId=${employeeId}&leaveTypeId=${leaveTypeId}` +
        `&fromDate=${fromDate}&toDate=${toDate}&halfDay=${halfDay}`
    ),
};

export const shiftApi = {
  roster: (onDate?: string, departmentId?: number) => {
    const q = new URLSearchParams();
    if (onDate) q.set("onDate", onDate);
    if (departmentId) q.set("departmentId", String(departmentId));
    return get<HrRoster>(`/api/hr/shifts/roster${q.size ? `?${q}` : ""}`);
  },
  assignments: (employeeId?: number, onDate?: string) => {
    const q = new URLSearchParams();
    if (employeeId) q.set("employeeId", String(employeeId));
    if (onDate) q.set("onDate", onDate);
    return get<HrShiftAssignment[]>(`/api/hr/shifts/assignments${q.size ? `?${q}` : ""}`);
  },
  assign: (body: Record<string, unknown>) =>
    post<HrShiftAssignment>("/api/hr/shifts/assignments", body),
  assignMany: (body: Record<string, unknown>) =>
    post<{ assigned: number; alreadyAssigned: string[] }>(
      "/api/hr/shifts/assignments/bulk",
      body
    ),
  endAssignment: (id: number, on?: string) =>
    post(`/api/hr/shifts/assignments/${id}/end${on ? `?on=${on}` : ""}`),

  requests: (status?: string) =>
    get<HrShiftRequest[]>(`/api/hr/shifts/requests${status ? `?status=${status}` : ""}`),
  request: (body: Record<string, unknown>) =>
    post<HrShiftRequest>("/api/hr/shifts/requests", body),
  decideRequest: (id: number, approve: boolean) =>
    post<HrShiftRequest>(`/api/hr/shifts/requests/${id}/decide?approve=${approve}`),

  attendanceRequests: (status?: string) =>
    get<HrAttendanceRequest[]>(
      `/api/hr/shifts/attendance-requests${status ? `?status=${status}` : ""}`
    ),
  requestAttendance: (body: Record<string, unknown>) =>
    post<HrAttendanceRequest>("/api/hr/shifts/attendance-requests", body),
  decideAttendanceRequest: (id: number, approve: boolean) =>
    post<HrAttendanceRequest>(
      `/api/hr/shifts/attendance-requests/${id}/decide?approve=${approve}`
    ),
};
