"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { dataState } from "@/components/ui/data-state";
import { PageHeader } from "@/components/ui/page-header";
import { StatTile } from "@/components/ui/stat-tile";
import { formatINR } from "@/lib/format";
import { projectDashboard } from "./api";

const PERCENT_KEYS = new Set(["profitPercent", "budgetUtilisationPercent"]);

export function ProjectDashboardPage({ projectId }: { projectId: number }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["project-dashboard", projectId],
    queryFn: () => projectDashboard(projectId),
  });

  if (isLoading || isError || !data) {
    return (
      <div className="max-w-5xl space-y-6">
        <PageHeader title="Project dashboard" />
        {dataState({
          isPending: isLoading,
          isError: isError || !data,
          loadingLabel: "Loading dashboard…",
          errorLabel: "Could not load the dashboard.",
        })}
      </div>
    );
  }

  return (
    <div className="max-w-5xl space-y-6">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <PageHeader title={`${data.projectName} — dashboard`} />
        <nav className="text-muted-foreground flex gap-3 text-sm">
          <Link
            className="hover:text-foreground hover:underline"
            href={`/projects/${projectId}/budget-vs-actual`}
          >
            Budget vs actual
          </Link>
          <Link
            className="hover:text-foreground hover:underline"
            href={`/projects/${projectId}/financial-ledger`}
          >
            Ledger
          </Link>
          <Link
            className="hover:text-foreground hover:underline"
            href={`/projects/${projectId}/pnl`}
          >
            P&amp;L
          </Link>
        </nav>
      </div>

      <div
        className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4"
        data-testid="summary-tiles"
      >
        {data.summary.map((t) => (
          <StatTile
            key={t.key}
            label={t.label}
            value={PERCENT_KEYS.has(t.key) ? `${t.value.toFixed(2)}%` : formatINR(t.value)}
            tone={t.key === "profitPercent" ? (t.value < 0 ? "negative" : "positive") : undefined}
          />
        ))}
      </div>

      <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Expense category</th>
              <th className="p-2 font-medium">Amount</th>
            </tr>
          </thead>
          <tbody>
            {data.expenseBreakdown.map((r) => (
              <tr key={r.bucket} className="border-b last:border-0">
                <td className="p-2">{r.bucket}</td>
                <td className="p-2">{formatINR(r.amount)}</td>
              </tr>
            ))}
          </tbody>
          <tfoot>
            <tr className="border-t">
              <td className="p-2 font-medium">Total Expenses</td>
              <td className="p-2 font-semibold" data-testid="breakdown-total">
                {formatINR(data.totalExpenses)}
              </td>
            </tr>
          </tfoot>
        </table>
      </div>
    </div>
  );
}
