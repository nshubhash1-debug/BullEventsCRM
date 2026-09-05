import { apiRequest } from "@/lib/api";

/**
 * The sales-floor half of inventory: the board, the moves made on it, the
 * pricing engine behind a quotation, and the approvals that gate both.
 *
 * Kept out of `crm-api.ts` because it is a different job. That file serves the
 * generic list views — one `/fields` call and one `/query` call per object.
 * Nothing here is a list: the board is a whole tower in one payload, and the
 * quote preview is a calculation.
 */

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

function put<T>(path: string, body?: unknown) {
  return apiRequest<T>(path, {
    method: "PUT",
    body: JSON.stringify(body ?? {}),
    auth: true,
  });
}

function del<T>(path: string) {
  return apiRequest<T>(path, { method: "DELETE", auth: true });
}

/* ------------------------------------------------------------------ *
 * Board
 * ------------------------------------------------------------------ */

export type UnitStatus =
  | "Available"
  | "Held"
  | "Blocked"
  | "Booked"
  | "Sold"
  | "NotForSale"
  | "Blackout";

/** Every status, in the order the legend and the filter chips read. */
export const UNIT_STATUSES: UnitStatus[] = [
  "Available",
  "Held",
  "Blocked",
  "Booked",
  "Sold",
  "NotForSale",
  "Blackout",
];

export interface BoardUnit {
  id: number;
  unitNumber: string;
  floor: number;
  /** Position in the floor plan, 1 upward — the building view's stacks. */
  position: number;
  configuration: string;
  carpetArea: number;
  builtUpArea: number | null;
  superArea: number | null;
  plcPerSqft: number;
  ratePerSqft: number;
  effectiveRate: number;
  totalPrice: number;
  status: UnitStatus;
  isCornerUnit: boolean;
  facing: string | null;
  heldByUserId: number | null;
  heldByName: string | null;
  heldUntil: string | null;
  holdReason: string | null;
  bookedByContactId: number | null;
  bookedByLeadId: number | null;
  customerName: string | null;
  customerPhone: string | null;
  salesPersonId: number | null;
  salesPersonName: string | null;
  bookedAt: string | null;
  bookedQuotationId: number | null;
  blockReason: string | null;
  /** Set while a request against this unit is still with a manager. */
  pendingApprovalId: number | null;
}

export interface InventoryFloor {
  floor: number;
  available: number;
  total: number;
  units: BoardUnit[];
}

export interface InventoryStats {
  total: number;
  available: number;
  held: number;
  blocked: number;
  booked: number;
  sold: number;
  notForSale: number;
  availableValue: number;
  soldValue: number;
  totalValue: number;
  soldPercent: number;
}

export interface RateCard {
  id: number;
  projectId: number;
  towerId: number | null;
  unitType: string | null;
  label: string;
  effectiveFrom: string;
  isActive: boolean;
  ratePerSqft: number;
}

export interface BoardTower {
  id: number;
  name: string;
  floorCount: number;
  unitsPerFloor: number;
  status: string;
  unitCount: number;
}

export interface InventoryBoard {
  projectId: number;
  projectName: string;
  developer: string | null;
  address: string | null;
  reraNumber: string | null;
  towerId: number | null;
  towerName: string | null;
  floorCount: number;
  unitsPerFloor: number;
  /**
   * The headline card, for projects that price on one. Where a project prices
   * by configuration there is no single active card — read `activeRateCards`,
   * because stating this one alone misprices every other configuration.
   */
  activeRateCard: RateCard | null;
  /** Every card in force today, most specific first. */
  activeRateCards: RateCard[];
  stats: InventoryStats;
  /** Top floor first, the way a building is drawn. */
  floors: InventoryFloor[];
  towers: BoardTower[];
}

export interface UnitStatusChange {
  status: UnitStatus;
  reason?: string | null;
  leadId?: number | null;
  contactId?: number | null;
  customerName?: string | null;
  customerPhone?: string | null;
  salesPersonId?: number | null;
  quotationId?: number | null;
  holdHours?: number | null;
  /** ISO date (yyyy-mm-dd) the hold/booking applies to. */
  eventDate?: string | null;
  eventSlot?: string | null;
}

export const EVENT_SLOTS = [
  "Morning",
  "Afternoon",
  "Evening",
  "Night",
  "FullDay",
] as const;

export type EventSlot = (typeof EVENT_SLOTS)[number];

/** Manager-facing approval kind labels (wire values stay Discount, UnitBooking…). */
export function approvalKindLabel(kind: string): string {
  switch (kind) {
    case "Discount":
      return "Discount exception";
    case "PriceOverride":
      return "Rate override";
    case "UnitBlock":
      return "Space off calendar";
    case "UnitBooking":
      return "Confirm space booking";
    default:
      return kind;
  }
}

export interface UnitStatusResult {
  unit: { id: number; unitNumber: string; status: UnitStatus };
  /** False when the move went to a manager instead of taking effect. */
  applied: boolean;
  approval: Approval | null;
  message: string;
}

export interface UnitHistoryEntry {
  id: number;
  fromStatus: string;
  toStatus: string;
  reason: string | null;
  leadId: number | null;
  contactId: number | null;
  partyName: string | null;
  actorName: string;
  createdAt: string;
}

