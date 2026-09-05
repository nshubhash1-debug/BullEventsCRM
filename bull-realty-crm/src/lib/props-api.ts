import { API_URL, apiRequest } from "@/lib/api";
import type { FilterField, PagedResult, QueryRequest } from "@/lib/query";

/* ------------------------------------------------------------------ *
 * Props, décor and stock — the godown.
 *
 * Distinct from `inventory-api`, which is the venue side: halls and lawns sold
 * by the date. This is countable stock sold by the piece, and its central
 * question is different — not "is the hall free on the 14th" but "how many of
 * these forty vases are still free between the 10th and the 14th".
 * ------------------------------------------------------------------ */

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

function del<T>(path: string) {
  return apiRequest<T>(path, { method: "DELETE", auth: true });
}

/**
 * Turns the API's relative photo path into something an `<img>` can load.
 *
 * The API stores `/props/thumb/abc.jpg` rather than a full URL so the same rows
 * survive the host changing between dev, LAN and the tunnel.
 */
export function propPhotoUrl(path: string | null | undefined) {
  if (!path) return null;
  if (path.startsWith("http://") || path.startsWith("https://")) return path;
  return `${API_URL}${path}`;
}

/* ---------------- vocabularies ---------------- */

export const PROP_ITEM_TYPES = [
  "Prop",
  "Furniture",
  "Fabric",
  "Equipment",
  "Floral",
  "Structure",
  "Consumable",
] as const;

export const PROP_ITEM_STATUSES = ["Active", "OnHold", "Retired"] as const;

export const PROP_OWNERSHIP_TYPES = ["Owned", "SubHired", "ClientSupplied"] as const;

export const PROP_CONDITIONS = ["Good", "Repairable", "Damaged", "Lost"] as const;

/**
 * The movement types a person may book by hand.
 *
 * Dispatch and return are missing on purpose: those belong to a gate pass,
 * which keeps the reservations in step with the counts. The API refuses them
 * here, and offering them in a dropdown would only produce an error.
 */
export const PROP_MANUAL_MOVEMENTS = [
  { value: "Purchase", label: "Purchase — new stock bought in" },
  { value: "Damage", label: "Damage — good stock became damaged" },
  { value: "Repair", label: "Repair — repairable stock is usable again" },
  { value: "WriteOff", label: "Write-off — off the books entirely" },
  { value: "Adjustment", label: "Adjustment — a physical count corrected this" },
] as const;

export const PROP_ISSUE_STATUSES = [
  "Draft",
  "Reserved",
  "Dispatched",
  "PartiallyReturned",
  "Returned",
  "Closed",
  "Cancelled",
] as const;

/** How each gate-pass status should read on screen. */
export const PROP_ISSUE_STATUS_LABELS: Record<string, string> = {
  Draft: "Draft",
  Reserved: "Reserved",
  Dispatched: "Out on event",
  PartiallyReturned: "Part returned",
  Returned: "Returned",
  Closed: "Closed",
  Cancelled: "Cancelled",
};

/* ---------------- shapes ---------------- */

export interface PropCategory {
  id: number;
  name: string;
  code: string;
  parentId: number | null;
  defaultItemType: string;
  sortOrder: number;
  isActive: boolean;
  itemCount: number;
  totalGoodQuantity: number;
}

export interface PropStore {
  id: number;
  name: string;
  code: string;
  city: string | null;
  address: string | null;
  keeperId: number | null;
  keeperName: string | null;
  isActive: boolean;
  itemCount: number;
}

export interface PropPhoto {
  id: number;
  url: string;
  thumbnailUrl: string | null;
  caption: string | null;
  sortOrder: number;
}

export interface PropItem {
  id: number;
  categoryId: number;
  categoryName: string;
  storeId: number | null;
  storeName: string | null;
  name: string;
  code: string;
  itemType: string;
  status: string;
  ownership: string;
  size: string | null;
  colour: string | null;
  material: string | null;
  unit: string;
  description: string | null;
  tags: string | null;
  goodQuantity: number;
  repairableQuantity: number;
  damagedQuantity: number;
  onHandQuantity: number;
  reorderLevel: number;
  isBelowReorderLevel: boolean;
  rentalRatePerDay: number | null;
  purchaseCost: number | null;
  replacementValue: number | null;
  supplierName: string | null;
  purchaseDate: string | null;
  weightKg: number | null;
  packingUnit: number | null;
  isFragile: boolean;
  isSerialised: boolean;
  turnaroundDays: number;
  storageLocation: string | null;
  ownerId: number | null;
  ownerName: string | null;
  primaryPhotoUrl: string | null;
  primaryThumbnailUrl: string | null;
  photoCount: number;
  photos: PropPhoto[];
  /** Only populated when the call named a date window. */
  reservedQuantity: number | null;
  availableQuantity: number | null;
  createdAt: string;
  updatedAt: string;
}

