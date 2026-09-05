import { apiRequest } from "@/lib/api";

/* ------------------------------------------------------------------ *
 * The post-sales file: agreement, bank, keys, and the two ways a
 * booking can end early — plus the letters that go out along the way.
 * ------------------------------------------------------------------ */

function get<T>(path: string) {
  return apiRequest<T>(path, { method: "GET", auth: true });
}

function post<T>(path: string, body: unknown) {
  return apiRequest<T>(path, { method: "POST", body: JSON.stringify(body), auth: true });
}

function put<T>(path: string, body: unknown) {
  return apiRequest<T>(path, { method: "PUT", body: JSON.stringify(body), auth: true });
}

function del<T>(path: string) {
  return apiRequest<T>(path, { method: "DELETE", auth: true });
}

/* ---------------- agreement ---------------- */

export const AGREEMENT_LADDER = [
  "NotStarted", "Drafted", "WithCustomer", "Franked", "Executed", "Registered",
] as const;

export interface Agreement {
  bookingId: number;
  status: string;
  statusLabel: string;
  allotmentLetterOn: string | null;
  draftSharedOn: string | null;
  frankedOn: string | null;
  executedOn: string | null;
  registeredOn: string | null;
  considerationValue: number;
  stampDuty: number;
  registrationFee: number;
  registrationNumber: string | null;
  subRegistrarOffice: string | null;
  notes: string | null;
  /** The rung this can move to next, or null once registered. */
  nextStatus: string | null;
}

/* ---------------- home loan ---------------- */

export interface HomeLoan {
  bookingId: number;
  bankName: string;
  branchName: string | null;
  applicationNumber: string | null;
  appliedOn: string | null;
  requestedAmount: number;
  sanctionedAmount: number;
  sanctionedOn: string | null;
  sanctionValidUntil: string | null;
  tripartiteSignedOn: string | null;
  disbursedAmount: number;
  undisbursedAmount: number;
  status: string;
  statusLabel: string;
  /** A lapsed sanction is why a bank-funded instalment stops arriving. */
  sanctionLapsed: boolean;
}

/* ---------------- possession ---------------- */

export interface Possession {
  bookingId: number;
  committedOn: string | null;
  offeredOn: string | null;
  inspectedOn: string | null;
  snagsRaised: number;
  snagsClosed: number;
  snagsOpen: number;
  maintenanceAdvanceMonths: number;
  maintenanceAmount: number;
  maintenanceCollected: boolean;
  corpusDeposit: number;
  corpusCollected: boolean;
  duesCleared: boolean;
  documentsHandedOver: boolean;
  handedOverOn: string | null;
  status: string;
  statusLabel: string;
  readyToHandOver: boolean;
  /** Everything still standing between here and the keys. */
  blockers: string[];
}

/* ---------------- ending early ---------------- */

export interface Cancellation {
  id: number;
  bookingId: number;
  requestedOn: string;
  reason: string;
  amountReceived: number;
  deductionPercent: number;
  deductionAmount: number;
  brokerageRecovered: number;
  otherDeductions: number;
  refundAmount: number;
  approvedOn: string | null;
  refundedOn: string | null;
  refundReference: string | null;
  status: string;
}

export interface Transfer {
  id: number;
  bookingId: number;
  requestedOn: string;
  fromName: string;
  toName: string;
  toPhone: string | null;
  toEmail: string | null;
  toPan: string | null;
  transferChargePercent: number;
  transferChargeAmount: number;
  transferChargeReceived: number;
  approvedOn: string | null;
  completedOn: string | null;
  status: string;
}

export interface Lifecycle {
  agreement: Agreement | null;
  loan: HomeLoan | null;
  possession: Possession | null;
  cancellations: Cancellation[];
  transfers: Transfer[];
}

/* ---------------- documents ---------------- */

export interface MergeField {
  token: string;
  label: string;
  group: string;
  example: string;
}

export interface TemplateReference {
  kinds: string[];
  mergeFields: MergeField[];
}

export interface DocumentTemplate {
  id: number;
  kind: string;
  name: string;
  description: string | null;
  subject: string | null;
  body: string;
  isDefault: boolean;
  isSystem: boolean;
  isActive: boolean;
  /** How many letters this template has produced. */
  usedCount: number;
}

export interface SaveTemplate {
  kind: string;
  name: string;
  description?: string | null;
  subject?: string | null;
  body: string;
  isDefault: boolean;
  isActive: boolean;
}

export interface Preview {
  subject: string;
  body: string;
  /** Tokens that did not resolve. A letter with blanks in it. */
  unresolved: string[];
}

export interface GeneratedDocument {
  id: number;
  bookingId: number;
  demandId: number | null;
  kind: string;
  number: string;
  title: string;
  subject: string | null;
  status: string;
  generatedAt: string;
  issuedAt: string | null;
  sentAt: string | null;
  sentTo: string | null;
  sentVia: string | null;
}

export interface BulkGenerateResult {
  produced: number;
  skipped: number;
  notes: string[];
}

/* ------------------------------------------------------------------ *
 * Calls
 * ------------------------------------------------------------------ */

