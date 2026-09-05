import type { CompanyOption, UserProfile } from "@/lib/api";

const TOKEN_KEY = "bull_events_access_token";
const USER_KEY = "bull_events_user";
const COMPANIES_KEY = "bull_events_companies";
const PENDING_COMPANY_KEY = "bull_events_company_pending";

export const USER_STORAGE_KEY = USER_KEY;
export const TOKEN_STORAGE_KEY = TOKEN_KEY;
export const COMPANIES_STORAGE_KEY = COMPANIES_KEY;
export const PENDING_COMPANY_STORAGE_KEY = PENDING_COMPANY_KEY;

export interface StoredSession {
  accessToken: string;
  user: UserProfile;
}

export function saveSession(
  accessToken: string,
  user: UserProfile,
  companies?: CompanyOption[]
) {
  if (typeof window === "undefined") return;
  localStorage.setItem(TOKEN_KEY, accessToken);
  localStorage.setItem(USER_KEY, JSON.stringify(user));
  if (companies) localStorage.setItem(COMPANIES_KEY, JSON.stringify(companies));
}

export function readSession(): StoredSession | null {
  if (typeof window === "undefined") return null;
  const accessToken = localStorage.getItem(TOKEN_KEY);
  const rawUser = localStorage.getItem(USER_KEY);
  if (!accessToken || !rawUser) return null;
  try {
    return { accessToken, user: JSON.parse(rawUser) as UserProfile };
  } catch {
    return null;
  }
}

/** The tenants this session may switch between, as of the last auth response. */
export function readCompanies(): CompanyOption[] {
  if (typeof window === "undefined") return [];
  const raw = localStorage.getItem(COMPANIES_KEY);
  if (!raw) return [];
  try {
    return JSON.parse(raw) as CompanyOption[];
  } catch {
    return [];
  }
}

export function saveCompanies(companies: CompanyOption[]) {
  if (typeof window === "undefined") return;
  localStorage.setItem(COMPANIES_KEY, JSON.stringify(companies));
}

/**
 * A signed-in platform admin holds a valid token before it has chosen a tenant,
 * so "authenticated" alone is not enough to let the CRM load. This flag is what
 * keeps the dashboard closed until a company has actually been picked.
 */
export function setCompanySelectionPending(pending: boolean) {
  if (typeof window === "undefined") return;
  if (pending) localStorage.setItem(PENDING_COMPANY_KEY, "1");
  else localStorage.removeItem(PENDING_COMPANY_KEY);
}

export function isCompanySelectionPending() {
  if (typeof window === "undefined") return false;
  return localStorage.getItem(PENDING_COMPANY_KEY) === "1";
}

export function clearSession() {
  if (typeof window === "undefined") return;
  localStorage.removeItem(TOKEN_KEY);
  localStorage.removeItem(USER_KEY);
  localStorage.removeItem(COMPANIES_KEY);
  localStorage.removeItem(PENDING_COMPANY_KEY);
}
