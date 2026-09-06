"use client";

import { useQuery } from "@tanstack/react-query";
import { formatINR } from "@/lib/format";
import { budgetVsActual } from "./api";

export function BudgetVsActualPage({ projectId }: { projectId: number }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["budget-vs-actual", projectId],
    queryFn: () => budgetVsActual(projectId),
  });

  if (isLoading) return <p className="p-4 text-sm">Loading…</p>;
  if (isError || !data)
    return <p className="text-negative p-4 text-sm">Could not load budget vs actual.</p>;

  const cls = (s: string) =>
    s === "Exceeded"
      ? "text-negative"
      : s === "Approaching"
        ? "text-attention"
        : "text-muted-foreground";

  return (
    <div className="max-w-4xl space-y-4">
      <h1 className="text-lg font-semibold">Budget vs actual</h1>
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

      <div className="overflow-x-auto rounded border">
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
                <td className={`p-2 font-medium ${cls(r.status)}`}>
                  {r.status === "Exceeded" ? "Over" : r.status}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
