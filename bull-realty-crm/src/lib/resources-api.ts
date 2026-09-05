import { apiRequest } from "@/lib/api";
import type { FilterField, PagedResult, QueryRequest } from "@/lib/query";
import type { PropIssue } from "@/lib/props-api";

/* ------------------------------------------------------------------ *
 * Vendors, crew and fleet — the three inventories beside the godown.
 *
 * All three answer the same question the props module does: can this be
 * promised for those dates? A null `isAvailable` means nobody asked about
 * dates, which is not the same as "no".
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
  return apiRequest<T>(path, { method: "PUT", body: JSON.stringify(body), auth: true });
}

function del<T>(path: string) {
  return apiRequest<T>(path, { method: "DELETE", auth: true });
}

/* ---------------- vocabularies ---------------- */

export const SERVICE_CATEGORIES = [
  "Venue",
  "Catering",
  "Decor",
  "Photography",
  "Videography",
  "Makeup",
  "Mehendi",
  "Entertainment",
  "SoundAndLight",
  "Invitations",
  "GiftsAndFavours",
  "Transport",
  "Accommodation",
  "Priest",
  "Choreography",
  "FullPlanning",
] as const;

export const VENDOR_STATUSES = ["Active", "OnWatch", "Blacklisted", "Pending"] as const;

export const VENDOR_RATE_BASES = [
  "PerEvent",
  "PerDay",
  "PerHour",
  "PerPlate",
  "PerPiece",
  "PerSqft",
  "Commission",
] as const;

export const PO_STATUSES = [
  "Draft",
  "Sent",
  "Confirmed",
  "Delivered",
  "Closed",
  "Cancelled",
] as const;

export const CREW_ROLES = [
  "EventManager",
  "Coordinator",
  "Supervisor",
  "Decorator",
  "Florist",
  "Carpenter",
  "Electrician",
  "LightTechnician",
  "SoundTechnician",
  "Bearer",
  "Helper",
  "Loader",
  "Driver",
  "Security",
  "Housekeeping",
  "Usher",
  "Photographer",
  "Videographer",
  "Anchor",
  "Chef",
] as const;

export const CREW_ENGAGEMENT_TYPES = ["Employee", "Freelancer", "Contractor"] as const;
export const CREW_STATUSES = ["Active", "Inactive", "Blacklisted"] as const;

export const CREW_ASSIGNMENT_STATUSES = [
  "Planned",
  "Confirmed",
  "Completed",
  "NoShow",
  "Cancelled",
] as const;

export const VEHICLE_TYPES = [
  "Tempo",
  "Truck",
  "Container",
  "Pickup",
  "Van",
  "Car",
  "Bus",
  "Tractor",
] as const;

export const TRIP_STATUSES = [
  "Planned",
  "Loading",
  "InTransit",
  "Delivered",
  "Returning",
  "Completed",
  "Cancelled",
] as const;

/** Enum values become sentences a person would say out loud. */
export const LABELS: Record<string, string> = {
  SoundAndLight: "Sound & light",
  GiftsAndFavours: "Gifts & favours",
  FullPlanning: "Full planning",
  OnWatch: "On watch",
  PerEvent: "Per event",
  PerDay: "Per day",
  PerHour: "Per hour",
  PerPlate: "Per plate",
  PerPiece: "Per piece",
  PerSqft: "Per sq ft",
  Delivered: "Delivered",
  EventManager: "Event manager",
  LightTechnician: "Light technician",
  SoundTechnician: "Sound technician",
  NoShow: "No-show",
  InTransit: "In transit",
  PartiallyReturned: "Part returned",
};

export function humaniseLabel(value: string | null | undefined) {
  if (!value) return "—";
  if (LABELS[value]) return LABELS[value];
  // "ChannelPartner" reads as "Channel partner".
  const spaced = value.replace(/([a-z])([A-Z])/g, "$1 $2");
  return spaced.charAt(0).toUpperCase() + spaced.slice(1).toLowerCase();
}

