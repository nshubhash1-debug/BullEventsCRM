import { apiRequest } from "@/lib/api";
import type { FilterField, PagedResult, QueryRequest } from "@/lib/query";

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

function put<T>(path: string, body: unknown) {
  return apiRequest<T>(path, {
    method: "PUT",
    body: JSON.stringify(body),
    auth: true,
  });
}

/* ====================================================================== *
 * Payroll and statutory
 * ====================================================================== */

/**
 * A payslip with its statutory working shown.
 *
 * The employer contributions ride along with the employee's because they are
 * what the monthly returns are filed from, and because cost to company means
 * nothing without them.
 */
export interface HrPayslip {
  id: number;
  payrollRunId: number;
  employeeId: number;
  employeeName: string;
  gross: number;
  lopDays: number;
  lopAmount: number;
  otherDeductions: number;
  pfEmployee: number;
  esicEmployee: number;
  incentive: number;
  overtimeAmount: number;
  net: number;

  payableGross: number;
  pfWage: number;
  pfEmployer: number;
  epsEmployer: number;
  epfEmployer: number;
  edli: number;
  pfAdminCharges: number;
  esicEmployer: number;
  professionalTax: number;
  lwfEmployee: number;
  lwfEmployer: number;
  tds: number;
  projectedAnnualTaxable: number;
  projectedAnnualTax: number;
  hraExemption: number;
  taxRegime: string | null;
  gratuityAccrual: number;
  costToCompany: number;
  totalDeductions: number;
}

export interface HrPtSlab {
  id: number;
  state: string;
  fromAmount: number;
  toAmount: number | null;
  amount: number;
  /** Set only on a month-specific row, like Maharashtra's February. */
  month: number | null;
  gender: string | null;
  effectiveFrom: string;
  isActive: boolean;
}

export interface HrIncomeTaxSlab {
  id: number;
  financialYear: number;
  regime: string;
  fromAmount: number;
  toAmount: number | null;
  rate: number;
  sortOrder: number;
}

export interface HrTaxRegime {
  id: number;
  financialYear: number;
  regime: string;
  standardDeduction: number;
  rebateIncomeCeiling: number;
  rebateMaximum: number;
  allowsChapterViaDeductions: boolean;
  allowsHraExemption: boolean;
  surchargeBands: string | null;
}

/** Rates arrive as fractions — 0.12, not 12. Multiply only for display. */
export interface HrStatutoryConfig {
  id: number;
  effectiveFrom: string;
  label: string;

  pfEnabled: boolean;
  pfEmployeeRate: number;
  pfEmployerRate: number;
  pfWageCeiling: number;
  pfRestrictEmployeeToCeiling: boolean;
  pfRestrictEmployerToCeiling: boolean;
  epsRate: number;
  epsWageCeiling: number;
  edliRate: number;
  pfAdminRate: number;

  esiEnabled: boolean;
  esiEmployeeRate: number;
  esiEmployerRate: number;
  esiWageThreshold: number;

  ptEnabled: boolean;
  ptDefaultState: string | null;

  tdsEnabled: boolean;
  cessRate: number;

  lwfEnabled: boolean;
  lwfEmployeeAmount: number;
  lwfEmployerAmount: number;
  lwfMonths: string | null;

  gratuityEnabled: boolean;
  gratuityDaysPerYear: number;
  gratuityMonthDays: number;
  gratuityEligibleYears: number;
  gratuityCeiling: number;

  notes: string | null;

  financialYear: number;
  professionalTaxSlabs: HrPtSlab[];
  incomeTaxSlabs: HrIncomeTaxSlab[];
  regimes: HrTaxRegime[];
}

export interface HrTaxProfile {
  id: number;
  employeeId: number;
  employeeName: string;
  financialYear: number;
  regime: string;
  annualRentPaid: number;
  rentsInMetro: boolean;
  landlordPan: string | null;
  section80C: number;
  section80Ccd1B: number;
  section80D: number;
  housingLoanInterest: number;
  section80Tta: number;
  otherDeductions: number;
  /** What the caps actually allow of the declarations above. */
  totalChapterVia: number;
  otherIncome: number;
  previousEmployerSalary: number;
  previousEmployerTds: number;
  proofsSubmitted: boolean;
  proofsSubmittedOn: string | null;
  notes: string | null;
}

export interface HrPtStateTotal {
  state: string;
  employees: number;
  amount: number;
}

/** A month's remittances, totalled by the authority each is paid to. */
export interface HrStatutorySummary {
  runId: number;
  year: number;
  month: number;
  status: string;
  slipCount: number;
  remittanceDueOn: string;

