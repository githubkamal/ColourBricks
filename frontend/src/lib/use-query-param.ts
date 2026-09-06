"use client";

import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { useCallback } from "react";

/**
 * Syncs one piece of UI state (a filter, tab, page number, …) to a URL query
 * param, so the view is deep-linkable and survives back/forward navigation.
 * Drop-in replacement for `useState` when the value should live in the URL.
 */
export function useQueryParam(key: string, defaultValue = ""): [string, (value: string) => void] {
  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();
  const value = searchParams.get(key) ?? defaultValue;

  const setValue = useCallback(
    (next: string) => {
      const params = new URLSearchParams(searchParams.toString());
      if (next === "" || next === defaultValue) {
        params.delete(key);
      } else {
        params.set(key, next);
      }
      const query = params.toString();
      router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
    },
    [key, defaultValue, pathname, router, searchParams],
  );

  return [value, setValue];
}

/** Same as {@link useQueryParam}, but for a numeric param (e.g. a page index). */
export function useQueryParamNumber(
  key: string,
  defaultValue: number,
): [number, (value: number) => void] {
  const [raw, setRaw] = useQueryParam(key, String(defaultValue));
  const value = Number.isFinite(Number(raw)) ? Number(raw) : defaultValue;
  const setValue = useCallback((next: number) => setRaw(String(next)), [setRaw]);
  return [value, setValue];
}
