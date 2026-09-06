"use client";

import { useQuery } from "@tanstack/react-query";
import { dataState } from "@/components/ui/data-state";
import { PageHeader } from "@/components/ui/page-header";
import { formatDate, formatINR } from "@/lib/format";
import { projectFinancialLedger } from "./api";

export function ProjectLedgerPage({ projectId }: { projectId: number }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["project-financial-ledger", projectId],
    queryFn: () => projectFinancialLedger(projectId),
  });

  if (isLoading || isError || !data) {
    return (
      <div className="max-w-4xl space-y-4">
        <PageHeader title="Project financial ledger" />
        {dataState({
          isPending: isLoading,
          isError: isError || !data,
          errorLabel: "Could not load the ledger.",
        })}
      </div>
    );
  }

  return (
    <div className="max-w-4xl space-y-4">
      <PageHeader title="Project financial ledger" />
      <p className="text-muted-foreground text-sm">
        Opening {formatINR(data.openingBalance)} · Closing{" "}
        <span data-testid="closing-balance" className="text-foreground font-medium">
          {formatINR(data.closingBalance)}
        </span>
      </p>

      <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Date</th>
              <th className="p-2 font-medium">Description</th>
              <th className="p-2 font-medium">Credit</th>
              <th className="p-2 font-medium">Debit</th>
              <th className="p-2 font-medium">Balance</th>
            </tr>
          </thead>
          <tbody>
            {data.lines.map((l) => (
              <tr
                key={l.entryId}
                className={
                  l.isReversal ? "text-attention border-b last:border-0" : "border-b last:border-0"
                }
              >
                <td className="p-2">{formatDate(l.date)}</td>
                <td className="p-2">{l.description}</td>
                <td className="p-2 tabular-nums">{l.credit > 0 ? formatINR(l.credit) : "—"}</td>
                <td className="p-2 tabular-nums">{l.debit > 0 ? formatINR(l.debit) : "—"}</td>
                <td className="p-2 font-medium tabular-nums">{formatINR(l.runningBalance)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
