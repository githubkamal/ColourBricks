"use client";

import { useQuery } from "@tanstack/react-query";
import { Badge, type badgeVariants } from "@/components/ui/badge";
import { dataState } from "@/components/ui/data-state";
import { PageHeader } from "@/components/ui/page-header";
import { formatINR } from "@/lib/format";
import type { VariantProps } from "class-variance-authority";
import { budgetVsActual } from "./api";

const STATUS_VARIANT: Record<string, VariantProps<typeof badgeVariants>["variant"]> = {
  Exceeded: "negative",
  Approaching: "attention",
};

export function BudgetVsActualPage({ projectId }: { projectId: number }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["budget-vs-actual", projectId],
    queryFn: () => budgetVsActual(projectId),
  });

  if (isLoading || isError || !data) {
    return (
      <div className="max-w-4xl space-y-4">
        <PageHeader title="Budget vs actual" />
        {dataState({ isPending: isLoading, isError: isError || !data, errorLabel: "Could not load budget vs actual." })}
      </div>
    );
  }

  return (
    <div className="max-w-4xl space-y-4">
      <PageHeader title="Budget vs actual" />
      <p
        className={
          data.actualCost > data.estimatedCost
            ? "text-negative font-medium"
            : "text-muted-foreground"
        }
        data-testid="overrun-message"
      >
        {data.overrunMessage} — estimated {formatINR(data.estimatedCost)}, actual{" "}
        {formatINR(data.actualCost)}
      </p>

      <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Category</th>
              <th className="p-2 font-medium">Budget</th>
              <th className="p-2 font-medium">Actual</th>
              <th className="p-2 font-medium">Variance</th>
              <th className="p-2 font-medium">Variance %</th>
              <th className="p-2 font-medium">Status</th>
            </tr>
          </thead>
          <tbody>
            {data.rows.map((r) => (
              <tr key={r.categoryId} className="border-b last:border-0">
                <td className="p-2">{r.categoryName}</td>
                <td className="p-2 tabular-nums">{formatINR(r.budget)}</td>
                <td className="p-2 tabular-nums">{formatINR(r.actual)}</td>
                <td className="p-2 tabular-nums">{formatINR(r.variance)}</td>
                <td className="p-2 tabular-nums">
                  {r.variancePercent == null ? "—" : `${r.variancePercent.toFixed(1)}%`}
                </td>
                <td className="p-2">
                  <Badge variant={STATUS_VARIANT[r.status] ?? "positive"}>
                    {r.status === "Exceeded" ? "Over" : r.status}
                  </Badge>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