/** A person attached to a unit, with enough to reach them. */
export interface UnitParty {
  id: number | null;
  name: string;
  phone: string | null;
  email: string | null;
  role: string | null;
  leadId: number | null;
  contactId: number | null;
}

export interface UnitQuotation {
  id: number;
  quoteNumber: string;
  version: number;
  status: string;
  approvalStatus: "NotRequired" | "Pending" | "Approved" | "Rejected";
  customerName: string;
  paymentPlanName: string | null;
  discountPercent: number;
  total: number;
  issueDate: string;
  validUntil: string;
  sentAt: string | null;
  ownerName: string | null;
}

/** Everything the unit detail window shows, in one call. */
export interface UnitDossier {
  unit: BoardUnit;
  projectName: string;
  towerName: string | null;
  /** The whole floor, so the plate can highlight this unit among its neighbours. */
  floorUnits: BoardUnit[];
  quotations: UnitQuotation[];
  history: UnitHistoryEntry[];
  customer: UnitParty | null;
  salesPerson: UnitParty | null;
  pendingApproval: Approval | null;
}

/* ------------------------------------------------------------------ *
 * Approvals
 * ------------------------------------------------------------------ */

export type ApprovalStatus = "Pending" | "Approved" | "Rejected" | "Cancelled";

export interface Approval {
  id: number;
  entityType: "Quotation" | "Unit";
  entityId: number;
  entityLabel: string;
  kind: "Discount" | "PriceOverride" | "UnitBlock" | "UnitBooking";
  status: ApprovalStatus;
  summary: string;
  reason: string | null;
  amount: number | null;
  requestedById: number;
  requestedByName: string;
  requestedAt: string;
  decidedById: number | null;
  decidedByName: string | null;
  decidedAt: string | null;
  decisionNote: string | null;
  branchId: number;
  branchName: string;
  ageHours: number;
}

export interface ApprovalSummary {
  pending: number;
  pendingQuotations: number;
  pendingUnits: number;
  /** Waiting more than a day. */
  overdue: number;
  raisedByMe: number;
  /** Whether the signed-in user may decide, not just raise. */
  canDecide: boolean;
}

/* ------------------------------------------------------------------ *
 * Quotation builder
 * ------------------------------------------------------------------ */

export interface PaymentPlanMilestone {
  id: number;
  sortOrder: number;
  label: string;
  basis: "Fixed" | "PercentOfTotal" | "PercentOfNetOfFixed" | "BalanceToPercent";
  percent: number;
  fixedAmount: number;
  dueOffsetDays: number | null;
  constructionStage: string | null;
}

export interface PaymentPlan {
  id: number;
  projectId: number | null;
  code: string;
  name: string;
  description: string | null;
  /** A fraction — 0.2 is twenty percent. */
  standardDiscount: number;
  discountTolerance: number;
  taxRate: number;
  isActive: boolean;
  sortOrder: number;

  /** The assured return the up-front plans are sold on. Zero on construction-linked. */
  assuredReturnPercent: number;
  assuredReturnYears: number;
  buyBackPercentPerYear: number;
  buyBackEligibleAfterYears: number;
  indicativeRentPerSqftPerMonth: number;
  returnConditions: string | null;

  milestones: PaymentPlanMilestone[];
}

/* ---------------- charge heads ---------------- */

export type ChargeBasis =
  | "PerSqft"
  | "Lumpsum"
  | "PerQuantity"
  | "PercentOfUnitCost"
  /**
   * Rate x head count — the catering basis, and the largest line on almost
   * every event bill. The count is the higher of the guest count and the
   * venue's minimum plate guarantee; the server does that flooring.
   */
  | "PerGuest";

/**
 * A revenue head on the proposal — catering, décor, photography, the security
 * deposit. Each carries its own GST, and only some are
 * spread across the payment plan.
 */
export interface ChargeHead {
  id: number;
  projectId: number | null;
  group: string;
  code: string;
  name: string;
  description: string | null;
  basis: ChargeBasis;
  rate: number;
  taxRate: number;
  defaultQuantity: number;
  /** Priced onto every quotation whether or not the rep picks it. */
  isMandatory: boolean;
  isRefundable: boolean;
  includeInSchedule: boolean;
  dueLabel: string | null;
  sortOrder: number;
}

export interface ChargeSelection {
  chargeHeadId: number;
  quantity?: number | null;
}

export interface QuoteCharge {
  chargeHeadId: number | null;
  sortOrder: number;
  group: string;
  name: string;
  basis: ChargeBasis;
  quantity: number;
  quantityUnit: string | null;
  rate: number;
  basicAmount: number;
  taxRate: number;
  taxAmount: number;
  totalAmount: number;
  isRefundable: boolean;
  includeInSchedule: boolean;
  dueLabel: string | null;
}

/**
 * The optional commercial terms on one quotation.
 *
 * Every field is nullable and null means "take the plan's default", so a screen
 * that does not know about an option cannot switch it off by omission. The rep
 * turning one off is an explicit `false` — a different fact from the plan never
 * offering it, and the document reads differently for each.
 */
export interface QuoteOptions {
  /** False holds the list price; no discount line prints at all. */
  applyDiscount?: boolean | null;
  /** What the concession is called on the document — "Launch offer". */
  discountLabel?: string | null;

  includeAssuredReturn?: boolean | null;
  /** A fraction per year. Null takes the plan's rate. */
  assuredReturnPercent?: number | null;
  assuredReturnYears?: number | null;

