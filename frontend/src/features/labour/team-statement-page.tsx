"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { listTeamsGrouped } from "@/features/teams/api";
import { TeamPicker } from "@/features/teams/team-picker";
import type { TeamDto } from "@/features/teams/types";
import { dataState } from "@/components/ui/data-state";
import { ExportMenu } from "@/components/ui/export-menu";
import { PageHeader } from "@/components/ui/page-header";
import type { ExportTable } from "@/lib/export-table";
import { formatDate, formatINR } from "@/lib/format";
import { useQueryParamNumber } from "@/lib/use-query-param";
import { teamStatement } from "./api";

export function TeamStatementPage() {
  const [teamId, setTeamId] = useQueryParamNumber("teamId", 0);
  // `undefined` = the user hasn't picked a team in this session yet, so a
  // deep-linked `?teamId=` (if any) still needs restoring from the team list.
  const [manualTeam, setManualTeam] = useState<TeamDto | null | undefined>(undefined);

  const { data: restoredTeam } = useQuery({
    queryKey: ["teams", "grouped", teamId],
    queryFn: () => listTeamsGrouped(),
    enabled: teamId > 0 && manualTeam === undefined,
    select: (groups) => groups.flatMap((g) => g.teams).find((t) => t.id === teamId) ?? null,
  });

  const team = manualTeam !== undefined ? manualTeam : (restoredTeam ?? null);

  function handleSelect(next: TeamDto | null) {
    setManualTeam(next);
    setTeamId(next?.id ?? 0);
  }

  const { data: rows = [] } = useQuery({
    queryKey: ["team-statement", team?.id],
    queryFn: () => teamStatement(team!.id),
    enabled: team !== null,
  });

  function buildExportTable(): ExportTable | null {
    if (!team || rows.length === 0) return null;
    return {
      filename: `${team.name}-statement`,
      title: `Team Statement — ${team.name}`,
      columns: ["Date", "Entry", "Work value", "Paid", "Running outstanding"],
      rows: rows.map((r) => [r.date, `${r.kind} — ${r.reference}`, r.workValue, r.paid, r.runningOutstanding]),
    };
  }

  return (
    <div className="max-w-3xl space-y-6">
      <div className="flex items-start justify-between gap-3">
        <PageHeader title="Team Statement" />
        {team && <ExportMenu table={buildExportTable} />}
      </div>
      <TeamPicker selected={team} onSelect={handleSelect} label="Team" />

      {team &&
        (dataState({ isEmpty: rows.length === 0, emptyLabel: "No activity yet." }) ?? (
          <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
            <table className="w-full text-sm">
              <thead className="bg-secondary/60 text-muted-foreground">
                <tr className="border-b text-left">
                  <th className="p-2 font-medium">Date</th>
                  <th className="p-2 font-medium">Entry</th>
                  <th className="p-2 font-medium">Work value</th>
                  <th className="p-2 font-medium">Paid</th>
                  <th className="p-2 font-medium">Running outstanding</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={i} className="border-b last:border-0">
                    <td className="p-2">{formatDate(r.date)}</td>
                    <td className="text-muted-foreground p-2">
                      {r.kind} — {r.reference}
                    </td>
                    <td className="p-2 tabular-nums">
                      {r.workValue > 0 ? formatINR(r.workValue) : "—"}
                    </td>
                    <td className="p-2 tabular-nums">{r.paid > 0 ? formatINR(r.paid) : "—"}</td>
                    <td className="p-2 font-medium tabular-nums">
                      {formatINR(r.runningOutstanding)}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}
    </div>
  );
}
