import { TOKEN_STORAGE_KEY } from "@/lib/session";

/**
 * Exported because a few things the API serves are not fetched through
 * {@link apiRequest} at all — prop photographs come back as relative paths and
 * are loaded by the browser's own `<img>`, which needs the origin spelled out.
 */
export const API_URL = process.env.NEXT_PUBLIC_API_URL ?? "http://localhost:5080";

export class ApiError extends Error {
  status: number;
  constructor(message: string, status: number) {
    super(message);
    this.status = status;
  }
}

/**
 * A 401 on any authenticated call now means the session was withdrawn — signed
 * out from another device, deactivated by an administrator, or the tenant
 * suspended — not merely that the token expired. Holding a dead token in local
 * storage would leave the app showing a shell it can no longer fill, so the
 * client folds the session here and sends the person back to sign in.
 *
 * The auth endpoints are exempt: a failed sign-in returns 401 too, and clearing
 * a session that does not exist yet would reload the page under the user.
 */
function endLocalSession(path: string) {
  if (typeof window === "undefined") return;
  if (path.startsWith("/api/auth/login") || path.startsWith("/api/auth/verify-otp")) return;
  if (window.location.pathname.startsWith("/login")) return;

  localStorage.removeItem(TOKEN_STORAGE_KEY);
  window.location.assign("/login");
}

async function request<T>(
  path: string,
  init?: RequestInit & { auth?: boolean }
): Promise<T> {
  const headers = new Headers(init?.headers);
  if (init?.body) headers.set("Content-Type", "application/json");

  if (init?.auth) {
    const token =
      typeof window !== "undefined"
        ? localStorage.getItem(TOKEN_STORAGE_KEY)
        : null;
    if (token) headers.set("Authorization", `Bearer ${token}`);
  }

  const res = await fetch(`${API_URL}${path}`, { ...init, headers });

  if (!res.ok) {
    const data = await res.json().catch(() => null);

    if (res.status === 401) endLocalSession(path);

    throw new ApiError(
      data?.message ?? "Something went wrong. Please try again.",
      res.status
    );
  }

  if (res.status === 204) return undefined as T;
  return res.json() as Promise<T>;
}

/**
 * A binary download over the same authenticated channel.
 *
 * Separate from `apiRequest`, which always parses the body as JSON — a PDF put
 * through that would come back mangled. Base URL, bearer token and error shape
 * still come from one place.
 */
export async function apiBlob(path: string): Promise<Blob> {
  const headers = new Headers();

  const token =
    typeof window !== "undefined" ? localStorage.getItem(TOKEN_STORAGE_KEY) : null;
  if (token) headers.set("Authorization", `Bearer ${token}`);

  const res = await fetch(`${API_URL}${path}`, { method: "GET", headers });

  if (!res.ok) {
    const data = await res.json().catch(() => null);
    throw new ApiError(
      data?.message ?? "Could not download this file.",
      res.status
    );
  }

  return res.blob();
}

/**
 * The raw authenticated fetch, exported so the CRM resource clients in
 * `crm-api.ts` share one place for base URL, bearer token and error shape.
 */
export function apiRequest<T>(
  path: string,
  init?: RequestInit & { auth?: boolean }
): Promise<T> {
  return request<T>(path, init);
}

function post<T>(path: string, body: unknown, opts?: { auth?: boolean }) {
  return request<T>(path, {
    method: "POST",
    body: JSON.stringify(body),
    auth: opts?.auth,
  });
}

function put<T>(path: string, body: unknown) {
  return request<T>(path, {
    method: "PUT",
    body: JSON.stringify(body),
    auth: true,
  });
}

function get<T>(path: string) {
  return request<T>(path, { method: "GET", auth: true });
}

function del<T>(path: string) {
  return request<T>(path, { method: "DELETE", auth: true });
}

export interface LoginResponse {
  pendingToken: string;
  maskedContact: string;
  /** False when no mail provider is connected and the code was not actually sent. */
  delivered: boolean;
  /**
   * Present only when the server has the sign-in code switched off, which it
   * permits in development alone. When it is here the password was enough and
   * the session is already open, so the code screen must be skipped rather than
   * shown with nothing to check.
   */
  session?: AuthResponse | null;
}

export interface UserProfile {
  id: number;
  name: string;
  email: string;
  role: string;
  companyId: number;
  companyName: string;
  branchIds: number[];
}

/** One tenant this session may open, as offered by the company picker. */
export interface CompanyOption {
  id: number;
  name: string;
  slug: string;
  planTier: string;
  status: string;
  branchCount: number;
  userCount: number;
  /** The company the signed-in user actually belongs to. */
  isHome: boolean;
}

export interface AuthResponse {
  accessToken: string;
  expiresAt: string;
  user: UserProfile;
  /** True for a platform admin, who must choose a tenant before the CRM loads. */
  requiresCompanySelection: boolean;
  companies: CompanyOption[];
  /** True while the account still carries the password it was created with. */
  mustChangePassword: boolean;
}

export function login(email: string, password: string) {
  return post<LoginResponse>("/api/auth/login", { email, password });
}

export function verifyOtp(pendingToken: string, code: string) {
  return post<AuthResponse>("/api/auth/verify-otp", { pendingToken, code });
}

/**
 * Replaces the signed-in account's password.
 *
 * The current password is sent as well as the new one — a token alone is not
 * enough, so a session lifted from an unlocked machine cannot lock the owner out.
 */
export interface DeviceSession {
  id: number;
  device: string;
  ipAddress: string | null;
  issuedAt: string;
  lastSeenAt: string;
  expiresAt: string;
  /** The session making this request — the one not to sign out by accident. */
  isCurrent: boolean;
}

/** Every device this account is signed in on. */
export function getSessions() {
  return get<DeviceSession[]>("/api/auth/sessions");
}

export function revokeSession(id: number) {
  return post<void>(`/api/auth/sessions/${id}/revoke`, {}, { auth: true });
}

export function revokeOtherSessions() {
  return post<{ ended: number }>("/api/auth/sessions/revoke-others", {}, { auth: true });
}

/**
 * Ends this session server-side.
 *
 * The token is a JWT and stays cryptographically valid until it expires, so
 * clearing local storage alone would leave a working credential behind on a
 * shared machine. This is what actually withdraws it.
 */