  includeBuyBack?: boolean | null;
  buyBackPercentPerYear?: number | null;
  buyBackEligibleAfterYears?: number | null;
  /** Years the buy-back accrues over. Null follows the assured horizon. */
  buyBackHorizonYears?: number | null;

  includeRentalYield?: boolean | null;
  rentPerSqftPerMonth?: number | null;

  returnConditions?: string | null;
}

export interface ReturnYear {
  year: number;
  periodEnd: string;
  /** Below one on a part year — a 4.5-year horizon ends on a half. */
  yearFraction: number;
  assuredReturn: number;
  rentalIncome: number;
  cumulativeReturn: number;
}

export interface InvestorAnnexure {
  basicSalePrice: number;

  hasAssuredReturn: boolean;
  hasBuyBack: boolean;
  hasRentalYield: boolean;

  assuredReturnPercent: number;
  assuredReturnYears: number;
  assuredReturnPerYear: number;
  assuredReturnPerMonth: number;
  assuredReturnAmount: number;

  buyBackPercentPerYear: number;
  buyBackEligibleAfterYears: number;
  buyBackHorizonYears: number;
  buyBackAmount: number;
  /** Basic price plus the accrued appreciation — what the developer buys back at. */
  buyBackValue: number;

  indicativeRentPerSqftPerMonth: number;
  indicativeRentPerMonth: number;
  indicativeRentPerYear: number;
  grossRentalYield: number;

  /** Assured return plus buy-back. Rent is an alternative to it, not an addition. */
  totalEarned: number;
  returnOnInvestment: number;
  annualisedReturn: number;
  horizonYears: number;

  schedule: ReturnYear[];
  conditions: string | null;
}

export interface QuoteMilestone {
  sortOrder: number;
  label: string;
  percent: number;
  basicAmount: number;
  taxAmount: number;
  totalAmount: number;
  dueDate: string | null;
}

export interface QuotePreview {
  unitId: number;
  unitNumber: string;
  towerName: string | null;
  projectName: string;
  unitType: string;
  floor: number;
  unitStatus: string;

  saleableArea: number;
  builtUpArea: number;
  carpetArea: number;
  plcPerSqft: number;
  facing: string | null;
  viewType: string | null;

  /* ---------------- head count ---------------- */

  /** Guests the client expects. */
  guestCount: number;
  /** The venue's plate guarantee this was priced against. */
  minimumPlates: number;
  /** The count the per-head lines were struck on — the higher of the two above. */
  billedHeads: number;
  /** True when the guarantee, not the guest count, set the bill. */
  minimumApplied: boolean;

  rateCardId: number | null;
  rateCardLabel: string | null;
  ratePerSqft: number;
  isRateOverridden: boolean;

  paymentPlanId: number;
  paymentPlanName: string;
  standardDiscount: number;
  discount: number;
  effectiveRatePerSqft: number;

  /** The unit cost alone — what the milestone percentages are struck on. */
  basicAmount: number;
  taxRate: number;
  taxAmount: number;
  totalAmount: number;
  amountInWords: string;

  charges: QuoteCharge[];
  chargesBasic: number;
  chargesTax: number;
  chargesTotal: number;
  refundableTotal: number;
  /** Unit cost plus every other head — what the buyer actually pays. */
  grandTotal: number;
  /** The part the payment plan spreads; the rest falls due on its own terms. */
  scheduledTotal: number;
  unscheduledTotal: number;
  grandTotalInWords: string;

  /** False when the offer holds the list price rather than discounting it. */
  discountApplied: boolean;
  discountLabel: string | null;
  /** Rate x area x discount — the money the buyer is told they save. */
  discountAmount: number;
  /** Null when nothing was switched on; no annexure is then printed. */
  annexure: InvestorAnnexure | null;

  milestones: QuoteMilestone[];
  /** Non-zero means the plan's percentages do not close. Show it, don't hide it. */
  scheduleVariance: number;

  requiresApproval: boolean;
  approvalReason: string | null;
}

export interface QuotationDetail {
  id: number;
  quoteNumber: string;
  title: string;
  version: number;
  status: string;
  approvalStatus: "NotRequired" | "Pending" | "Approved" | "Rejected";
  leadId: number | null;
  contactId: number | null;
  opportunityId: number | null;
  projectId: number | null;
  projectName: string | null;
  unitId: number | null;
  unitNumber: string | null;
  towerName: string | null;
  unitType: string | null;
  customerName: string;
  customerEmail: string | null;
  customerPhone: string | null;
  billingAddress: string | null;
  paymentPlanId: number | null;
  paymentPlanName: string | null;
  saleableArea: number;
  builtUpArea: number;
  carpetArea: number;

  /* ---------------- the event this offer is for ---------------- */

  eventType: string | null;
  eventDate: string | null;
  eventEndDate: string | null;
  eventSlot: string | null;
  /** Comma separated — "Mehendi,Sangeet,Wedding". */
  functions: string | null;
  guestCount: number;
  /** The plate guarantee the per-head lines were floored at. */
  minimumPlates: number;

  rateCardLabel: string | null;
  ratePerSqft: number;
  plcPerSqft: number;
  effectiveRatePerSqft: number;
  standardDiscountPercent: number;
  discountPercent: number;
  discountApplied: boolean;
  discountLabel: string | null;
  discountAmount: number;
  subtotal: number;
  taxPercent: number;
  taxAmount: number;
  total: number;
  amountInWords: string | null;

