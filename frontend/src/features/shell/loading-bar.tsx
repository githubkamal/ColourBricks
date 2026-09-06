"use client";

import { usePathname, useSearchParams } from "next/navigation";
import { useEffect, useRef, useSyncExternalStore } from "react";
import {
  isLoadingActive,
  startNavigation,
  stopNavigation,
  subscribeLoading,
} from "@/lib/loading-bar";

/** Same-origin, in-page navigation this bar should track — not a new tab, download, or external link. */
function isTrackableNavClick(event: MouseEvent): HTMLAnchorElement | null {
  if (event.defaultPrevented || event.button !== 0) return null;
  if (event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) return null;

  const anchor = (event.target as Element | null)?.closest?.("a[href]") as HTMLAnchorElement | null;
  if (!anchor || anchor.target === "_blank" || anchor.hasAttribute("download")) return null;

  const url = new URL(anchor.href, window.location.href);
  if (url.origin !== window.location.origin) return null;
  if (url.pathname === window.location.pathname && url.search === window.location.search)
    return null;

  return anchor;
}

/**
 * A thin top-of-viewport progress bar for both loading sources the client asked
 * for (2026-09-04): API calls (via `lib/loading-bar.ts`, wired into `apiClient`'s
 * single request choke point) and page navigation. The App Router has no
 * built-in "navigation started" event, so navigation is tracked by intercepting
 * same-page link clicks (start) and the resulting pathname/search change (stop) —
 * covers Link/sidebar navigation, which is the overwhelming majority of it; a
 * safety timeout clears a stuck bar if a click doesn't end in a route change
 * (an in-page anchor, a click that got cancelled, etc).
 */
export function LoadingBar() {
  const active = useSyncExternalStore(subscribeLoading, isLoadingActive, () => false);
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const timeoutRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(() => {
    function onClick(event: MouseEvent) {
      if (isTrackableNavClick(event)) {
        startNavigation();
        if (timeoutRef.current) clearTimeout(timeoutRef.current);
        timeoutRef.current = setTimeout(stopNavigation, 8000);
      }
    }
    document.addEventListener("click", onClick, true);
    return () => document.removeEventListener("click", onClick, true);
  }, []);

  useEffect(() => {
    // pathname/searchParams change is what "the navigation finished" means here.
    stopNavigation();
    if (timeoutRef.current) clearTimeout(timeoutRef.current);
  }, [pathname, searchParams]);

  return (
    <div
      aria-hidden="true"
      data-testid="loading-bar"
      className={`bg-primary fixed top-0 right-0 left-0 z-50 h-0.5 origin-left transition-[transform,opacity] duration-300 ease-out ${
        active ? "scale-x-100 opacity-100" : "scale-x-0 opacity-0"
      }`}
    />
  );
}