export function endSession() {
  return post<void>("/api/auth/sign-out", {}, { auth: true });
}

export function changePassword(currentPassword: string, newPassword: string) {
  return post<{ message: string }>(
    "/api/auth/change-password",
    { currentPassword, newPassword },
    { auth: true }
  );
}

export function getSelectableCompanies() {
  return get<CompanyOption[]>("/api/auth/companies");
}

/**
 * Swaps the tenant this session is working in.
 *
 * The API re-issues the access token against the chosen company, and every
 * server-side query filter reads the tenant off that token — so the client's
 * only job is to store what comes back.
 */
export function selectCompany(companyId: number) {
  return post<AuthResponse>("/api/auth/select-company", { companyId }, { auth: true });
}

export const PLAN_TIERS = ["Starter", "Growth", "Professional", "Enterprise"] as const;

/* ------------------------------------------------------------------ *
 * Subscription
 * ------------------------------------------------------------------ */

/** The value a limit takes when the plan does not cap it. */
export const UNLIMITED = -1;

export interface PlanLimit {
  key: string;
  label: string;
  allowed: number;
  used: number;
  /** True when a contract moved this ceiling off the published plan. */
  overridden: boolean;
}

export interface Plan {
  key: string;
  name: string;
  tagline: string;
  seats: number;
  leads: number;
  branches: number;
  modules: string[];
  apiAccess: boolean;
  webhooks: boolean;
  customFields: boolean;
  sso: boolean;
  pricePerSeat: number;
  isCurrent: boolean;
}

export interface Subscription {
  companyId: number;
  companyName: string;
  planKey: string;
  planName: string;
  status: string;
  trialEndsAt: string | null;
  trialDaysLeft: number | null;
  renewsAt: string | null;
  entitlementNote: string | null;
  limits: PlanLimit[];
  modules: string[];
  apiAccess: boolean;
  webhooks: boolean;
  customFields: boolean;
  sso: boolean;
  /** Seats in use × the plan's per-seat price, in rupees. */
  monthlyCost: number;
  availablePlans: Plan[];
}

export interface UsagePoint {
  day: string;
  activeUsers: number;
  leads: number;
  leadsCreated: number;
  signIns: number;
  quotations: number;
}

export interface UpdateSubscriptionInput {
  planTier?: string;
  status?: string;
  trialEndsAt?: string | null;
  renewsAt?: string | null;
  seatLimitOverride?: number | null;
  leadLimitOverride?: number | null;
  branchLimitOverride?: number | null;
  entitlementNote?: string | null;
}

export function getSubscription() {
  return get<Subscription>("/api/subscription");
}

export function getUsage(days = 30) {
  return get<UsagePoint[]>(`/api/subscription/usage?days=${days}`);
}

/** Platform operators only — a tenant that could lift its own ceilings has none. */
export function updateSubscription(companyId: number, input: UpdateSubscriptionInput) {
  return put<Subscription>(`/api/subscription/${companyId}`, input);
}
export const COMPANY_STATUSES = ["Active", "Trial", "Suspended"] as const;

export function isPlatformAdmin(user: Pick<UserProfile, "role">) {
  return user.role === "SuperAdmin";
}

export interface Company {
  id: number;
  name: string;
  slug: string;
  planTier: string;
  status: string;
  createdAt: string;
}

export interface Branch {
  id: number;
  name: string;
  city: string;
  address: string | null;
  contactPhone: string | null;
  userCount: number;
}

export interface UserListItem {
  id: number;
  name: string;
  email: string;
  /** The stored key, e.g. "SalesExecutive". */
  role: string;
  /** The catalogue's display name for that key. */
  roleName: string;
  scope: "Own" | "Team" | "Company" | "Platform";
  isActive: boolean;
  branchIds: number[];
  branchNames: string[];
  managerId: number | null;
  managerName: string | null;
  teamSize: number;
  /** True while the account still holds the password it was created with. */
  mustChangePassword: boolean;
  /** Null for an account that has never completed a sign-in. */
  lastLoginAt: string | null;
  createdAt: string;
  /** How many modules are turned on or off for this person specifically. */
  moduleOverrides: number;
}

/**
 * The roles the API will accept, in the order an org chart runs.
 *
 * These are the keys in the server's `RoleCatalog`, and they have to match it
 * exactly: an unrecognised key used to be accepted and quietly resolved to the
 * narrowest seat, so a "Branch Manager" created from this list was really a
 * sales executive. The server rejects unknown keys now, and the roles screen
 * reads the live catalogue — this list is the fallback for the invite dialog
 * when that fetch has not landed.
 */
export const ROLES = [
  { value: "CompanyAdmin", label: "Company Admin" },
  { value: "AGM", label: "AGM" },
  { value: "SalesManager", label: "Sales Manager" },
  { value: "SalesExecutive", label: "Sales Executive" },
  { value: "TeleSales", label: "Tele Sales" },
  { value: "PostSales", label: "Post Sales" },
  { value: "BackOffice", label: "Back Office" },
  { value: "MIS", label: "MIS" },
  { value: "HR", label: "HR" },
  { value: "Employee", label: "Employee" },
  { value: "ChannelPartner", label: "Channel Partner" },
] as const;

/** How wide a role's data scope reaches, for the badge on the user list. */
export const SCOPE_LABELS: Record<UserListItem["scope"], string> = {
  Own: "Own records",
  Team: "Own + team",
  Company: "Whole company",
  Platform: "Every company",
};

export interface CreateCompanyInput {
  name: string;
  slug?: string;
  planTier?: string;
  headOfficeCity: string;
  headOfficeName?: string;
  adminName: string;
  adminEmail: string;
  adminPassword: string;
}

export interface UpdateCompanyInput {
  name: string;
  planTier?: string;
  status?: string;
}

export function getCompanies() {
  return get<Company[]>("/api/companies");
}

/** Platform admins only — provisions the company, its head office and its admin. */
export function createCompany(input: CreateCompanyInput) {
  return post<Company>("/api/companies", input, { auth: true });
}

export function updateCompany(id: number, input: UpdateCompanyInput) {
  return put<Company>(`/api/companies/${id}`, input);
}

export function getBranches() {
  return get<Branch[]>("/api/branches");
}

export interface BranchInput {
  name: string;
  city: string;
  address?: string;
  contactPhone?: string;
}