/* ---------------- vendor shapes ---------------- */

export interface VendorRate {
  id: number;
  vendorId: number;
  service: string;
  name: string;
  basis: string;
  rate: number;
  sellRate: number | null;
  marginPerUnit: number | null;
  minimumQuantity: number | null;
  notes: string | null;
  isActive: boolean;
}

export interface VendorDocument {
  id: number;
  documentType: string;
  fileName: string;
  url: string | null;
  issueDate: string | null;
  expiryDate: string | null;
  isExpired: boolean;
  notes: string | null;
}

export interface Vendor {
  id: number;
  name: string;
  code: string;
  status: string;
  services: string[];
  contactPerson: string | null;
  phone: string | null;
  altPhone: string | null;
  email: string | null;
  address: string | null;
  city: string | null;
  coverageAreas: string | null;
  website: string | null;
  gstNumber: string | null;
  panNumber: string | null;
  bankAccountName: string | null;
  bankAccountNumber: string | null;
  bankIfsc: string | null;
  paymentTermDays: number;
  advanceFraction: number | null;
  concurrentEventCapacity: number;
  rating: number | null;
  completedEvents: number;
  notes: string | null;
  ownerId: number | null;
  ownerName: string | null;
  rateCount: number;
  lowestRate: number | null;
  expiringDocuments: number;
  committedOnDates: number | null;
  isAvailable: boolean | null;
  rates: VendorRate[];
  documents: VendorDocument[];
  createdAt: string;
  updatedAt: string;
}

export interface PoLine {
  id: number;
  vendorRateId: number | null;
  description: string;
  basis: string;
  quantity: number;
  rate: number;
  sellRate: number | null;
  lineCost: number;
  lineSell: number;
  notes: string | null;
  sortOrder: number;
}

export interface VendorPayment {
  id: number;
  amount: number;
  paidOn: string;
  mode: string;
  reference: string | null;
  kind: string;
  tdsAmount: number | null;
  notes: string | null;
}

export interface PurchaseOrder {
  id: number;
  code: string;
  status: string;
  vendorId: number;
  vendorName: string;
  vendorPhone: string | null;
  service: string;
  leadId: number | null;
  bookingId: number | null;
  quotationId: number | null;
  projectId: number | null;
  eventName: string | null;
  eventType: string | null;
  clientName: string | null;
  venueName: string | null;
  venueAddress: string | null;
  guestCount: number | null;
  serviceDate: string;
  serviceEndDate: string;
  reportingTime: string | null;
  totalCost: number;
  totalSell: number;
  margin: number;
  amountPaid: number;
  amountDue: number;
  retentionAmount: number | null;
  rating: number | null;
  terms: string | null;
  notes: string | null;
  coordinatorId: number | null;
  coordinatorName: string | null;
  lineCount: number;
  sentAt: string | null;
  confirmedAt: string | null;
  deliveredAt: string | null;
  createdAt: string;
  lines: PoLine[];
  payments: VendorPayment[];
}

/* ---------------- crew shapes ---------------- */

export interface CrewMember {
  id: number;
  name: string;
  code: string;
  engagementType: string;
  status: string;
  employeeId: number | null;
  employeeName: string | null;
  supplierVendorId: number | null;
  supplierVendorName: string | null;
  primaryRole: string;
  secondaryRoles: string[];
  yearsExperience: number | null;
  phone: string | null;
  altPhone: string | null;
  email: string | null;
  address: string | null;
  city: string | null;
  photoUrl: string | null;
  willTravel: boolean;
  dayRate: number | null;
  overtimeHourlyRate: number | null;
  rating: number | null;
  eventsWorked: number;
  noShowCount: number;
  idProofType: string | null;
  idProofNumber: string | null;
  notes: string | null;
  ownerId: number | null;
  clashingAssignments: number | null;
  isAvailable: boolean | null;
  createdAt: string;
  updatedAt: string;
}

