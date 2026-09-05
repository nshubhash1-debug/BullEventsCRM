import { apiRequest } from "@/lib/api";
import type { FilterField, PagedResult, QueryRequest } from "@/lib/query";

/**
 * Clients for the CRM objects that make up the Leads workspace.
 *
 * Every list view goes through the same two calls: `/fields` for the filter
 * metadata and `/query` for a page. Because the server owns both, adding a
 * filterable column is a backend change the UI picks up on its own.
 */

function get<T>(path: string) {
  return apiRequest<T>(path, { method: "GET", auth: true });
}

function post<T>(path: string, body: unknown) {
  return apiRequest<T>(path, {
    method: "POST",
    body: JSON.stringify(body ?? {}),
    auth: true,
  });
}

function patch<T>(path: string, body: unknown) {
  return apiRequest<T>(path, {
    method: "PATCH",
    body: JSON.stringify(body ?? {}),
    auth: true,
  });
}

function remove(path: string) {
  return apiRequest<void>(path, { method: "DELETE", auth: true });
}

/** Every list view is one of these — same shape, different resource. */
export interface ListResource<T> {
  fields: () => Promise<FilterField[]>;
  query: (request: QueryRequest) => Promise<PagedResult<T>>;
}

function listResource<T>(base: string): ListResource<T> {
  return {
    fields: () => get<FilterField[]>(`${base}/fields`),
    query: (request) => post<PagedResult<T>>(`${base}/query`, request),
  };
}

/* ------------------------------------------------------------------ *
 * Leads
 * ------------------------------------------------------------------ */

export interface LeadRow {
  id: number;

  // ---- personal ----
  salutation: string | null;
  name: string;
  companyName: string | null;
  phone: string | null;
  phone2: string | null;
  email: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
  country: string | null;
  zone: string | null;
  dateOfBirth: string | null;
  anniversaryDate: string | null;
  maritalStatus: string | null;
  fatherOrSpouseName: string | null;
  occupation: string | null;
  designation: string | null;
  nationality: string | null;
  /** The other half of the couple, or the guest of honour. */
  partnerName: string | null;
  partnerPhone: string | null;
  partnerEmail: string | null;
  inquirerRole: string | null;

  // ---- lead ----
  source: string;
  stage: string;
  subStatus: string | null;
  priority: string;
  branchId: number;
  branchName: string;
  ownerId: number | null;
  ownerName: string | null;
  supportingManagerId: number | null;
  supportingManagerName: string | null;
  notes: string | null;

  // ---- the event ----
  budgetMin: number | null;
  budgetMax: number | null;
  eventType: string | null;
  eventCategory: string | null;
  eventDate: string | null;
  eventEndDate: string | null;
  eventSlot: string | null;
  isDateFlexible: boolean;
  guestCount: number | null;
  /** Comma separated — "Mehendi,Sangeet,Wedding". */
  functions: string | null;
  /** Comma separated — "Venue,Catering,Decor". */
  servicesNeeded: string | null;
  mealPreference: string | null;
  preferredLocality: string | null;
  paymentMode: string | null;
  /** The venue named on the enquiry. */
  interestedProjectId: number | null;
  interestedProjectName: string | null;
  venueStatus: string | null;
  planningPackage: string | null;
  ceremonyGuestCount: number | null;
  receptionGuestCount: number | null;
  portalName: string | null;
  ceremonyStyle: string | null;
  consultAt: string | null;
  questionnaireStatus: string | null;
  questionnaireToken: string | null;
  autoAckAt: string | null;
  /** Whole days until the event; negative once it has passed. */
  daysToEvent: number | null;

  // ---- attribution ----
  campaign: string | null;
  utmSource: string | null;
  utmMedium: string | null;
  referredBy: string | null;
  tags: string | null;

  // ---- intelligence ----
  score: number | null;
  band: string | null;
  scoredAt: string | null;

  // ---- engagement ----
  // Derived server-side from the visit tables, so they follow a reschedule.
  siteVisitStatus: string | null;
  siteVisitAt: string | null;
  siteVisitCount: number;
  obmStatus: string | null;
  obmVisitAt: string | null;
  obmVisitCount: number;

  // ---- lifecycle ----
  lastActivityType: string | null;
  lastActivitySummary: string | null;
  lastActivityAt: string | null;
  slaDueAt: string | null;
  firstResponseAt: string | null;
  slaState: "OnTrack" | "AtRisk" | "Breached" | "Met" | "None";
  isConverted: boolean;
  convertedAt: string | null;
  convertedContactId: number | null;
  convertedOpportunityId: number | null;
  convertedBookingId: number | null;
  lossReason: string | null;
  activityCount: number;

  createdAt: string;
  updatedAt: string;
}

export interface LeadActivityRow {
  id: number;
  type: string;
  remarks: string | null;
  fromStage: string | null;
  toStage: string | null;
  actorName: string;
  createdAt: string;
}

export interface RelatedRecord {
  kind: string;
  id: number;
  title: string;
  subtitle: string | null;
  status: string | null;
  amount: number | null;
  at: string;
}

