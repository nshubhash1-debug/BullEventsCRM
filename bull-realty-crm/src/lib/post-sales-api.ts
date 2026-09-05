import { apiRequest } from "@/lib/api";

/* ------------------------------------------------------------------ *
 * The post-sales client.
 *
 * Kept apart from the pipeline client because it is a different desk with a
 * different vocabulary — demands, receipts, allocations, ageing — and folding
 * it into the sales one would put two unrelated languages in a single file
 * nobody can scan.
 * ------------------------------------------------------------------ */

function get<T>(path: string) {
  return apiRequest<T>(path, { method: "GET", auth: true });
}

function post<T>(path: string, body: unknown) {
  return apiRequest<T>(path, {
    method: "POST",
    body: JSON.stringify(body),
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

/* ---------------- bookings ---------------- */

export interface BookingRow {
  id: number;
  bookingNumber: string;
  status: string;
  statusLabel: string;
  bookingDate: string;

  /** The contracted event date. Null on a contract raised before one was fixed. */
  eventDate: string | null;
  eventType: string | null;
  guestCount: number;
  /** Whole days until the event; negative once it has passed. */
  daysToEvent: number | null;

  projectName: string;
  towerName: string | null;
  unitNumber: string;
  configuration: string | null;
  saleableArea: number;
  customerName: string;
  customerPhone: string | null;
  agreementValue: number;
  grandTotal: number;
  demanded: number;
  received: number;
  outstanding: number;
  interestDue: number;
  overdueDays: number;
  /** Received over grand total. What the collections desk sorts on. */
  collectedFraction: number;
  ownerName: string | null;
  agreementStatus: string;
  possessionStatus: string;
  hasLoan: boolean;
}

export interface Applicant {
  id: number;
  role: string;
  salutation: string | null;
  name: string;
  relation: string | null;
  phone: string | null;
  email: string | null;
  address: string | null;
  dateOfBirth: string | null;
  pan: string | null;
  /** Only ever the last four digits — the full number is never stored. */
  aadhaarLast4: string | null;
  kycStatus: string;
  kycVerifiedOn: string | null;
}

export interface Milestone {
  id: number;
  sortOrder: number;
  label: string;
  percent: number;
  basicAmount: number;
  taxAmount: number;
  totalAmount: number;
  constructionStage: string | null;
  dueDate: string | null;
  status: string;
  demandId: number | null;
  received: number;
}

export interface BookingDetail {
  summary: BookingRow;
  applicants: Applicant[];
  milestones: Milestone[];
  paymentPlanName: string | null;
  paymentPlanId: number | null;
  notes: string | null;
  quotationId: number | null;
  leadId: number | null;
  unitId: number;
}

export interface BookingDocument {
  id: number;
  key: string;
  name: string;
  stage: string;
  isRequired: boolean;
  status: string;
  receivedOn: string | null;
  verifiedOn: string | null;
  expiresOn: string | null;
  fileName: string | null;
  fileUrl: string | null;
  notes: string | null;
}

/* ---------------- customers ---------------- */

export interface CustomerUnit {
  bookingId: number;
  bookingNumber: string;
  status: string;
  bookingDate: string;
  projectName: string;
  towerName: string | null;
  unitNumber: string;
  configuration: string | null;
  grandTotal: number;
  received: number;
  outstanding: number;
  overdueDays: number;
}

export interface Customer {
  /** The lead they came from. A customer only exists once that lead is Booked. */
  leadId: number | null;
  name: string;
  phone: string | null;
  email: string | null;
  city: string | null;
  source: string | null;
  ownerName: string | null;
  lifecycleStage: string;
  customerSince: string;
  units: number;
  portfolio: number;
  received: number;
  outstanding: number;
  overdueDays: number;
  coApplicants: number;
  kycVerified: number;
  kycPending: number;
  documentsPending: number;
  bookings: CustomerUnit[];
}

/* ---------------- collections ---------------- */

export interface DemandRow {
  id: number;
  demandNumber: string;
  bookingId: number;
  bookingNumber: string;
  customerName: string;
  customerPhone: string | null;
  projectName: string;
  towerName: string | null;
  unitNumber: string;
  label: string;
  raisedOn: string;
  dueDate: string;
  totalAmount: number;
  received: number;
  outstanding: number;
  interestDue: number;
  interestRatePercent: number;
  status: string;
  /** Days past due. Negative while it is still in hand. */
  ageDays: number;
  bucket: string;
}

export interface Allocation {
  demandId: number;
  demandNumber: string;
  label: string;
  amount: number;
  towardsInterest: number;
}

export interface ReceiptRow {
  id: number;
  receiptNumber: string;
  bookingId: number;
  bookingNumber: string;
  customerName: string;
  unitNumber: string;
  receivedOn: string;
  amount: number;
  tdsAmount: number;
  /** What the customer is credited with: paid plus the TDS they withheld. */
  creditedAmount: number;
  mode: string;
  instrument: string | null;
  bankName: string | null;
  status: string;
  unallocated: number;
  bouncedOn: string | null;
  bounceReason: string | null;
  allocations: Allocation[];
}

export interface LedgerLine {
  on: string;
  kind: string;
  reference: string;
  description: string;
  debit: number;
  credit: number;
  balance: number;
}

export interface Ledger {
  bookingId: number;
  bookingNumber: string;
  customerName: string;
  unitLabel: string;
  grandTotal: number;
  demanded: number;
  received: number;
  interestCharged: number;
  interestWaived: number;
  outstanding: number;
  /** Billed by the plan but not yet demanded — the rest of the schedule. */
  notYetDemanded: number;
  overdueDays: number;
  overdueAmount: number;
  lines: LedgerLine[];
}

export interface AgeingBucket {
  bucket: string;
  count: number;
  amount: number;
}

export interface ForecastPoint {
  month: string;
  scheduled: number;
  demanded: number;
}

export interface CollectionsSummary {
  totalSold: number;
  totalDemanded: number;
  totalReceived: number;
  totalOutstanding: number;
  interestDue: number;
  /** Received over demanded — not over sale value, which would measure construction. */
  collectionEfficiency: number;
  liveBookings: number;
  overdueBookings: number;
  dueThisMonth: number;
  receivedThisMonth: number;
  bouncedThisMonth: number;
  ageing: AgeingBucket[];
  forecast: ForecastPoint[];
}

export interface EscrowSummary {
  projectId: number | null;
  projectName: string;
  collected: number;
  designated: number;
  free: number;
  transferred: number;
  pendingTransfer: number;
}

export interface BulkDemandResult {
  raised: number;
  totalAmount: number;
  skipped: number;
  reasons: string[];
}

/* ---------------- vocabulary ---------------- */

export const PAYMENT_MODES = [
  "NEFT", "RTGS", "IMPS", "UPI", "Cheque", "DD", "Cash", "LoanDisbursement",
] as const;

export const AGEING_BUCKETS = ["Current", "1-30", "31-60", "61-90", "90+"] as const;

export const CONSTRUCTION_STAGES = [
  "Excavation", "Foundation", "Plinth", "Slab", "Superstructure", "Brickwork",
  "Plaster", "Flooring", "Plumbing & Electrical", "Doors & Windows", "Painting",
  "External Development", "Occupancy Certificate", "Possession",
] as const;

export const BOOKING_STATUSES = [
  "Booked", "Allotted", "AgreementPending", "AgreementExecuted",
  "Registered", "PossessionOffered", "HandedOver", "Cancelled", "Transferred",
] as const;

export const DOCUMENT_STATUSES = [
  "Pending", "Received", "Verified", "Rejected", "NotApplicable",
] as const;

/* ------------------------------------------------------------------ *
 * Calls
 * ------------------------------------------------------------------ */

export const postSalesApi = {
  bookings: (params: {
    status?: string;
    projectId?: number;
    tower?: string;
    overdueOnly?: boolean;
    search?: string;
  } = {}) => {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== "") {
        query.set(key, String(value));
      }
    }
    return get<BookingRow[]>(`/api/bookings?${query}`);
  },

  booking: (id: number) => get<BookingDetail>(`/api/bookings/${id}`),

  /**
   * Everyone who has actually bought.
   *
   * Gated server-side on the lead reaching Booked, so a deal the sales desk has
   * not closed can never surface here.
   */
  customers: (params: { search?: string; overdueOnly?: boolean } = {}) => {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== "") {
        query.set(key, String(value));
      }
    }
    return get<Customer[]>(`/api/bookings/customers?${query}`);
  },

  create: (input: {
    quotationId: number;
    bookingDate?: string;
    ownerId?: number | null;
    applicants: Array<Partial<Applicant> & { role: string; name: string }>;
  }) => post<BookingDetail>("/api/bookings", input),

  addApplicant: (bookingId: number, input: Partial<Applicant> & { role: string; name: string }) =>
    post<Applicant>(`/api/bookings/${bookingId}/applicants`, input),

  saveApplicant: (applicantId: number, input: Partial<Applicant> & { role: string; name: string }) =>
    put<Applicant>(`/api/bookings/applicants/${applicantId}`, input),

  /* ---------------- the plan, which post-sales owns ---------------- */

  reschedule: (bookingId: number, milestoneId: number, dueDate: string, reason: string) =>
    post<Milestone>(`/api/bookings/${bookingId}/plan/${milestoneId}/reschedule`, { dueDate, reason }),

  waiveInstalment: (bookingId: number, milestoneId: number, reason: string) =>
    post<Milestone>(`/api/bookings/${bookingId}/plan/${milestoneId}/waive`, { reason }),

  addInstalment: (bookingId: number, input: {
    label: string;
    basicAmount: number;
    taxAmount: number;
    dueDate?: string | null;
  }) => post<Milestone>(`/api/bookings/${bookingId}/plan/instalments`, input),

  revisePlan: (bookingId: number, paymentPlanId: number, reason: string) =>
    post<BookingDetail>(`/api/bookings/${bookingId}/plan/revise`, { paymentPlanId, reason }),

  /* ---------------- documents ---------------- */

  documents: (bookingId: number) =>
    get<BookingDocument[]>(`/api/bookings/${bookingId}/documents`),

  saveDocument: (documentId: number, input: {
    status: string;
    receivedOn?: string | null;
    expiresOn?: string | null;
    fileName?: string | null;
    fileUrl?: string | null;
    notes?: string | null;
    isRequired?: boolean;
  }) => put<BookingDocument>(`/api/bookings/documents/${documentId}`, input),

  /* ---------------- money ---------------- */

  demands: (params: {
    status?: string;
    bucket?: string;
    projectId?: number;
    tower?: string;
    bookingId?: number;
  } = {}) => {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== "") {
        query.set(key, String(value));
      }
    }
    return get<DemandRow[]>(`/api/collections/demands?${query}`);
  },

  raiseDemand: (bookingId: number, input: {
    milestoneId: number;
    dueDate?: string | null;
    interestRatePercent?: number | null;
  }) => post<DemandRow>(`/api/collections/bookings/${bookingId}/demands`, input),

  /** The slab-cast run: every instalment on a stage, across a tower or a project. */
  bulkRaise: (input: {
    constructionStage: string;
    projectId?: number | null;
    tower?: string | null;
    dueDate?: string | null;
    interestRatePercent?: number | null;
  }) => post<BulkDemandResult>("/api/collections/demands/bulk", input),

  waiveInterest: (demandId: number, reason: string, amount?: number | null) =>
    post<DemandRow>(`/api/collections/demands/${demandId}/waive-interest`, { amount, reason }),

  receipts: (params: { bookingId?: number; status?: string; since?: string } = {}) => {
    const query = new URLSearchParams();
    for (const [key, value] of Object.entries(params)) {
      if (value !== undefined && value !== null && value !== "") {
        query.set(key, String(value));
      }
    }
    return get<ReceiptRow[]>(`/api/collections/receipts?${query}`);
  },

  receive: (input: {
    bookingId: number;
    receivedOn?: string | null;
    amount: number;
    tdsAmount: number;
    mode: string;
    instrument?: string | null;
    bankName?: string | null;
    instrumentDate?: string | null;
    status?: string | null;
    notes?: string | null;
  }) => post<ReceiptRow>("/api/collections/receipts", input),

  clearReceipt: (receiptId: number, clearedOn?: string | null) =>
    post<void>(`/api/collections/receipts/${receiptId}/clear`, { clearedOn }),

  bounceReceipt: (receiptId: number, reason: string) =>
    post<void>(`/api/collections/receipts/${receiptId}/bounce`, { reason }),

  ledger: (bookingId: number) =>
    get<Ledger>(`/api/collections/bookings/${bookingId}/ledger`),

  summary: (projectId?: number) =>
    get<CollectionsSummary>(
      `/api/collections/summary${projectId ? `?projectId=${projectId}` : ""}`
    ),

  escrow: () => get<EscrowSummary[]>("/api/collections/escrow"),
};

