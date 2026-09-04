"use client";

import { useQuery } from "@tanstack/react-query";
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
      <h1 className="text-lg font-semibold">Loan Outstanding</h1>

      <div className="rounded border p-4">
        <p className="text-muted-foreground text-xs">Company principal outstanding</p>
        <p className="text-2xl font-semibold tabular-nums" data-testid="loan-total-outstanding">
          {isPending ? "…" : formatINR(data?.companyPrincipalOutstanding ?? 0)}
        </p>
      </div>

      <div className="rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Project</th>
              <th className="p-2 font-medium">Loans</th>
              <th className="p-2 font-medium">Principal outstanding</th>
            </tr>
          </thead>
          <tbody>
            {isPending && (
              <tr>
                <td colSpan={3} className="text-muted-foreground p-3 text-center">
                  Loading…
                </td>
              </tr>
            )}
            {!isPending && (data?.byProject.length ?? 0) === 0 && (
              <tr>
                <td colSpan={3} className="text-muted-foreground p-3 text-center">
                  No outstanding loans.
                </td>
              </tr>
            )}
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
    </div>
  );
}
