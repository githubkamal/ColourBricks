"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Fragment, useState } from "react";
import { toast } from "sonner";
import { listProjects } from "@/features/projects/api";
import { ProjectPicker, type ProjectPickerSelection } from "@/features/projects/project-picker";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import {
  commitBankImport,
  discardBankImport,
  getBankImport,
  removeStagedRow,
  setRowAllocations,
  type BankImportBatch,
  type StagedBankRow,
} from "./api";

const round3 = (n: number) => Math.round((n + Number.EPSILON) * 1000) / 1000;

export function BankImportReviewPage({ batchId }: { batchId: number }) {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();

  const { data: batch, isLoading } = useQuery({
    queryKey: ["bank-import", batchId],
    queryFn: () => getBankImport(batchId),
  });

  const { data: projects } = useQuery({
    queryKey: ["projects", "for-bank-import"],
    queryFn: () => listProjects({ status: "Ongoing", pageSize: 200 }),
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["bank-import", batchId] });

  const commit = useMutation({
    mutationFn: () => commitBankImport(batchId),
    onSuccess: (r) => {
      toast.success(
        `Committed ${r.committed} transaction(s) — ${r.removed} removed, ${r.duplicate} duplicate, ${r.parseError} parse error`,
      );
      void refresh();
    },
    onError: (e) =>
      toast.error(
        e instanceof ApiError ? (e.fieldErrors?.rows?.[0] ?? e.message) : "Commit failed",
      ),
  });

  const discard = useMutation({
    mutationFn: () => discardBankImport(batchId),
    onSuccess: () => {
      toast.success("Import discarded");
      void refresh();
    },
    onError: () => toast.error("Could not discard the import"),
  });

  if (isLoading || !batch) {
    return <p className="p-4 text-sm">Loading import…</p>;
  }

  const projectOptions = (projects?.items ?? []).map((p) => ({ id: p.id, name: p.name }));
  const live = batch.rows.filter((r) => !r.isRemoved);
  // A duplicate row now blocks commit until it's explicitly removed, exactly like an
  // unmapped row — it doesn't just get silently skipped anymore (client request,
  // 2026-09-04).
  const duplicatesRemaining = live.filter(
    (r) => r.parseState === "Parsed" && r.duplicateOfBankTransactionId !== null,
  );
  const unmapped = live.filter(
    (r) => r.parseState === "Parsed" && r.duplicateOfBankTransactionId === null && !r.readyToCommit,
  );
  const isDraft = batch.status === "Draft";
  const canCommit =
    isDraft &&
    unmapped.length === 0 &&
    duplicatesRemaining.length === 0 &&
    live.some((r) => r.readyToCommit);

  return (
    <div className="max-w-5xl space-y-6">
      {dialog}
      <div>
        <h1 className="text-lg font-semibold">Review import — {batch.fileName}</h1>
        <p className="text-muted-foreground text-sm">
          Status {batch.status}. Map every row you keep to project(s); remove the rest. Nothing is
          saved to the ledger until you commit.
        </p>
      </div>

      <Counts batch={batch} />

      <div className="bg-card overflow-x-auto rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">#</th>
              <th className="p-2 font-medium">Date</th>
              <th className="p-2 font-medium">Narration</th>
              <th className="p-2 font-medium">Debit</th>
              <th className="p-2 font-medium">Credit</th>
              <th className="p-2 font-medium">Project mapping</th>
              <th className="p-2 font-medium" />
            </tr>
          </thead>
          <tbody>
            {batch.rows.map((row) => (
              <RowLine
                key={`${row.id}:${row.isRemoved}:${row.allocatedTotal}:${row.allocations.length}`}
                batchId={batchId}
                row={row}
                editable={isDraft}
                projects={projectOptions}
                onChanged={refresh}
              />
            ))}
          </tbody>
        </table>
      </div>

      {isDraft && (
        <div className="flex items-center gap-3">
          <Button
            type="button"
            disabled={!canCommit || commit.isPending}
            onClick={() => commit.mutate()}
          >
            Commit {live.filter((r) => r.readyToCommit).length} transaction(s)
          </Button>
          <Button
            type="button"
            variant="secondary"
            disabled={discard.isPending}
            onClick={async () => {
              const { confirmed } = await confirm({
                title: "Discard this import?",
                description:
                  "The staged rows and any mappings will be lost. This cannot be undone.",
                destructive: true,
                confirmLabel: "Discard import",
              });
              if (confirmed) discard.mutate();
            }}
          >
            Discard import
          </Button>
          {duplicatesRemaining.length > 0 && (
            <span className="text-attention text-sm" data-testid="duplicate-warning">
              {duplicatesRemaining.length} duplicate row(s) must be removed before you can commit
            </span>
          )}
          {unmapped.length > 0 && (
            <span className="text-attention text-sm">
              {unmapped.length} row(s) still need a project mapping
            </span>
          )}
        </div>
      )}
    </div>
  );
}