  pfMembers: number;
  pfEmployee: number;
  eps: number;
  epf: number;
  edli: number;
  pfAdmin: number;
  pfTotal: number;

  esiMembers: number;
  esiEmployee: number;
  esiEmployer: number;
  esiTotal: number;

  ptTotal: number;
  ptByState: HrPtStateTotal[];

  lwfTotal: number;

  tdsMembers: number;
  tdsTotal: number;

  grossTotal: number;
  netTotal: number;
  costToCompany: number;

  /** The gaps that get a filing rejected, not the ones that merely look untidy. */
  missingUan: number;
  missingEsiIp: number;
  missingPan: number;
}

export type HrRegisterFile =
  | "pf-ecr.txt"
  | "esi.csv"
  | "pt.csv"
  | "tds.csv"
  | "register.csv";

/** The states this build carries professional tax slabs for. */
export const PT_STATES = [
  "Maharashtra", "Karnataka", "West Bengal", "Tamil Nadu", "Andhra Pradesh",
  "Telangana", "Gujarat", "Madhya Pradesh", "Kerala", "Odisha", "Assam",
  "Bihar", "Jharkhand", "Meghalaya", "Tripura", "Puducherry", "Sikkim",
  "Nagaland", "Manipur", "Mizoram",
] as const;

export interface HrEmployee {
  id: number;
  employeeCode: string;
  name: string;
  phone: string | null;
  email: string | null;
  departmentId: number | null;
  departmentName: string;
  designationName: string;
  managerName: string | null;
  location: string | null;
  joiningDate: string;
  employmentType: string;
  status: string;
  collarType: string;
  shiftName: string | null;
}

export interface HrDashboard {
  headcount: number;
  presentToday: number;
  absentToday: number;
  onLeaveToday: number;
  wfhToday: number;
  lateToday: number;
  joinersThisMonth: number;
  exitsThisMonth: number;
  pendingApprovals: number;
  pendingExpenses: number;
  openTickets: number;
  activeGoals: number;
  interviewsThisWeek: number;
  holidaysUpcoming: number;
}

export interface HrOrgNode {
  id: number;
  employeeCode: string;
  name: string;
  title: string;
  department: string;
  children: HrOrgNode[];
}

export const hrEmployeesApi = {
  fields: () => get<FilterField[]>("/api/hr/employees/fields"),
  query: (request: QueryRequest) =>
    post<PagedResult<HrEmployee>>("/api/hr/employees/query", request),
  get: (id: number) => get<HrEmployee>(`/api/hr/employees/${id}`),
  create: (body: Record<string, unknown>) =>
    post<HrEmployee>("/api/hr/employees", body),
  update: (id: number, body: Record<string, unknown>) =>
    put<HrEmployee>(`/api/hr/employees/${id}`, body),
  onboarding: (id: number) =>
    get<{ id: number; title: string; done: boolean }[]>(
      `/api/hr/employees/${id}/onboarding`
    ),
  toggleOnboarding: (itemId: number) =>
    post<{ id: number; title: string; done: boolean }>(
      `/api/hr/employees/onboarding/${itemId}/toggle`
    ),
  documents: (id: number) =>
    get<{ id: number; documentType: string; fileName: string; expiryDate: string | null }[]>(
      `/api/hr/employees/${id}/documents`
    ),
  addDocument: (id: number, body: Record<string, unknown>) =>
    post(`/api/hr/employees/${id}/documents`, body),
};

