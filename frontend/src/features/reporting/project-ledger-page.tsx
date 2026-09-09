"use client";

import { useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { listExpenseCategories } from "@/features/direct-expenses/api";
import { ProjectTabs } from "@/features/projects/project-tabs";
import { dataState } from "@/components/ui/data-state";
import { ExportMenu } from "@/components/ui/export-menu";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import type { ExportTable } from "@/lib/export-table";
import { formatDate, formatINR } from "@/lib/format";
import { cn } from "@/lib/utils";
import { projectFinancialLedger, type ProjectLedgerLine } from "./api";

type Period = "Weekly" | "Monthly" | "Yearly" | "Custom";
const PERIODS: Period[] = ["Weekly", "Monthly", "Yearly", "Custom"];

type SortKey = "date" | "category" | "credit" | "debit" | "runningBalance";
const SORT_LABELS: Record<SortKey, string> = {
  date: "Date",
  category: "Category",
  credit: "Credit",
  debit: "Debit",
  runningBalance: "Balance",
};

function toISO(d: Date): string {
  return d.toISOString().slice(0, 10);
}

/** Weekly/Monthly/Yearly compute a concrete range client-side; Custom passes through. */
function computeRange(
  period: Period,
  customFrom: string,
  customTo: string,
): { dateFrom?: string; dateTo?: string } {
  const today = new Date();
  switch (period) {
    case "Custom":
      return { dateFrom: customFrom || undefined, dateTo: customTo || undefined };
    case "Weekly": {
      const from = new Date(today);
      from.setDate(from.getDate() - 6);
      return { dateFrom: toISO(from), dateTo: toISO(today) };
    }
    case "Yearly":
      return { dateFrom: `${today.getFullYear()}-01-01`, dateTo: toISO(today) };
    case "Monthly":
    default:
      return {
        dateFrom: `${today.getFullYear()}-${String(today.getMonth() + 1).padStart(2, "0")}-01`,
        dateTo: toISO(today),
      };
  }
}

function sortLines(lines: ProjectLedgerLine[], sortBy: SortKey, sortDir: "asc" | "desc") {
  const dir = sortDir === "asc" ? 1 : -1;
  return [...lines].sort((a, b) => {
    switch (sortBy) {
      case "category":
        return dir * a.categoryName.localeCompare(b.categoryName);
      case "credit":
        return dir * (a.credit - b.credit);
      case "debit":
        return dir * (a.debit - b.debit);
      case "runningBalance":
        return dir * (a.runningBalance - b.runningBalance);
      case "date":
      default:
        return dir * a.date.localeCompare(b.date) || dir * (a.entryId - b.entryId);
    }
  });
}

export function ProjectLedgerPage({ projectId }: { projectId: number }) {
  const [period, setPeriod] = useState<Period>("Monthly");
  const [customFrom, setCustomFrom] = useState("");
  const [customTo, setCustomTo] = useState("");
  const [categoryId, setCategoryId] = useState<number | "">("");
  const [sortBy, setSortBy] = useState<SortKey>("date");
  const [sortDir, setSortDir] = useState<"asc" | "desc">("asc");

  const isCustom = period === "Custom";
  const range = computeRange(period, customFrom, customTo);

  const { data: categories = [] } = useQuery({
    queryKey: ["expense-categories"],
    queryFn: () => listExpenseCategories(),
  });

  const { data, isLoading, isError } = useQuery({
    queryKey: ["project-financial-ledger", projectId, range.dateFrom, range.dateTo, categoryId],
    queryFn: () =>
      projectFinancialLedger(projectId, {
        dateFrom: range.dateFrom,
        dateTo: range.dateTo,
        categoryId: categoryId === "" ? undefined : categoryId,
      }),
    enabled: !isCustom || (customFrom !== "" && customTo !== ""),
  });

  const sortedLines = useMemo(
    () => (data ? sortLines(data.lines, sortBy, sortDir) : []),
    [data, sortBy, sortDir],
  );

  function toggleSort(key: SortKey) {
    if (key === sortBy) {
      setSortDir((d) => (d === "asc" ? "desc" : "asc"));
    } else {
      setSortBy(key);
      setSortDir("asc");
    }
  }

  function buildExportTable(): ExportTable | null {
    if (!data) return null;
    return {
      filename: `project-${projectId}-ledger`,
      title: "Project financial ledger",
      columns: ["Date", "Description", "Category", "Credit", "Debit", "Balance"],
      rows: sortedLines.map((l) => [
        l.date,
        l.description,
        l.categoryName,
        l.credit,
        l.debit,
        l.runningBalance,
      ]),
    };
  }

  const header = (
    <div className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <Select
          aria-label="Category"
          value={categoryId}
          onChange={(e) => setCategoryId(e.target.value ? Number(e.target.value) : "")}
        >
          <option value="">All categories</option>
          {categories.map((c) => (
            <option key={c.id} value={c.id}>
              {c.name}
            </option>
          ))}
        </Select>
        {isCustom && (
          <>
            <Input
              type="date"
              aria-label="From"
              value={customFrom}
              onChange={(e) => setCustomFrom(e.target.value)}
              className="h-8 w-36"
            />
            <span className="text-muted-foreground text-sm">to</span>
            <Input
              type="date"
              aria-label="To"
              value={customTo}
              onChange={(e) => setCustomTo(e.target.value)}
              className="h-8 w-36"
            />
          </>
        )}
        <div className="inline-flex gap-1 rounded-full border p-1" role="group" aria-label="Period">
          {PERIODS.map((p) => (
            <button
              key={p}
              type="button"
              onClick={() => setPeriod(p)}
              className={cn(
                "rounded-full px-3 py-1 text-xs font-medium transition-colors",
                p === period ? "bg-primary text-primary-foreground" : "hover:bg-secondary",
              )}
            >
              {p}
            </button>
          ))}
        </div>
        <ExportMenu table={buildExportTable} disabled={!data} />
      </div>
    </div>
  );

  if (isCustom && (customFrom === "" || customTo === "")) {
    return (
      <div className="max-w-5xl space-y-4">
        <ProjectTabs projectId={projectId} title="Project Financial Ledger" />
        {header}
        {dataState({ isEmpty: true, emptyLabel: "Pick a start and end date to load the ledger." })}
      </div>
    );
  }

  if (isLoading || isError || !data) {
    return (
      <div className="max-w-5xl space-y-4">
        <ProjectTabs projectId={projectId} title="Project Financial Ledger" />
        {header}
        {dataState({
          isPending: isLoading,
          isError: isError || !data,
          errorLabel: "Could not load the ledger.",
        })}
      </div>
    );
  }

  return (
    <div className="max-w-5xl space-y-4">
      <ProjectTabs projectId={projectId} title="Project Financial Ledger" />
      {header}
      <p className="text-muted-foreground text-sm">
        Opening {formatINR(data.openingBalance)} · Closing{" "}
        <span data-testid="closing-balance" className="text-foreground font-medium">
          {formatINR(data.closingBalance)}
        </span>
      </p>

      <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              {(["date", "category", "credit", "debit", "runningBalance"] as SortKey[]).map(
                (key) => (
                  <th
                    key={key}
                    className="hover:text-foreground cursor-pointer p-2 font-medium select-none"
                    onClick={() => toggleSort(key)}
                  >
                    {SORT_LABELS[key]}
                    {sortBy === key ? (sortDir === "desc" ? " ▼" : " ▲") : ""}
                  </th>
                ),
              )}
              <th className="p-2 font-medium">Description</th>
            </tr>
          </thead>
          <tbody>
            {sortedLines.map((l) => (
              <tr
                key={l.entryId}
                className={
                  l.isReversal ? "text-attention border-b last:border-0" : "border-b last:border-0"
                }
              >
                <td className="p-2">{formatDate(l.date)}</td>
                <td className="text-muted-foreground p-2">{l.categoryName}</td>
                <td className="p-2 tabular-nums">{l.credit > 0 ? formatINR(l.credit) : "—"}</td>
                <td className="p-2 tabular-nums">{l.debit > 0 ? formatINR(l.debit) : "—"}</td>
                <td className="p-2 font-medium tabular-nums">{formatINR(l.runningBalance)}</td>
                <td className="p-2">{l.description}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