function Counts({ batch }: { batch: BankImportBatch }) {
  const c = batch.counts;
  const items: [string, number][] = [
    ["Total", c.total],
    ["Mapped", c.mapped],
    ["Unmapped", c.unmapped],
    ["Removed", c.removed],
    ["Duplicate", c.duplicate],
    ["Parse error", c.parseError],
  ];
  if (batch.status === "Committed") items.push(["Committed", c.committed]);
  return (
    <div className="flex flex-wrap gap-4 text-sm" data-testid="import-counts">
      {items.map(([label, n]) => (
        <span key={label}>
          <span className="text-muted-foreground">{label}:</span>{" "}
          <span className="font-medium">{n}</span>
        </span>
      ))}
    </div>
  );
}

type ProjectOption = { id: number; name: string };
type Line = { project: ProjectPickerSelection | null; amount: string };

function RowLine({
  batchId,
  row,
  editable,
  projects,
  onChanged,
}: {
  batchId: number;
  row: StagedBankRow;
  editable: boolean;
  projects: ProjectOption[];
  onChanged: () => void;
}) {
  const isCredit = row.credit > 0;
  const target = row.debit > 0 ? row.debit : row.credit;

  const [lines, setLines] = useState<Line[]>(() =>
    row.allocations.length > 0
      ? row.allocations.map((a) => ({
          project: { id: a.projectId, name: projects.find((p) => p.id === a.projectId)?.name ?? "" },
          amount: String(a.amount),
        }))
      : [{ project: null, amount: isCredit ? String(target) : "" }],
  );

  const save = useMutation({
    mutationFn: () =>
      setRowAllocations(
        batchId,
        row.id,
        lines
          .filter((l) => l.project !== null)
          .map((l) => ({ projectId: l.project!.id, amount: Number(l.amount) })),
      ),
    onSuccess: () => {
      toast.success(`Line ${row.sourceLineNo} mapped`);
      onChanged();
    },
    onError: (e) =>
      toast.error(
        e instanceof ApiError ? (e.fieldErrors?.allocations?.[0] ?? e.message) : "Could not save",
      ),
  });

  const remove = useMutation({
    mutationFn: (reason: string) => removeStagedRow(batchId, row.id, reason),
    onSuccess: () => {
      toast.success(`Line ${row.sourceLineNo} removed`);
      onChanged();
    },
    onError: () => toast.error("Could not remove the row"),
  });

  const { confirm: confirmRemove, dialog: removeDialog } = useConfirmDialog();

  const allocated = round3(lines.reduce((s, l) => s + (Number(l.amount) || 0), 0));
  const balanced = Math.abs(allocated - target) < 0.0005;

  const statusChip = row.isRemoved
    ? "Removed"
    : row.parseState === "Error"
      ? "Parse error"
      : row.duplicateOfBankTransactionId !== null
        ? "Duplicate"
        : row.readyToCommit
          ? "Mapped"
          : "Needs mapping";

  return (
    <Fragment>
      {removeDialog}
      <tr
        className={
          row.isRemoved
            ? "text-muted-foreground border-b line-through last:border-0"
            : "border-b last:border-0"
        }
      >
        <td className="p-2 align-top">{row.sourceLineNo}</td>
        <td className="p-2 align-top">{row.valueDate ? formatDate(row.valueDate) : "—"}</td>
        <td className="max-w-xs p-2 align-top">
          <div className="truncate break-words">{row.narration}</div>
          <div className="text-muted-foreground text-xs">
            {statusChip}
            {row.parseError ? ` — ${row.parseError}` : ""}
            {row.bankReference ? ` · ref ${row.bankReference}` : ""}
          </div>
        </td>
        <td className="p-2 align-top tabular-nums">{row.debit > 0 ? formatINR(row.debit) : "—"}</td>
        <td className="p-2 align-top tabular-nums">
          {row.credit > 0 ? formatINR(row.credit) : "—"}
        </td>
        <td className="p-2 align-top">
          {row.isRemoved ||
          row.parseState === "Error" ||
          row.duplicateOfBankTransactionId !== null ? (
            <span className="text-muted-foreground text-xs">not applicable</span>
          ) : editable ? (
            <div className="space-y-1">
              {lines.map((l, i) => (
                <div key={i} className="flex items-center gap-2">
                  <ProjectPicker
                    status="Ongoing"
                    ariaLabel={`Line ${row.sourceLineNo} project ${i + 1}`}
                    selected={l.project}
                    onSelect={(p) =>
                      setLines((cur) =>
                        cur.map((x, xi) =>
                          xi === i ? { ...x, project: p ? { id: p.id, name: p.name } : null } : x,
                        ),
                      )
                    }
                  />
                  <AmountInput
                    className="w-28"
                    aria-label={`Line ${row.sourceLineNo} amount ${i + 1}`}
                    value={l.amount}
                    onChange={(v) =>
                      setLines((cur) => cur.map((x, xi) => (xi === i ? { ...x, amount: v } : x)))
                    }
                  />
                  {!isCredit && lines.length > 1 && (
                    <button
                      type="button"
                      className="text-muted-foreground text-xs"
                      onClick={() => setLines((cur) => cur.filter((_, xi) => xi !== i))}
                    >
                      remove
                    </button>
                  )}
                </div>
              ))}
              {!isCredit && (
                <button
                  type="button"
                  className="text-xs underline"
                  onClick={() => setLines((cur) => [...cur, { project: null, amount: "" }])}
                >
                  + project
                </button>
              )}
              <div
                className={
                  balanced
                    ? "text-muted-foreground text-xs tabular-nums"
                    : "text-attention text-xs tabular-nums"
                }
              >
                allocated {formatINR(allocated)} of {formatINR(target)}
              </div>
              <Button
                type="button"
                className="h-7 px-2 text-xs"
                disabled={!balanced || save.isPending || lines.some((l) => l.project === null)}
                onClick={() => save.mutate()}
              >
                Save mapping
              </Button>
            </div>
          ) : (
            <ul className="text-xs">
              {row.allocations.map((a, i) => (
                <li key={i} className="tabular-nums">
                  {a.projectName}: {formatINR(a.amount)}
                </li>
              ))}
            </ul>
          )}
        </td>
        <td className="p-2 align-top">
          {editable && !row.isRemoved && (
            <button
              type="button"
              className="text-negative text-xs"
              disabled={remove.isPending}
              onClick={async () => {
                const { confirmed, value: reason } = await confirmRemove({
                  title: "Remove this row?",
                  description: "It will be excluded from the commit.",
                  inputLabel: "Reason",
                  destructive: true,
                  confirmLabel: "Remove",
                });
                if (confirmed && reason?.trim()) remove.mutate(reason.trim());
              }}
            >
              Remove
            </button>
          )}
        </td>
      </tr>
    </Fragment>
  );
}
