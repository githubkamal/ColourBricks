"use client";

import { useQuery } from "@tanstack/react-query";
import { ProjectTabs } from "@/features/projects/project-tabs";
import { dataState } from "@/components/ui/data-state";
import { Select } from "@/components/ui/select";
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

  if (isLoading || isError || !data) {
    return (
      <div className="max-w-5xl space-y-4">
        <ProjectTabs projectId={projectId} title="Profit & Loss" />
        {dataState({
          isPending: isLoading,
          isError: isError || !data,
          errorLabel: "Could not load P&L.",
        })}
      </div>
    );
  }

  const rows: [string, string][] = [
    ["Revenue", formatINR(data.revenue)],
    ["Estimated cost", formatINR(data.estimatedCost)],
    ["Actual cost", formatINR(data.actualCost)],
    ["Gross profit", formatINR(data.grossProfit)],
    ["Profit %", `${data.profitPercent.toFixed(2)}%`],
    ["Budget variance", formatINR(data.budgetVariance)],
  ];

  return (
    <div className="max-w-5xl space-y-4">
      <ProjectTabs projectId={projectId} title="Profit & Loss" />

      <label className="flex items-center gap-2 text-sm">
        <span>Revenue basis</span>
        <Select
          aria-label="Revenue basis"
          value={basis}
          onChange={(e) => setBasis(e.target.value as "Contract" | "Receipts")}
        >
          <option value="Contract">Contract value</option>
          <option value="Receipts">Receipts to date</option>
        </Select>
      </label>
      <p className="text-muted-foreground text-xs" data-testid="basis-label">
        Revenue shown on the{" "}
        <strong>{data.revenueBasis === "Contract" ? "contract value" : "receipts to date"}</strong>{" "}
        basis.
      </p>

      <div className="bg-card border-border rounded-xl border p-4 shadow-xs">
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
    </div>
  );
}
