/**
 * A minimal, stateful stand-in for `next/navigation`'s `useRouter`/`usePathname`/
 * `useSearchParams`, for components that use `useQueryParam`/`useQueryParamNumber`
 * (or call these hooks directly) outside of Next's App Router test harness.
 * `router.replace`/`push` updates the in-memory URL and notifies subscribers, so
 * components re-render with the new search params the way they would in the app.
 *
 * Usage: `vi.mock("next/navigation", () => import("@/test/next-navigation-mock"));`
 * Call `resetNavigationMock()` in `beforeEach` if a test file relies on a clean URL.
 */
import { useSyncExternalStore } from "react";
import { vi } from "vitest";

let currentPathname = "/test";
let currentSearch = "";
const listeners = new Set<() => void>();

function notify() {
  listeners.forEach((listener) => listener());
}

function subscribe(listener: () => void) {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function applyUrl(url: string) {
  const [pathname, search = ""] = url.split("?");
  currentPathname = pathname;
  currentSearch = search;
  notify();
}

export function resetNavigationMock(pathname = "/test", search = "") {
  currentPathname = pathname;
  currentSearch = search;
  notify();
}

export function useRouter() {
  return {
    replace: applyUrl,
    push: applyUrl,
    back: vi.fn(),
    forward: vi.fn(),
    refresh: vi.fn(),
    prefetch: vi.fn(),
  };
}

export function usePathname() {
  return useSyncExternalStore(subscribe, () => currentPathname);
}

export function useSearchParams() {
  const search = useSyncExternalStore(subscribe, () => currentSearch);
  return new URLSearchParams(search);
}
