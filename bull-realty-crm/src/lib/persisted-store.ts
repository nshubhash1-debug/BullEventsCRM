"use client";

import * as React from "react";

/**
 * Hydration-safe localStorage-backed state.
 *
 * Built on `useSyncExternalStore` (rather than `useEffect` + `setState`) so the
 * server render and the first client render agree, and so we stay clear of the
 * React Compiler's `set-state-in-effect` / purity rules. Snapshots are cached
 * against the raw stored string so `getSnapshot` returns a stable reference.
 *
 * `fallback` must be a module-level constant — a fresh object literal on every
 * render would make the snapshot unstable and loop.
 */

const listeners = new Set<() => void>();
const cache = new Map<string, { raw: string | null; parsed: unknown }>();

function emit() {
  for (const listener of listeners) listener();
}

function subscribe(onStoreChange: () => void) {
  listeners.add(onStoreChange);
  window.addEventListener("storage", onStoreChange);
  return () => {
    listeners.delete(onStoreChange);
    window.removeEventListener("storage", onStoreChange);
  };
}

function read<T>(key: string, fallback: T): T {
  if (typeof window === "undefined") return fallback;

  const raw = window.localStorage.getItem(key);
  const hit = cache.get(key);
  if (hit && hit.raw === raw) return hit.parsed as T;

  let parsed = fallback;
  if (raw !== null) {
    try {
      parsed = JSON.parse(raw) as T;
    } catch {
      parsed = fallback;
    }
  }

  cache.set(key, { raw, parsed });
  return parsed;
}

export function usePersistedState<T>(key: string, fallback: T) {
  const value = React.useSyncExternalStore(
    subscribe,
    () => read(key, fallback),
    () => fallback
  );

  const setValue = React.useCallback(
    (next: T) => {
      window.localStorage.setItem(key, JSON.stringify(next));
      emit();
    },
    [key]
  );

  return [value, setValue] as const;
}
