"use client";

import { Fragment, useMemo, useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { usePathname, useRouter, useSearchParams } from "next/navigation";
import { getSystemSettings } from "@/features/admin/system-settings-api";
import { attachmentPreviewUrl } from "@/features/attachments/api";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Input } from "@/components/ui/input";
import { formatINR } from "@/lib/format";
import {
  DATE_PRESETS,
  emptyFilter,
  reportCatalog,
  runReport,
  type DatePreset,
  type ReportCatalogEntry,
  type ReportColumn,
  type ReportFilterState,
} from "./api";

const FILTER_PARAM_KEYS: (keyof ReportFilterState)[] = [
  "datePreset",
  "dateFrom",
  "dateTo",
  "projectId",
  "vendorId",
  "subcontractorId",
  "departmentId",
  "itemId",
  "categoryId",
  "paymentModeId",
  "accountId",
  "paymentStatus",
  "transactionType",
  "reconciliationStatus",
  "search",
  "sortBy",
  "sortDir",
  "page",
  "pageSize",
];

const ID_FILTERS: { flag: string; key: keyof ReportFilterState; label: string }[] = [
  { flag: "project", key: "projectId", label: "Project" },
  { flag: "vendor", key: "vendorId", label: "Vendor" },
  { flag: "subcontractor", key: "subcontractorId", label: "Subcontractor" },
  { flag: "department", key: "departmentId", label: "Department" },
  { flag: "item", key: "itemId", label: "Item" },
  { flag: "category", key: "categoryId", label: "Category" },
  { flag: "paymentMode", key: "paymentModeId", label: "Payment mode" },
  { flag: "account", key: "accountId", label: "Account" },
];

const TEXT_FILTERS: { flag: string; key: keyof ReportFilterState; label: string }[] = [
  { flag: "paymentStatus", key: "paymentStatus", label: "Payment status" },
  { flag: "transactionType", key: "transactionType", label: "Transaction type" },
  { flag: "reconciliationStatus", key: "reconciliationStatus", label: "Reconciliation status" },
];

interface SavedView {
  name: string;
  filter: ReportFilterState;
}

function readJson<T>(key: string, fallback: T): T {
  try {
    const raw = localStorage.getItem(key);
    return raw ? (JSON.parse(raw) as T) : fallback;
  } catch {
    return fallback;
  }
}

function writeJson(key: string, value: unknown): void {
  try {
    localStorage.setItem(key, JSON.stringify(value));
  } catch {
    /* private mode / disabled storage — the shell still works, just no persistence */
  }
}