  chargesBasic: number;
  chargesTax: number;
  chargesTotal: number;
  refundableTotal: number;
  grandTotal: number;
  scheduledTotal: number;
  grandTotalInWords: string | null;
  charges: QuoteCharge[];
  /** The return offer as it was struck, not as the plan reads today. */
  annexure: InvestorAnnexure | null;

  issueDate: string;
  validUntil: string;
  sentAt: string | null;
  ownerId: number | null;
  ownerName: string | null;
  branchId: number;
  branchName: string;
  notes: string | null;
  termsAndConditions: string | null;
  rejectionReason: string | null;
  milestones: QuoteMilestone[];
  createdAt: string;
  updatedAt: string;
}

export interface QuotationResult {
  quotation: QuotationDetail;
  approval: Approval | null;
  message: string;
}

export interface CreateUnitQuotation {
  unitId: number;
  paymentPlanId: number;
  discount?: number | null;
  rateOverride?: number | null;
  bookingDate?: string | null;
  charges?: ChargeSelection[] | null;
  options?: QuoteOptions | null;
  customerName: string;
  customerEmail?: string | null;
  customerPhone?: string | null;
  billingAddress?: string | null;
  leadId?: number | null;
  contactId?: number | null;
  opportunityId?: number | null;
  ownerId?: number | null;
  branchId?: number | null;
  notes?: string | null;
  termsAndConditions?: string | null;
  approvalReason?: string | null;
  validDays?: number | null;

  /* ---------------- the event ---------------- */

  eventType?: string | null;
  /** ISO date. Anchors the pre-event instalments on the schedule. */
  eventDate?: string | null;
  eventEndDate?: string | null;
  eventSlot?: string | null;
  /** Comma separated — "Mehendi,Sangeet,Wedding". */
  functions?: string | null;
  guestCount?: number | null;
  /** A negotiated plate guarantee. Null takes the space's own minimum. */
  minimumPlates?: number | null;
}

/**
 * Edits a quotation by re-running the pricing engine over it.
 *
 * Every field is optional and null means "keep what it has", so a screen that
 * only changes the discount does not have to echo the whole record back and
 * risk clearing something it never showed.
 */
export interface RepriceQuotation {
  paymentPlanId?: number | null;
  discount?: number | null;
  rateOverride?: number | null;
  bookingDate?: string | null;
  charges?: ChargeSelection[] | null;
  /** Null keeps the offer as it stands rather than reapplying the plan defaults. */
  options?: QuoteOptions | null;
  /** Re-points the quotation at a record. Zero unlinks; null leaves it alone. */
  leadId?: number | null;
  contactId?: number | null;
  opportunityId?: number | null;
  customerName?: string | null;
  customerEmail?: string | null;
  customerPhone?: string | null;
  billingAddress?: string | null;
  notes?: string | null;
  termsAndConditions?: string | null;
  approvalReason?: string | null;
  validDays?: number | null;
  ownerId?: number | null;

  /* ---------------- the event ---------------- */
  //
  // Null keeps what the proposal already carries, like every other field here.
  // Moving the date or the head count is a deliberate re-offer.

  eventDate?: string | null;
  guestCount?: number | null;
  minimumPlates?: number | null;
}

/* ------------------------------------------------------------------ *
 * Timeline & Activity
 * ------------------------------------------------------------------ */

export interface QuotationActivity {
  id: number;
  type: string;
  description: string;
  metadata: string | null;
  actorId: number | null;
  actorName: string;
  createdAt: string;
}

/* ------------------------------------------------------------------ *
 * Follow-ups
 * ------------------------------------------------------------------ */

export interface QuotationFollowUp {
  id: number;
  quotationId: number;
  channel: string;
  note: string;
  outcome: string | null;
  nextFollowUpAt: string | null;
  createdByName: string;
  createdAt: string;
}

export interface FollowUpDue {
  quotationId: number;
  quoteNumber: string;
  customerName: string;
  status: string;
  grandTotal: number;
  nextFollowUpAt: string | null;
  followUpNote: string | null;
  ownerName: string | null;
  isOverdue: boolean;
}

/* ------------------------------------------------------------------ *
 * Negotiation
 * ------------------------------------------------------------------ */

export interface QuotationNegotiation {
  id: number;
  quotationId: number;
  round: number;
  type: "CounterOffer" | "Concession" | "FinalOffer";
  requestedDiscount: number | null;
  offeredDiscount: number | null;
  customerDemand: string | null;
  ourResponse: string | null;
  deltaAmount: number | null;
  createdByName: string;
  createdAt: string;
}

/* ------------------------------------------------------------------ *
 * Analytics
 * ------------------------------------------------------------------ */

export interface QuotationFunnel {
  draft: number;
  sent: number;
  underReview: number;
  negotiation: number;
  accepted: number;
  rejected: number;
  expired: number;
}

export interface LossReason {
  reason: string;
  count: number;
}

export interface QuotationWinLoss {
  won: number;
  lost: number;
  topLossReasons: LossReason[];
}

export interface MonthlyTrend {
  month: string;
  quoted: number;
  accepted: number;
  count: number;
}

export interface RepPerformance {
  ownerId: number | null;
  repName: string;
  quoted: number;
  accepted: number;
  value: number;
  acceptedValue: number;
}

