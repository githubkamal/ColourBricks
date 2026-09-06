"use client";

import { useQuery } from "@tanstack/react-query";
import { dataState } from "@/components/ui/data-state";
import { PageHeader } from "@/components/ui/page-header";
import { StatTile } from "@/components/ui/stat-tile";
import { formatINR } from "@/lib/format";
import { companyDashboard } from "./api";

const COUNT_KEYS = new Set(["ongoingProjects", "completedProjects", "pendingReconciliation"]);

export function CompanyDashboardPage() {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["company-dashboard"],
    queryFn: companyDashboard,
  });

  if (isLoading || isError || !data) {
    return (
      <div className="max-w-5xl space-y-6">
        <PageHeader title="Company dashboard" />
        {dataState({
          isPending: isLoading,
          isError: isError || !data,
          errorLabel: "Could not load the dashboard.",
        })}
      </div>
    );
  }

  return (
    <div className="max-w-5xl space-y-6">
      <PageHeader title="Company dashboard" />

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3" data-testid="company-tiles">
        {data.tiles.map((t) => (
          <StatTile
            key={t.key}
            label={t.label}
            value={COUNT_KEYS.has(t.key) ? String(t.value) : formatINR(t.value)}
          />
        ))}
      </div>

      <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Project</th>
              <th className="p-2 font-medium">Revenue</th>
              <th className="p-2 font-medium">Actual cost</th>
              <th className="p-2 font-medium">Profit</th>
            </tr>
          </thead>
          <tbody>
            {data.projectProfitability.map((r) => (
              <tr key={r.projectId} className="border-b last:border-0">
                <td className="p-2">{r.projectName}</td>
                <td className="p-2 tabular-nums">{formatINR(r.revenue)}</td>
                <td className="p-2 tabular-nums">{formatINR(r.actualCost)}</td>
                <td
                  className={
                    r.profit < 0
                      ? "text-negative p-2 tabular-nums"
                      : "text-positive p-2 tabular-nums"
                  }
                >
                  {formatINR(r.profit)}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {data.monthlyFlow.length > 0 && (
        <div className="bg-card border-border rounded-xl border p-4 text-sm shadow-xs">
          <p className="mb-2 font-medium">Monthly income vs expense</p>
          <ul className="divide-border divide-y">
            {data.monthlyFlow.map((m) => (
              <li key={m.month} className="flex justify-between py-1.5">
                <span>{m.month}</span>
                <span className="tabular-nums">
                  <span className="text-positive">{formatINR(m.income)}</span> /{" "}
                  <span className="text-negative">{formatINR(m.expense)}</span>
                </span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