export function ReportShell({ reportKey }: { reportKey: string }) {
  const { data: catalog = [] } = useQuery({
    queryKey: ["report-catalog"],
    queryFn: reportCatalog,
  });
  const entry: ReportCatalogEntry | undefined = catalog.find((c) => c.key === reportKey);
  const { confirm, dialog } = useConfirmDialog();

  const router = useRouter();
  const pathname = usePathname();
  const searchParams = useSearchParams();

  // Filter/sort/groupBy/page all live in the URL so a report view is
  // deep-linkable and survives back/forward navigation.
  const filter: ReportFilterState = useMemo(() => {
    const sortDirRaw = searchParams.get("sortDir");
    const pageRaw = Number(searchParams.get("page"));
    const pageSizeRaw = Number(searchParams.get("pageSize"));
    return {
      datePreset: (searchParams.get("datePreset") as DatePreset | null) ?? emptyFilter.datePreset,
      dateFrom: searchParams.get("dateFrom") ?? undefined,
      dateTo: searchParams.get("dateTo") ?? undefined,
      projectId: searchParams.get("projectId") ?? undefined,
      vendorId: searchParams.get("vendorId") ?? undefined,
      subcontractorId: searchParams.get("subcontractorId") ?? undefined,
      departmentId: searchParams.get("departmentId") ?? undefined,
      itemId: searchParams.get("itemId") ?? undefined,
      categoryId: searchParams.get("categoryId") ?? undefined,
      paymentModeId: searchParams.get("paymentModeId") ?? undefined,
      accountId: searchParams.get("accountId") ?? undefined,
      paymentStatus: searchParams.get("paymentStatus") ?? undefined,
      transactionType: searchParams.get("transactionType") ?? undefined,
      reconciliationStatus: searchParams.get("reconciliationStatus") ?? undefined,
      search: searchParams.get("search") ?? undefined,
      sortBy: searchParams.get("sortBy") ?? undefined,
      sortDir: sortDirRaw === "asc" || sortDirRaw === "desc" ? sortDirRaw : undefined,
      page: Number.isFinite(pageRaw) && pageRaw > 0 ? pageRaw : emptyFilter.page,
      pageSize:
        Number.isFinite(pageSizeRaw) && pageSizeRaw > 0 ? pageSizeRaw : emptyFilter.pageSize,
    };
  }, [searchParams]);

  const groupBy = searchParams.get("groupBy") ?? "";

  const [hidden, setHidden] = useState<Set<string>>(
    () => new Set(readJson<string[]>(`reportshell:${reportKey}:hidden`, [])),
  );
  const [views, setViews] = useState<SavedView[]>(() =>
    readJson<SavedView[]>(`reportshell:${reportKey}:views`, []),
  );

  function updateParams(updates: Record<string, string | number | undefined>) {
    const params = new URLSearchParams(searchParams.toString());
    for (const [key, value] of Object.entries(updates)) {
      if (value === undefined || value === "") {
        params.delete(key);
      } else {
        params.set(key, String(value));
      }
    }
    const query = params.toString();
    router.replace(query ? `${pathname}?${query}` : pathname, { scroll: false });
  }

  function applyFilter(next: ReportFilterState) {
    const updates: Record<string, string | number | undefined> = {};
    for (const key of FILTER_PARAM_KEYS) {
      updates[key] = next[key] as string | number | undefined;
    }
    updateParams(updates);
  }

  function setGroupBy(next: string) {
    updateParams({ groupBy: next || undefined });
  }

  const { data: result, isLoading } = useQuery({
    queryKey: ["report-run", reportKey, filter],
    queryFn: () => runReport(reportKey, filter),
    enabled: entry !== undefined,
  });
  // Company profile (System Settings, client-confirmed scope, 2026-09-04) — shown
  // only in the print view and prepended to the CSV export, per that scope.
  const { data: company } = useQuery({ queryKey: ["system-settings"], queryFn: getSystemSettings });

  const columns = entry?.columns ?? [];
  const visibleColumns = columns.filter((c) => !hidden.has(c.key));
  const supports = (flag: string) => entry?.supportedFilters.includes(flag) ?? false;

  const patch = (p: Partial<ReportFilterState>) =>
    updateParams({ ...p, page: p.page ?? 1 } as Record<string, string | number | undefined>);

  const toggleColumn = (key: string) => {
    setHidden((prev) => {
      const next = new Set(prev);
      if (next.has(key)) {
        next.delete(key);
      } else {
        next.add(key);
      }
      writeJson(`reportshell:${reportKey}:hidden`, [...next]);
      return next;
    });
  };

  const sortByColumn = (key: string) => {
    const dir: "asc" | "desc" = filter.sortBy === key && filter.sortDir === "asc" ? "desc" : "asc";
    patch({ sortBy: key, sortDir: dir });
  };

  const reset = () => {
    const updates: Record<string, string | number | undefined> = { groupBy: undefined };
    for (const key of FILTER_PARAM_KEYS) {
      updates[key] = emptyFilter[key] as string | number | undefined;
    }
    updateParams(updates);
  };

  const saveView = async () => {
    const { confirmed, value } = await confirm({
      title: "Save this view",
      inputLabel: "View name",
      confirmLabel: "Save",
    });
    const name = value?.trim();
    if (!confirmed || !name) return;
    const next = [...views.filter((v) => v.name !== name), { name, filter }];
    setViews(next);
    writeJson(`reportshell:${reportKey}:views`, next);
  };

  const groups = useMemo(() => {
    const rows = result?.rows ?? [];
    if (!groupBy) return [{ label: "", rows }];
    const map = new Map<string, Record<string, unknown>[]>();
    for (const row of rows) {
      const label = String(row[groupBy] ?? "—");
      const bucket = map.get(label) ?? [];
      bucket.push(row);
      map.set(label, bucket);
    }
    return [...map.entries()].map(([label, rs]) => ({ label, rows: rs }));
  }, [result?.rows, groupBy]);

  const exportCsv = () => {
    if (!result) return;
    const companyLine = company ? `${JSON.stringify(company.companyName)}\n` : "";
    const header = visibleColumns.map((c) => c.header).join(",");
    const body = result.rows
      .map((row) => visibleColumns.map((c) => JSON.stringify(row[c.key] ?? "")).join(","))
      .join("\n");
    const blob = new Blob([`${companyLine}${entry?.title ?? reportKey}\n${header}\n${body}`], {
      type: "text/csv",
    });
    const url = URL.createObjectURL(blob);
    const link = document.createElement("a");
    link.href = url;
    link.download = `${reportKey}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  };

  if (!entry) {
    return <p className="text-muted-foreground text-sm">Unknown report: {reportKey}</p>;
  }

  function renderCell(row: Record<string, unknown>, column: ReportColumn) {
    const value = row[column.key];
    if (column.numeric && typeof value === "number") return formatINR(value);
    return value === null || value === undefined ? "" : String(value);
  }

  return (
    <div className="space-y-4">
      {dialog}
      {company && (
        <div className="hidden items-start gap-3 print:flex">
          {(company.companyLogoAttachmentId || company.companyLogoUrl) && (
            // eslint-disable-next-line @next/next/no-img-element -- print-only, no need for next/image's optimisation pipeline
            <img
              src={
                company.companyLogoAttachmentId
                  ? attachmentPreviewUrl(company.companyLogoAttachmentId)
                  : (company.companyLogoUrl ?? undefined)
              }
              alt=""
              className="h-12 w-auto object-contain"
            />
          )}
          <div>
            <p className="text-base font-semibold">{company.companyName}</p>
            {company.companyAddress && <p className="text-xs">{company.companyAddress}</p>}
            {company.companyGstin && <p className="text-xs">GSTIN: {company.companyGstin}</p>}
          </div>
        </div>
      )}
      <h1 className="text-lg font-semibold">{entry.title}</h1>

      <div
        className="bg-card flex flex-wrap items-end gap-3 rounded border p-3"
        data-report-controls
      >
        {supports("date") && (
          <>
            <label className="space-y-1">
              <span className="text-sm font-medium">Date preset</span>
              <select
                className="bg-card block rounded border px-3 py-1.5 text-sm"
                aria-label="Date preset"
                value={filter.datePreset}
                onChange={(e) =>
                  patch({ datePreset: e.target.value as ReportFilterState["datePreset"] })
                }
              >
                {DATE_PRESETS.map((p) => (
                  <option key={p} value={p}>
                    {p}
                  </option>
                ))}
              </select>
            </label>
            {filter.datePreset === "Custom" && (
              <>
                <label className="space-y-1">
                  <span className="text-sm font-medium">From</span>
                  <Input
                    type="date"
                    aria-label="From"
                    value={filter.dateFrom ?? ""}
                    onChange={(e) => patch({ dateFrom: e.target.value })}
                  />
                </label>
                <label className="space-y-1">
                  <span className="text-sm font-medium">To</span>
                  <Input
                    type="date"
                    aria-label="To"
                    value={filter.dateTo ?? ""}
                    onChange={(e) => patch({ dateTo: e.target.value })}
                  />
                </label>
              </>
            )}
          </>
        )}

        {ID_FILTERS.filter((f) => supports(f.flag)).map((f) => (
          <label key={f.key} className="space-y-1">
            <span className="text-sm font-medium">{f.label} id</span>
            <Input
              className="w-28"
              inputMode="numeric"
              aria-label={f.label}
              value={(filter[f.key] as string | undefined) ?? ""}
              onChange={(e) => patch({ [f.key]: e.target.value } as Partial<ReportFilterState>)}
            />
          </label>
        ))}

        {TEXT_FILTERS.filter((f) => supports(f.flag)).map((f) => (
          <label key={f.key} className="space-y-1">
            <span className="text-sm font-medium">{f.label}</span>
            <Input
              className="w-36"
              aria-label={f.label}
              value={(filter[f.key] as string | undefined) ?? ""}
              onChange={(e) => patch({ [f.key]: e.target.value } as Partial<ReportFilterState>)}
            />
          </label>
        ))}

        {supports("search") && (
          <label className="space-y-1">
            <span className="text-sm font-medium">Search</span>
            <Input
              aria-label="Search"
              value={filter.search ?? ""}
              onChange={(e) => patch({ search: e.target.value })}
            />
          </label>
        )}

        <Button type="button" variant="secondary" onClick={reset}>
          Reset filters
        </Button>
      </div>

      <div className="flex flex-wrap items-center gap-3 text-sm" data-report-controls>
        <details className="relative">
          <summary className="cursor-pointer rounded border px-3 py-1.5">Columns</summary>
          <div className="bg-background absolute z-10 mt-1 space-y-1 rounded border p-2 shadow">
            {columns.map((c) => (
              <label key={c.key} className="flex items-center gap-2 whitespace-nowrap">
                <input
                  type="checkbox"
                  checked={!hidden.has(c.key)}
                  onChange={() => toggleColumn(c.key)}
                />
                {c.header}
              </label>
            ))}
          </div>
        </details>

        <label className="flex items-center gap-2">
          Group by
          <select
            className="bg-card rounded border px-2 py-1"
            aria-label="Group by"
            value={groupBy}
            onChange={(e) => setGroupBy(e.target.value)}
          >
            <option value="">None</option>
            {visibleColumns.map((c) => (
              <option key={c.key} value={c.key}>
                {c.header}
              </option>
            ))}
          </select>
        </label>

        <button
          type="button"
          className="rounded border px-3 py-1.5"
          title="Downloads a .csv file — opens directly in Excel, Google Sheets, etc."
          onClick={exportCsv}
        >
          Export as CSV
        </button>
        <button
          type="button"
          className="rounded border px-3 py-1.5"
          title="Opens the print dialog — choose “Save as PDF” as the destination to export a PDF"
          onClick={() => window.print()}
        >
          Print / Export to PDF
        </button>
        <button type="button" className="rounded border px-3 py-1.5" onClick={saveView}>
          Save view
        </button>
        {views.length > 0 && (
          <select
            className="bg-card rounded border px-2 py-1"
            aria-label="Saved views"
            value=""
            onChange={(e) => {
              const view = views.find((v) => v.name === e.target.value);
              if (view) applyFilter(view.filter);
            }}
          >
            <option value="">Load view…</option>
            {views.map((v) => (
              <option key={v.name} value={v.name}>
                {v.name}
              </option>
            ))}
          </select>
        )}
      </div>

      <div className="bg-card overflow-x-auto rounded border" data-report-grid>
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground sticky top-0">
            <tr className="border-b text-left">
              {visibleColumns.map((c) => {
                const sortable = entry.sortableColumnKeys.includes(c.key);
                return (
                  <th
                    key={c.key}
                    className={
                      sortable
                        ? "cursor-pointer p-2 font-medium select-none"
                        : "p-2 font-medium select-none"
                    }
                    onClick={sortable ? () => sortByColumn(c.key) : undefined}
                    onKeyDown={
                      sortable
                        ? (e) => {
                            if (e.key === "Enter" || e.key === " ") {
                              e.preventDefault();
                              sortByColumn(c.key);
                            }
                          }
                        : undefined
                    }
                    tabIndex={sortable ? 0 : undefined}
                    aria-sort={
                      sortable && filter.sortBy === c.key
                        ? filter.sortDir === "desc"
                          ? "descending"
                          : "ascending"
                        : undefined
                    }
                    title={sortable ? undefined : "This column can't be sorted"}
                  >
                    {c.header}
                    {filter.sortBy === c.key ? (filter.sortDir === "desc" ? " ▼" : " ▲") : ""}
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {isLoading && (
              <tr>
                <td
                  colSpan={visibleColumns.length}
                  className="text-muted-foreground p-3 text-center"
                >
                  Loading…
                </td>
              </tr>
            )}
            {!isLoading && (result?.rows.length ?? 0) === 0 && (
              <tr>
                <td
                  colSpan={visibleColumns.length}
                  className="text-muted-foreground p-3 text-center"
                >
                  No rows.
                </td>
              </tr>
            )}
            {groups.map((group) => (
              <Fragment key={group.label || "all"}>
                {group.label && (
                  <tr className="bg-secondary/40">
                    <td colSpan={visibleColumns.length} className="p-2 font-medium">
                      {group.label} ({group.rows.length})
                    </td>
                  </tr>
                )}
                {group.rows.map((row, i) => (
                  <tr key={`${group.label}-${i}`} className="border-b last:border-0">
                    {visibleColumns.map((c) => (
                      <td key={c.key} className={c.numeric ? "p-2 text-right tabular-nums" : "p-2"}>
                        {renderCell(row, c)}
                      </td>
                    ))}
                  </tr>
                ))}
              </Fragment>
            ))}
          </tbody>
          {result && Object.keys(result.totals).length > 0 && (
            <tfoot className="sticky bottom-0">
              <tr className="bg-secondary/60 border-t font-medium">
                {visibleColumns.map((c, i) => (
                  <td key={c.key} className={c.numeric ? "p-2 text-right tabular-nums" : "p-2"}>
                    {i === 0 && !c.total ? "Total" : ""}
                    {c.total && c.total in result.totals ? formatINR(result.totals[c.total]) : ""}
                  </td>
                ))}
              </tr>
            </tfoot>
          )}
        </table>
      </div>

      {result && (
        <div className="flex items-center gap-3 text-sm" data-report-pagination>
          <span className="text-muted-foreground" data-testid="report-rowcount">
            {result.totalCount} rows
          </span>
          <label className="flex items-center gap-2">
            Page size
            <select
              className="bg-card rounded border px-2 py-1"
              aria-label="Page size"
              value={filter.pageSize}
              onChange={(e) => patch({ pageSize: Number(e.target.value) })}
            >
              {[25, 50, 100, 200].map((n) => (
                <option key={n} value={n}>
                  {n}
                </option>
              ))}
            </select>
          </label>
          <button
            type="button"
            className="rounded border px-3 py-1 disabled:opacity-50"
            disabled={result.page <= 1}
            onClick={() => updateParams({ page: filter.page - 1 })}
          >
            Prev
          </button>
          <span>
            Page {result.page} of {Math.max(result.totalPages, 1)}
          </span>
          <button
            type="button"
            className="rounded border px-3 py-1 disabled:opacity-50"
            disabled={result.page >= result.totalPages}
            onClick={() => updateParams({ page: filter.page + 1 })}
          >
            Next
          </button>
        </div>
      )}
    </div>
  );
}
