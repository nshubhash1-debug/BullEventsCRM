import * as React from "react";

const RECENTS_KEY = "bull_events_setup_recents";
const LIMIT = 8;

/**
 * The `storage` event only fires in *other* tabs, so a write here would not
 * refresh the list on the page that made it. This is the same-tab half of the
 * signal.
 */
const CHANGED = "bull-events:setup-recents";

const EMPTY: string[] = [];

/*
 * Cached against the raw stored string so `getSnapshot` returns a stable
 * reference — re-parsing on every render would hand useSyncExternalStore a new
 * array each time and loop. The same shape the session store uses.
 */
let cachedRaw: string | null | undefined;
let cached: string[] = EMPTY;

function read(): string[] {
  const raw = localStorage.getItem(RECENTS_KEY);
  if (raw === cachedRaw) return cached;

  cachedRaw = raw;

  try {
    const parsed: unknown = raw ? JSON.parse(raw) : [];
    cached = Array.isArray(parsed)
      ? parsed.filter((entry): entry is string => typeof entry === "string")
      : EMPTY;
  } catch {
    cached = EMPTY;
  }

  return cached;
}

function subscribe(callback: () => void) {
  window.addEventListener("storage", callback);
  window.addEventListener(CHANGED, callback);

  return () => {
    window.removeEventListener("storage", callback);
    window.removeEventListener(CHANGED, callback);
  };
}

function serverSnapshot(): string[] {
  return EMPTY;
}

/**
 * The setup pages this browser opened last.
 *
 * Local rather than server-side because it is a convenience, not a record: it
 * should follow the machine somebody administers from, and it is not worth a
 * write on every navigation. A full or disabled storage is not worth failing
 * navigation over either, so every path here swallows its own errors.
 */
export function useSetupRecents(): string[] {
  return React.useSyncExternalStore(subscribe, read, serverSnapshot);
}

export function rememberSetupVisit(href: string) {
  if (typeof window === "undefined") return;

  try {
    const current = read();
    if (current[0] === href) return;

    const next = [href, ...current.filter((entry) => entry !== href)].slice(0, LIMIT);

    localStorage.setItem(RECENTS_KEY, JSON.stringify(next));
    window.dispatchEvent(new Event(CHANGED));
  } catch {
    // Ignored on purpose — see above.
  }
}
