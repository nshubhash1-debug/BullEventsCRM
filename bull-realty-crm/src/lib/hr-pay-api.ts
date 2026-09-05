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

/* ---------------- components ---------------- */

export interface HrSalaryComponent {
  id: number;
  name: string;
  abbreviation: string;
  /** Earning or Deduction. */
  componentType: string;
  /** Fixed, PercentOfBase or Formula. */
  calculation: string;
  formula: string | null;
  affectsPf: boolean;
  affectsEsi: boolean;
  isTaxable: boolean;
  isHra: boolean;
  dependsOnPaymentDays: boolean;
  /** Computed by the payroll engine, not from a formula. */
  isStatutory: boolean;
  sortOrder: number;
  isActive: boolean;
  notes: string | null;
  usedInStructures: number;
}

export interface HrFormulaTry {
  result: number;
  error: string | null;
  references: string[];
}

/* ---------------- structures ---------------- */

export interface HrPayStructureLine {
  id: number;
  salaryComponentId: number;
  componentName: string;
  abbreviation: string;
  componentType: string;
  calculation: string;
  amount: number;
  formula: string | null;
  sortOrder: number;
}

export interface HrPayStructure {
  id: number;
  name: string;
  notes: string | null;
  isActive: boolean;
  overtimeRate: number;
  employeesAssigned: number;
  lines: HrPayStructureLine[];
}

export interface HrStructurePreviewLine {
  name: string;
  abbreviation: string;
  componentType: string;
  amount: number;
  formula: string | null;
  error: string | null;
}

export interface HrStructurePreview {
  structureName: string;
  base: number;
  gross: number;
  deductions: number;
  net: number;
  lines: HrStructurePreviewLine[];
}

/* ---------------- assignments ---------------- */

export interface HrPayAssignment {
  id: number;
  employeeId: number;
  employeeName: string;
  payStructureId: number;
  structureName: string;
  base: number;
  effectiveFrom: string;
}

export interface HrComponentAmount {
  name: string;
  abbreviation: string;
  componentType: string;
  amount: number;
  affectsPf: boolean;
  isHra: boolean;
  isTaxable: boolean;
  dependsOnPaymentDays: boolean;
}

export interface HrPayBreakdown {
  employeeId: number;
  employeeName: string;
  asOf: string;
  /** True when this came from the older Basic/HRA/Allowances record. */
  fromLegacyStructure: boolean;
  gross: number;
  pfWageBase: number;
  hra: number;
  deductions: number;
  components: HrComponentAmount[];
}

/* ---------------- one-off pay and advances ---------------- */

export interface HrAdditionalSalary {
  id: number;
  employeeId: number;
  employeeName: string;
  salaryComponentId: number;
  componentName: string;
  componentType: string;
  amount: number;
  year: number;
  month: number;
  isRecurring: boolean;
  recurringUntil: string | null;
  dependsOnPaymentDays: boolean;
  reason: string | null;
  status: string;
  paidInPayrollRunId: number | null;
}

export interface HrAdvance {
  id: number;
  employeeId: number;
  employeeName: string;
  amount: number;
  instalments: number;
  instalmentAmount: number;
  recoveryStartYear: number;
  recoveryStartMonth: number;
  purpose: string | null;
  status: string;
  paidOn: string | null;
  writeOffReason: string | null;
  recovered: number;
  outstanding: number;
  instalmentsTaken: number;
}

/* ---------------- payslip detail ---------------- */

export interface HrPayslipLine {
  name: string;
  abbreviation: string;
  componentType: string;
  amount: number;
  isStatutory: boolean;
  sortOrder: number;
}

/* ---------------- Form 16 ---------------- */

export interface HrForm16 {
  employeeId: number;
  employeeName: string;
  employeeCode: string;
  pan: string | null;
  financialYear: number;
  regime: string;
  monthsPaid: number;
  grossSalary: number;
  hraExemption: number;
  standardDeduction: number;
  professionalTax: number;
  chapterViaDeductions: number;
  housingLoanInterest: number;
  taxableIncome: number;
  taxDeducted: number;
  providentFundEmployee: number;
  projectedAnnualTax: number;
}

export const ADVANCE_STATUSES = [
  "Requested", "Approved", "Paid", "Recovering", "Closed", "Rejected", "WrittenOff",
] as const;

export const payApi = {
  components: () => get<HrSalaryComponent[]>("/api/hr/pay/components"),
  saveComponent: (body: Record<string, unknown>) =>
    post<number>("/api/hr/pay/components", body),
  tryFormula: (formula: string, base: number, values?: Record<string, number>) =>
    post<HrFormulaTry>("/api/hr/pay/components/try-formula", { formula, base, values }),

  structures: () => get<HrPayStructure[]>("/api/hr/pay/structures"),
  saveStructure: (body: Record<string, unknown>) =>
    post<number>("/api/hr/pay/structures", body),
  preview: (id: number, base: number) =>
    get<HrStructurePreview>(`/api/hr/pay/structures/${id}/preview?base=${base}`),

  assignments: (employeeId?: number) =>
    get<HrPayAssignment[]>(
      `/api/hr/pay/assignments${employeeId ? `?employeeId=${employeeId}` : ""}`
    ),
  assign: (body: Record<string, unknown>) =>
    post<HrPayAssignment>("/api/hr/pay/assignments", body),
  breakdown: (employeeId: number, asOf?: string) =>
    get<HrPayBreakdown>(
      `/api/hr/pay/employees/${employeeId}/breakdown${asOf ? `?asOf=${asOf}` : ""}`
    ),

  additional: (year?: number, month?: number) => {
    const q = new URLSearchParams();
    if (year) q.set("year", String(year));
    if (month) q.set("month", String(month));
    return get<HrAdditionalSalary[]>(`/api/hr/pay/additional${q.size ? `?${q}` : ""}`);
  },
  addAdditional: (body: Record<string, unknown>) =>
    post<HrAdditionalSalary>("/api/hr/pay/additional", body),
  cancelAdditional: (id: number) => post(`/api/hr/pay/additional/${id}/cancel`),

  advances: (status?: string) =>
    get<HrAdvance[]>(`/api/hr/pay/advances${status ? `?status=${status}` : ""}`),
  requestAdvance: (body: Record<string, unknown>) =>
    post<HrAdvance>("/api/hr/pay/advances", body),
  setAdvanceStatus: (id: number, status: string, reason?: string) =>
    post<HrAdvance>(
      `/api/hr/pay/advances/${id}/status?status=${status}` +
        (reason ? `&reason=${encodeURIComponent(reason)}` : "")
    ),

  payslipLines: (payslipId: number) =>
    get<HrPayslipLine[]>(`/api/hr/payslips/${payslipId}/lines`),

  form16: (financialYear?: number) =>
    get<HrForm16[]>(
      `/api/hr/statutory/form16${financialYear ? `?financialYear=${financialYear}` : ""}`
    ),
};