export function createBranch(input: BranchInput) {
  return post<Branch>("/api/branches", input, { auth: true });
}

export function updateBranch(id: number, input: BranchInput) {
  return put<Branch>(`/api/branches/${id}`, input);
}

export function getUsers() {
  return get<UserListItem[]>("/api/users");
}

export interface CreateUserInput {
  name: string;
  email: string;
  password: string;
  role: string;
  branchIds: number[];
  managerId?: number | null;
  /** Defaults to true server-side — an admin-typed password is a shared one. */
  mustChangePassword?: boolean;
}

export function createUser(input: CreateUserInput) {
  return post<UserListItem>("/api/users", input, { auth: true });
}

export interface UpdateUserInput {
  role: string;
  isActive: boolean;
  branchIds: number[];
  managerId?: number | null;
}

export function updateUser(id: number, input: UpdateUserInput) {
  return put<UserListItem>(`/api/users/${id}`, input);
}

/**
 * Sets a password on somebody else's account. The account is always left
 * blocked until its owner replaces what was set — the server does not offer a
 * way to skip that, and neither does this.
 */
export function resetUserPassword(id: number, newPassword: string) {
  return post<void>(`/api/users/${id}/reset-password`, { newPassword }, { auth: true });
}

/** Activate or deactivate several accounts; returns the rows as they now stand. */
export function setUsersActive(userIds: number[], isActive: boolean) {
  return post<UserListItem[]>("/api/users/bulk-status", { userIds, isActive }, { auth: true });
}

/* ------------------------------------------------------------------ *
 * Integrations
 * ------------------------------------------------------------------ */

export interface ApiKey {
  id: number;
  name: string;
  /** Enough of the key to recognise it, never enough to use it. */
  prefix: string;
  scopes: string[];
  createdByName: string | null;
  createdAt: string;
  expiresAt: string | null;
  lastUsedAt: string | null;
  lastUsedIp: string | null;
  callCount: number;
  revokedAt: string | null;
  isLive: boolean;
}

/** The one response that carries the secret — returned once, at creation. */
export interface MintedApiKey {
  key: ApiKey;
  secret: string;
}

export interface ApiScope {
  scope: string;
  label: string;
  description: string;
}

export interface WebhookEndpoint {
  id: number;
  name: string;
  url: string;
  events: string[];
  isActive: boolean;
  createdAt: string;
  consecutiveFailures: number;
  disabledAt: string | null;
  disabledReason: string | null;
  /** Present only on the create response. */
  secret: string | null;
}

export interface WebhookEventType {
  event: string;
  description: string;
}

export interface WebhookDelivery {
  id: number;
  endpointId: number;
  endpointName: string;
  event: string;
  status: string;
  attempts: number;
  createdAt: string;
  lastAttemptAt: string | null;
  nextAttemptAt: string | null;
  responseCode: number | null;
  error: string | null;
}

export interface SaveWebhookInput {
  name: string;
  url: string;
  events: string[];
  isActive: boolean;
}

export const integrationsApi = {
  scopes: () => get<ApiScope[]>("/api/admin/integrations/scopes"),
  keys: () => get<ApiKey[]>("/api/admin/integrations/keys"),
  createKey: (input: { name: string; scopes: string[]; expiresAt: string | null }) =>
    post<MintedApiKey>("/api/admin/integrations/keys", input, { auth: true }),
  revokeKey: (id: number) =>
    post<ApiKey>(`/api/admin/integrations/keys/${id}/revoke`, {}, { auth: true }),

  events: () => get<WebhookEventType[]>("/api/admin/integrations/events"),
  webhooks: () => get<WebhookEndpoint[]>("/api/admin/integrations/webhooks"),
  createWebhook: (input: SaveWebhookInput) =>
    post<WebhookEndpoint>("/api/admin/integrations/webhooks", input, { auth: true }),
  updateWebhook: (id: number, input: SaveWebhookInput) =>
    put<WebhookEndpoint>(`/api/admin/integrations/webhooks/${id}`, input),
  deleteWebhook: (id: number) =>
    del<void>(`/api/admin/integrations/webhooks/${id}`),

  deliveries: (endpointId?: number, status?: string) => {
    const query = new URLSearchParams();
    if (endpointId) query.set("endpointId", String(endpointId));
    if (status) query.set("status", status);
    return get<WebhookDelivery[]>(`/api/admin/integrations/deliveries?${query}`);
  },
  replay: (id: number) =>
    post<WebhookDelivery>(`/api/admin/integrations/deliveries/${id}/replay`, {}, { auth: true }),
};

/* ------------------------------------------------------------------ *
 * Connectors
 * ------------------------------------------------------------------ */

export const CONNECTOR_CATEGORIES = [
  "Property portals",
  "Advertising",
  "Telephony",
  "Messaging",
  "Web",
] as const;

export interface CredentialField {
  key: string;
  label: string;
  help: string;
  /** Write-only from here — the value never comes back. */
  secret: boolean;
  required: boolean;
}

export interface ConnectorDefinition {
  provider: string;
  name: string;
  category: string;
  tagline: string;
  description: string;
  direction: "Inbound" | "Outbound" | "Both";
  credentials: CredentialField[];
  /** True when the provider delivers by calling a URL we hand them. */
  hasInboundEndpoint: boolean;
  setupSteps: string[];
  /** False while the outbound half still needs a live provider account. */
  live: boolean;
  configuredCount: number;
}

export interface Connector {
  id: number;
  provider: string;
  providerName: string;
  category: string;
  name: string;
  isActive: boolean;
  status: "NotConfigured" | "Connected" | "Error";
  lastError: string | null;
  lastEventAt: string | null;
  eventCount: number;
  defaultBranchId: number | null;
  defaultBranchName: string | null;
  defaultOwnerId: number | null;
  defaultOwnerName: string | null;
  sourceLabel: string | null;
  /** The URL to paste into the provider's dashboard. */
  inboundUrl: string | null;
  /** Which credentials are set, by key. Never the values. */
  credentialsSet: string[];
  createdAt: string;
}

export interface SaveConnectorInput {
  provider: string;
  name: string;
  isActive: boolean;
  defaultBranchId?: number | null;
  defaultOwnerId?: number | null;
  sourceLabel?: string | null;
  /** Only what changed. An absent key keeps whatever is stored. */
  credentials?: Record<string, string>;
}

