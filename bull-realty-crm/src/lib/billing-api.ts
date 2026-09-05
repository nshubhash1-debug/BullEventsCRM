import { apiRequest } from "@/lib/api";

function get<T>(path: string) {
  return apiRequest<T>(path, { method: "GET", auth: true });
}

function post<T>(path: string, body: unknown) {
  return apiRequest<T>(path, { method: "POST", body: JSON.stringify(body), auth: true });
}

/**
 * The statutory side of post-sales: tax invoices, credit notes, the cheque
 * drawer, and the TDS certificates buyers owe back.
 *
 * Deliberately a separate client from `post-sales-api`. Collections asks who to
 * chase; this asks what was filed. The two read the same bookings and want
 * entirely different lists.
 */

/* ------------------------------------------------------------------ *
 * Types
 * ------------------------------------------------------------------ */

export type TaxInvoice = {
  id: number;
  bookingId: number;
  bookingNumber: string;
  projectName: string;
  unitNumber: string;
  demandId: number;
  demandNumber: string;
  demandLabel: string;
  invoiceNumber: string;
  invoiceDate: string;
  treatment: string;
  treatmentLabel: string;
  sacCode: string;
  grossValue: number;
  landAbatement: number;
  taxableValue: number;
  gstRate: number;
  cgstAmount: number;
  sgstAmount: number;
  igstAmount: number;
  totalTax: number;
  invoiceTotal: number;
  credited: number;
  customerName: string;
  customerPan: string | null;
  placeOfSupply: string | null;
  status: string;
  notes: string | null;
};

export type CreditNote = {
  id: number;
  bookingId: number;
  bookingNumber: string;
  taxInvoiceId: number | null;
  invoiceNumber: string | null;
  creditNoteNumber: string;
  issuedOn: string;
  reason: string;
  reasonLabel: string;
  narrative: string | null;
  grossValue: number;
  taxableValue: number;
  gstRate: number;
  cgstAmount: number;
  sgstAmount: number;
  creditTotal: number;
};

export type Cheque = {
  id: number;
  bookingId: number;
  bookingNumber: string;
  customerName: string;
  projectName: string;
  unitNumber: string;
  demandId: number | null;
  demandNumber: string | null;
  chequeNumber: string;
  bankName: string;
  branchName: string | null;
  amount: number;
  chequeDate: string;
  receivedOn: string;
  depositedOn: string | null;
  clearedOn: string | null;
  bouncedOn: string | null;
  bounceReason: string | null;
  receiptId: number | null;
  status: string;
  notes: string | null;
  dueForBanking: boolean;
  daysSinceBankable: number;
};

export type TdsRow = {
  id: number;
  bookingId: number;
  bookingNumber: string;
  receiptId: number;
  receiptNumber: string;
  receivedOn: string;
  deductorName: string | null;
  deductorPan: string | null;
  amountPaid: number;
  tdsAmount: number;
  quarter: string | null;
  certificateNumber: string | null;
  certificateDate: string | null;
  challanNumber: string | null;
  receivedCertificateOn: string | null;
  status: string;
  notes: string | null;
  awaitingDays: number;
};

export type GstProfile = {
  id: number;
  projectId: number | null;
  projectName: string | null;
  legalName: string;
  tradeName: string | null;
  gstin: string;
  pan: string | null;
  stateName: string;
  stateCode: string;
  registeredAddress: string | null;
  defaultTreatment: string;
  defaultSacCode: string;
  occupancyCertificateOn: string | null;
  invoicePrefix: string;
  creditNotePrefix: string;
  bankAccountName: string | null;
  bankAccountNumber: string | null;
  bankIfsc: string | null;
  bankBranch: string | null;
  isActive: boolean;
};

export type BillingSummary = {
  invoicesDraft: number;
  invoicesIssued: number;
  taxBilled: number;
  taxCredited: number;
  chequesHeld: number;
  chequesHeldValue: number;
  chequesDueForBanking: number;
  chequesDueValue: number;
  chequesBounced: number;
  tdsAwaited: number;
  tdsAwaitedValue: number;
  tdsMismatched: number;
  tdsMismatchedValue: number;
  needsGstProfile: boolean;
};

export type BillingReference = {
  treatments: Array<{ value: string; label: string; rate: number }>;
  creditReasons: Array<{ value: string; label: string }>;
  invoiceStatuses: string[];
  chequeStatuses: string[];
  tdsStatuses: string[];
};

export type BulkResult = {
  count: number;
  raised: string[];
  skipped: string[];
};

/* ------------------------------------------------------------------ *
 * Client
 * ------------------------------------------------------------------ */

const q = (params: Record<string, string | number | null | undefined>) => {
  const search = new URLSearchParams();

  for (const [key, value] of Object.entries(params)) {
    if (value !== null && value !== undefined && value !== "") {
      search.set(key, String(value));
    }
  }

  const text = search.toString();
  return text ? `?${text}` : "";
};

