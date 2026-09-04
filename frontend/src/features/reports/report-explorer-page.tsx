"use client";

import { useState } from "react";
import { useQuery } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { reportCatalog } from "./api";
import { ReportShell } from "./report-shell";

/**
 * Deep-linkable via `?report=<key>` (e.g. from the Vendors > Outstanding menu
 * item) so a nav link can drop the user straight onto a specific report
 * instead of always defaulting to the first one in the catalog.
 */
export function ReportExplorerPage() {
  const { data: catalog = [] } = useQuery({ queryKey: ["report-catalog"], queryFn: reportCatalog });
  const searchParams = useSearchParams();
  const presetKey = searchParams?.get("report") ?? "";
  const [selected, setSelected] = useState<string>("");

  const active = selected || presetKey || catalog[0]?.key || "";

  return (
    <div className="space-y-4">
      <label className="flex items-center gap-2 text-sm">
        <span className="font-medium">Report</span>
        <select
          className="rounded border bg-transparent px-3 py-1.5"
          aria-label="Report"
          value={active}
          onChange={(e) => setSelected(e.target.value)}
        >
          {catalog.map((entry) => (
            <option key={entry.key} value={entry.key}>
              {entry.title}
            </option>
          ))}
        </select>
      </label>

      {active && <ReportShell key={active} reportKey={active} />}
    </div>
  );
}
