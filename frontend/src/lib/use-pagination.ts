import { useState } from "react";

/**
 * Client-side pagination for a list already fetched in full (client request,
 * 2026-09-07 — "pagination for all the grids"). Many master/transaction lists
 * fetch their whole table in one call; this slices it into pages without
 * requiring a backend paging rewrite for each one. Pages a list endpoint
 * already paginates server-side (PagedResult) should use that instead.
 */
export function usePagination<T>(rows: readonly T[] | undefined, pageSize = 20) {
  const [page, setPage] = useState(1);

  const total = rows?.length ?? 0;
  const pageCount = Math.max(1, Math.ceil(total / pageSize));
  // Clamp during render (not an effect) so a shrinking list — e.g. after a
  // filter change or a delete — never strands the viewer on an empty page.
  const clampedPage = Math.min(page, pageCount);
  if (clampedPage !== page) setPage(clampedPage);

  const start = (clampedPage - 1) * pageSize;
  const pageRows = rows?.slice(start, start + pageSize) ?? [];

  return { page: clampedPage, setPage, pageCount, pageRows, total, pageSize };
}
