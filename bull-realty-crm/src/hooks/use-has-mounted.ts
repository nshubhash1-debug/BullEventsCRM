"use client";

import * as React from "react";

function subscribe() {
  return () => {};
}

function onClient() {
  return true;
}

function onServer() {
  return false;
}

/**
 * False during the server render and the hydrating render, true afterwards.
 *
 * Built on `useSyncExternalStore` rather than `useEffect` + `setState` so the
 * two renders agree on the markup and nothing has to set state from an effect
 * body. Screens that read `localStorage` gate on this: the stored value is
 * simply read during render once the flag flips.
 */
export function useHasMounted() {
  return React.useSyncExternalStore(subscribe, onClient, onServer);
}