export const lifecycleApi = {
  forBooking: (id: number) => get<Lifecycle>(`/api/bookings/${id}/lifecycle`),

  advanceAgreement: (
    id: number,
    body: {
      toStatus: string;
      on?: string | null;
      registrationNumber?: string | null;
      subRegistrarOffice?: string | null;
      stampDuty?: number | null;
      registrationFee?: number | null;
    }
  ) => post<Agreement>(`/api/bookings/${id}/agreement/advance`, body),

  saveLoan: (
    id: number,
    body: {
      bankName: string;
      branchName?: string | null;
      applicationNumber?: string | null;
      requestedAmount: number;
    }
  ) => put<HomeLoan>(`/api/bookings/${id}/loan`, body),

  sanction: (
    id: number,
    body: { sanctionedAmount: number; sanctionedOn?: string | null; validUntil?: string | null }
  ) => post<HomeLoan>(`/api/bookings/${id}/loan/sanction`, body),

  tripartite: (id: number, on: string | null) =>
    post<HomeLoan>(`/api/bookings/${id}/loan/tripartite`, on),

  disburse: (id: number, body: { amount: number; on?: string | null; reference?: string | null }) =>
    post<HomeLoan>(`/api/bookings/${id}/loan/disburse`, body),

  offerPossession: (
    id: number,
    body: {
      on?: string | null;
      maintenanceAmount: number;
      maintenanceMonths: number;
      corpusDeposit: number;
    }
  ) => post<Possession>(`/api/bookings/${id}/possession/offer`, body),

  inspect: (id: number, body: { on?: string | null; snagsRaised: number }) =>
    post<Possession>(`/api/bookings/${id}/possession/inspect`, body),

  closeSnags: (id: number, closed: number) =>
    post<Possession>(`/api/bookings/${id}/possession/snags`, { closed }),

  collect: (id: number, body: { maintenance: boolean; corpus: boolean }) =>
    post<Possession>(`/api/bookings/${id}/possession/collect`, body),

  handOver: (id: number, body: { on?: string | null; documentsHandedOver: boolean }) =>
    post<Possession>(`/api/bookings/${id}/possession/handover`, body),

  requestCancellation: (
    id: number,
    body: { reason: string; deductionPercent: number; otherDeductions: number }
  ) => post<Cancellation>(`/api/bookings/${id}/cancellations`, body),

  approveCancellation: (id: number, cancellationId: number) =>
    post<Cancellation>(`/api/bookings/${id}/cancellations/${cancellationId}/approve`, {}),

  refund: (id: number, cancellationId: number, body: { on?: string | null; reference: string }) =>
    post<Cancellation>(`/api/bookings/${id}/cancellations/${cancellationId}/refund`, body),

  requestTransfer: (
    id: number,
    body: {
      toName: string;
      toPhone?: string | null;
      toEmail?: string | null;
      toPan?: string | null;
      transferChargePercent: number;
    }
  ) => post<Transfer>(`/api/bookings/${id}/transfers`, body),

  transferPayment: (id: number, transferId: number, amount: number) =>
    post<Transfer>(`/api/bookings/${id}/transfers/${transferId}/payment`, { amount }),

  completeTransfer: (id: number, transferId: number) =>
    post<Transfer>(`/api/bookings/${id}/transfers/${transferId}/complete`, {}),
};

export const documentsApi = {
  reference: () => get<TemplateReference>("/api/post-sales/documents/reference"),

  templates: () => get<DocumentTemplate[]>("/api/post-sales/documents/templates"),
  createTemplate: (body: SaveTemplate) =>
    post<DocumentTemplate>("/api/post-sales/documents/templates", body),
  updateTemplate: (id: number, body: SaveTemplate) =>
    put<DocumentTemplate>(`/api/post-sales/documents/templates/${id}`, body),
  deleteTemplate: (id: number) =>
    del<void>(`/api/post-sales/documents/templates/${id}`),

  preview: (body: {
    body: string;
    subject?: string | null;
    bookingId: number;
    demandId?: number | null;
    receiptId?: number | null;
  }) => post<Preview>("/api/post-sales/documents/preview", body),

  forBooking: (bookingId: number) =>
    get<GeneratedDocument[]>(`/api/bookings/${bookingId}/letters`),

  read: (id: number) =>
    get<{ number: string; title: string; subject: string | null; body: string }>(
      `/api/post-sales/documents/${id}/body`
    ),

  generate: (
    bookingId: number,
    body: { kind: string; templateId?: number | null; demandId?: number | null; receiptId?: number | null }
  ) => post<GeneratedDocument>(`/api/bookings/${bookingId}/letters`, body),

  bulk: (body: { kind: string; templateId?: number | null; demandIds: number[] }) =>
    post<BulkGenerateResult>("/api/post-sales/documents/bulk", body),

  issue: (id: number) => post<GeneratedDocument>(`/api/post-sales/documents/${id}/issue`, {}),

  markSent: (id: number, body: { via: string; to?: string | null }) =>
    post<GeneratedDocument>(`/api/post-sales/documents/${id}/sent`, body),
};

/* ---------------- vocabulary ---------------- */

export const TEMPLATE_LABELS: Record<string, string> = {
  DemandLetter: "Demand letter",
  ReminderLetter: "Payment reminder",
  AllotmentLetter: "Allotment letter",
  WelcomeLetter: "Welcome letter",
  PaymentReceipt: "Payment receipt",
  StatementOfAccount: "Statement of account",
  PossessionOffer: "Offer of possession",
  PossessionLetter: "Possession letter",
  NocForLoan: "NOC for home loan",
  CancellationLetter: "Cancellation letter",
  TransferLetter: "Transfer letter",
  BrokerageInvoice: "Brokerage advice",
};

export const AGREEMENT_LABELS: Record<string, string> = {
  NotStarted: "Not started",
  Drafted: "Drafted",
  WithCustomer: "With customer",
  Franked: "Franked",
  Executed: "Executed",
  Registered: "Registered",
};
