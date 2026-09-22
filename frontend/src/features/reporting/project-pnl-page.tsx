"use client";

import { useQuery } from "@tanstack/react-query";
import { Landmark, Percent, Scale, TrendingDown, TrendingUp, Wallet } from "lucide-react";
import { ProjectTabs } from "@/features/projects/project-tabs";
import { dataState } from "@/components/ui/data-state";
import { Select } from "@/components/ui/select";
import { StatTile } from "@/components/ui/stat-tile";
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

  const profitTone = data.grossProfit < 0 ? "negative" : "positive";
  const varianceTone = data.budgetVariance > 0 ? "attention" : "positive";

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

      <div className="grid grid-cols-2 gap-3 sm:grid-cols-3">
        <StatTile
          label="Revenue"
          value={formatINR(data.revenue)}
          tone="positive"
          icon={TrendingUp}
        />
        <StatTile
          label="Estimated cost"
          value={formatINR(data.estimatedCost)}
          tone="neutral"
          icon={Wallet}
        />
        <StatTile
          label="Actual cost"
          value={formatINR(data.actualCost)}
          tone="negative"
          icon={TrendingDown}
        />
        <StatTile
          label="Gross profit"
          value={formatINR(data.grossProfit)}
          tone={profitTone}
          icon={Scale}
        />
        <StatTile
          label="Profit %"
          value={`${data.profitPercent.toFixed(2)}%`}
          tone={profitTone}
          icon={Percent}
        />
        <StatTile
          label="Budget variance"
          value={formatINR(data.budgetVariance)}
          tone={varianceTone}
          icon={Landmark}
        />
      </div>
    </div>
  );
}