export interface ProjectBreakdown {
  projectId: number | null;
  project: string;
  quoted: number;
  accepted: number;
  avgDiscount: number;
  totalValue: number;
}

export interface QuotationAnalytics {
  funnel: QuotationFunnel;
  conversionRate: number;
  avgDealSize: number;
  avgDaysToClose: number;
  avgDaysToExpiry: number;
  winLoss: QuotationWinLoss;
  monthlyTrend: MonthlyTrend[];
  repPerformance: RepPerformance[];
  projectBreakdown: ProjectBreakdown[];
}

/* ------------------------------------------------------------------ *
 * Shareable links
 * ------------------------------------------------------------------ */

export interface ShareLink {
  id: number;
  quotationId: number;
  token: string;
  url: string;
  createdAt: string;
  expiresAt: string;
  isActive: boolean;
  viewCount: number;
  lastViewedAt: string | null;
  respondedAt: string | null;
  responseStatus: string | null;
  customerComment: string | null;
  createdByName: string;
}

export interface PublicQuotation {
  quoteNumber: string;
  title: string;
  version: number;
  status: string;
  customerName: string;
  projectName: string | null;
  towerName: string | null;
  unitNumber: string | null;
  unitType: string | null;
  saleableArea: number;
  carpetArea: number;
  builtUpArea: number;
  rateCardLabel: string | null;
  ratePerSqft: number;
  plcPerSqft: number;
  effectiveRatePerSqft: number;
  discountPercent: number;
  subtotal: number;
  taxPercent: number;
  taxAmount: number;
  total: number;
  chargesTotal: number;
  grandTotal: number;
  grandTotalInWords: string | null;
  paymentPlanName: string | null;
  charges: QuoteCharge[];
  milestones: QuoteMilestone[];
  issueDate: string;
  validUntil: string;
  notes: string | null;
  annexure: InvestorAnnexure | null;
  isExpired: boolean;
  canRespond: boolean;

  /* ---------------- whose quotation this is ---------------- */
  //
  // The public link is the one place the CRM shows itself to somebody outside
  // the company. A buyer should see the developer they are buying from, not the
  // platform underneath.
  sellerName: string;
  sellerBrandColor: string | null;
  sellerLogoUrl: string | null;
}

/* ------------------------------------------------------------------ *
 * Templates
 * ------------------------------------------------------------------ */

export interface TemplateCharge {
  chargeHeadId: number;
  quantity: number;
}

export interface QuotationTemplate {
  id: number;
  projectId: number | null;
  projectName: string | null;
  name: string;
  description: string | null;
  paymentPlanId: number | null;
  paymentPlanName: string | null;
  defaultDiscount: number | null;
  defaultNotes: string | null;
  defaultTermsAndConditions: string | null;
  validDays: number | null;
  isActive: boolean;
  sortOrder: number;

  /** Null means the template has no opinion and the plan decides. */
  applyDiscount: boolean | null;
  includeAssuredReturn: boolean | null;
  includeBuyBack: boolean | null;
  includeRentalYield: boolean | null;

  charges: TemplateCharge[];
  createdAt: string;
}

/* ------------------------------------------------------------------ *
 * Delivery (Email & WhatsApp)
 * ------------------------------------------------------------------ */

export interface SendQuotationEmail {
  to: string;
  cc?: string | null;
  subject: string;
  body: string;
  attachPdf?: boolean;
}

export interface SendQuotationWhatsApp {
  phone: string;
  message?: string | null;
}

export interface DeliveryResult {
  success: boolean;
  message: string;
  sentAt: string;
}

/* ------------------------------------------------------------------ *
 * Multi-Unit / Combo Quotation
 * ------------------------------------------------------------------ */

export interface MultiUnitPreviewRequest {
  unitIds: number[];
  paymentPlanId: number;
  discount?: number | null;
  rateOverride?: number | null;
  bookingDate?: string | null;
  charges?: ChargeSelection[] | null;
}

export interface MultiUnitItemPreview {
  unitId: number;
  unitNumber: string;
  towerName: string | null;
  unitType: string;
  floor: number;
  saleableArea: number;
  ratePerSqft: number;
  effectiveRatePerSqft: number;
  basicAmount: number;
  totalAmount: number;
}

export interface MultiUnitPreview {
  units: MultiUnitItemPreview[];
  paymentPlanId: number;
  paymentPlanName: string;
  standardDiscount: number;
  discount: number;
  combinedSaleableArea: number;
  combinedBasicAmount: number;
  combinedTaxAmount: number;
  combinedTotalAmount: number;
  charges: QuoteCharge[];
  combinedChargesTotal: number;
  combinedGrandTotal: number;
  grandTotalInWords: string;
  milestones: QuoteMilestone[];
  requiresApproval: boolean;
  approvalReason: string | null;
}

export interface CreateMultiUnitQuotation {
  unitIds: number[];
  paymentPlanId: number;
  discount?: number | null;
  rateOverride?: number | null;
  bookingDate?: string | null;
  charges?: ChargeSelection[] | null;
  customerName: string;
  customerEmail?: string | null;
  customerPhone?: string | null;
  billingAddress?: string | null;
  leadId?: number | null;
  contactId?: number | null;
  opportunityId?: number | null;
  ownerId?: number | null;
  branchId?: number | null;
  notes?: string | null;
  termsAndConditions?: string | null;
  approvalReason?: string | null;
  validDays?: number | null;
}