/* ------------------------------------------------------------------ *
 * Formatting
 *
 * Indian grouping throughout: a developer reading ₹1,92,74,700 as ₹19,274,700
 * misreads it by an order of magnitude for a beat, and post-sales is where the
 * numbers are large enough for that to matter.
 * ------------------------------------------------------------------ */

const rupees = new Intl.NumberFormat("en-IN", {
  style: "currency",
  currency: "INR",
  maximumFractionDigits: 0,
});

const rupeesExact = new Intl.NumberFormat("en-IN", {
  style: "currency",
  currency: "INR",
  minimumFractionDigits: 2,
  maximumFractionDigits: 2,
});

export function inr(value: number | null | undefined) {
  return rupees.format(value ?? 0);
}

export function inrExact(value: number | null | undefined) {
  return rupeesExact.format(value ?? 0);
}

/**
 * Lakh and crore, the way this market actually talks about a price.
 *
 * A cost sheet says ₹1.92 Cr; nobody in the room says nineteen million.
 */
export function inrShort(value: number | null | undefined) {
  const amount = value ?? 0;
  const sign = amount < 0 ? "-" : "";
  const abs = Math.abs(amount);

  if (abs >= 10_000_000) return `${sign}₹${(abs / 10_000_000).toFixed(2)} Cr`;
  if (abs >= 100_000) return `${sign}₹${(abs / 100_000).toFixed(2)} L`;
  if (abs >= 1_000) return `${sign}₹${(abs / 1_000).toFixed(1)} K`;

  return `${sign}₹${abs.toFixed(0)}`;
}

export function shortDate(iso: string | null | undefined) {
  if (!iso) return "—";
  return new Date(iso).toLocaleDateString("en-IN", {
    day: "numeric",
    month: "short",
    year: "numeric",
  });
}