export interface LeadRelated {
  calls: RelatedRecord[];
  siteVisits: RelatedRecord[];
  obmVisits: RelatedRecord[];
  followUps: RelatedRecord[];
  quotations: RelatedRecord[];
  opportunities: RelatedRecord[];
  totalCalls: number;
  totalSiteVisits: number;
  completedSiteVisits: number;
  totalObmVisits: number;
  totalFollowUps: number;
  openFollowUps: number;
  totalQuotations: number;
  quotedValue: number;
}

export interface FieldChange {
  at: string;
  userName: string;
  field: string;
  from: string | null;
  to: string | null;
  action: string;
}

export const leadsApi = {
  ...listResource<LeadRow>("/api/leads"),
  one: (id: number) => get<LeadRow>(`/api/leads/${id}`),
  activities: (id: number) => get<LeadActivityRow[]>(`/api/leads/${id}/activities`),
  logActivity: (id: number, input: { type: string; remarks?: string }) =>
    post<LeadActivityRow>(`/api/leads/${id}/activities`, input),
  related: (id: number) => get<LeadRelated>(`/api/leads/${id}/related`),
  history: (id: number) => get<FieldChange[]>(`/api/leads/${id}/history`),
  /** Writes one field. The audit trail behind it powers the History tab. */
  patchField: (id: number, field: string, value: string | null) =>
    patch<LeadRow>(`/api/leads/${id}/field`, { field, value }),
  transfer: (id: number, ownerId: number, reason?: string) =>
    post<LeadRow>(`/api/leads/${id}/transfer`, { ownerId, reason }),
  bulkAssign: (leadIds: number[], ownerId: number | null) =>
    post<BulkResult>("/api/leads/bulk/assign", { leadIds, ownerId }),
  bulkStage: (leadIds: number[], stage: string) =>
    post<BulkResult>("/api/leads/bulk/stage", { leadIds, stage }),
  bulkDelete: (leadIds: number[]) =>
    post<BulkResult>("/api/leads/bulk/delete", { leadIds }),
  convert: (id: number, input: ConvertLeadInput) =>
    post<ConvertLeadResult>(`/api/leads/${id}/convert`, input),
  board: (params?: { search?: string; month?: string }) => {
    const q = new URLSearchParams();
    if (params?.search) q.set("search", params.search);
    if (params?.month) q.set("month", params.month);
    const suffix = q.size ? `?${q}` : "";
    return get<LeadBoard>(`/api/leads/board${suffix}`);
  },
  availability: (date: string) =>
    get<LeadConflict[]>(`/api/leads/availability?date=${encodeURIComponent(date)}`),
  sendQuestionnaire: (id: number) =>
    post<LeadRow>(`/api/leads/${id}/questionnaire/send`, {}),
};

export interface BulkResult {
  affected: number;
  message: string;
}

export interface ConvertLeadInput {
  createOpportunity: boolean;
  createEvent: boolean;
  opportunityName?: string;
  amount?: number;
  expectedCloseDate?: string;
  projectId?: number;
  unitId?: number;
}

export interface ConvertLeadResult {
  leadId: number;
  contactId: number;
  contactName: string;
  opportunityId: number | null;
  opportunityName: string | null;
  bookingId: number | null;
  bookingNumber: string | null;
  message: string;
}

export interface LeadBoardColumn {
  stage: string;
  label: string;
  count: number;
  value: number;
  averageDaysInStage: number;
  leads: LeadRow[];
}

export interface LeadBoard {
  columns: LeadBoardColumn[];
  total: number;
  pipelineValue: number;
}

export interface LeadConflict {
  id: number;
  name: string;
  stage: string;
  eventDate: string | null;
  eventEndDate: string | null;
  guestCount: number | null;
  venueName: string | null;
}

/* ------------------------------------------------------------------ *
 * Contacts
 * ------------------------------------------------------------------ */

