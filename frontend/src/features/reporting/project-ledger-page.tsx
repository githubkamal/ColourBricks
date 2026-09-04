"use client";

import { useQuery } from "@tanstack/react-query";
import { formatDate, formatINR } from "@/lib/format";
import { projectFinancialLedger } from "./api";

export function ProjectLedgerPage({ projectId }: { projectId: number }) {
  const { data, isLoading, isError } = useQuery({
    queryKey: ["project-financial-ledger", projectId],
    queryFn: () => projectFinancialLedger(projectId),
  });

  if (isLoading) return <p className="p-4 text-sm">Loading…</p>;
  if (isError || !data)
    return <p className="text-negative p-4 text-sm">Could not load the ledger.</p>;

  return (
    <div className="max-w-4xl space-y-4">
      <h1 className="text-lg font-semibold">Project financial ledger</h1>
      <p className="text-muted-foreground text-sm">
        Opening {formatINR(data.openingBalance)} · Closing{" "}
        <span data-testid="closing-balance" className="font-medium">
          {formatINR(data.closingBalance)}
        </span>
      </p>

      <div className="overflow-x-auto rounded border">
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
                <td className="p-2">{l.credit > 0 ? formatINR(l.credit) : "—"}</td>
                <td className="p-2">{l.debit > 0 ? formatINR(l.debit) : "—"}</td>
                <td className="p-2 font-medium">{formatINR(l.runningBalance)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