/* ------------------------------------------------------------------ *
 * Clients
 * ------------------------------------------------------------------ */

export const boardApi = {
  board: (
    projectId: number,
    opts?: {
      towerId?: number | null;
      eventDate?: string | null;
      slot?: string | null;
    }
  ) => {
    const query = new URLSearchParams();
    if (opts?.towerId) query.set("towerId", String(opts.towerId));
    if (opts?.eventDate) query.set("eventDate", opts.eventDate);
    if (opts?.slot) query.set("slot", opts.slot);
    const qs = query.toString();
    return get<InventoryBoard>(
      `/api/inventory/projects/${projectId}/board${qs ? `?${qs}` : ""}`
    );
  },
  rateCards: (projectId: number) =>
    get<RateCard[]>(`/api/inventory/projects/${projectId}/rate-cards`),
  setStatus: (unitId: number, body: UnitStatusChange) =>
    post<UnitStatusResult>(`/api/inventory/units/${unitId}/status`, body),
  history: (unitId: number) =>
    get<UnitHistoryEntry[]>(`/api/inventory/units/${unitId}/history`),
  dossier: (unitId: number) =>
    get<UnitDossier>(`/api/inventory/units/${unitId}/dossier`),
  eventDossier: (unitId: number) =>
    get<SpaceEventDossier>(`/api/inventory/units/${unitId}/event-dossier`),
};

/* ------------------------------------------------------------------ *
 * Diary / packages / peak
 * ------------------------------------------------------------------ */

export interface DiaryCell {
  date: string;
  status: UnitStatus;
  clientName: string | null;
  eventType: string | null;
  slot: string | null;
  guestCount: number | null;
  holdExpiresAt: string | null;
  spaceBookingId: number | null;
  leadId: number | null;
  quotationId: number | null;
  isPeak: boolean;
  notes: string | null;
}

export interface DiarySpaceRow {
  unitId: number;
  name: string;
  spaceType: string;
  seatingCapacity: number;
  floatingCapacity: number;
  isOutdoor: boolean;
  isAirConditioned: boolean;
  minimumPlates: number;
  pricePerPlate: number;
  basePrice: number;
  turnaroundHours: number;
  catalogueStatus: string;
  days: DiaryCell[];
}

export interface VenuePeakWindow {
  id: number;
  startDate: string;
  endDate: string;
  label: string;
  premiumFraction: number;
}

export interface InventoryDiary {
  projectId: number;
  projectName: string;
  from: string;
  to: string;
  slot: string;
  peakWindows: VenuePeakWindow[];
  spaces: DiarySpaceRow[];
}

export interface DiaryMoveBody {
  unitId: number;
  eventDate: string;
  eventEndDate?: string | null;
  slot: string;
  status: UnitStatus;
  clientName?: string | null;
  eventType?: string | null;
  guestCount?: number | null;
  leadId?: number | null;
  contactId?: number | null;
  quotationId?: number | null;
  notes?: string | null;
  holdHours?: number | null;
  additionalUnitIds?: number[] | null;
}

export interface SpaceEventDossier {
  seatingCapacity: number;
  floatingCapacity: number;
  theatreCapacity: number | null;
  isOutdoor: boolean;
  isAirConditioned: boolean;
  hasStage: boolean;
  hasAttachedKitchen: boolean;
  minimumPlates: number;
  pricePerPlate: number;
  peakDatePremium: number;
  securityDeposit: number;
  basePrice: number;
  turnaroundHours: number;
  layoutImageUrl: string | null;
  allowsOutsideCatering: boolean;
  allowsAlcohol: boolean;
  allowsOpenFlame: boolean;
  noiseCurfew: string | null;
  parkingCapacity: number | null;
  guestRooms: number | null;
  upcomingBookings: DiaryCell[];
}

export interface VenuePackage {
  id: number;
  projectId: number;
  projectName: string;
  name: string;
  code: string;
  description: string | null;
  planningPackage: string | null;
  indicativeRental: number | null;
  indicativePerPlate: number | null;
  defaultMinimumPlates: number | null;
  defaultGuestCount: number | null;
  isActive: boolean;
  spaces: {
    unitId: number;
    unitName: string;
    spaceType: string;
    seatingCapacity: number;
    sortOrder: number;
    defaultSlot: string | null;
  }[];
}

export const diaryApi = {
  get: (projectId: number, from: string, to: string, slot?: string) => {
    const q = new URLSearchParams({ from, to });
    if (slot) q.set("slot", slot);
    return get<InventoryDiary>(
      `/api/inventory/projects/${projectId}/diary?${q}`
    );
  },
  move: (projectId: number, body: DiaryMoveBody) =>
    post<{ groupRef: string; count: number; message: string }>(
      `/api/inventory/projects/${projectId}/diary/moves`,
      body
    ),
  release: (spaceBookingId: number) =>
    del<void>(`/api/inventory/space-bookings/${spaceBookingId}`),
  conflicts: (
    projectId: number,
    eventDate: string,
    opts?: { eventEndDate?: string; slot?: string; unitId?: number }
  ) => {
    const q = new URLSearchParams({ eventDate });
    if (opts?.eventEndDate) q.set("eventEndDate", opts.eventEndDate);
    if (opts?.slot) q.set("slot", opts.slot);
    if (opts?.unitId) q.set("unitId", String(opts.unitId));
    return get<
      {
        unitId: number;
        unitName: string;
        eventDate: string;
        slot: string;
        status: string;
        clientName: string | null;
      }[]
    >(`/api/inventory/projects/${projectId}/conflicts?${q}`);
  },
};