export interface ContactRow {
  id: number;
  salutation: string | null;
  firstName: string;
  lastName: string | null;
  fullName: string;
  designation: string | null;
  accountName: string | null;
  phone: string | null;
  phone2: string | null;
  email: string | null;
  whatsAppNumber: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  country: string | null;
  pincode: string | null;
  type: string;
  lifecycleStage: string;
  source: string;
  tags: string | null;
  segment: string | null;
  lifetimeValue: number;
  dealCount: number;
  budgetMin: number | null;
  budgetMax: number | null;
  preferredConfiguration: string | null;
  preferredLocality: string | null;
  doNotCall: boolean;
  doNotEmail: boolean;
  whatsAppOptIn: boolean;
  panNumber: string | null;
  gstin: string | null;
  dateOfBirth: string | null;
  branchId: number;
  branchName: string;
  ownerId: number | null;
  ownerName: string | null;
  convertedFromLeadId: number | null;
  openOpportunities: number;
  lastActivityAt: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface TimelineEntry {
  type: string;
  title: string;
  detail: string | null;
  actor: string;
  at: string;
}

export const contactsApi = {
  ...listResource<ContactRow>("/api/contacts"),
  one: (id: number) => get<ContactRow>(`/api/contacts/${id}`),
  timeline: (id: number) => get<TimelineEntry[]>(`/api/contacts/${id}/timeline`),
  remove: (id: number) => remove(`/api/contacts/${id}`),
};

/* ------------------------------------------------------------------ *
 * Opportunities
 * ------------------------------------------------------------------ */

export interface OpportunityRow {
  id: number;
  name: string;
  contactId: number | null;
  contactName: string | null;
  leadId: number | null;
  projectId: number | null;
  projectName: string | null;
  unitId: number | null;
  unitNumber: string | null;
  stage: string;
  type: string;
  source: string;
  forecastCategory: string;
  amount: number;
  expectedCommission: number | null;
  currency: string;
  probability: number;
  aiProbability: number | null;
  aiBand: string | null;
  expectedCloseDate: string;
  actualCloseDate: string | null;
  daysInStage: number;
  branchId: number;
  branchName: string;
  ownerId: number | null;
  ownerName: string | null;
  nextStep: string | null;
  nextStepDueAt: string | null;
  lossReason: string | null;
  competitorName: string | null;
  description: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface PipelineColumn {
  stage: string;
  label: string;
  count: number;
  value: number;
  weightedValue: number;
  averageDaysInStage: number;
  deals: OpportunityRow[];
}

export interface PipelineBoard {
  columns: PipelineColumn[];
  totalDeals: number;
  openValue: number;
  weightedValue: number;
  winRate: number;
  averageAgeDays: number;
  scoringEngine: string;
}

export const opportunitiesApi = {
  ...listResource<OpportunityRow>("/api/opportunities"),
  one: (id: number) => get<OpportunityRow>(`/api/opportunities/${id}`),
  pipeline: (request: QueryRequest) =>
    post<PipelineBoard>("/api/opportunities/pipeline", request),
  moveStage: (
    id: number,
    body: { stage: string; probability?: number; lossReason?: string }
  ) => patch<OpportunityRow>(`/api/opportunities/${id}/stage`, body),
  remove: (id: number) => remove(`/api/opportunities/${id}`),
};

/* ------------------------------------------------------------------ *
 * Follow-ups
 * ------------------------------------------------------------------ */

export interface FollowUpRow {
  id: number;
  subject: string;
  description: string | null;
  relatedType: string;
  relatedId: number;
  relatedName: string;
  channel: string;
  status: string;
  priority: string;
  dueAt: string;
  reminderAt: string | null;
  completedAt: string | null;
  outcome: string | null;
  slaMinutes: number;
  isOverdue: boolean;
  minutesToDue: number;
  branchId: number;
  branchName: string;
  ownerId: number | null;
  ownerName: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface AgendaBucket {
  key: string;
  label: string;
  count: number;
}

export interface Agenda {
  overdue: number;
  today: number;
  tomorrow: number;
  thisWeek: number;
  later: number;
  byChannel: AgendaBucket[];
  byPriority: AgendaBucket[];
}

export interface FollowUpInput {
  subject: string;
  description?: string;
  relatedType: string;
  relatedId: number;
  relatedName: string;
  channel: string;
  status: string;
  priority: string;
  dueAt: string;
  reminderAt?: string;
  outcome?: string;
  slaMinutes: number;
  branchId: number;
  ownerId?: number | null;
}

/** Scheduled, Confirmed, Completed, NoShow, Cancelled, Rescheduled. */
export interface VisitStatusChange {
  status: string;
  /** Required for Rescheduled; also accepted when re-opening into a new slot. */
  scheduledAt?: string | null;
  reason?: string | null;
}

/** Open, InProgress, Completed, Cancelled — plus an optional new due date. */
export interface FollowUpStatusChange {
  status: string;
  dueAt?: string | null;
  outcome?: string | null;
}

export const followUpsApi = {
  ...listResource<FollowUpRow>("/api/follow-ups"),
  create: (input: FollowUpInput) => post<FollowUpRow>("/api/follow-ups", input),
  agenda: (ownerId?: number) =>
    get<Agenda>(`/api/follow-ups/agenda${ownerId ? `?ownerId=${ownerId}` : ""}`),
  complete: (
    id: number,
    body: {
      outcome?: string;
      nextDueAt?: string;
      nextSubject?: string;
      nextChannel?: string;
    }
  ) => patch<FollowUpRow>(`/api/follow-ups/${id}/complete`, body),
  setStatus: (id: number, body: FollowUpStatusChange) =>
    patch<FollowUpRow>(`/api/follow-ups/${id}/status`, body),
  remove: (id: number) => remove(`/api/follow-ups/${id}`),
};

/* ------------------------------------------------------------------ *
 * Calls
 * ------------------------------------------------------------------ */

export interface CallRow {
  id: number;
  relatedType: string;
  relatedId: number;
  relatedName: string;
  direction: string;
  outcome: string;
  disposition: string | null;
  phoneNumber: string | null;
  startedAt: string;
  durationSeconds: number;
  waitSeconds: number | null;
  agentId: number | null;
  agentName: string;
  notes: string | null;
  recordingUrl: string | null;
  sentimentScore: number | null;
  sentimentLabel: string | null;
  followUpAt: string | null;
  branchId: number;
  branchName: string;
  createdAt: string;
}

export interface CallScorecard {
  agentId: number | null;
  agentName: string;
  calls: number;
  connected: number;
  connectRate: number;
  talkMinutes: number;
  averageCallSeconds: number;
  progressed: number;
  progressionRate: number;
  positiveSentiment: number;
}

export interface CallDailyPoint {
  date: string;
  calls: number;
  connected: number;
  talkMinutes: number;
}

export interface CallHourPoint {
  hour: number;
  calls: number;
  connected: number;
}

export interface CallAnalytics {
  windowDays: number;
  totalCalls: number;
  connected: number;
  connectRate: number;
  talkMinutes: number;
  averageCallSeconds: number;
  inbound: number;
  outbound: number;
  missed: number;
  averageWaitSeconds: number;
  daily: CallDailyPoint[];
  hourly: CallHourPoint[];
  byOutcome: AgendaBucket[];
  byDisposition: AgendaBucket[];
  bySentiment: AgendaBucket[];
}

export interface CallInput {
  relatedType: string;
  relatedId: number;
  relatedName: string;
  direction: string;
  outcome: string;
  disposition?: string | null;
  phoneNumber?: string | null;
  startedAt: string;
  durationSeconds: number;
  waitSeconds?: number | null;
  agentId?: number | null;
  notes?: string | null;
  followUpAt?: string | null;
  branchId: number;
}

export const callsApi = {
  ...listResource<CallRow>("/api/calls"),
  create: (input: CallInput) => post<CallRow>("/api/calls", input),
  scorecard: (days = 30) =>
    get<CallScorecard[]>(`/api/calls/scorecard?days=${days}`),
  analytics: (days = 30) =>
    get<CallAnalytics>(`/api/calls/analytics?days=${days}`),
};

/* ------------------------------------------------------------------ *
 * Site visits
 * ------------------------------------------------------------------ */

export interface SiteVisitRow {
  id: number;
  visitCode: string;
  leadId: number | null;
  contactId: number | null;
  opportunityId: number | null;
  projectId: number | null;
  projectName: string | null;
  unitId: number | null;
  unitNumber: string | null;
  visitorName: string;
  visitorPhone: string | null;
  partySize: number;
  visitType: string;
  status: string;
  scheduledAt: string;
  /** Length of the booked calendar slot. */
  slotMinutes: number;
  checkInAt: string | null;
  checkOutAt: string | null;
  /** Measured time on site, once checked out. */
  durationMinutes: number | null;
  hostId: number | null;
  hostName: string | null;
  transportMode: string | null;
  pickupLocation: string | null;
  feedback: string | null;
  interestLevel: string | null;
  rating: number | null;
  budgetDiscussed: number | null;
  nextAction: string | null;
  cancellationReason: string | null;
  branchId: number;
  branchName: string;
  createdAt: string;
  updatedAt: string;
}

export interface VisitProjectStat {
  projectName: string;
  visits: number;
  completed: number;
  highInterest: number;
  averageRating: number;
}

export interface SiteVisitInsights {
  total: number;
  completed: number;
  noShow: number;
  completionRate: number;
  noShowRate: number;
  averageDurationMinutes: number;
  highInterest: number;
  byProject: VisitProjectStat[];
  byWeekday: AgendaBucket[];
}

export interface SiteVisitInput {
  leadId?: number | null;
  contactId?: number | null;
  opportunityId?: number | null;
  projectId?: number | null;
  unitId?: number | null;
  visitorName: string;
  visitorPhone?: string | null;
  partySize: number;
  visitType: string;
  status: string;
  scheduledAt: string;
  /** How long the calendar slot is held for. */
  durationMinutes: number;
  checkInAt?: string | null;
  checkOutAt?: string | null;
  hostId?: number | null;
  transportMode?: string | null;
  pickupLocation?: string | null;
  feedback?: string | null;
  interestLevel?: string | null;
  rating?: number | null;
  budgetDiscussed?: number | null;
  nextAction?: string | null;
  cancellationReason?: string | null;
  branchId: number;
  /** Book on top of an existing meeting. Off unless the rep insists. */
  allowOverlap?: boolean;
}

export const siteVisitsApi = {
  ...listResource<SiteVisitRow>("/api/site-visits"),
  create: (input: SiteVisitInput) => post<SiteVisitRow>("/api/site-visits", input),
  insights: (days = 90) =>
    get<SiteVisitInsights>(`/api/site-visits/insights?days=${days}`),
  checkIn: (id: number) => patch<SiteVisitRow>(`/api/site-visits/${id}/check-in`, {}),
  checkOut: (
    id: number,
    body: {
      feedback?: string;
      interestLevel?: string;
      rating?: number;
      budgetDiscussed?: number;
      nextAction?: string;
    }
  ) => patch<SiteVisitRow>(`/api/site-visits/${id}/check-out`, body),
  setStatus: (id: number, body: VisitStatusChange) =>
    patch<SiteVisitRow>(`/api/site-visits/${id}/status`, body),
};

/* ------------------------------------------------------------------ *
 * OBM visits
 * ------------------------------------------------------------------ */

export interface ObmVisitRow {
  id: number;
  visitCode: string;
  /** Set when the meeting was held for one specific lead; most are not. */
  leadId: number | null;
  leadName: string | null;
  partnerName: string;
  partnerType: string;
  contactPerson: string | null;
  contactPhone: string | null;
  status: string;
  scheduledAt: string;
  /** Length of the booked calendar slot. */
  slotMinutes: number;
  checkInAt: string | null;
  checkOutAt: string | null;
  /** Measured meeting length, once checked out. */
  durationMinutes: number | null;
  latitude: number | null;
  longitude: number | null;
  locationLabel: string | null;
  city: string | null;
  distanceKm: number | null;
  expenseAmount: number | null;
  purpose: string | null;
  outcome: string | null;
  meetingNotes: string | null;
  leadsGenerated: number;
  businessValue: number | null;
  nextMeetingAt: string | null;
  agentId: number | null;
  agentName: string;
  branchId: number;
  branchName: string;
  createdAt: string;
  updatedAt: string;
}

export interface FieldProductivity {
  agentId: number | null;
  agentName: string;
  visits: number;
  completed: number;
  leadsGenerated: number;
  businessValue: number;
  expense: number;
  distanceKm: number;
  costPerLead: number;
  leadsPerVisit: number;
}

export interface ObmVisitInput {
  leadId?: number | null;
  partnerName: string;
  partnerType: string;
  contactPerson?: string | null;
  contactPhone?: string | null;
  status: string;
  scheduledAt: string;
  /** How long the calendar slot is held for. */
  durationMinutes: number;
  checkInAt?: string | null;
  checkOutAt?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  locationLabel?: string | null;
  city?: string | null;
  distanceKm?: number | null;
  expenseAmount?: number | null;
  purpose?: string | null;
  outcome?: string | null;
  meetingNotes?: string | null;
  leadsGenerated: number;
  businessValue?: number | null;
  nextMeetingAt?: string | null;
  agentId?: number | null;
  branchId: number;
  /** Book on top of an existing meeting. Off unless the rep insists. */
  allowOverlap?: boolean;
}

export const obmVisitsApi = {
  ...listResource<ObmVisitRow>("/api/obm-visits"),
  create: (input: ObmVisitInput) => post<ObmVisitRow>("/api/obm-visits", input),
  productivity: (days = 30) =>
    get<FieldProductivity[]>(`/api/obm-visits/productivity?days=${days}`),
  checkIn: (id: number, body: { latitude?: number; longitude?: number; locationLabel?: string }) =>
    patch<ObmVisitRow>(`/api/obm-visits/${id}/check-in`, body),
  setStatus: (id: number, body: VisitStatusChange) =>
    patch<ObmVisitRow>(`/api/obm-visits/${id}/status`, body),
};

/* ------------------------------------------------------------------ *
 * Scheduling
 * ------------------------------------------------------------------ */

export type SlotState = "Available" | "Busy" | "Past" | "OutsideHours";

export interface SlotBooking {
  kind: string;
  id: number;
  title: string;
  subtitle: string | null;
  start: string;
  end: string;
}

export interface Slot {
  start: string;
  end: string;
  /** "14:30" — the label shown on the grid button. */
  label: string;
  state: SlotState;
  /** What is already in this slot, when it is busy. */
  bookings: SlotBooking[];
}

export interface Availability {
  date: string;
  userId: number | null;
  userName: string;
  slotMinutes: number;
  durationMinutes: number;
  businessStart: string;
  businessEnd: string;
  slots: Slot[];
  availableCount: number;
  busyCount: number;
  bookingCount: number;
}

export const schedulingApi = {
  /**
   * The slot grid for one person on one day. `durationMinutes` matters: a slot
   * only reads as available if the whole meeting fits inside working hours
   * without touching anything already booked.
   */
  availability: (params: {
    date: string;
    userId?: number | null;
    slotMinutes?: number;
    durationMinutes?: number;
  }) => {
    const query = new URLSearchParams({ date: params.date });
    if (params.userId) query.set("userId", String(params.userId));
    if (params.slotMinutes) query.set("slotMinutes", String(params.slotMinutes));
    if (params.durationMinutes) {
      query.set("durationMinutes", String(params.durationMinutes));
    }
    return get<Availability>(`/api/scheduling/availability?${query}`);
  },

  /** The day's meetings in order — the queue behind a busy grid. */
  day: (date: string, userId?: number | null) => {
    const query = new URLSearchParams({ date });
    if (userId) query.set("userId", String(userId));
    return get<SlotBooking[]>(`/api/scheduling/day?${query}`);
  },
};

/* ------------------------------------------------------------------ *
 * Quotations
 * ------------------------------------------------------------------ */

export interface QuotationLine {
  id: number;
  description: string;
  category: string | null;
  quantity: number;
  unit: string | null;
  unitPrice: number;
  discountPercent: number;
  lineTotal: number;
  sortOrder: number;
}

export interface QuotationRow {
  id: number;
  quoteNumber: string;
  title: string;
  version: number;
  contactId: number | null;
  leadId: number | null;
  opportunityId: number | null;
  projectId: number | null;
  projectName: string | null;
  unitId: number | null;
  unitNumber: string | null;
  customerName: string;
  customerEmail: string | null;
  customerPhone: string | null;
  billingAddress: string | null;
  status: string;
  issueDate: string;
  validUntil: string;
  sentAt: string | null;
  respondedAt: string | null;
  isExpired: boolean;
  currency: string;
  subtotal: number;
  discountPercent: number;
  discountAmount: number;
  taxPercent: number;
  taxAmount: number;
  /** The unit cost — what the payment plan percentages are struck on. */
  total: number;
  chargesTotal: number;
  /** Unit cost plus every other charge head. What the buyer actually pays. */
  grandTotal: number;
  approvalStatus: "NotRequired" | "Pending" | "Approved" | "Rejected";
  paymentTerms: string | null;
  notes: string | null;
  termsAndConditions: string | null;
  rejectionReason: string | null;
  branchId: number;
  branchName: string;
  ownerId: number | null;
  ownerName: string | null;
  lines: QuotationLine[];
  createdAt: string;
  updatedAt: string;
}

export const quotationsApi = {
  ...listResource<QuotationRow>("/api/quotations"),
  one: (id: number) => get<QuotationRow>(`/api/quotations/${id}`),
  revise: (id: number) => post<QuotationRow>(`/api/quotations/${id}/revise`, {}),
  changeStatus: (id: number, status: string, reason?: string) =>
    patch<QuotationRow>(`/api/quotations/${id}/status`, { status, reason }),
};

/* ------------------------------------------------------------------ *
 * Inventory
 * ------------------------------------------------------------------ */

export interface ProjectRow {
  id: number;
  name: string;
  code: string;
  developer: string | null;
  type: string;
  status: string;
  city: string | null;
  locality: string | null;
  address: string | null;
  reraNumber: string | null;
  launchDate: string | null;
  possessionDate: string | null;
  priceMin: number | null;
  priceMax: number | null;
  amenities: string | null;
  description: string | null;
  totalUnits: number;
  availableUnits: number;
  bookedUnits: number;
  inventoryValue: number;
  createdAt: string;
}

export interface UnitRow {
  id: number;
  projectId: number;
  projectName: string;
  towerId: number | null;
  towerName: string | null;
  unitNumber: string;
  floor: number;
  configuration: string;
  carpetArea: number;
  builtUpArea: number | null;
  superArea: number | null;
  areaUnit: string;
  facing: string | null;
  viewType: string | null;
  bathrooms: number;
  balconies: number;
  parkingSlots: number;
  isCornerUnit: boolean;
  vastuCompliant: boolean;

