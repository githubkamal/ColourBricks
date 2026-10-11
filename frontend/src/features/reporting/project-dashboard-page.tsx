"use client";

import { useQuery } from "@tanstack/react-query";
import { ProjectTabs } from "@/features/projects/project-tabs";
import { dataState } from "@/components/ui/data-state";
import { Skeleton } from "@/components/ui/skeleton";
import { StatTile, StatTileSkeleton } from "@/components/ui/stat-tile";
import { formatINR } from "@/lib/format";
import { projectDashboard, type ProjectDashboard } from "./api";
import { ATTENTION, ChartCard, ChartEmpty, DonutChart, Gauge, NEGATIVE, POSITIVE } from "./charts";
import { tileIcon } from "./tile-icon";
import { tileTone } from "./tile-tone";
import { TotalRevenueChart } from "./total-revenue-chart";

const PERCENT_KEYS = new Set(["profitPercent", "budgetUtilisationPercent"]);

function tileValue(data: ProjectDashboard, key: string): number {
  return data.summary.find((t) => t.key === key)?.value ?? 0;
}

export function ProjectDashboardPage({ projectId }: { projectId: number }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["project-dashboard", projectId],
    queryFn: () => projectDashboard(projectId),
  });

  if (isLoading) {
    return (
      <div className="max-w-5xl space-y-6">
        <ProjectTabs projectId={projectId} title="Project Dashboard" />
        <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
          {Array.from({ length: 8 }, (_, i) => (
            <StatTileSkeleton key={i} />
          ))}
        </div>
        <div className="bg-card border-border rounded-xl border p-4 shadow-xs">
          <Skeleton className="h-60 w-full rounded-lg" />
        </div>
      </div>
    );
  }

  if (isError || !data) {
    return (
      <div className="max-w-5xl space-y-6">
        <ProjectTabs projectId={projectId} title="Project Dashboard" />
        {dataState({
          isError: true,
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

      <ProjectCharts data={data} />

      <div
        data-table-scroll
        className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs"
      >
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

/** The project dashboard's chart grid (client request, 2026-10-11 — Ledgerstone's charts). */
function ProjectCharts({ data }: { data: ProjectDashboard }) {
  const budgetUsed = tileValue(data, "budgetUtilisationPercent");
  const profitPercent = tileValue(data, "profitPercent");
  const expenseMix = data.expenseBreakdown
    .filter((r) => r.amount > 0)
    .map((r) => ({ name: r.bucket, value: r.amount }));
  const paidVsDue = [
    { name: "Paid", value: tileValue(data, "totalPaid") },
    { name: "Outstanding", value: tileValue(data, "totalOutstanding") },
  ].filter((r) => r.value > 0);
  const budgetTone = budgetUsed > 100 ? NEGATIVE : budgetUsed >= 90 ? ATTENTION : POSITIVE;

  return (
    <div className="@container space-y-4">
      <div className="grid gap-4 @4xl:grid-cols-3">
        <div className="min-w-0 @4xl:col-span-2">
          {data.monthlyFlow.length > 0 ? (
            <TotalRevenueChart data={data.monthlyFlow} />
          ) : (
            <ChartCard title="Income vs expenses" bodyClassName="h-72">
              <ChartEmpty label="No income or expenses recorded yet." />
            </ChartCard>
          )}
        </div>
        <div className="grid gap-4 @xl:grid-cols-2 @4xl:grid-cols-1">
          <ChartCard
            title="Budget used"
            subtitle="Actual cost against the budget"
            bodyClassName="h-40"
          >
            <Gauge
              value={budgetUsed}
              label="of budget"
              color={budgetTone}
              caption={budgetUsed > 100 ? "Over budget" : undefined}
            />
          </ChartCard>
          <ChartCard title="Profit margin" subtitle="Actual profit on income" bodyClassName="h-40">
            <Gauge
              value={profitPercent}
              label="margin"
              color={profitPercent < 0 ? NEGATIVE : POSITIVE}
              caption={profitPercent < 0 ? "Running at a loss" : undefined}
            />
          </ChartCard>
        </div>
      </div>

      <div className="grid gap-4 @3xl:grid-cols-2">
        <ChartCard title="Where the money went" subtitle="Expenses by type" bodyClassName="h-auto">
          {expenseMix.length > 0 ? (
            <DonutChart rows={expenseMix} centerLabel="spent" />
          ) : (
            <div className="h-48">
              <ChartEmpty label="No expenses yet." />
            </div>
          )}
        </ChartCard>
        <ChartCard title="Paid vs outstanding" bodyClassName="h-auto">
          {paidVsDue.length > 0 ? (
            <DonutChart rows={paidVsDue} centerLabel="total" />
          ) : (
            <div className="h-48">
              <ChartEmpty label="Nothing paid or owed yet." />
            </div>
          )}
        </ChartCard>
      </div>
    </div>
  );
}
