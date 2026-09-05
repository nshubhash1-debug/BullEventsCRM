"use client";

import * as React from "react";
import { usePathname } from "next/navigation";

import { useHasMounted } from "@/hooks/use-has-mounted";
import {
  APP_STORAGE_KEY,
  appById,
  appOwningPathname,
  type AppDef,
} from "@/lib/app-nav-config";

function readStored(): AppDef {
  try {
    return appById(window.localStorage.getItem(APP_STORAGE_KEY));
  } catch {
    // Private-mode storage failures are not worth breaking navigation over.
    return appById(null);
  }
}

/**
 * Which app the chrome is currently showing.
 *
 * Derived from the URL wherever the URL says — a bookmark, a refresh and a back
 * button then all land on the app the link belongs to. The stored id only fills
 * the gap: the administration screens belong to every app, so opening Companies
 * from HR should leave you in HR rather than silently dropping you into Lead
 * Management.
 */
export function useActiveApp(): AppDef {
  const pathname = usePathname();
  const hasMounted = useHasMounted();

  const owner = appOwningPathname(pathname);
  const ownerId = owner?.id;

  React.useEffect(() => {
    if (!ownerId) return;
    try {
      window.localStorage.setItem(APP_STORAGE_KEY, ownerId);
    } catch {
      // See readStored.
    }
  }, [ownerId]);

  // Before hydration there is no storage to read, so an unowned route renders
  // the default app and settles onto the remembered one on the client.
  return owner ?? (hasMounted ? readStored() : appById(null));
}

/** The app to open after sign-in — the last one used, or Lead Management. */
export function readStoredApp(): AppDef {
  if (typeof window === "undefined") return appById(null);
  return readStored();
}