export const packagesApi = {
  list: (projectId?: number) =>
    get<VenuePackage[]>(
      `/api/inventory/packages${projectId ? `?projectId=${projectId}` : ""}`
    ),
  create: (body: {
    projectId: number;
    name: string;
    code: string;
    description?: string | null;
    planningPackage?: string | null;
    indicativeRental?: number | null;
    indicativePerPlate?: number | null;
    defaultMinimumPlates?: number | null;
    defaultGuestCount?: number | null;
    isActive: boolean;
    unitIds: number[];
  }) => post<VenuePackage>("/api/inventory/packages", body),
  remove: (id: number) => del<void>(`/api/inventory/packages/${id}`),
  peakDates: (projectId: number) =>
    get<VenuePeakWindow[]>(`/api/inventory/projects/${projectId}/peak-dates`),
  createPeak: (body: {
    projectId: number;
    startDate: string;
    endDate: string;
    label: string;
    premiumFraction: number;
    notes?: string | null;
  }) => post<VenuePeakWindow>("/api/inventory/peak-dates", body),
  removePeak: (id: number) => del<void>(`/api/inventory/peak-dates/${id}`),
};

export const approvalsApi = {
  list: (params?: { status?: string; entityType?: string; mine?: boolean }) => {
    const query = new URLSearchParams();
    if (params?.status) query.set("status", params.status);
    if (params?.entityType) query.set("entityType", params.entityType);
    if (params?.mine) query.set("mine", "true");

    const suffix = query.toString();
    return get<Approval[]>(`/api/approvals${suffix ? `?${suffix}` : ""}`);
  },
  summary: () => get<ApprovalSummary>("/api/approvals/summary"),
  approve: (id: number, note?: string) =>
    post<Approval>(`/api/approvals/${id}/approve`, { note }),
  reject: (id: number, note?: string) =>
    post<Approval>(`/api/approvals/${id}/reject`, { note }),
  cancel: (id: number, note?: string) =>
    post<Approval>(`/api/approvals/${id}/cancel`, { note }),
};

