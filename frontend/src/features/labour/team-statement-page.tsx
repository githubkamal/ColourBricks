"use client";

import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listTeamsGrouped } from "@/features/teams/api";
import { TeamPicker } from "@/features/teams/team-picker";
import type { TeamDto } from "@/features/teams/types";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { ExportMenu } from "@/components/ui/export-menu";
import { PageHeader } from "@/components/ui/page-header";
import { ApiError } from "@/lib/api";
import type { ExportTable } from "@/lib/export-table";
import { formatDate, formatINR } from "@/lib/format";
import { useQueryParamNumber } from "@/lib/use-query-param";
import {
  deleteWork,
  deleteWorkPayment,
  getWork,
  teamStatement,
  type TeamStatementRow,
} from "./api";
import { WorkEntryForm } from "./work-entry-form";

export function TeamStatementPage() {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();
  const [teamId, setTeamId] = useQueryParamNumber("teamId", 0);
  // `undefined` = the user hasn't picked a team in this session yet, so a
  // deep-linked `?teamId=` (if any) still needs restoring from the team list.
  const [manualTeam, setManualTeam] = useState<TeamDto | null | undefined>(undefined);
  const [editingId, setEditingId] = useState<number | null>(null);

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

  const { data: editingEntry } = useQuery({
    queryKey: ["labour-work", "entry", editingId],
    queryFn: () => getWork(editingId!),
    enabled: editingId !== null,
  });

  function refresh() {
    void queryClient.invalidateQueries({ queryKey: ["team-statement"] });
    void queryClient.invalidateQueries({ queryKey: ["labour-work"] });
  }

  async function remove(row: TeamStatementRow) {
    const isPayment = row.kind === "Payment";
    const label = row.referenceNumber ?? (isPayment ? "this payment" : "this work entry");
    const { confirmed } = await confirm({
      title: isPayment
        ? `Delete the ${formatINR(row.paid)} payment on ${label}?`
        : `Delete ${label}?`,
      description: isPayment
        ? "The work entry stays; its outstanding goes back up. The books keep a reversal record."
        : "Any payments made against it are deleted too. The books keep a reversal record.",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (!confirmed) return;
    try {
      if (isPayment) await deleteWorkPayment(row.paymentId!);
      else await deleteWork(row.workEntryId!);
      toast.success(isPayment ? "Payment deleted" : `${label} deleted`);
      refresh();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not delete");
    }
  }

  function buildExportTable(): ExportTable | null {
    if (!team || rows.length === 0) return null;
    return {
      filename: `${team.name}-statement`,
      title: `Team Statement — ${team.name}`,
      columns: ["Date", "Ref no.", "Project", "Entry", "Work value", "Paid", "Running outstanding"],
      rows: rows.map((r) => [
        r.date,
        r.referenceNumber ?? "",
        r.projectName ?? "",
        `${r.kind} — ${r.reference}`,
        r.workValue,
        r.paid,
        r.runningOutstanding,
      ]),
    };
  }

  return (
    <div className="max-w-5xl space-y-6">
      {dialog}
      <div className="flex items-start justify-between gap-3">
        <PageHeader title="Team Statement" />
        {team && <ExportMenu table={buildExportTable} />}
      </div>
      <TeamPicker selected={team} onSelect={handleSelect} label="Team" />

      {team &&
        (dataState({ isEmpty: rows.length === 0, emptyLabel: "No activity yet." }) ?? (
          <div
            data-table-scroll
            className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs"
          >
            <table className="w-full text-sm">
              <thead className="bg-secondary/60 text-muted-foreground">
                <tr className="border-b text-left">
                  <th className="p-2 font-medium">Date</th>
                  <th className="p-2 font-medium">Ref no.</th>
                  <th className="p-2 font-medium">Project</th>
                  <th className="p-2 font-medium">Entry</th>
                  <th className="p-2 font-medium">Work value</th>
                  <th className="p-2 font-medium">Paid</th>
                  <th className="p-2 font-medium">Running outstanding</th>
                  <th className="p-2 font-medium">
                    <span className="sr-only">Actions</span>
                  </th>
                </tr>
              </thead>
              <tbody>
                {rows.map((r, i) => (
                  <tr key={i} className="border-b last:border-0">
                    <td data-nowrap className="p-2">
                      {formatDate(r.date)}
                    </td>
                    <td data-nowrap className="p-2 font-medium">
                      {r.referenceNumber ?? "—"}
                    </td>
                    <td className="text-muted-foreground p-2">{r.projectName ?? "—"}</td>
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
                    <td data-nowrap className="p-2 text-right">
                      {r.kind === "Work" && r.workEntryId !== null && (
                        <Button
                          type="button"
                          size="xs"
                          variant="ghost"
                          onClick={() => setEditingId(r.workEntryId)}
                        >
                          Edit
                        </Button>
                      )}
                      {(r.kind === "Work" ? r.workEntryId : r.paymentId) !== null && (
                        <Button
                          type="button"
                          size="xs"
                          variant="ghost"
                          className="text-destructive"
                          onClick={() => void remove(r)}
                        >
                          Delete
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}

      <Dialog open={editingId !== null} onOpenChange={(open) => !open && setEditingId(null)}>
        <DialogContent className="max-w-2xl">
          <DialogTitle className="mb-3">Edit work entry</DialogTitle>
          {editingEntry && editingEntry.id === editingId ? (
            <WorkEntryForm
              key={editingEntry.id}
              entry={editingEntry}
              onSaved={() => {
                setEditingId(null);
                refresh();
              }}
              onCancel={() => setEditingId(null)}
            />
          ) : (
            <p className="text-muted-foreground text-sm">Loading…</p>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