export interface CrewAssignment {
  id: number;
  crewMemberId: number;
  crewMemberName: string;
  crewMemberCode: string;
  crewMemberPhone: string | null;
  engagementType: string;
  status: string;
  role: string;
  leadId: number | null;
  bookingId: number | null;
  projectId: number | null;
  propIssueId: number | null;
  eventName: string | null;
  clientName: string | null;
  venueName: string | null;
  fromDate: string;
  toDate: string;
  reportingTime: string | null;
  closingTime: string | null;
  dayRate: number | null;
  overtimeHours: number;
  overtimeAmount: number | null;
  allowanceAmount: number | null;
  totalCost: number;
  isPaid: boolean;
  paidOn: string | null;
  rating: number | null;
  notes: string | null;
  createdAt: string;
}

export interface CrewRoleCoverage {
  role: string;
  onRoster: number;
  available: number;
  committed: number;
  averageDayRate: number | null;
}

/* ---------------- fleet shapes ---------------- */

export interface Vehicle {
  id: number;
  registrationNumber: string;
  name: string;
  vehicleType: string;
  status: string;
  supplierVendorId: number | null;
  supplierVendorName: string | null;
  payloadKg: number | null;
  capacityCubicFeet: number | null;
  passengerSeats: number | null;
  defaultDriverCrewId: number | null;
  defaultDriverName: string | null;
  dayRate: number | null;
  ratePerKm: number | null;
  insuranceExpiry: string | null;
  permitExpiry: string | null;
  pucExpiry: string | null;
  fitnessExpiry: string | null;
  hasLapsedPapers: boolean;
  lastServicedOn: string | null;
  odometerKm: number | null;
  storeId: number | null;
  storeName: string | null;
  notes: string | null;
  clashingTrips: number | null;
  isAvailable: boolean | null;
  createdAt: string;
}

export interface TripLoad {
  id: number;
  propIssueId: number;
  issueCode: string;
  eventName: string | null;
  venueName: string | null;
  pieces: number;
  weightKg: number | null;
  sortOrder: number;
}

export interface VehicleTrip {
  id: number;
  code: string;
  status: string;
  direction: string;
  vehicleId: number;
  vehicleRegistration: string;
  vehicleName: string;
  payloadKg: number | null;
  driverCrewId: number | null;
  driverName: string | null;
  driverPhone: string | null;
  fromDate: string;
  toDate: string;
  departureTime: string | null;
  fromLocation: string | null;
  toLocation: string | null;
  leadId: number | null;
  projectId: number | null;
  eventName: string | null;
  startOdometerKm: number | null;
  endOdometerKm: number | null;
  distanceKm: number | null;
  fuelCost: number | null;
  tollCost: number | null;
  otherCost: number | null;
  totalRunningCost: number;
  loadedWeightKg: number | null;
  plannedWeightKg: number | null;
  isOverloaded: boolean;
  notes: string | null;
  loads: TripLoad[];
  createdAt: string;
}

/* ---------------- kits, pull sheets, utilisation ---------------- */

export interface PropKitLine {
  id: number;
  propItemId: number;
  itemName: string;
  itemCode: string;
  categoryName: string;
  unit: string;
  primaryThumbnailUrl: string | null;
  quantity: number;
  isOptional: boolean;
  notes: string | null;
  sortOrder: number;
  goodQuantity: number;
  availableQuantity: number | null;
  isShort: boolean | null;
}

export interface PropKit {
  id: number;
  name: string;
  code: string;
  description: string | null;
  setupType: string | null;
  eventType: string | null;
  coverImageUrl: string | null;
  rentalRatePerDay: number | null;
  lineRateTotal: number;
  setupHours: number | null;
  crewRequired: number | null;
  isActive: boolean;
  lineCount: number;
  totalPieces: number;
  shortLines: number | null;
  canFulfil: boolean | null;
  lines: PropKitLine[];
  createdAt: string;
}

export interface PullSheetLine {
  propItemId: number;
  itemName: string;
  itemCode: string;
  unit: string;
  size: string | null;
  storageLocation: string | null;
  quantity: number;
  packingUnit: number | null;
  crates: number;
  weightKg: number | null;
  lineWeightKg: number | null;
  isFragile: boolean;
  notes: string | null;
}

