"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { TeamPicker } from "@/features/teams/team-picker";
import type { TeamDto } from "@/features/teams/types";
import { formatDate, formatINR } from "@/lib/format";
import { teamStatement } from "./api";

export function TeamStatementPage() {
  const [team, setTeam] = useState<TeamDto | null>(null);

  const { data: rows = [] } = useQuery({
    queryKey: ["team-statement", team?.id],
    queryFn: () => teamStatement(team!.id),
    enabled: team !== null,
  });

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">Team Statement</h1>
      <TeamPicker selected={team} onSelect={setTeam} label="Team" />

      {team && (
        <div className="rounded border">
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
              {rows.length === 0 && (
                <tr>
                  <td colSpan={5} className="text-muted-foreground p-3 text-center">
                    No activity yet.
                  </td>
                </tr>
              )}
              {rows.map((r, i) => (
                <tr key={i} className="border-b last:border-0">
                  <td className="p-2">{formatDate(r.date)}</td>
                  <td className="text-muted-foreground p-2">
                    {r.kind} — {r.reference}
                  </td>
                  <td className="p-2">{r.workValue > 0 ? formatINR(r.workValue) : "—"}</td>
                  <td className="p-2">{r.paid > 0 ? formatINR(r.paid) : "—"}</td>
                  <td className="p-2 font-medium">{formatINR(r.runningOutstanding)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
