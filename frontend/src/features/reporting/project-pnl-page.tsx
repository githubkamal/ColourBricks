"use client";

import { useQuery } from "@tanstack/react-query";
import { formatINR } from "@/lib/format";
import { useQueryParam } from "@/lib/use-query-param";
import { projectPnl } from "./api";

export function ProjectPnlPage({ projectId }: { projectId: number }) {
  const [basisParam, setBasisParam] = useQueryParam("basis", "Contract");
  const basis: "Contract" | "Receipts" = basisParam === "Receipts" ? "Receipts" : "Contract";
  const setBasis = (v: "Contract" | "Receipts") => setBasisParam(v);
  const { data, isLoading, isError } = useQuery({
    queryKey: ["project-pnl", projectId, basis],
    queryFn: () => projectPnl(projectId, basis),
  });

  if (isLoading) return <p className="p-4 text-sm">Loading…</p>;
  if (isError || !data) return <p className="text-negative p-4 text-sm">Could not load P&amp;L.</p>;

  const rows: [string, string][] = [
    ["Revenue", formatINR(data.revenue)],
    ["Estimated cost", formatINR(data.estimatedCost)],
    ["Actual cost", formatINR(data.actualCost)],
    ["Gross profit", formatINR(data.grossProfit)],
    ["Profit %", `${data.profitPercent.toFixed(2)}%`],
    ["Budget variance", formatINR(data.budgetVariance)],
  ];

  return (
    <div className="max-w-md space-y-4">
      <h1 className="text-lg font-semibold">{data.projectName} — Profit &amp; Loss</h1>

      <label className="flex items-center gap-2 text-sm">
        <span>Revenue basis</span>
        <select
          className="rounded border bg-transparent px-2 py-1 text-sm"
          aria-label="Revenue basis"
          value={basis}
          onChange={(e) => setBasis(e.target.value as "Contract" | "Receipts")}
        >
          <option value="Contract">Contract value</option>
          <option value="Receipts">Receipts to date</option>
        </select>
      </label>
      <p className="text-muted-foreground text-xs" data-testid="basis-label">
        Revenue shown on the{" "}
        <strong>{data.revenueBasis === "Contract" ? "contract value" : "receipts to date"}</strong>{" "}
        basis.
      </p>

      <table className="w-full text-sm">
        <tbody>
          {rows.map(([k, v]) => (
            <tr key={k} className="border-b last:border-0">
              <td className="py-1.5">{k}</td>
              <td className="py-1.5 text-right font-medium tabular-nums">{v}</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