export interface PullSheetGroup {
  heading: string;
  pieces: number;
  weightKg: number | null;
  lines: PullSheetLine[];
}

export interface PullSheet {
  propIssueId: number;
  issueCode: string;
  status: string;
  eventName: string | null;
  clientName: string | null;
  venueName: string | null;
  venueAddress: string | null;
  dispatchDate: string;
  eventDate: string | null;
  expectedReturnDate: string;
  vehicleNumber: string | null;
  driverName: string | null;
  siteInChargeName: string | null;
  totalLines: number;
  totalPieces: number;
  totalCrates: number;
  totalWeightKg: number | null;
  fragileLines: number;
  groups: PullSheetGroup[];
}

export interface UtilisationRow {
  propItemId: number;
  itemName: string;
  itemCode: string;
  categoryName: string;
  unit: string;
  primaryThumbnailUrl: string | null;
  goodQuantity: number;
  timesIssued: number;
  piecesIssued: number;
  piecesDamaged: number;
  piecesLost: number;
  daysOut: number;
  utilisationRate: number;
  estimatedRevenue: number;
  lossValue: number;
  lastIssuedOn: string | null;
}

export interface Utilisation {
  from: string;
  to: string;
  windowDays: number;
  itemsTracked: number;
  itemsNeverIssued: number;
  estimatedRevenue: number;
  lossValue: number;
  busiest: UtilisationRow[];
  idle: UtilisationRow[];
}

/* ---------------- the day sheet ---------------- */

export interface DaySheet {
  from: string;
  to: string;
  dispatches: {
    id: number;
    code: string;
    status: string;
    eventName: string | null;
    clientName: string | null;
    venueName: string | null;
    dispatchDate: string;
    expectedReturnDate: string;
    pieces: number;
  }[];
  crew: {
    id: number;
    name: string;
    phone: string | null;
    role: string;
    status: string;
    eventName: string | null;
    venueName: string | null;
    fromDate: string;
    toDate: string;
    reportingTime: string | null;
  }[];
  orders: {
    id: number;
    code: string;
    vendorName: string;
    phone: string | null;
    service: string;
    status: string;
    eventName: string | null;
    venueName: string | null;
    serviceDate: string;
    reportingTime: string | null;
    totalCost: number;
  }[];
  trips: {
    id: number;
    code: string;
    vehicle: string;
    driverName: string | null;
    status: string;
    direction: string;
    eventName: string | null;
    toLocation: string | null;
    fromDate: string;
    departureTime: string | null;
    loads: number;
  }[];
  summary: {
    gatePasses: number;
    pieces: number;
    crewBooked: number;
    vendorsDue: number;
    vendorCost: number;
    tripsRunning: number;
  };
}

export interface EventResourceSheet {
  leadId: number;
  clientName: string | null;
  eventType: string | null;
  gatePasses: PropIssue[];
  crew: CrewAssignment[];
  purchaseOrders: PurchaseOrder[];
  trips: VehicleTrip[];
  totalPieces: number;
  totalCrew: number;
  vendorCost: number;
  crewCost: number;
  transportCost: number;
  totalCommitted: number;
}

/* ------------------------------------------------------------------ *
 * Calls
 * ------------------------------------------------------------------ */

