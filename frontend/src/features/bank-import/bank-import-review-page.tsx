"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { Fragment, useState } from "react";
import { toast } from "sonner";
import { getAccount } from "@/features/accounts/api";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem, PartyType } from "@/features/parties/types";
import { ProjectPicker, type ProjectPickerSelection } from "@/features/projects/project-picker";
import { SubmitButton } from "@/components/ui/submit-button";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import {
  commitBankImport,
  discardBankImport,
  getBankImport,
  MAPPING_TARGETS,
  removeStagedRow,
  setRowAllocations,
  type BankImportBatch,
  type MappingTarget,
  type StagedBankRow,
  type StagedProjectAllocation,
} from "./api";
import { BalanceSummary } from "./balance-summary";

const round3 = (n: number) => Math.round((n + Number.EPSILON) * 1000) / 1000;

export function BankImportReviewPage({ batchId }: { batchId: number }) {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();

  const { data: batch, isLoading } = useQuery({
    queryKey: ["bank-import", batchId],
    queryFn: () => getBankImport(batchId),
  });

  const { data: account } = useQuery({
    queryKey: ["accounts", batch?.accountId],
    queryFn: () => getAccount(batch!.accountId),
    enabled: batch !== undefined,
  });

  const refresh = () => queryClient.invalidateQueries({ queryKey: ["bank-import", batchId] });

  const commit = useMutation({
    mutationFn: () => commitBankImport(batchId),
    onSuccess: (r) => {
      toast.success(
        `Committed ${r.committed} transaction(s) — ${r.removed} removed, ${r.duplicate} duplicate, ${r.parseError} parse error`,
      );
      void refresh();
      void queryClient.invalidateQueries({ queryKey: ["accounts"] });
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

  const live = batch.rows.filter((r) => !r.isRemoved);
  // Rows that will land in the account on commit — the same set the server promotes.
  const incoming = live.filter(
    (r) => r.parseState === "Parsed" && r.duplicateOfBankTransactionId === null,
  );
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
          Status {batch.status}. Map every row you keep to a project, vendor, field officer, labour
          team, client or bucket; remove the rest. Nothing is saved to the ledger until you commit.
        </p>
      </div>

      <Counts batch={batch} />

      {account &&
        (isDraft ? (
          <BalanceSummary
            currentBalance={account.statementBalance}
            rows={incoming}
            currentLabel={`${account.name} balance now`}
            resultLabel="Balance after commit"
          />
        ) : (
          <p className="text-sm" data-testid="balance-summary">
            <span className="text-muted-foreground">{account.name} balance:</span>{" "}
            <span className="font-semibold tabular-nums">
              {formatINR(account.statementBalance)}
            </span>
          </p>
        ))}

      <div data-table-scroll className="bg-card overflow-x-auto rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">#</th>
              <th className="p-2 font-medium">Date</th>
              <th className="p-2 font-medium">Narration</th>
              <th className="p-2 font-medium">Debit</th>
              <th className="p-2 font-medium">Credit</th>
              <th className="p-2 font-medium">Mapping</th>
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
                onChanged={refresh}
              />
            ))}
          </tbody>
        </table>
      </div>

      {isDraft && (
        <div className="flex items-center gap-3">
          <SubmitButton
            type="button"
            disabled={!canCommit}
            mutation={commit}
            onClick={() => commit.mutate()}
          >
            Commit {live.filter((r) => r.readyToCommit).length} transaction(s)
          </SubmitButton>
          <Button
            type="button"
            variant="secondary"
            loading={discard.isPending}
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
              {unmapped.length} row(s) still need a mapping
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

const TARGET_LABELS: Record<MappingTarget, string> = {
  Project: "Project",
  Vendor: "Vendor",
  FieldOfficer: "Field officer",
  Labour: "Labour team",
  Client: "Client",
  Personal: "Personal",
  Office: "Office",
  Savings: "Savings",
  Other: "Other",
};

/** Party targets and the party role each one searches; the rest are buckets or a project. */
const PARTY_TYPE: Partial<Record<MappingTarget, PartyType>> = {
  Vendor: "Vendor",
  FieldOfficer: "FieldOfficer",
  Labour: "Subcontractor",
  Client: "Client",
};

type Line = {
  target: MappingTarget;
  project: ProjectPickerSelection | null;
  party: PartySearchItem | null;
  amount: string;
};

const blankLine = (amount = ""): Line => ({
  target: "Project",
  project: null,
  party: null,
  amount,
});

function lineFromAllocation(a: StagedProjectAllocation): Line {
  const partyType = PARTY_TYPE[a.target];
  return {
    target: a.target,
    project: a.projectId !== null ? { id: a.projectId, name: a.projectName } : null,
    party:
      a.partyId !== null
        ? {
            id: a.partyId,
            name: a.partyName,
            types: partyType ? [partyType] : [],
            category: null,
            isActive: true,
          }
        : null,
    amount: String(a.amount),
  };
}

/** A line is complete once it names whatever its target needs. */
function lineComplete(l: Line): boolean {
  if (l.target === "Project") return l.project !== null;
  if (PARTY_TYPE[l.target]) return l.party !== null;
  return true;
}

function describe(a: StagedProjectAllocation): string {
  const label = TARGET_LABELS[a.target] ?? a.target;
  if (a.target === "Project") return a.projectName;
  const who = a.partyName ? `${label}: ${a.partyName}` : label;
  return a.projectName ? `${who} (${a.projectName})` : who;
}

function RowLine({
  batchId,
  row,
  editable,
  onChanged,
}: {
  batchId: number;
  row: StagedBankRow;
  editable: boolean;
  onChanged: () => void;
}) {
  const isCredit = row.credit > 0;
  const target = row.debit > 0 ? row.debit : row.credit;

  const [lines, setLines] = useState<Line[]>(() =>
    row.allocations.length > 0
      ? row.allocations.map(lineFromAllocation)
      : [blankLine(isCredit ? String(target) : "")],
  );

  const updateLine = (i: number, patch: Partial<Line>) =>
    setLines((cur) => cur.map((x, xi) => (xi === i ? { ...x, ...patch } : x)));

  const save = useMutation({
    mutationFn: () =>
      setRowAllocations(
        batchId,
        row.id,
        lines.map((l) => ({
          target: l.target,
          amount: Number(l.amount),
          projectId:
            l.target === "Project" || PARTY_TYPE[l.target] ? (l.project?.id ?? null) : null,
          partyId: PARTY_TYPE[l.target] ? (l.party?.id ?? null) : null,
        })),
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
              {lines.map((l, i) => {
                const partyType = PARTY_TYPE[l.target];
                return (
                  <div key={i} className="flex flex-wrap items-center gap-2">
                    <Select
                      className="h-8 w-32 text-xs"
                      aria-label={`Line ${row.sourceLineNo} map to ${i + 1}`}
                      value={l.target}
                      onChange={(e) =>
                        updateLine(i, {
                          target: e.target.value as MappingTarget,
                          party: null,
                        })
                      }
                    >
                      {MAPPING_TARGETS.map((t) => (
                        <option key={t} value={t}>
                          {TARGET_LABELS[t]}
                        </option>
                      ))}
                    </Select>
                    {partyType && (
                      <div className="w-48">
                        <PartyPicker
                          key={l.target}
                          type={partyType}
                          ariaLabel={`Line ${row.sourceLineNo} ${TARGET_LABELS[l.target].toLowerCase()} ${i + 1}`}
                          selected={l.party}
                          onSelect={(p) => updateLine(i, { party: p })}
                        />
                      </div>
                    )}
                    {(l.target === "Project" || partyType) && (
                      <div className="w-48">
                        <ProjectPicker
                          status="Ongoing"
                          ariaLabel={`Line ${row.sourceLineNo} project ${i + 1}${
                            partyType ? " (optional)" : ""
                          }`}
                          selected={l.project}
                          onSelect={(p) =>
                            updateLine(i, { project: p ? { id: p.id, name: p.name } : null })
                          }
                        />
                      </div>
                    )}
                    <AmountInput
                      className="w-28"
                      aria-label={`Line ${row.sourceLineNo} amount ${i + 1}`}
                      value={l.amount}
                      onChange={(v) => updateLine(i, { amount: v })}
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
                );
              })}
              {!isCredit && (
                <button
                  type="button"
                  className="text-xs underline"
                  onClick={() => setLines((cur) => [...cur, blankLine()])}
                >
                  + line
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
              <SubmitButton
                type="button"
                className="h-7 px-2 text-xs"
                disabled={!balanced || !lines.every(lineComplete)}
                mutation={save}
                onClick={() => save.mutate()}
              >
                Save mapping
              </SubmitButton>
            </div>
          ) : (
            <ul className="text-xs">
              {row.allocations.map((a, i) => (
                <li key={i} className="tabular-nums">
                  {describe(a)}: {formatINR(a.amount)}
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
