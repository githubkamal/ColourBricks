"use client";

import { useQuery } from "@tanstack/react-query";
import { Select } from "@/components/ui/select";
import { useQueryParam } from "@/lib/use-query-param";
import { reportCatalog } from "./api";
import { ReportShell } from "./report-shell";

/**
 * Deep-linkable via `?report=<key>` (e.g. from the Vendors > Outstanding menu
 * item) so a nav link can drop the user straight onto a specific report
 * instead of always defaulting to the first one in the catalog.
 */
export function ReportExplorerPage() {
  const { data: catalog = [] } = useQuery({ queryKey: ["report-catalog"], queryFn: reportCatalog });
  const [selected, setSelected] = useQueryParam("report", "");

  const active = selected || catalog[0]?.key || "";

  return (
    <div className="space-y-4">
      <label className="flex items-center gap-2 text-sm">
        <span className="font-medium">Report</span>
        <Select aria-label="Report" value={active} onChange={(e) => setSelected(e.target.value)}>
          {catalog.map((entry) => (
            <option key={entry.key} value={entry.key}>
              {entry.title}
            </option>
          ))}
        </Select>
      </label>

      {active && <ReportShell key={active} reportKey={active} />}
    </div>
  );
}