  /* ---- capacity: what decides whether a space fits the party ---- */

  /** Guests at tables. Always well below `floatingCapacity`. */
  seatingCapacity: number;
  /** Guests standing, cocktail style. */
  floatingCapacity: number;
  theatreCapacity: number | null;
  isAirConditioned: boolean;
  isOutdoor: boolean;
  hasStage: boolean;
  hasAttachedKitchen: boolean;

  status: string;
  /** The hall rental. */
  basePrice: number;
  pricePerSqft: number;
  floorRisePremium: number;
  plcCharges: number;
  totalPrice: number;

  /* ---- event pricing ---- */

  pricePerPlate: number;
  /** Plates billed whether or not the guests turn up. The real floor price. */
  minimumPlates: number;
  /** Extra on a peak date, as a fraction of the rental. */
  peakDatePremium: number;
  securityDeposit: number;

  heldByUserId: number | null;
  heldByName: string | null;
  heldUntil: string | null;
  holdReason: string | null;
  bookedByContactId: number | null;
  bookedAt: string | null;
  createdAt: string;
}

export interface AvailabilityCell {
  unitId: number;
  unitNumber: string;
  configuration: string;
  status: string;
  totalPrice: number;
  carpetArea: number;
  towerName: string | null;
}

export interface ConfigurationStat {
  configuration: string;
  total: number;
  available: number;
  averagePrice: number;
  averagePricePerSqft: number;
}

export interface AvailabilityGrid {
  projectId: number;
  projectName: string;
  totalUnits: number;
  availableUnits: number;
  availableValue: number;
  floors: { floor: number; units: AvailabilityCell[] }[];
  byStatus: AgendaBucket[];
  byConfiguration: ConfigurationStat[];
}

export const inventoryApi = {
  fields: () => get<FilterField[]>("/api/inventory/fields"),
  query: (request: QueryRequest) =>
    post<PagedResult<UnitRow>>("/api/inventory/units/query", request),
  projects: () => get<ProjectRow[]>("/api/inventory/projects"),
  availability: (projectId: number) =>
    get<AvailabilityGrid>(`/api/inventory/projects/${projectId}/availability`),
  hold: (
    unitId: number,
    hours: number,
    reason?: string,
    opts?: { eventDate?: string; eventSlot?: string; clientName?: string }
  ) =>
    post<UnitRow>(`/api/inventory/units/${unitId}/hold`, {
      hours,
      reason,
      eventDate: opts?.eventDate,
      eventSlot: opts?.eventSlot ?? "Evening",
      clientName: opts?.clientName,
    }),
  release: (unitId: number, opts?: { eventDate?: string; eventSlot?: string }) => {
    const q = new URLSearchParams();
    if (opts?.eventDate) q.set("eventDate", opts.eventDate);
    if (opts?.eventSlot) q.set("eventSlot", opts.eventSlot);
    const qs = q.toString();
    return post<UnitRow>(
      `/api/inventory/units/${unitId}/release${qs ? `?${qs}` : ""}`,
      {}
    );
  },
};

/* ------------------------------------------------------------------ *
 * Intelligence
 * ------------------------------------------------------------------ */

export interface ModelHealth {
  name: string;
  engine: string;
  isTrained: boolean;
  trainedAt: string | null;
  trainingRows: number;
  primaryMetric: number | null;
  metricName: string | null;
  message: string | null;
}

export interface PriorityLead {
  leadId: number;
  name: string;
  stage: string;
  priority: string;
  score: number;
  band: string;
  nextAction: string;
  flag: string | null;
}

export interface IntelligenceDigest {
  priorityLeads: PriorityLead[];
  hotLeads: number;
  unassignedLeads: number;
  slaBreached: number;
  staleLeads: number;
  overdueFollowUps: number;
  expiringQuotations: number;
  expiringHolds: number;
  models: ModelHealth[];
}

export interface ForecastPoint {
  period: string;
  value: number;
  lowerBound: number | null;
  upperBound: number | null;
  isForecast: boolean;
}

export interface ForecastResult {
  engine: string;
  series: ForecastPoint[];
  nextPeriodValue: number;
  horizonTotal: number;
  confidence: number | null;
  message: string;
}

export interface SegmentProfile {
  clusterId: number;
  name: string;
  description: string;
  size: number;
  averageLifetimeValue: number;
  averageDeals: number;
  averageDaysSinceActivity: number;
}

export interface SegmentationResult {
  engine: string;
  segments: SegmentProfile[];
  assignments: { contactId: number; clusterId: number; segment: string; distance: number }[];
  daviesBouldinIndex: number | null;
  message: string;
}

export interface AnomalyPoint {
  period: string;
  value: number;
  isAnomaly: boolean;
  score: number;
  pValue: number;
  note: string | null;
}

export interface AnomalyReport {
  engine: string;
  metric: string;
  points: AnomalyPoint[];
  anomalyCount: number;
  message: string;
}

export interface ScoreSignal {
  label: string;
  detail: string;
  points: number;
}

export interface AssignmentSuggestion {
  userId: number;
  name: string;
  fit: number;
  reason: string;
  factors: ScoreSignal[];
}

export interface AutoAssignResult {
  assigned: number;
  assignments: {
    leadId: number;
    leadName: string;
    ownerId: number;
    ownerName: string;
    fit: number;
    reason: string;
  }[];
  message: string;
}

export interface WinInsight {
  probability: number;
  band: string;
  engine: string;
  drivers: ScoreSignal[];
}

export interface RecommendedAction {
  action: string;
  reason: string;
  urgency: "High" | "Medium" | "Low";
  activityType: string;
}

export interface DuplicateMatch {
  leadId: number;
  name: string;
  phone: string | null;
  email: string | null;
  stage: string;
  reason: string;
  /** 0–100. Anything above ~80 is almost certainly the same person. */
  confidence: number;
}

export interface LeadInsights {
  leadId: number;
  score: number;
  band: string;
  /** "MLNet:FastTree" when the trained model produced it, else "Heuristic". */
  engine: string;
  signals: ScoreSignal[];
  actions: RecommendedAction[];
  duplicates: DuplicateMatch[];
}

export const intelligenceApi = {
  digest: () => get<IntelligenceDigest>("/api/intelligence/digest"),
  models: () => get<ModelHealth[]>("/api/intelligence/models"),
  trainAll: () => post<ModelHealth[]>("/api/intelligence/models/train", {}),
  rescore: () => post<BulkResult>("/api/intelligence/leads/rescore", {}),
  forecast: (metric: "revenue" | "leads" | "bookings", horizon = 3) =>
    get<ForecastResult>(
      `/api/intelligence/forecast?metric=${metric}&horizon=${horizon}`
    ),
  segments: (clusters = 4) =>
    get<SegmentationResult>(`/api/intelligence/segments?clusters=${clusters}`),
  applySegments: (clusters = 4) =>
    post<BulkResult>(`/api/intelligence/segments/apply?clusters=${clusters}`, {}),
  anomalies: (metric: "leads" | "calls" | "visits", days = 90) =>
    get<AnomalyReport>(`/api/intelligence/anomalies?metric=${metric}&days=${days}`),
  assignmentFor: (leadId: number) =>
    get<AssignmentSuggestion[]>(`/api/intelligence/leads/${leadId}/assignment`),
  autoAssign: () => post<AutoAssignResult>("/api/intelligence/leads/auto-assign", {}),
  winInsight: (opportunityId: number) =>
    get<WinInsight>(`/api/intelligence/opportunities/${opportunityId}`),
  leadInsights: (leadId: number) =>
    get<LeadInsights>(`/api/intelligence/leads/${leadId}`),
};

/* ------------------------------------------------------------------ *
 * Formatting shared across the workspace
 * ------------------------------------------------------------------ */

/** Indian numbering — 1.2 Cr reads better than ₹12,00,00,000 in a table cell. */
export function formatMoney(value: number | null | undefined, currency = "₹") {
  if (value === null || value === undefined) return "—";
  const abs = Math.abs(value);

  if (abs >= 10_000_000) return `${currency}${(value / 10_000_000).toFixed(2)} Cr`;
  if (abs >= 100_000) return `${currency}${(value / 100_000).toFixed(2)} L`;
  if (abs >= 1_000) return `${currency}${(value / 1_000).toFixed(1)}K`;

  return `${currency}${value.toFixed(0)}`;
}

export function formatCompact(value: number | null | undefined) {
  if (value === null || value === undefined) return "—";
  const abs = Math.abs(value);
  if (abs >= 10_000_000) return `${(value / 10_000_000).toFixed(1)}Cr`;
  if (abs >= 100_000) return `${(value / 100_000).toFixed(1)}L`;
  if (abs >= 1_000) return `${(value / 1_000).toFixed(1)}K`;
  return String(Math.round(value));
}

export function formatDuration(seconds: number | null | undefined) {
  if (!seconds) return "—";
  const minutes = Math.floor(seconds / 60);
  const remainder = seconds % 60;
  return minutes > 0 ? `${minutes}m ${remainder}s` : `${remainder}s`;
}

export function timeAgo(iso: string | null | undefined) {
  if (!iso) return "—";
  const diff = Date.now() - new Date(iso).getTime();
  const future = diff < 0;
  const minutes = Math.floor(Math.abs(diff) / 60_000);

  const label =
    minutes < 1
      ? "now"
      : minutes < 60
        ? `${minutes}m`
        : minutes < 1440
          ? `${Math.floor(minutes / 60)}h`
          : minutes < 43_200
            ? `${Math.floor(minutes / 1440)}d`
            : `${Math.floor(minutes / 43_200)}mo`;

  if (label === "now") return "just now";
  return future ? `in ${label}` : `${label} ago`;
}

export function formatDate(iso: string | null | undefined) {
  if (!iso) return "—";
  return new Date(iso).toLocaleDateString(undefined, {
    day: "2-digit",
    month: "short",
    year: "2-digit",
  });
}

export function formatDateTime(iso: string | null | undefined) {
  if (!iso) return "—";
  return new Date(iso).toLocaleString(undefined, {
    day: "2-digit",
    month: "short",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/** Date, time and year — for columns where "when exactly" is the whole point. */
export function formatDateTimeFull(iso: string | null | undefined) {
  if (!iso) return "—";
  return new Date(iso).toLocaleString(undefined, {
    day: "2-digit",
    month: "short",
    year: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
  });
}

/**
 * Brand and product names that are already one word, whatever their casing.
 * Splitting on the capital would give "Whats App" and "My Operator".
 */
const COMPOUND_WORDS: Record<string, string> = {
  WhatsApp: "WhatsApp",
  MyOperator: "MyOperator",
  NRIRemittance: "NRI remittance",
  SelfFunded: "Self-funded",
  HomeLoan: "Home loan",

  // Event vocabulary whose split reads wrong, or which carries an accent or a
  // different label from its stored value.
  Decor: "Décor",
  SoundAndLight: "Sound & light",
  GiftsAndFavours: "Gifts & favours",
  CorporatePo: "Corporate PO",
  NonVegetarian: "Non-vegetarian",
  PreWeddingShoot: "Pre-wedding shoot",
  FullDay: "Full day",
  PreFunctionArea: "Pre-function area",
  MandapArea: "Mandap area",

  // The two stages and the status whose stored name is not what the product
  // calls them — see LEAD_STAGES and the inventory status styles.
  ObmVisit: "Client meeting",
  SiteVisit: "Venue visit",
  Sold: "Confirmed",
  NotForSale: "Not bookable",
  ChannelPartner: "Referral partner",
  EventPortal: "Event portal",
  VendorPartner: "Vendor partner",
  RepeatClient: "Repeat client",
};

/** `VendorPartner` → `Vendor Partner`, for values the API returns raw. */
export function humanise(value: string | null | undefined) {
  if (!value) return "—";
  if (COMPOUND_WORDS[value]) return COMPOUND_WORDS[value];

  return value.replace(/([a-z0-9])([A-Z])/g, "$1 $2");
}

export function initials(name: string) {
  return name
    .split(" ")
    .map((part) => part[0])
    .filter(Boolean)
    .slice(0, 2)
    .join("")
    .toUpperCase();
}