export const quoteBuilderApi = {
  plans: (projectId?: number | null) =>
    get<PaymentPlan[]>(
      `/api/quotations/payment-plans${projectId ? `?projectId=${projectId}` : ""}`
    ),
  chargeHeads: (projectId?: number | null) =>
    get<ChargeHead[]>(
      `/api/quotations/charge-heads${projectId ? `?projectId=${projectId}` : ""}`
    ),
  preview: (body: {
    unitId: number;
    paymentPlanId: number;
    discount?: number | null;
    rateOverride?: number | null;
    bookingDate?: string | null;
    charges?: ChargeSelection[] | null;
    options?: QuoteOptions | null;
    /** ISO date. Anchors the pre-event instalments on the schedule. */
    eventDate?: string | null;
    guestCount?: number | null;
    /** A negotiated plate guarantee. Null takes the space's own minimum. */
    minimumPlates?: number | null;
  }) => post<QuotePreview>("/api/quotations/preview", body),
  createFromUnit: (body: CreateUnitQuotation) =>
    post<QuotationResult>("/api/quotations/from-unit", body),
  reprice: (id: number, body: RepriceQuotation) =>
    put<QuotationResult>(`/api/quotations/${id}/reprice`, body),
  detail: (id: number) => get<QuotationDetail>(`/api/quotations/${id}/detail`),

  /* ---- lifecycle ---- */
  issue: (id: number) => post<QuotationDetail>(`/api/quotations/${id}/issue`),
  submitForApproval: (id: number, reason?: string | null) =>
    post<QuotationResult>(`/api/quotations/${id}/submit-approval`, { reason }),
  withdrawApproval: (id: number) =>
    post<QuotationDetail>(`/api/quotations/${id}/withdraw-approval`),
  accept: (id: number) => post<QuotationDetail>(`/api/quotations/${id}/accept`),
  decline: (id: number, reason?: string | null) =>
    post<QuotationDetail>(`/api/quotations/${id}/decline`, { reason }),
  remove: (id: number) => del<void>(`/api/quotations/${id}`),

  /**
   * The PDF is a protected download, so it cannot be a plain `<a href>` — the
   * bearer token never reaches the request. Fetching it as a blob and handing
   * the browser an object URL is what makes the link work while keeping the
   * endpoint authenticated.
   */
  pdfUrl: (id: number) => `/api/quotations/${id}/pdf`,

  /* ---- timeline ---- */
  timeline: (id: number) =>
    get<QuotationActivity[]>(`/api/quotations/${id}/timeline`),

  /* ---- follow-ups ---- */
  addFollowUp: (id: number, body: {
    note: string;
    channel?: string;
    outcome?: string;
    nextFollowUpAt?: string | null;
  }) => post<QuotationFollowUp>(`/api/quotations/${id}/follow-ups`, body),
  followUps: (id: number) =>
    get<QuotationFollowUp[]>(`/api/quotations/${id}/follow-ups`),
  followUpsDue: () =>
    get<FollowUpDue[]>("/api/quotations/follow-ups/due"),
  extendValidity: (id: number, body: {
    newValidUntil: string;
    reason?: string | null;
  }) => post<QuotationDetail>(`/api/quotations/${id}/extend-validity`, body),

  /* ---- negotiations ---- */
  addNegotiation: (id: number, body: {
    type: string;
    requestedDiscount?: number | null;
    offeredDiscount?: number | null;
    customerDemand?: string | null;
    ourResponse?: string | null;
    deltaAmount?: number | null;
  }) => post<QuotationNegotiation>(`/api/quotations/${id}/negotiations`, body),
  negotiations: (id: number) =>
    get<QuotationNegotiation[]>(`/api/quotations/${id}/negotiations`),

  /* ---- analytics ---- */
  analytics: () =>
    get<QuotationAnalytics>("/api/quotations/analytics"),

  /* ---- shareable links ---- */
  createShareLink: (id: number, expiryDays?: number) =>
    post<ShareLink>(`/api/quotations/${id}/share`, { expiryDays }),
  shareLinks: (id: number) =>
    get<ShareLink[]>(`/api/quotations/${id}/share-links`),
  revokeShareLink: (linkId: number) =>
    del<void>(`/api/quotations/share-links/${linkId}`),

  /* ---- templates ---- */
  templates: (projectId?: number | null) =>
    get<QuotationTemplate[]>(
      `/api/quotations/templates${projectId ? `?projectId=${projectId}` : ""}`
    ),
  createTemplate: (body: {
    projectId?: number | null;
    name: string;
    description?: string | null;
    paymentPlanId?: number | null;
    defaultDiscount?: number | null;
    defaultNotes?: string | null;
    defaultTermsAndConditions?: string | null;
    validDays?: number | null;
    charges?: TemplateCharge[] | null;
    applyDiscount?: boolean | null;
    includeAssuredReturn?: boolean | null;
    includeBuyBack?: boolean | null;
    includeRentalYield?: boolean | null;
  }) => post<QuotationTemplate>("/api/quotations/templates", body),
  updateTemplate: (id: number, body: {
    name?: string;
    description?: string | null;
    paymentPlanId?: number | null;
    defaultDiscount?: number | null;
    defaultNotes?: string | null;
    defaultTermsAndConditions?: string | null;
    validDays?: number | null;
    isActive?: boolean;
    charges?: TemplateCharge[] | null;
    applyDiscount?: boolean | null;
    includeAssuredReturn?: boolean | null;
    includeBuyBack?: boolean | null;
    includeRentalYield?: boolean | null;
  }) => put<QuotationTemplate>(`/api/quotations/templates/${id}`, body),
  deleteTemplate: (id: number) =>
    del<void>(`/api/quotations/templates/${id}`),

  /* ---- delivery ---- */
  sendEmail: (id: number, body: SendQuotationEmail) =>
    post<DeliveryResult>(`/api/quotations/${id}/send-email`, body),
  sendWhatsApp: (id: number, body: SendQuotationWhatsApp) =>
    post<DeliveryResult>(`/api/quotations/${id}/send-whatsapp`, body),

  /* ---- multi-unit ---- */
  previewMulti: (body: MultiUnitPreviewRequest) =>
    post<MultiUnitPreview>("/api/quotations/preview-multi", body),
  createFromUnits: (body: CreateMultiUnitQuotation) =>
    post<QuotationResult>("/api/quotations/from-units", body),
};

/** Public endpoints — no auth required. */
export const publicApi = {
  quotation: (token: string) =>
    apiRequest<PublicQuotation>(`/api/public/quotation/${token}`, { method: "GET" }),
  respond: (token: string, body: { status: string; comment?: string | null }) =>
    apiRequest<{ message: string }>(`/api/public/quotation/${token}/respond`, {
      method: "POST",
      body: JSON.stringify(body),
    }),
};

/* ------------------------------------------------------------------ *
 * Formatting
 * ------------------------------------------------------------------ */

/** Indian digit grouping — 1,92,19,200 rather than 19,219,200. */
export function formatIndian(value: number | null | undefined, decimals = 0) {
  if (value === null || value === undefined || Number.isNaN(value)) return "—";
  return value.toLocaleString("en-IN", {
    minimumFractionDigits: decimals,
    maximumFractionDigits: decimals,
  });
}

export function formatRupees(value: number | null | undefined, decimals = 0) {
  if (value === null || value === undefined || Number.isNaN(value)) return "—";
  return `₹${formatIndian(value, decimals)}`;
}

/**
 * Crore and lakh short form for tiles and headline stats, where the exact
 * paise are noise and the magnitude is the point.
 */
export function formatCompactRupees(value: number | null | undefined) {
  if (value === null || value === undefined || Number.isNaN(value)) return "—";

  const abs = Math.abs(value);
  if (abs >= 1_00_00_000) return `₹${(value / 1_00_00_000).toFixed(2)} Cr`;
  if (abs >= 1_00_000) return `₹${(value / 1_00_000).toFixed(2)} L`;
  if (abs >= 1_000) return `₹${(value / 1_000).toFixed(1)} K`;
  return `₹${value.toFixed(0)}`;
}

export function formatPercent(fraction: number | null | undefined, decimals = 0) {
  if (fraction === null || fraction === undefined) return "—";
  return `${(fraction * 100).toFixed(decimals)}%`;
}