export interface ConnectorEvent {
  id: number;
  kind: string;
  outcome: "Created" | "Duplicate" | "Rejected" | "Failed";
  detail: string | null;
  leadId: number | null;
  ipAddress: string | null;
  at: string;
  /** The body as it arrived. */
  payload: string;
}

export interface ConnectorTestResult {
  sample: unknown;
  mapped: Record<string, unknown>;
}

export const connectorsApi = {
  catalogue: () => get<ConnectorDefinition[]>("/api/admin/connectors/catalogue"),
  list: () => get<Connector[]>("/api/admin/connectors"),
  get: (id: number) => get<Connector>(`/api/admin/connectors/${id}`),

  create: (input: SaveConnectorInput) =>
    post<Connector>("/api/admin/connectors", input, { auth: true }),
  update: (id: number, input: SaveConnectorInput) =>
    put<Connector>(`/api/admin/connectors/${id}`, input),
  remove: (id: number) => del<void>(`/api/admin/connectors/${id}`),

  /** Issues a new inbound URL and retires the one already pasted somewhere. */
  rotate: (id: number) =>
    post<Connector>(`/api/admin/connectors/${id}/rotate`, {}, { auth: true }),

  events: (id: number, outcome?: string) =>
    get<ConnectorEvent[]>(
      `/api/admin/connectors/${id}/events${outcome ? `?outcome=${outcome}` : ""}`
    ),

  test: (id: number) =>
    post<ConnectorTestResult>(`/api/admin/connectors/${id}/test`, {}, { auth: true }),
};

/* ------------------------------------------------------------------ *
 * Audit trail
 * ------------------------------------------------------------------ */

export interface AuditChange {
  field: string;
  from: string | null;
  to: string | null;
}

export interface AuditEntry {
  id: number;
  userId: number | null;
  userName: string;
  entity: string;
  entityId: string;
  action: string;
  changes: AuditChange[];
  ipAddress: string | null;
  at: string;
}

export interface AuditPage {
  items: AuditEntry[];
  total: number;
  page: number;
  pageSize: number;
  /** The entity names present in this company's trail, for the filter. */
  entities: string[];
  actors: string[];
}

export interface AuditFilters {
  entity?: string;
  entityId?: string;
  actor?: string;
  action?: string;
  since?: string;
  page?: number;
  pageSize?: number;
}

export function getAuditTrail(filters: AuditFilters = {}) {
  const query = new URLSearchParams();

  for (const [key, value] of Object.entries(filters)) {
    if (value !== undefined && value !== null && value !== "") {
      query.set(key, String(value));
    }
  }

  return get<AuditPage>(`/api/admin/audit?${query}`);
}

/** Everything that ever happened to one record, oldest first. */
export function getRecordHistory(entity: string, entityId: string | number) {
  return get<AuditEntry[]>(`/api/admin/audit/${entity}/${entityId}`);
}

/* ------------------------------------------------------------------ *
 * Tenant customisation
 * ------------------------------------------------------------------ */

export const CUSTOM_FIELD_TYPES = [
  { value: "Text", label: "Text", hint: "A single line" },
  { value: "LongText", label: "Long text", hint: "A paragraph" },
  { value: "Number", label: "Number", hint: "Amounts, counts, percentages" },
  { value: "Date", label: "Date", hint: "A calendar day, with no time on it" },
  { value: "Select", label: "Choice", hint: "One of a list you define" },
  { value: "Checkbox", label: "Yes / no", hint: "A single tick" },
] as const;

export interface CustomField {
  id: number;
  object: string;
  /** The JSON property values are stored under. Fixed once the field exists. */
  key: string;
  label: string;
  helpText: string | null;
  type: string;
  options: string[];
  required: boolean;
  showInList: boolean;
  sortOrder: number;
  isActive: boolean;
  /** How many records already carry a value here — what a delete would strand. */
  usedBy: number;
}

export interface SaveCustomFieldInput {
  object: string;
  label: string;
  helpText?: string | null;
  type: string;
  options?: string[];
  required: boolean;
  showInList: boolean;
  sortOrder: number;
  isActive: boolean;
}

export interface PickListEntry {
  id: number;
  list: string;
  /** What gets stored on the record. Frozen; only the label is renameable. */
  value: string;
  label: string;
  sortOrder: number;
  isActive: boolean;
  /** Ships with the product — renameable and retireable, not deletable. */
  isSystem: boolean;
}

export interface PickList {
  key: string;
  label: string;
  description: string;
  values: PickListEntry[];
}

export interface Branding {
  companyName: string;
  brandColor: string | null;
  logoUrl: string | null;
}

export interface SetupStep {
  key: string;
  title: string;
  description: string;
  done: boolean;
  progress: number;
  target: number;
  href: string | null;
}

export interface Choice {
  value: string;
  label: string;
}

export interface FormField {
  key: string;
  label: string;
  helpText: string | null;
  type: string;
  options: string[];
  required: boolean;
  showInList: boolean;
  sortOrder: number;
}

/** The admin surface — changing what everybody in the company sees. */
export const customisationApi = {
  extendableObjects: () =>
    get<string[]>("/api/admin/customisation/fields/objects"),

  fields: (securedObject?: string) =>
    get<CustomField[]>(
      `/api/admin/customisation/fields${securedObject ? `?securedObject=${securedObject}` : ""}`
    ),
  createField: (input: SaveCustomFieldInput) =>
    post<CustomField>("/api/admin/customisation/fields", input, { auth: true }),
  updateField: (id: number, input: SaveCustomFieldInput) =>
    put<CustomField>(`/api/admin/customisation/fields/${id}`, input),
  deleteField: (id: number) =>
    del<void>(`/api/admin/customisation/fields/${id}`),

  pickLists: () => get<PickList[]>("/api/admin/customisation/pick-lists"),
  addValue: (list: string, label: string, sortOrder: number) =>
    post<PickListEntry>(
      `/api/admin/customisation/pick-lists/${list}`,
      { label, sortOrder },
      { auth: true }
    ),
  updateValue: (id: number, input: { label: string; sortOrder: number; isActive: boolean }) =>
    put<PickListEntry>(`/api/admin/customisation/pick-lists/values/${id}`, input),
  deleteValue: (id: number) =>
    del<void>(`/api/admin/customisation/pick-lists/values/${id}`),

  branding: () => get<Branding>("/api/admin/customisation/branding"),
  saveBranding: (input: { brandColor: string | null; logoUrl: string | null }) =>
    put<Branding>("/api/admin/customisation/branding", input),

  setup: () => get<SetupStep[]>("/api/admin/customisation/setup"),
};

