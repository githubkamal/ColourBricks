"use client";

import { useQuery } from "@tanstack/react-query";
import { apiClient } from "@/lib/api";
import { formatINR } from "@/lib/format";

interface IntegrityViolation {
  entity: string;
  id: number;
  name: string;
  expected: number;
  actual: number;
  detail: string;
}

interface IntegrityControlResult {
  control: string;
  formula: string;
  passed: boolean;
  violations: IntegrityViolation[];
}

interface IntegrityCheckReport {
  passed: boolean;
  runAtUtc: string;
  controls: IntegrityControlResult[];
}

function integrityCheck(): Promise<IntegrityCheckReport> {
  return apiClient.get<IntegrityCheckReport>("/admin/integrity-check");
}

export function IntegrityCheckPage() {
  const { data, isLoading, isError, refetch, isFetching } = useQuery({
    queryKey: ["integrity-check"],
    queryFn: integrityCheck,
  });

  return (
    <div className="max-w-4xl space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Data Integrity Controls</h1>
        <button
          type="button"
          className="rounded border px-3 py-1.5 text-sm"
          onClick={() => void refetch()}
          disabled={isFetching}
        >
          {isFetching ? "Checking…" : "Re-run"}
        </button>
      </div>
      <p className="text-muted-foreground text-sm">
        BRD §38 reconciliation controls — each figure recomputed from the primary records and
        compared against what the ledger serves.
      </p>

      {isLoading && <p className="text-sm">Running controls…</p>}
      {isError && <p className="text-negative text-sm">Could not run the integrity check.</p>}

      {data && (
        <>
          <p
            className={data.passed ? "text-positive font-medium" : "text-negative font-medium"}
            data-testid="integrity-summary"
          >
            {data.passed ? "All controls passed." : "One or more controls failed."}
          </p>

          <div className="space-y-3">
            {data.controls.map((c) => (
              <div key={c.control} className="rounded border p-3">
                <div className="flex items-center justify-between">
                  <span className="font-medium">{c.control}</span>
                  <span className={c.passed ? "text-positive text-sm" : "text-negative text-sm"}>
                    {c.passed ? "Pass" : "Fail"}
                  </span>
                </div>
                <p className="text-muted-foreground mt-1 text-xs">{c.formula}</p>
                {c.violations.length > 0 && (
                  <table className="mt-2 w-full text-sm">
                    <thead className="text-muted-foreground text-left">
                      <tr>
                        <th className="py-1 pr-2 font-medium">Record</th>
                        <th className="py-1 pr-2 font-medium">Expected</th>
                        <th className="py-1 pr-2 font-medium">Actual</th>
                        <th className="py-1 font-medium">Detail</th>
                      </tr>
                    </thead>
                    <tbody>
                      {c.violations.map((v) => (
                        <tr key={`${v.entity}-${v.id}`}>
                          <td className="py-1 pr-2">
                            {v.entity} #{v.id}
                            {v.name ? ` — ${v.name}` : ""}
                          </td>
                          <td className="py-1 pr-2">{formatINR(v.expected)}</td>
                          <td className="py-1 pr-2">{formatINR(v.actual)}</td>
                          <td className="text-muted-foreground py-1">{v.detail}</td>
                        </tr>
                      ))}
                    </tbody>
                  </table>
                )}
              </div>
            ))}
          </div>
        </>
      )}
    </div>
  );
}