export const hrApi = {
  dashboard: () => get<HrDashboard>("/api/hr/dashboard"),
  me: () => get<{ employee: HrEmployee; balances: { leaveType: string; closing: number }[]; recentPayslips: { id: number; net: number; payrollRunId: number }[] }>("/api/hr/me"),
  departments: () =>
    get<{ id: number; name: string; code: string | null; location: string | null; isActive: boolean }[]>(
      "/api/hr/departments"
    ),
  createDepartment: (body: Record<string, unknown>) => post("/api/hr/departments", body),
  shifts: () =>
    get<{ id: number; name: string; startTime: string; endTime: string; graceMinutes: number; weeklyOff: string }[]>(
      "/api/hr/shifts"
    ),
  createShift: (body: Record<string, unknown>) => post("/api/hr/shifts", body),
  leaveTypes: () =>
    get<{ id: number; code: string; name: string; monthlyEntitlement: number; paid: boolean; approvalLevels: number }[]>(
      "/api/hr/leave-types"
    ),
  createLeaveType: (body: Record<string, unknown>) => post("/api/hr/leave-types", body),
  attendance: (from?: string, to?: string) => {
    const q = new URLSearchParams();
    if (from) q.set("from", from);
    if (to) q.set("to", to);
    return get<
      {
        id: number;
        employeeId: number;
        employeeName: string;
        workDate: string;
        inTime: string | null;
        outTime: string | null;
        status: string;
        isLate: boolean;
      }[]
    >(`/api/hr/attendance?${q}`);
  },
  markAttendance: (body: Record<string, unknown>) => post("/api/hr/attendance", body),
  corrections: () =>
    get<{ id: number; employeeName: string; workDate: string; reason: string; status: string }[]>(
      "/api/hr/corrections"
    ),
  requestCorrection: (body: Record<string, unknown>) => post("/api/hr/corrections", body),
  decideCorrection: (id: number, approve: boolean) =>
    post(`/api/hr/corrections/${id}/decide?approve=${approve}`),
  leaveRequests: () =>
    get<{ id: number; employeeName: string; leaveType: string; fromDate: string; toDate: string; status: string }[]>(
      "/api/hr/leave-requests"
    ),
  applyLeave: (body: Record<string, unknown>) => post("/api/hr/leave-requests", body),
  decideLeave: (id: number, approve: boolean) =>
    post(`/api/hr/leave-requests/${id}/decide?approve=${approve}`),
  salary: (employeeId: number) =>
    get<{ gross: number; basic: number; hra: number } | null>(`/api/hr/salary/${employeeId}`),
  saveSalary: (body: Record<string, unknown>) => post("/api/hr/salary", body),
  payrollRuns: () =>
    get<{ id: number; year: number; month: number; status: string; slipCount: number; netTotal: number }[]>(
      "/api/hr/payroll"
    ),
  processPayroll: (year: number, month: number) =>
    post(`/api/hr/payroll/process?year=${year}&month=${month}`),
  reviewPayroll: (id: number) => post(`/api/hr/payroll/${id}/review`),
  slips: (runId: number) => get<HrPayslip[]>(`/api/hr/payroll/${runId}/slips`),
  payslipHtmlUrl: (slipId: number) => `/api/hr/payslips/${slipId}/html`,

  /* ---------------- statutory ---------------- */

  statutoryConfig: () => get<HrStatutoryConfig | null>("/api/hr/statutory/config"),
  saveStatutoryConfig: (body: Record<string, unknown>) =>
    post<number>("/api/hr/statutory/config", body),
  statutorySummary: (runId: number) =>
    get<HrStatutorySummary>(`/api/hr/statutory/runs/${runId}/summary`),
  taxProfiles: (financialYear?: number) =>
    get<HrTaxProfile[]>(
      `/api/hr/statutory/tax-profiles${financialYear ? `?financialYear=${financialYear}` : ""}`
    ),
  saveTaxProfile: (body: Record<string, unknown>) =>
    post<HrTaxProfile>("/api/hr/statutory/tax-profiles", body),

  /// The registers download rather than render — they go to a portal, not a screen.
  registerUrl: (runId: number, file: HrRegisterFile) =>
    `/api/hr/statutory/runs/${runId}/${file}`,
  vacancies: () =>
    get<{ id: number; position: string; location: string | null; status: string }[]>(
      "/api/hr/vacancies"
    ),
  createVacancy: (body: Record<string, unknown>) => post("/api/hr/vacancies", body),
  candidates: () =>
    get<{ id: number; name: string; stage: string; phone: string | null; convertedEmployeeId: number | null }[]>(
      "/api/hr/candidates"
    ),
  createCandidate: (body: Record<string, unknown>) => post("/api/hr/candidates", body),
  moveStage: (id: number, stage: string) =>
    post(`/api/hr/candidates/${id}/stage?stage=${encodeURIComponent(stage)}`),
  convertCandidate: (id: number) => post(`/api/hr/candidates/${id}/convert`),
  generateLetter: (employeeId: number, kind: string) =>
    post("/api/hr/letters", { employeeId, kind }),
  resignations: () =>
    get<{ id: number; employeeName: string; lastWorkingDay: string; status: string; employeeId: number }[]>(
      "/api/hr/resignations"
    ),
  resign: (body: Record<string, unknown>) => post("/api/hr/resignations", body),
  decideResignation: (id: number, approve: boolean) =>
    post(`/api/hr/resignations/${id}/decide?approve=${approve}`),
  saveFnf: (body: Record<string, unknown>) => post("/api/hr/fnf", body),
  deployments: () =>
    get<{ id: number; employeeName: string; eventName: string; eventDate: string; roleOnSite: string; overtimeHours: number; incentiveAmount: number }[]>(
      "/api/hr/deployments"
    ),
  deploy: (body: Record<string, unknown>) => post("/api/hr/deployments", body),
  employeeCsvUrl: () => {
    const root = process.env.NEXT_PUBLIC_API_URL?.replace(/\/$/, "") ?? "http://localhost:5090";
    return `${root}/api/hr/reports/employees.csv`;
  },
};