export const vendorsApi = {
  fields: () => get<FilterField[]>("/api/vendors/fields"),
  query: (request: QueryRequest) =>
    post<PagedResult<Vendor>>("/api/vendors/query", request),
  get: (id: number) => get<Vendor>(`/api/vendors/${id}`),
  create: (body: Record<string, unknown>) => post<Vendor>("/api/vendors", body),
  update: (id: number, body: Record<string, unknown>) =>
    put<Vendor>(`/api/vendors/${id}`, body),
  remove: (id: number) => del<void>(`/api/vendors/${id}`),

  addRate: (id: number, body: Record<string, unknown>) =>
    post<Vendor>(`/api/vendors/${id}/rates`, body),
  updateRate: (rateId: number, body: Record<string, unknown>) =>
    put<Vendor>(`/api/vendors/rates/${rateId}`, body),
  deleteRate: (rateId: number) => del<Vendor>(`/api/vendors/rates/${rateId}`),

  addDocument: (id: number, body: Record<string, unknown>) =>
    post<Vendor>(`/api/vendors/${id}/documents`, body),
  deleteDocument: (documentId: number) =>
    del<void>(`/api/vendors/documents/${documentId}`),
  expiringDocuments: (withinDays = 60) =>
    get<
      {
        id: number;
        vendorId: number;
        vendorName: string;
        phone: string | null;
        documentType: string;
        fileName: string;
        expiryDate: string;
        isExpired: boolean;
      }[]
    >(`/api/vendors/documents/expiring?withinDays=${withinDays}`),

  availability: (body: {
    from: string;
    to: string;
    service?: string;
    city?: string;
    excludePurchaseOrderId?: number;
  }) => post<Vendor[]>("/api/vendors/availability", body),

  orders: (
    params: {
      status?: string;
      vendorId?: number;
      leadId?: number;
      unpaidOnly?: boolean;
      take?: number;
    } = {}
  ) => {
    const search = new URLSearchParams();
    if (params.status) search.set("status", params.status);
    if (params.vendorId) search.set("vendorId", String(params.vendorId));
    if (params.leadId) search.set("leadId", String(params.leadId));
    if (params.unpaidOnly) search.set("unpaidOnly", "true");
    if (params.take) search.set("take", String(params.take));
    const q = search.toString();
    return get<PurchaseOrder[]>(`/api/vendors/orders${q ? `?${q}` : ""}`);
  },

  order: (id: number) => get<PurchaseOrder>(`/api/vendors/orders/${id}`),
  createOrder: (body: Record<string, unknown>) =>
    post<PurchaseOrder>("/api/vendors/orders", body),
  updateOrder: (id: number, body: Record<string, unknown>) =>
    put<PurchaseOrder>(`/api/vendors/orders/${id}`, body),
  addOrderLine: (id: number, body: Record<string, unknown>) =>
    post<PurchaseOrder>(`/api/vendors/orders/${id}/lines`, body),
  removeOrderLine: (id: number, lineId: number) =>
    del<PurchaseOrder>(`/api/vendors/orders/${id}/lines/${lineId}`),
  setOrderStatus: (id: number, status: string, rating?: number) =>
    post<PurchaseOrder>(
      `/api/vendors/orders/${id}/status?status=${status}${
        rating ? `&rating=${rating}` : ""
      }`
    ),
  addPayment: (id: number, body: Record<string, unknown>) =>
    post<PurchaseOrder>(`/api/vendors/orders/${id}/payments`, body),
};