/**
 * The read-only surface every seat uses.
 *
 * Separate from the admin one because a sales executive filling in a lead needs
 * the source list, and gating that behind "may administer users" would leave
 * them with an empty dropdown.
 */
export const workspaceApi = {
  pickList: (list: string) => get<Choice[]>(`/api/workspace/pick-lists/${list}`),
  pickLists: () => get<Record<string, Choice[]>>("/api/workspace/pick-lists"),
  fields: (securedObject: string) =>
    get<FormField[]>(`/api/workspace/fields/${securedObject}`),
  branding: () => get<Branding>("/api/workspace/branding"),
};

/** The named lists a company can edit, in the order the admin screen shows them. */
export const PICK_LISTS = [
  "lead-source",
  "loss-reason",
  "requirement-type",
  "funding-mode",
  "call-outcome",
  "visit-outcome",
  "service-category",
  "function-type",
] as const;

/**
 * The pipeline, in order.
 *
 * `ObmVisit` and `SiteVisit` keep their stored values — they are permission
 * keys and activity types across the CRM — and are labelled for what they are
 * on an events desk: the planner going to the client, and the client touring
 * the venue.
 */
export const LEAD_STAGES = [
  { value: "New", label: "New" },
  { value: "Contacted", label: "Responded" },
  { value: "Qualified", label: "Qualified" },
  { value: "ObmVisit", label: "Consult booked" },
  { value: "SiteVisit", label: "Venue walkthrough" },
  { value: "ProposalSent", label: "Proposal sent" },
  { value: "ContractSent", label: "Contract sent" },
  { value: "Booked", label: "Booked" },
  { value: "Lost", label: "Lost" },
  { value: "Nurture", label: "Nurture" },
] as const;

export const LEAD_SOURCES = [
  { value: "Website", label: "Website" },
  { value: "Referral", label: "Referral" },
  { value: "WalkIn", label: "Walk-in" },
  { value: "EventPortal", label: "Event portal" },
  { value: "SocialAds", label: "Social ads" },
  { value: "VendorPartner", label: "Vendor partner" },
  { value: "Exhibition", label: "Wedding expo" },
  { value: "RepeatClient", label: "Repeat client" },
  { value: "Other", label: "Other" },
] as const;

/** The occasion, grouped the way the enquiry form asks it. */
export const EVENT_TYPES = [
  { value: "Wedding", label: "Wedding", group: "Wedding" },
  { value: "Reception", label: "Reception", group: "Wedding" },
  { value: "Engagement", label: "Engagement", group: "Wedding" },
  { value: "Sangeet", label: "Sangeet", group: "Wedding" },
  { value: "Mehendi", label: "Mehendi", group: "Wedding" },
  { value: "Haldi", label: "Haldi", group: "Wedding" },
  { value: "DestinationWedding", label: "Destination wedding", group: "Wedding" },
  { value: "PreWeddingShoot", label: "Pre-wedding shoot", group: "Wedding" },
  { value: "Birthday", label: "Birthday", group: "Social" },
  { value: "Anniversary", label: "Anniversary", group: "Social" },
  { value: "BabyShower", label: "Baby shower", group: "Social" },
  { value: "Naming", label: "Naming ceremony", group: "Social" },
  { value: "HouseWarming", label: "House warming", group: "Social" },
  { value: "Festival", label: "Festival", group: "Social" },
  { value: "Conference", label: "Conference", group: "Corporate" },
  { value: "ProductLaunch", label: "Product launch", group: "Corporate" },
  { value: "AnnualDay", label: "Annual day", group: "Corporate" },
  { value: "Offsite", label: "Offsite", group: "Corporate" },
  { value: "Exhibition", label: "Exhibition", group: "Corporate" },
  { value: "AwardNight", label: "Award night", group: "Corporate" },
  { value: "Other", label: "Other", group: "Other" },
] as const;

export const EVENT_CATEGORIES = [
  { value: "Wedding", label: "Wedding" },
  { value: "Social", label: "Social" },
  { value: "Corporate", label: "Corporate" },
  { value: "Other", label: "Other" },
] as const;

/**
 * Which category an occasion belongs to.
 *
 * Mirrors `EventTypes.CategoryOf` on the server so the form can fill the
 * category in as the type is picked, rather than making the user answer the
 * same question twice.
 */
export function eventCategoryOf(type: string | null | undefined): string | null {
  if (!type) return null;
  return EVENT_TYPES.find((t) => t.value === type)?.group ?? "Other";
}

export const EVENT_SLOTS = [
  { value: "Morning", label: "Morning" },
  { value: "Afternoon", label: "Afternoon" },
  { value: "Evening", label: "Evening" },
  { value: "Night", label: "Night" },
  { value: "FullDay", label: "Full day" },
] as const;

/** What the planner is being asked to handle. A lead carries several. */
export const SERVICE_CATEGORIES = [
  { value: "Venue", label: "Venue" },
  { value: "Catering", label: "Catering" },
  { value: "Decor", label: "Décor" },
  { value: "Photography", label: "Photography" },
  { value: "Videography", label: "Videography" },
  { value: "Makeup", label: "Makeup" },
  { value: "Mehendi", label: "Mehendi artist" },
  { value: "Entertainment", label: "Entertainment" },
  { value: "SoundAndLight", label: "Sound & light" },
  { value: "Invitations", label: "Invitations" },
  { value: "GiftsAndFavours", label: "Gifts & favours" },
  { value: "Transport", label: "Transport" },
  { value: "Accommodation", label: "Accommodation" },
  { value: "Priest", label: "Priest" },
  { value: "Choreography", label: "Choreography" },
  { value: "FullPlanning", label: "Full planning" },
] as const;

/** The functions in a wedding run, in the order they are held. */
export const FUNCTION_TYPES = [
  { value: "Engagement", label: "Engagement" },
  { value: "Mehendi", label: "Mehendi" },
  { value: "Haldi", label: "Haldi" },
  { value: "Sangeet", label: "Sangeet" },
  { value: "Wedding", label: "Wedding" },
  { value: "Reception", label: "Reception" },
] as const;

