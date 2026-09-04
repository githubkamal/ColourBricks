"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { formatINR } from "@/lib/format";
import { projectDashboard } from "./api";

const PERCENT_KEYS = new Set(["profitPercent", "budgetUtilisationPercent"]);

export function ProjectDashboardPage({ projectId }: { projectId: number }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["project-dashboard", projectId],
    queryFn: () => projectDashboard(projectId),
  });

  if (isLoading) return <p className="p-4 text-sm">Loading dashboard…</p>;
  if (isError || !data)
    return <p className="text-negative p-4 text-sm">Could not load the dashboard.</p>;

  return (
    <div className="max-w-5xl space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">{data.projectName} — dashboard</h1>
        <nav className="text-sm">
          <Link className="underline" href={`/projects/${projectId}/budget-vs-actual`}>
            Budget vs actual
          </Link>{" "}
          ·{" "}
          <Link className="underline" href={`/projects/${projectId}/financial-ledger`}>
            Ledger
          </Link>{" "}
          ·{" "}
          <Link className="underline" href={`/projects/${projectId}/pnl`}>
            P&amp;L
          </Link>
        </nav>
      </div>

      <div
        className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4"
        data-testid="summary-tiles"
      >
        {data.summary.map((t) => (
          <div key={t.key} className="rounded border p-3">
            <div className="text-muted-foreground text-xs">{t.label}</div>
            <div className="text-base font-semibold">
              {PERCENT_KEYS.has(t.key) ? `${t.value.toFixed(2)}%` : formatINR(t.value)}
            </div>
          </div>
        ))}
      </div>

      <div className="rounded border">
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
