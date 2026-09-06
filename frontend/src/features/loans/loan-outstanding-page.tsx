"use client";

import { useQuery } from "@tanstack/react-query";
import { dataState } from "@/components/ui/data-state";
import { PageHeader } from "@/components/ui/page-header";
import { StatTile } from "@/components/ui/stat-tile";
import { formatINR } from "@/lib/format";
import { loanOutstandingSummary } from "./api";

/** Loans > Outstanding (BRD §48): total principal still owed, by project. */
export function LoanOutstandingPage() {
  const { data, isPending } = useQuery({
    queryKey: ["loan-outstanding-summary"],
    queryFn: loanOutstandingSummary,
  });

  return (
    <div className="max-w-2xl space-y-6">
      <PageHeader title="Loan Outstanding" />

      <StatTile
        label="Company principal outstanding"
        value={
          <span data-testid="loan-total-outstanding">
            {isPending ? "…" : formatINR(data?.companyPrincipalOutstanding ?? 0)}
          </span>
        }
      />

      {dataState({
        isPending,
        isEmpty: !isPending && (data?.byProject.length ?? 0) === 0,
        emptyLabel: "No outstanding loans.",
      }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Project</th>
                <th className="p-2 font-medium">Loans</th>
                <th className="p-2 font-medium">Principal outstanding</th>
              </tr>
            </thead>
            <tbody>
              {data?.byProject.map((row) => (
                <tr key={row.projectId ?? "company"} className="border-b last:border-0">
                  <td className="p-2">{row.projectId ? row.projectName : "Company-wide"}</td>
                  <td className="p-2 tabular-nums">{row.loanCount}</td>
                  <td className="p-2 tabular-nums">{formatINR(row.principalOutstanding)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