export const crewApi = {
  fields: () => get<FilterField[]>("/api/crew/fields"),
  query: (request: QueryRequest) =>
    post<PagedResult<CrewMember>>("/api/crew/query", request),
  get: (id: number) => get<CrewMember>(`/api/crew/${id}`),
  create: (body: Record<string, unknown>) => post<CrewMember>("/api/crew", body),
  update: (id: number, body: Record<string, unknown>) =>
    put<CrewMember>(`/api/crew/${id}`, body),
  remove: (id: number) => del<void>(`/api/crew/${id}`),

  availability: (body: {
    from: string;
    to: string;
    role?: string;
    engagementType?: string;
    city?: string;
    availableOnly?: boolean;
    excludeAssignmentId?: number;
  }) => post<CrewMember[]>("/api/crew/availability", body),

  coverage: (body: { from: string; to: string }) =>
    post<CrewRoleCoverage[]>("/api/crew/coverage", body),

  assignments: (
    params: {
      status?: string;
      crewMemberId?: number;
      leadId?: number;
      from?: string;
      to?: string;
      unpaidOnly?: boolean;
      take?: number;
    } = {}
  ) => {
    const search = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== false) {
        search.set(key, String(value));
      }
    });
    const q = search.toString();
    return get<CrewAssignment[]>(`/api/crew/assignments${q ? `?${q}` : ""}`);
  },

  assign: (body: Record<string, unknown>) =>
    post<CrewAssignment>("/api/crew/assignments", body),
  requisition: (body: Record<string, unknown>) =>
    post<CrewAssignment[]>("/api/crew/requisition", body),
  setAssignmentStatus: (
    id: number,
    status: string,
    extra: { rating?: number; overtimeHours?: number } = {}
  ) => {
    const search = new URLSearchParams({ status });
    if (extra.rating) search.set("rating", String(extra.rating));
    if (extra.overtimeHours) search.set("overtimeHours", String(extra.overtimeHours));
    return post<CrewAssignment>(`/api/crew/assignments/${id}/status?${search}`);
  },
  markPaid: (id: number, paidOn?: string) =>
    post<CrewAssignment>(
      `/api/crew/assignments/${id}/pay${paidOn ? `?paidOn=${paidOn}` : ""}`
    ),
  removeAssignment: (id: number) => del<void>(`/api/crew/assignments/${id}`),
};

export const fleetApi = {
  vehicles: (params: { status?: string; vehicleType?: string } = {}) => {
    const search = new URLSearchParams();
    if (params.status) search.set("status", params.status);
    if (params.vehicleType) search.set("vehicleType", params.vehicleType);
    const q = search.toString();
    return get<Vehicle[]>(`/api/fleet/vehicles${q ? `?${q}` : ""}`);
  },
  vehicle: (id: number) => get<Vehicle>(`/api/fleet/vehicles/${id}`),
  createVehicle: (body: Record<string, unknown>) =>
    post<Vehicle>("/api/fleet/vehicles", body),
  updateVehicle: (id: number, body: Record<string, unknown>) =>
    put<Vehicle>(`/api/fleet/vehicles/${id}`, body),
  removeVehicle: (id: number) => del<void>(`/api/fleet/vehicles/${id}`),

  availability: (body: {
    from: string;
    to: string;
    vehicleType?: string;
    minimumPayloadKg?: number;
    availableOnly?: boolean;
    excludeTripId?: number;
  }) => post<Vehicle[]>("/api/fleet/availability", body),

  compliance: (withinDays = 45) =>
    get<
      {
        id: number;
        name: string;
        registrationNumber: string;
        insuranceExpiry: string | null;
        permitExpiry: string | null;
        pucExpiry: string | null;
        fitnessExpiry: string | null;
        isLapsed: boolean;
      }[]
    >(`/api/fleet/compliance?withinDays=${withinDays}`),

  trips: (
    params: {
      status?: string;
      vehicleId?: number;
      leadId?: number;
      from?: string;
      to?: string;
      take?: number;
    } = {}
  ) => {
    const search = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null) search.set(key, String(value));
    });
    const q = search.toString();
    return get<VehicleTrip[]>(`/api/fleet/trips${q ? `?${q}` : ""}`);
  },

  trip: (id: number) => get<VehicleTrip>(`/api/fleet/trips/${id}`),
  createTrip: (body: Record<string, unknown>) =>
    post<VehicleTrip>("/api/fleet/trips", body),
  addLoad: (id: number, issueId: number) =>
    post<VehicleTrip>(`/api/fleet/trips/${id}/loads/${issueId}`),
  removeLoad: (id: number, issueId: number) =>
    del<VehicleTrip>(`/api/fleet/trips/${id}/loads/${issueId}`),
  setTripStatus: (id: number, status: string) =>
    post<VehicleTrip>(`/api/fleet/trips/${id}/status?status=${status}`),
  closeTrip: (id: number, body: Record<string, unknown>) =>
    post<VehicleTrip>(`/api/fleet/trips/${id}/close`, body),
};