export const MEAL_PREFERENCES = [
  { value: "Vegetarian", label: "Vegetarian" },
  { value: "NonVegetarian", label: "Non-vegetarian" },
  { value: "Jain", label: "Jain" },
  { value: "Vegan", label: "Vegan" },
  { value: "Mixed", label: "Mixed" },
] as const;

export const PAYMENT_PREFERENCES = [
  { value: "SelfFunded", label: "Self-funded" },
  { value: "Instalments", label: "Instalments" },
  { value: "CorporatePo", label: "Corporate PO" },
  { value: "Sponsored", label: "Sponsored" },
  { value: "Mixed", label: "Mixed" },
] as const;

export const PLANNING_PACKAGES = [
  { value: "FullPlanning", label: "Full planning" },
  { value: "Partial", label: "Partial" },
  { value: "DayOf", label: "Day-of" },
  { value: "VenueStyling", label: "Venue styling" },
  { value: "ConsultOnly", label: "Consult only" },
] as const;

export const INQUIRER_ROLES = [
  { value: "Couple", label: "Couple" },
  { value: "Parent", label: "Parent" },
  { value: "Relative", label: "Relative" },
  { value: "CorporateAdmin", label: "Corporate admin" },
  { value: "Other", label: "Other" },
] as const;

export const VENUE_STATUSES = [
  { value: "Searching", label: "Still searching" },
  { value: "Shortlisted", label: "Shortlisted" },
  { value: "Booked", label: "Venue booked" },
] as const;

export const EVENT_PORTALS = [
  { value: "WedMeGood", label: "WedMeGood" },
  { value: "WeddingWire", label: "WeddingWire" },
  { value: "ShaadiSaga", label: "ShaadiSaga" },
  { value: "VenueLook", label: "VenueLook" },
  { value: "TheKnot", label: "The Knot" },
  { value: "Other", label: "Other" },
] as const;

export const CEREMONY_STYLES = [
  { value: "Hindu", label: "Hindu" },
  { value: "Muslim", label: "Muslim" },
  { value: "Christian", label: "Christian" },
  { value: "Sikh", label: "Sikh" },
  { value: "Civil", label: "Civil" },
  { value: "Other", label: "Other" },
] as const;

export const LEAD_PRIORITIES = [
  { value: "Low", label: "Low" },
  { value: "Medium", label: "Medium" },
  { value: "High", label: "High" },
  { value: "Hot", label: "Hot" },
] as const;

export const SALUTATIONS = ["Mr.", "Ms.", "Mrs.", "Dr."] as const;

export const LEAD_ACTIVITY_TYPES = [
  { value: "Note", label: "Note" },
  { value: "Call", label: "Call" },
  { value: "Email", label: "Email" },
  { value: "WhatsApp", label: "WhatsApp" },
  { value: "SiteVisit", label: "Venue Visit" },
  { value: "Task", label: "Task" },
] as const;

export interface Lead {
  id: number;
  salutation: string | null;
  name: string;
  companyName: string | null;
  phone: string | null;
  phone2: string | null;
  email: string | null;
  address: string | null;
  city: string | null;
  country: string | null;
  source: string;
  stage: string;
  priority: string;
  branchId: number;
  branchName: string;
  ownerId: number | null;
  ownerName: string | null;
  notes: string | null;

  /* ---------------- the event ---------------- */

  partnerName: string | null;
  partnerPhone: string | null;
  partnerEmail: string | null;
  inquirerRole: string | null;
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
  budgetMin: number | null;
  budgetMax: number | null;
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
  /** Whole days until the event; negative once it has passed. */
  daysToEvent: number | null;

  createdAt: string;
  updatedAt: string;
}

export interface LeadInput {
  salutation?: string;
  name: string;
  companyName?: string;
  phone?: string;
  phone2?: string;
  email?: string;
  address?: string;
  city?: string;
  country?: string;
  source: string;
  priority?: string;
  branchId: number;
  ownerId?: number | null;
  notes?: string;

  partnerName?: string;
  partnerPhone?: string;
  partnerEmail?: string;
  inquirerRole?: string;
  eventType?: string;
  eventCategory?: string;
  /** ISO date. The field that makes an event enquiry actionable. */
  eventDate?: string;
  eventEndDate?: string;
  eventSlot?: string;
  isDateFlexible?: boolean;
  guestCount?: number;
  functions?: string;
  servicesNeeded?: string;
  mealPreference?: string;
  preferredLocality?: string;
  paymentMode?: string;
  budgetMin?: number;
  budgetMax?: number;
  interestedProjectId?: number | null;
  venueStatus?: string;
  planningPackage?: string;
  ceremonyGuestCount?: number;
  receptionGuestCount?: number;
  portalName?: string;
  ceremonyStyle?: string;
  consultAt?: string;
}

export function getLeads() {
  return get<Lead[]>("/api/leads");
}

export function getLead(id: number) {
  return get<Lead>(`/api/leads/${id}`);
}

export function createLead(input: LeadInput) {
  return post<Lead>("/api/leads", input, { auth: true });
}

export function updateLead(
  id: number,
  input: LeadInput & { stage: string }
) {
  return put<Lead>(`/api/leads/${id}`, input);
}

export interface LeadActivity {
  id: number;
  type: string;
  remarks: string | null;
  fromStage: string | null;
  toStage: string | null;
  actorName: string;
  createdAt: string;
}

export function getLeadActivities(leadId: number) {
  return get<LeadActivity[]>(`/api/leads/${leadId}/activities`);
}

export function createLeadActivity(
  leadId: number,
  input: { type: string; remarks?: string }
) {
  return post<LeadActivity>(`/api/leads/${leadId}/activities`, input, {
    auth: true,
  });
}

/* ------------------------------------------------------------------ *
 * Local intelligence — ML.NET scoring, duplicates, next-best-action
 * ------------------------------------------------------------------ */

export type ScoreBand = "Hot" | "Warm" | "Cool" | "Cold";

export interface ScoreSignal {
  label: string;
  detail: string;
  points: number;
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
  confidence: number;
}

