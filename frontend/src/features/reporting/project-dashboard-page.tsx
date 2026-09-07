"use client";

import { useQuery } from "@tanstack/react-query";
import { ProjectTabs } from "@/features/projects/project-tabs";
import { dataState } from "@/components/ui/data-state";
import { StatTile } from "@/components/ui/stat-tile";
import { formatINR } from "@/lib/format";
import { projectDashboard } from "./api";
import { tileIcon } from "./tile-icon";
import { tileTone } from "./tile-tone";
import { TotalRevenueChart } from "./total-revenue-chart";

const PERCENT_KEYS = new Set(["profitPercent", "budgetUtilisationPercent"]);

export function ProjectDashboardPage({ projectId }: { projectId: number }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["project-dashboard", projectId],
    queryFn: () => projectDashboard(projectId),
  });

  if (isLoading || isError || !data) {
    return (
      <div className="max-w-5xl space-y-6">
        <ProjectTabs projectId={projectId} title="Project Dashboard" />
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
      <ProjectTabs projectId={projectId} title="Project Dashboard" />

      <div
        className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4"
        data-testid="summary-tiles"
      >
        {data.summary.map((t) => (
          <StatTile
            key={t.key}
            label={t.label}
            value={PERCENT_KEYS.has(t.key) ? `${t.value.toFixed(2)}%` : formatINR(t.value)}
            tone={tileTone(t.key, t.value)}
            icon={tileIcon(t.key)}
          />
        ))}
      </div>

      <TotalRevenueChart data={data.monthlyFlow} />

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
