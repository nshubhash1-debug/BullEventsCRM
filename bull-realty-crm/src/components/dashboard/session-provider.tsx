"use client";

import * as React from "react";
import { useRouter } from "next/navigation";

import {
  BOOT_HOLD_MS,
  CrmSplash,
  useMinimumDelay,
} from "@/components/shell/crm-loader";
import { useHasMounted } from "@/hooks/use-has-mounted";
import { endSession, type CompanyOption, type UserProfile } from "@/lib/api";
import {
  clearSession,
  COMPANIES_STORAGE_KEY,
  isCompanySelectionPending,
  USER_STORAGE_KEY,
} from "@/lib/session";

interface SessionContextValue {
  user: UserProfile;
  /** True when this user may open a company other than the one it belongs to. */
  canSwitchCompany: boolean;
  companies: CompanyOption[];
  signOut: () => void;
}

const SessionContext = React.createContext<SessionContextValue | null>(null);

/*
 * Both stores below cache against the raw stored string so `getSnapshot`
 * returns a stable reference — re-parsing on every render would hand
 * useSyncExternalStore a new object each time and loop.
 */

let cachedUserRaw: string | null | undefined;
let cachedUser: UserProfile | null = null;

const NO_COMPANIES: CompanyOption[] = [];
let cachedCompaniesRaw: string | null | undefined;
let cachedCompanies: CompanyOption[] = NO_COMPANIES;

function subscribe(callback: () => void) {
  window.addEventListener("storage", callback);
  return () => window.removeEventListener("storage", callback);
}

function getUserSnapshot() {
  const raw = localStorage.getItem(USER_STORAGE_KEY);
  if (raw === cachedUserRaw) return cachedUser;

  cachedUserRaw = raw;
  try {
    cachedUser = raw ? (JSON.parse(raw) as UserProfile) : null;
  } catch {
    cachedUser = null;
  }
  return cachedUser;
}

function getUserServerSnapshot() {
  return null;
}

function getCompaniesSnapshot() {
  const raw = localStorage.getItem(COMPANIES_STORAGE_KEY);
  if (raw === cachedCompaniesRaw) return cachedCompanies;

  cachedCompaniesRaw = raw;
  try {
    cachedCompanies = raw
      ? (JSON.parse(raw) as CompanyOption[])
      : NO_COMPANIES;
  } catch {
    cachedCompanies = NO_COMPANIES;
  }
  return cachedCompanies;
}

function getCompaniesServerSnapshot() {
  return NO_COMPANIES;
}

export function SessionProvider({ children }: { children: React.ReactNode }) {
  const router = useRouter();
  const hasMounted = useHasMounted();

  const user = React.useSyncExternalStore(
    subscribe,
    getUserSnapshot,
    getUserServerSnapshot
  );
  const companies = React.useSyncExternalStore(
    subscribe,
    getCompaniesSnapshot,
    getCompaniesServerSnapshot
  );

  // A platform admin is authenticated the moment the OTP clears, but its token
  // still points at a company it never chose. Rendering the CRM then would show
  // one tenant's data under another tenant's name, so the gate is "signed in
  // AND a company has been picked", not just "signed in".
  const needsCompany =
    hasMounted && user !== null && isCompanySelectionPending();

  React.useEffect(() => {
    if (!hasMounted) return;
    if (user === null) router.replace("/login");
    else if (needsCompany) router.replace("/select-company");
  }, [hasMounted, user, needsCompany, router]);

  const signOut = React.useCallback(() => {
    // Told to the server first, and deliberately not awaited: the token stays
    // cryptographically valid until it expires, so clearing local storage alone
    // would leave a working credential behind on a shared machine. Failing to
    // reach the server must still sign the person out of this browser, which is
    // why the local clear happens either way.
    void endSession().catch(() => undefined);

    clearSession();
    router.replace("/login");
  }, [router]);

  const value = React.useMemo<SessionContextValue | null>(
    () =>
      user === null
        ? null
        : {
            user,
            canSwitchCompany:
              user.role === "SuperAdmin" || companies.length > 1,
            companies,
            signOut,
          },
    [user, companies, signOut]
  );

  const bootElapsed = useMinimumDelay(BOOT_HOLD_MS);

  if (!hasMounted || value === null || needsCompany || !bootElapsed) {
    // useMinimumDelay gates this branch and CrmSplash paces its own ring
    // against the same constant, so the bar reaches its ceiling exactly as the
    // hold expires rather than resetting halfway or finishing early.
    return <CrmSplash />;
  }

  return (
    <SessionContext.Provider value={value}>{children}</SessionContext.Provider>
  );
}

export function useSession() {
  const ctx = React.useContext(SessionContext);
  if (!ctx) {
    throw new Error("useSession must be used within a SessionProvider");
  }
  return ctx;
}