export interface LeadInsights {
  leadId: number;
  score: number;
  band: ScoreBand;
  /** "MLNet" when the trained model produced the score, else "Heuristic". */
  engine: string;
  signals: ScoreSignal[];
  actions: RecommendedAction[];
  duplicates: DuplicateMatch[];
}

export interface ScoredLead {
  leadId: number;
  name: string;
  stage: string;
  score: number;
  band: ScoreBand;
}

export interface ModelStatus {
  engine: string;
  trainedAt: string | null;
  trainingRows: number;
  accuracy: number | null;
  areaUnderRocCurve: number | null;
  message: string | null;
}

export function getModelStatus() {
  return get<ModelStatus>("/api/intelligence/model");
}

export function trainModel() {
  return post<ModelStatus>("/api/intelligence/model/train", {}, { auth: true });
}

export function getLeadInsights(leadId: number) {
  return get<LeadInsights>(`/api/intelligence/leads/${leadId}`);
}

export function getLeadScores() {
  return get<ScoredLead[]>("/api/intelligence/leads/scores");
}

export function checkDuplicates(input: {
  name: string;
  phone?: string;
  phone2?: string;
  email?: string;
  city?: string;
  excludeLeadId?: number;
}) {
  return post<DuplicateMatch[]>("/api/intelligence/duplicates", input, {
    auth: true,
  });
}

/* ------------------------------------------------------------------ *
 * Admin Console — access model
 * ------------------------------------------------------------------ */

export interface RoleSummary {
  key: string;
  name: string;
  description: string;
  scope: "Own" | "Team" | "Company" | "Platform";
  scopeLabel: string;
  modules: string[];
  readOnly: boolean;
  wonBusinessOnly: boolean;
  peopleData: boolean;
  userCount: number;
  profileId: number | null;
}

export interface ObjectPermissionRow {
  object: string;
  actions: string[];
}

export interface ProfileDetail {
  id: number;
  name: string;
  description: string | null;
  roleKey: string | null;
  isSystem: boolean;
  modules: string[];
  objects: ObjectPermissionRow[];
}

export interface HierarchyNode {
  id: number;
  name: string;
  email: string;
  role: string;
  roleName: string;
  scope: string;
  isActive: boolean;
  managerId: number | null;
  /** Everyone beneath this person at any depth — what a team scope reaches. */
  teamSize: number;
  reports: HierarchyNode[];
}

export interface TeamMember {
  id: number;
  name: string;
  email: string;
  role: string;
  roleName: string;
  isActive: boolean;
  /** How far below the manager: 1 is a direct report, 0 for an unassigned person. */
  depth: number;
  /** How many people sit under them in turn. */
  teamSize: number;
  branches: string[];
}

export interface TeamSummary {
  managerId: number;
  managerName: string;
  managerEmail: string;
  managerRole: string;
  managerRoleName: string;
  scope: "Own" | "Team" | "Company" | "Platform";
  scopeLabel: string;
  managerIsActive: boolean;
  directReports: number;
  totalMembers: number;
  inactiveMembers: number;
  depth: number;
  /** False when the manager's role cannot actually reach their reports' records. */
  scopeCoversTeam: boolean;
  members: TeamMember[];
}

export interface TeamsOverview {
  teams: TeamSummary[];
  /** People who report to nobody and carry nobody. */
  unassigned: TeamMember[];
}

export interface UserModuleRow {
  module: string;
  /** What applies after the role and any override are folded together. */
  effective: boolean;
  fromRole: boolean;
  /** Null means no exception — the module follows the role. */
  override: boolean | null;
}

export interface FieldPermissionRow {
  object: string;
  field: string;
  label: string;
  /** Why this field is worth restricting — shown under the label. */
  why: string;
  /** Personal or commercially sensitive; these lead the list. */
  sensitive: boolean;
  canRead: boolean;
  canEdit: boolean;
}

export interface ObjectVisibilityRow {
  object: string;
  visibility: "Private" | "PublicRead" | "PublicReadWrite";
  grantAccessUsingHierarchy: boolean;
}

/** Every action a permission matrix cell can carry, in escalating order. */
export const OBJECT_ACTIONS = [
  { value: "View", label: "View", hint: "Open records they already reach" },
  { value: "Create", label: "Create", hint: "Add new records" },
  { value: "Edit", label: "Edit", hint: "Change records they own" },
  { value: "Delete", label: "Delete", hint: "Remove records they own" },
  {
    value: "ViewAll",
    label: "View all",
    hint: "Read every record of this type, ignoring the sharing rules",
  },
  {
    value: "ModifyAll",
    label: "Modify all",
    hint: "Edit and delete every record, ignoring sharing and ownership",
  },
] as const;

export const VISIBILITY_OPTIONS = [
  {
    value: "Private",
    label: "Private",
    hint: "Only the owner, and whoever is above them in the reporting line.",
  },
  {
    value: "PublicRead",
    label: "Public read",
    hint: "Everyone in the company reads it; only the owner edits.",
  },
  {
    value: "PublicReadWrite",
    label: "Public read/write",
    hint: "Everyone in the company reads and edits it.",
  },
] as const;

/* ---------------- login history ---------------- */

export interface LoginSession {
  id: number;
  userId: number;
  userName: string;
  userEmail: string;
  role: string;
  issuedAt: string;
  expiresAt: string;
  lastSeenAt: string;
  revokedAt: string | null;
  revokedReason: string | null;
  device: string | null;
  ipAddress: string | null;
  /** Neither revoked nor expired — somebody is holding this right now. */
  isLive: boolean;
}

export interface LoginHistory {
  sessions: LoginSession[];
  live: number;
  revoked: number;
  expired: number;
  distinctUsers: number;
  windowDays: number;
}

/* ---------------- security health ---------------- */

export interface SecurityFinding {
  key: string;
  title: string;
  /** Ok, Warning or Risk. */
  severity: string;
  finding: string;
  why: string;
  fix: string | null;
  fixHref: string | null;
  count: number;
}

export interface SecurityHealth {
  score: number;
  grade: string;
  risks: number;
  warnings: number;
  passed: number;
  checkedAt: string;
  findings: SecurityFinding[];
}

/* ---------------- sharing rules ---------------- */

export interface NamedOption {
  value: string;
  label: string;
}