export const kitsApi = {
  list: (params: { from?: string; to?: string; activeOnly?: boolean } = {}) => {
    const search = new URLSearchParams();
    if (params.from) search.set("from", params.from);
    if (params.to) search.set("to", params.to);
    if (params.activeOnly === false) search.set("activeOnly", "false");
    const q = search.toString();
    return get<PropKit[]>(`/api/props/kits${q ? `?${q}` : ""}`);
  },
  get: (id: number, from?: string, to?: string) => {
    const search = new URLSearchParams();
    if (from) search.set("from", from);
    if (to) search.set("to", to);
    const q = search.toString();
    return get<PropKit>(`/api/props/kits/${id}${q ? `?${q}` : ""}`);
  },
  create: (body: Record<string, unknown>) => post<PropKit>("/api/props/kits", body),
  update: (id: number, body: Record<string, unknown>) =>
    put<PropKit>(`/api/props/kits/${id}`, body),
  remove: (id: number) => del<void>(`/api/props/kits/${id}`),
  addLine: (id: number, body: Record<string, unknown>) =>
    post<PropKit>(`/api/props/kits/${id}/lines`, body),
  removeLine: (id: number, lineId: number) =>
    del<PropKit>(`/api/props/kits/${id}/lines/${lineId}`),

  applyToIssue: (issueId: number, body: Record<string, unknown>) =>
    post<PropIssue>(`/api/props/issues/${issueId}/apply-kit`, body),

  pullSheet: (issueId: number) =>
    get<PullSheet>(`/api/props/issues/${issueId}/pull-sheet`),

  utilisation: (
    params: { from?: string; to?: string; categoryId?: number; take?: number } = {}
  ) => {
    const search = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null) search.set(key, String(value));
    });
    const q = search.toString();
    return get<Utilisation>(`/api/props/utilisation${q ? `?${q}` : ""}`);
  },
};

/* ------------------------------------------------------------------ *
 * The resource plan behind a proposal
 * ------------------------------------------------------------------ */

export const QUOTATION_RESOURCE_KINDS = ["Prop", "PropKit", "Crew", "Vendor"] as const;

export const QUOTATION_RESOURCE_STATES = [
  "Planned",
  "Held",
  "Converted",
  "Released",
] as const;

/** The bandings a proposal groups its lines under, in print order. */
export const CHARGE_GROUPS = [
  "Unit Charge",
  "Catering",
  "Décor & Styling",
  "Photography & Film",
  "Entertainment",
  "Logistics & Staffing",
  "Statutory & Permits",
  "Deposits",
] as const;

/** "Unit Charge" is the stored name; everyone calls it the venue. */
export function chargeGroupLabel(group: string) {
  return group === "Unit Charge" ? "Venue" : group;
}

export interface QuotationResource {
  id: number;
  quotationId: number;
  resourceKind: string;
  state: string;
  propItemId: number | null;
  propKitId: number | null;
  crewMemberId: number | null;
  crewMemberName: string | null;
  crewRole: string | null;
  vendorId: number | null;
  vendorName: string | null;
  vendorRateId: number | null;
  description: string;
  chargeGroup: string;
  isInternalOnly: boolean;
  sortOrder: number;
  notes: string | null;
  quantity: number;
  quantityUnit: string | null;
  days: number;
  unitCost: number;
  unitSell: number;
  taxRate: number;
  lineCost: number;
  lineSell: number;
  lineMargin: number;
  marginFraction: number | null;
  fromDate: string | null;
  toDate: string | null;
  primaryThumbnailUrl: string | null;
  availableQuantity: number | null;
  isShort: boolean | null;
  propReservationId: number | null;
  propIssueId: number | null;
  crewAssignmentId: number | null;
  vendorPurchaseOrderId: number | null;
  createdAt: string;
}

export interface MarginByGroup {
  group: string;
  label: string;
  sell: number;
  cost: number;
  margin: number;
  marginFraction: number | null;
  lineCount: number;
}