export interface PropMovement {
  id: number;
  propItemId: number;
  itemName: string;
  itemCode: string;
  movementType: string;
  quantity: number;
  fromCondition: string | null;
  toCondition: string | null;
  balanceAfter: number;
  propIssueId: number | null;
  issueCode: string | null;
  leadId: number | null;
  amount: number | null;
  notes: string | null;
  handledBy: string | null;
  movedAt: string;
  recordedBy: string | null;
}

export interface PropIssueLine {
  id: number;
  propItemId: number;
  itemName: string;
  itemCode: string;
  categoryName: string;
  unit: string;
  primaryThumbnailUrl: string | null;
  reservedQuantity: number;
  issuedQuantity: number;
  returnedQuantity: number;
  damagedQuantity: number;
  lostQuantity: number;
  consumedQuantity: number;
  pendingQuantity: number;
  ratePerDay: number | null;
  chargeableDays: number;
  lineTotal: number;
  notes: string | null;
  sortOrder: number;
  availableQuantity: number | null;
}

export interface PropIssue {
  id: number;
  code: string;
  status: string;
  leadId: number | null;
  bookingId: number | null;
  quotationId: number | null;
  projectId: number | null;
  eventName: string | null;
  eventType: string | null;
  clientName: string | null;
  venueName: string | null;
  venueAddress: string | null;
  dispatchDate: string;
  eventDate: string | null;
  expectedReturnDate: string;
  actualReturnDate: string | null;
  storeId: number | null;
  storeName: string | null;
  vehicleNumber: string | null;
  driverName: string | null;
  driverPhone: string | null;
  siteInChargeId: number | null;
  siteInChargeName: string | null;
  notes: string | null;
  damageRecovery: number | null;
  ownerId: number | null;
  ownerName: string | null;
  lineCount: number;
  totalReserved: number;
  totalIssued: number;
  totalReturned: number;
  totalPending: number;
  estimatedValue: number;
  isOverdue: boolean;
  dispatchedAt: string | null;
  closedAt: string | null;
  createdAt: string;
  lines: PropIssueLine[];
}

export interface PropAvailabilityHold {
  reservationId: number;
  propIssueId: number | null;
  issueCode: string | null;
  quantity: number;
  fromDate: string;
  toDate: string;
  eventName: string | null;
  clientName: string | null;
  status: string;
}

export interface PropAvailability {
  propItemId: number;
  itemName: string;
  itemCode: string;
  categoryName: string;
  unit: string;
  primaryThumbnailUrl: string | null;
  goodQuantity: number;
  reservedQuantity: number;
  availableQuantity: number;
  holds: PropAvailabilityHold[];
}

export interface PropCalendarCell {
  date: string;
  reserved: number;
  available: number;
}

export interface PropCalendarRow {
  propItemId: number;
  itemName: string;
  itemCode: string;
  unit: string;
  goodQuantity: number;
  days: PropCalendarCell[];
}

export interface PropCategorySummary {
  categoryId: number;
  categoryName: string;
  itemCount: number;
  goodQuantity: number;
  repairableQuantity: number;
  damagedQuantity: number;
  estimatedValue: number;
}

export interface PropDashboard {
  totalItems: number;
  totalPieces: number;
  goodPieces: number;
  repairablePieces: number;
  damagedPieces: number;
  catalogueValue: number;
  itemsBelowReorder: number;
  openIssues: number;
  dispatchedIssues: number;
  overdueIssues: number;
  piecesOutOnEvents: number;
  dispatchesThisWeek: number;
  returnsDueThisWeek: number;
  categories: PropCategorySummary[];
  lowStock: PropItem[];
  overdue: PropIssue[];
}

/* ---------------- rates ---------------- */

/**
 * One item's commercial numbers.
 *
 * Every field is optional and only what is sent gets written, so a screen that
 * only sets rental rates cannot blank the replacement values somebody filled in
 * last week.
 */
export interface PropRateRow {
  propItemId: number;
  rentalRatePerDay?: number | null;
  replacementValue?: number | null;
  purchaseCost?: number | null;
  reorderLevel?: number | null;
  weightKg?: number | null;
  packingUnit?: number | null;
  storageLocation?: string | null;
}