export const billingApi = {
  summary: () => get<BillingSummary>("/api/billing/summary"),
  reference: () => get<BillingReference>("/api/billing/reference"),

  /* ---------------- invoices ---------------- */

  invoices: (filters: { bookingId?: number; status?: string } = {}) =>
    get<TaxInvoice[]>(`/api/billing/invoices${q(filters)}`),

  raise: (demandId: number, body: { treatment?: string | null; on?: string | null }) =>
    post<TaxInvoice>(`/api/billing/demands/${demandId}/invoice`, body),

  bulk: (projectId?: number) =>
    post<BulkResult>(`/api/billing/invoices/bulk${q({ projectId })}`, {}),

  issue: (id: number) => post<TaxInvoice>(`/api/billing/invoices/${id}/issue`, {}),

  cancelInvoice: (id: number, reason: string | null) =>
    post<TaxInvoice>(`/api/billing/invoices/${id}/cancel`, { reason }),

  /* ---------------- credit notes ---------------- */

  creditNotes: (filters: { bookingId?: number } = {}) =>
    get<CreditNote[]>(`/api/billing/credit-notes${q(filters)}`),

  credit: (
    invoiceId: number,
    body: { grossValue: number; reason: string; narrative?: string | null; on?: string | null }
  ) => post<CreditNote>(`/api/billing/invoices/${invoiceId}/credit`, body),

  /* ---------------- cheque register ---------------- */

  cheques: (filters: { bookingId?: number; status?: string } = {}) =>
    get<Cheque[]>(`/api/billing/cheques${q(filters)}`),

  takeCheque: (
    bookingId: number,
    body: {
      chequeNumber: string;
      bankName: string;
      branchName?: string | null;
      amount: number;
      chequeDate: string;
      demandId?: number | null;
      notes?: string | null;
    }
  ) => post<Cheque>(`/api/billing/bookings/${bookingId}/cheques`, body),

  deposit: (id: number, on?: string | null) =>
    post<Cheque>(`/api/billing/cheques/${id}/deposit`, { on: on ?? null }),

  clearCheque: (id: number, on?: string | null) =>
    post<Cheque>(`/api/billing/cheques/${id}/clear`, { on: on ?? null }),

  bounceCheque: (id: number, reason: string, on?: string | null) =>
    post<Cheque>(`/api/billing/cheques/${id}/bounce`, { reason, on: on ?? null }),

  returnCheque: (id: number, reason: string | null) =>
    post<Cheque>(`/api/billing/cheques/${id}/return`, { reason }),

  /* ---------------- TDS ---------------- */

  tds: (filters: { bookingId?: number; status?: string } = {}) =>
    get<TdsRow[]>(`/api/billing/tds${q(filters)}`),

  syncTds: (bookingId?: number) =>
    post<{ opened: number }>(`/api/billing/tds/sync${q({ bookingId })}`, {}),

  recordCertificate: (
    id: number,
    body: {
      certificateNumber: string;
      certificateDate: string;
      challanNumber?: string | null;
      certifiedAmount?: number | null;
      fileUrl?: string | null;
    }
  ) => post<TdsRow>(`/api/billing/tds/${id}/certificate`, body),

  verifyCertificate: (id: number) => post<TdsRow>(`/api/billing/tds/${id}/verify`, {}),

  /* ---------------- GST profiles ---------------- */

  profiles: () => get<GstProfile[]>("/api/billing/gst-profiles"),

  saveProfile: (body: Partial<GstProfile> & { legalName: string; gstin: string; stateName: string }) =>
    post<GstProfile>("/api/billing/gst-profiles", body),
};

/* ------------------------------------------------------------------ *
 * Presentation
 * ------------------------------------------------------------------ */

export const INVOICE_TONE: Record<string, "neutral" | "info" | "success" | "warning" | "danger"> = {
  Draft: "neutral",
  Issued: "success",
  Credited: "warning",
  Cancelled: "danger",
};

export const CHEQUE_TONE: Record<string, "neutral" | "info" | "success" | "warning" | "danger"> = {
  Held: "neutral",
  Deposited: "info",
  Cleared: "success",
  Bounced: "danger",
  Returned: "warning",
};

export const TDS_TONE: Record<string, "neutral" | "info" | "success" | "warning" | "danger"> = {
  Awaited: "warning",
  Received: "info",
  Verified: "success",
  Mismatched: "danger",
};

/**
 * What each cheque status means in the drawer, said plainly.
 *
 * "Held" is the one worth spelling out — a developer reading the register wants
 * to know it means the paper is in a folder and the money is not in the bank.
 */
export const CHEQUE_HINT: Record<string, string> = {
  Held: "In the drawer",
  Deposited: "At the bank",
  Cleared: "Money in",
  Bounced: "Returned unpaid",
  Returned: "Handed back",
};