export interface QuotationMargin {
  quotationId: number;
  quoteNumber: string;
  status: string;
  customerName: string;
  eventType: string | null;
  eventDate: string | null;
  guestCount: number;
  revenue: number;
  revenueExTax: number;
  plannedCost: number;
  plannedMargin: number;
  plannedMarginFraction: number | null;
  committedCost: number;
  committedMargin: number;
  committedMarginFraction: number | null;
  /** Committed minus planned. Positive means delivery is running over. */
  costVariance: number;
  resourceCount: number;
  heldLines: number;
  convertedLines: number;
  shortLines: number;
  byGroup: MarginByGroup[];
  resources: QuotationResource[];
}

export interface HoldResult {
  held: number;
  skipped: number;
  warnings: string[];
  plan: QuotationMargin;
}

export interface ConversionResult {
  quotationId: number;
  propIssueId: number | null;
  propIssueCode: string | null;
  propLines: number;
  propPieces: number;
  crewAssignmentIds: number[];
  crewBooked: number;
  purchaseOrderIds: number[];
  purchaseOrderCodes: string[];
  vendorLines: number;
  committedCost: number;
  warnings: string[];
}

export interface QuotationResourceOptions {
  from: string;
  to: string;
  kits: PropKit[];
  props: {
    propItemId: number;
    itemName: string;
    itemCode: string;
    categoryName: string;
    unit: string;
    primaryThumbnailUrl: string | null;
    goodQuantity: number;
    reservedQuantity: number;
    availableQuantity: number;
  }[];
  crewCoverage: CrewRoleCoverage[];
  vendors: Vendor[];
}

export const quotationResourcesApi = {
  plan: (quotationId: number) =>
    get<QuotationMargin>(`/api/quotations/${quotationId}/resources`),

  margin: (quotationId: number) =>
    get<QuotationMargin>(`/api/quotations/${quotationId}/margin`),

  options: (quotationId: number, params: { search?: string; categoryId?: number } = {}) => {
    const search = new URLSearchParams();
    if (params.search) search.set("search", params.search);
    if (params.categoryId) search.set("categoryId", String(params.categoryId));
    const q = search.toString();
    return get<QuotationResourceOptions>(
      `/api/quotations/${quotationId}/resource-options${q ? `?${q}` : ""}`
    );
  },

  add: (quotationId: number, body: Record<string, unknown>) =>
    post<QuotationMargin>(`/api/quotations/${quotationId}/resources`, body),

  addKit: (quotationId: number, body: Record<string, unknown>) =>
    post<QuotationMargin>(`/api/quotations/${quotationId}/resources/kit`, body),

  addCrew: (quotationId: number, body: Record<string, unknown>) =>
    post<QuotationMargin>(`/api/quotations/${quotationId}/resources/crew`, body),

  update: (quotationId: number, resourceId: number, body: Record<string, unknown>) =>
    put<QuotationMargin>(`/api/quotations/${quotationId}/resources/${resourceId}`, body),

  remove: (quotationId: number, resourceId: number) =>
    del<QuotationMargin>(`/api/quotations/${quotationId}/resources/${resourceId}`),

  hold: (quotationId: number, expiresAt?: string) =>
    post<HoldResult>(`/api/quotations/${quotationId}/resources/hold`, {
      expiresAt: expiresAt ?? null,
    }),

  release: (quotationId: number) =>
    post<QuotationMargin>(`/api/quotations/${quotationId}/resources/release`),

  convert: (quotationId: number, body: Record<string, unknown> = {}) =>
    post<ConversionResult>(`/api/quotations/${quotationId}/resources/convert`, body),
};

export const eventResourcesApi = {
  forLead: (leadId: number) =>
    get<EventResourceSheet>(`/api/event-resources/lead/${leadId}`),
  daySheet: (from?: string, to?: string) => {
    const search = new URLSearchParams();
    if (from) search.set("from", from);
    if (to) search.set("to", to);
    const q = search.toString();
    return get<DaySheet>(`/api/event-resources/day-sheet${q ? `?${q}` : ""}`);
  },
};