export const hrx = {
  org: () => get<HrOrgNode[]>("/api/hr/org"),
  punches: () =>
    get<{ id: number; employeeName: string; at: string; kind: string; latitude: number | null; longitude: number | null }[]>(
      "/api/hr/punches"
    ),
  punch: (body: Record<string, unknown>) => post("/api/hr/punches", body),
  holidays: () =>
    get<{ id: number; name: string; onDate: string; optional: boolean; locations: string | null }[]>(
      "/api/hr/holidays"
    ),
  createHoliday: (body: Record<string, unknown>) => post("/api/hr/holidays", body),
  policies: () =>
    get<{ id: number; title: string; category: string; body: string; effectiveFrom: string }[]>(
      "/api/hr/policies"
    ),
  createPolicy: (body: Record<string, unknown>) => post("/api/hr/policies", body),
  announcements: () =>
    get<{ id: number; title: string; body: string; pinUntil: string | null; audience: string }[]>(
      "/api/hr/announcements"
    ),
  createAnnouncement: (body: Record<string, unknown>) => post("/api/hr/announcements", body),
  expenses: () =>
    get<{ id: number; employeeName: string; claimDate: string; category: string; amount: number; description: string | null; status: string }[]>(
      "/api/hr/expenses"
    ),
  createExpense: (body: Record<string, unknown>) => post("/api/hr/expenses", body),
  decideExpense: (id: number, approve: boolean) =>
    post(`/api/hr/expenses/${id}/decide?approve=${approve}`),
  tickets: () =>
    get<{ id: number; employeeName: string; subject: string; category: string; priority: string; body: string; status: string }[]>(
      "/api/hr/tickets"
    ),
  createTicket: (body: Record<string, unknown>) => post("/api/hr/tickets", body),
  ticketStatus: (id: number, status: string) =>
    post(`/api/hr/tickets/${id}/status?status=${encodeURIComponent(status)}`),
  cycles: () =>
    get<{ id: number; name: string; fromDate: string; toDate: string; status: string }[]>(
      "/api/hr/cycles"
    ),
  createCycle: (body: Record<string, unknown>) => post("/api/hr/cycles", body),
  goals: () =>
    get<{ id: number; employeeName: string; title: string; kra: string | null; target: string | null; weight: number; progress: number; status: string }[]>(
      "/api/hr/goals"
    ),
  createGoal: (body: Record<string, unknown>) => post("/api/hr/goals", body),
  goalProgress: (id: number, progress: number) =>
    post(`/api/hr/goals/${id}/progress?progress=${progress}`),
  appraisals: () =>
    get<{ id: number; employeeName: string; selfScore: number | null; managerScore: number | null; rating: string | null; status: string }[]>(
      "/api/hr/appraisals"
    ),
  saveAppraisal: (body: Record<string, unknown>) => post("/api/hr/appraisals", body),
  interviews: () =>
    get<{ id: number; candidateName: string; scheduledAt: string; panel: string | null; mode: string; score: number | null; recommendation: string | null; status: string }[]>(
      "/api/hr/interviews"
    ),
  createInterview: (body: Record<string, unknown>) => post("/api/hr/interviews", body),
  scoreInterview: (id: number, score: number, recommendation: string) =>
    post(`/api/hr/interviews/${id}/score?score=${score}&recommendation=${encodeURIComponent(recommendation)}`),
  trainings: () =>
    get<{ id: number; title: string; trainer: string | null; fromDate: string; toDate: string; venue: string | null; status: string; enrolments: number }[]>(
      "/api/hr/trainings"
    ),
  createTraining: (body: Record<string, unknown>) => post("/api/hr/trainings", body),
  enrol: (id: number, employeeId: number) =>
    post(`/api/hr/trainings/${id}/enrol?employeeId=${employeeId}`),
  lifecycle: () =>
    get<{ id: number; employeeName: string; kind: string; effectiveOn: string; fromValue: string | null; toValue: string | null; notes: string | null; status: string }[]>(
      "/api/hr/lifecycle"
    ),
  createLifecycle: (body: Record<string, unknown>) => post("/api/hr/lifecycle", body),
  applyLifecycle: (id: number) => post(`/api/hr/lifecycle/${id}/apply`),
  assets: () =>
    get<{ id: number; employeeId: number; assetType: string; serialNo: string | null; issueDate: string; condition: string; returnDate: string | null }[]>(
      "/api/hr/assets"
    ),
  issueAsset: (body: Record<string, unknown>) => post("/api/hr/assets", body),
  returnAsset: (id: number) => post(`/api/hr/assets/${id}/return`),
};