export interface PropRateBulkResult {
  updated: number;
  skipped: number;
  stillUnpriced: number;
  totalItems: number;
  catalogueValue: number;
  warnings: string[];
}

export interface PropPricingCoverage {
  categoryId: number;
  categoryName: string;
  itemCount: number;
  priced: number;
  unpriced: number;
  averageRate: number | null;
  categoryValue: number;
}

export interface PropPricingSummary {
  totalItems: number;
  priced: number;
  unpriced: number;
  catalogueValue: number;
  averageRate: number | null;
  byCategory: PropPricingCoverage[];
}

export interface PropAvailabilityRequest {
  from: string;
  to: string;
  itemIds?: number[];
  categoryId?: number;
  excludePropIssueId?: number;
}

/* ---------------- calls ---------------- */

export const propsApi = {
  fields: () => get<FilterField[]>("/api/props/fields"),

  query: (request: QueryRequest) =>
    post<PagedResult<PropItem>>("/api/props/items/query", request),

  item: (id: number) => get<PropItem>(`/api/props/items/${id}`),

  createItem: (body: Record<string, unknown>) =>
    post<PropItem>("/api/props/items", body),

  updateItem: (id: number, body: Record<string, unknown>) =>
    put<PropItem>(`/api/props/items/${id}`, body),

  deleteItem: (id: number) => del<void>(`/api/props/items/${id}`),

  addPhoto: (id: number, body: { url: string; thumbnailUrl?: string; caption?: string }) =>
    post<PropPhoto>(`/api/props/items/${id}/photos`, body),

  deletePhoto: (photoId: number) => del<void>(`/api/props/photos/${photoId}`),

  categories: () => get<PropCategory[]>("/api/props/categories"),

  createCategory: (body: Record<string, unknown>) =>
    post<PropCategory>("/api/props/categories", body),

  updateCategory: (id: number, body: Record<string, unknown>) =>
    put<PropCategory>(`/api/props/categories/${id}`, body),

  stores: () => get<PropStore[]>("/api/props/stores"),

  createStore: (body: Record<string, unknown>) =>
    post<PropStore>("/api/props/stores", body),

  availability: (request: PropAvailabilityRequest) =>
    post<PropAvailability[]>("/api/props/availability", request),

  calendar: (request: PropAvailabilityRequest) =>
    post<PropCalendarRow[]>("/api/props/calendar", request),

  movements: (id: number, take = 100) =>
    get<PropMovement[]>(`/api/props/items/${id}/movements?take=${take}`),

  recordMovement: (body: Record<string, unknown>) =>
    post<PropItem>("/api/props/movements", body),

  issues: (params: {
    status?: string;
    leadId?: number;
    overdueOnly?: boolean;
    take?: number;
  } = {}) => {
    const search = new URLSearchParams();
    if (params.status) search.set("status", params.status);
    if (params.leadId) search.set("leadId", String(params.leadId));
    if (params.overdueOnly) search.set("overdueOnly", "true");
    if (params.take) search.set("take", String(params.take));
    const query = search.toString();
    return get<PropIssue[]>(`/api/props/issues${query ? `?${query}` : ""}`);
  },

  issue: (id: number) => get<PropIssue>(`/api/props/issues/${id}`),

  createIssue: (body: Record<string, unknown>) =>
    post<PropIssue>("/api/props/issues", body),

  updateIssue: (id: number, body: Record<string, unknown>) =>
    put<PropIssue>(`/api/props/issues/${id}`, body),

  addLine: (id: number, body: Record<string, unknown>) =>
    post<PropIssue>(`/api/props/issues/${id}/lines`, body),

  removeLine: (id: number, lineId: number) =>
    del<PropIssue>(`/api/props/issues/${id}/lines/${lineId}`),

  reserve: (id: number) => post<PropIssue>(`/api/props/issues/${id}/reserve`),

  dispatch: (id: number, body: Record<string, unknown>) =>
    post<PropIssue>(`/api/props/issues/${id}/dispatch`, body),

  returnStock: (id: number, body: Record<string, unknown>) =>
    post<PropIssue>(`/api/props/issues/${id}/return`, body),

  close: (id: number) => post<PropIssue>(`/api/props/issues/${id}/close`),

  cancel: (id: number) => post<PropIssue>(`/api/props/issues/${id}/cancel`),

  dashboard: () => get<PropDashboard>("/api/props/dashboard"),

  pricingCoverage: () => get<PropPricingSummary>("/api/props/pricing-coverage"),

  setRates: (rows: PropRateRow[]) =>
    post<PropRateBulkResult>("/api/props/rates", { rows }),
};