export interface SharingRule {
  id: number;
  name: string;
  description: string | null;
  object: string;
  ownerRoleKey: string | null;
  ownerRoleName: string | null;
  criteriaField: string | null;
  criteriaOperator: string | null;
  criteriaValue: string | null;
  target: string;
  targetRoleKey: string | null;
  targetRoleName: string | null;
  targetBranchId: number | null;
  targetBranchName: string | null;
  targetUserId: number | null;
  targetUserName: string | null;
  grantEdit: boolean;
  isActive: boolean;
}

export interface SharingRules {
  rules: SharingRule[];
  objects: string[];
  roles: NamedOption[];
  branches: NamedOption[];
  users: NamedOption[];
  /** Which fields a criteria rule may test, per object. */
  shareableFields: Record<string, string[]>;
}

export interface SaveSharingRule {
  name: string;
  description?: string | null;
  object: string;
  ownerRoleKey?: string | null;
  criteriaField?: string | null;
  criteriaOperator?: string | null;
  criteriaValue?: string | null;
  target: string;
  targetRoleKey?: string | null;
  targetBranchId?: number | null;
  targetUserId?: number | null;
  grantEdit: boolean;
  isActive: boolean;
}

/* ---------------- login policies ---------------- */

export interface LoginPolicy {
  profileId: number;
  profileName: string;
  roleKey: string | null;
  userCount: number;
  allowedIpRanges: string | null;
  loginFromMinute: number | null;
  loginToMinute: number | null;
  /** Bitmask from Sunday. 127 is every day. */
  allowedDays: number;
  idleTimeoutMinutes: number | null;
  isActive: boolean;
}

export interface SaveLoginPolicy {
  allowedIpRanges?: string | null;
  loginFromMinute?: number | null;
  loginToMinute?: number | null;
  allowedDays: number;
  idleTimeoutMinutes?: number | null;
  isActive: boolean;
}

/* ---------------- permission sets ---------------- */

export interface PermissionGrant {
  id: number;
  userId: number;
  userName: string;
  expiresAt: string | null;
  reason: string | null;
  /** Past its expiry. Kept for the history; the resolver already ignores it. */
  hasLapsed: boolean;
}

export interface PermissionSetSummary {
  id: number;
  name: string;
  description: string | null;
  isSystem: boolean;
  objects: ObjectPermissionRow[];
  grants: PermissionGrant[];
}

export interface SavePermissionSet {
  name: string;
  description?: string | null;
  objects?: ObjectPermissionRow[];
}

export const accessApi = {
  roles: () => get<RoleSummary[]>("/api/admin/access/roles"),

  loginHistory: (params: { days?: number; userId?: number; liveOnly?: boolean } = {}) => {
    const query = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) {
      if (v !== undefined && v !== null) query.set(k, String(v));
    }
    return get<LoginHistory>(`/api/admin/access/login-history?${query}`);
  },
  revokeSession: (id: number) =>
    post<void>(`/api/admin/access/login-history/${id}/revoke`, {}),

  securityHealth: () => get<SecurityHealth>("/api/admin/access/security-health"),

  permissionSets: () => get<PermissionSetSummary[]>("/api/admin/access/permission-sets"),
  createPermissionSet: (body: SavePermissionSet) =>
    post<PermissionSetSummary>("/api/admin/access/permission-sets", body),
  updatePermissionSet: (id: number, body: SavePermissionSet) =>
    put<PermissionSetSummary>(`/api/admin/access/permission-sets/${id}`, body),
  deletePermissionSet: (id: number) =>
    del<void>(`/api/admin/access/permission-sets/${id}`),
  grantPermissionSet: (
    id: number,
    body: { userId: number; expiresAt?: string | null; reason?: string | null }
  ) => post<PermissionSetSummary>(`/api/admin/access/permission-sets/${id}/grant`, body),
  revokePermissionSet: (grantId: number) =>
    del<void>(`/api/admin/access/permission-sets/grants/${grantId}`),

  sharingRules: () => get<SharingRules>("/api/admin/access/sharing-rules"),
  createSharingRule: (body: SaveSharingRule) =>
    post<SharingRule>("/api/admin/access/sharing-rules", body),
  updateSharingRule: (id: number, body: SaveSharingRule) =>
    put<SharingRule>(`/api/admin/access/sharing-rules/${id}`, body),
  deleteSharingRule: (id: number) =>
    del<void>(`/api/admin/access/sharing-rules/${id}`),

  loginPolicies: () => get<LoginPolicy[]>("/api/admin/access/login-policies"),
  saveLoginPolicy: (profileId: number, body: SaveLoginPolicy) =>
    put<LoginPolicy>(`/api/admin/access/login-policies/${profileId}`, body),

  fields: (profileId: number) =>
    get<FieldPermissionRow[]>(`/api/admin/access/profiles/${profileId}/fields`),
  saveFields: (profileId: number, fields: FieldPermissionRow[]) =>
    put<FieldPermissionRow[]>(`/api/admin/access/profiles/${profileId}/fields`, { fields }),

  visibility: () => get<ObjectVisibilityRow[]>("/api/admin/access/visibility"),
  setVisibility: (row: ObjectVisibilityRow) =>
    put<void>(`/api/admin/access/visibility/${row.object}`, row),

  profile: (id: number) => get<ProfileDetail>(`/api/admin/access/profiles/${id}`),
  saveProfile: (id: number, body: { modules?: string[]; objects?: ObjectPermissionRow[] }) =>
    put<ProfileDetail>(`/api/admin/access/profiles/${id}`, body),

  hierarchy: () => get<HierarchyNode[]>("/api/admin/access/hierarchy"),
  setManager: (userId: number, managerId: number | null) =>
    put<void>(`/api/admin/access/users/${userId}/manager`, { managerId }),

  userModules: (userId: number) =>
    get<UserModuleRow[]>(`/api/admin/access/users/${userId}/modules`),
  setUserModule: (userId: number, module: string, granted: boolean | null) =>
    put<void>(`/api/admin/access/users/${userId}/modules/${module}`, granted),

  teams: () => get<TeamsOverview>("/api/admin/access/teams"),
  /** Moves a batch under one manager, or off the tree with `null`. */
  reassign: (userIds: number[], managerId: number | null) =>
    post<TeamsOverview>(
      "/api/admin/access/teams/reassign",
      { userIds, managerId },
      { auth: true }
    ),
};
