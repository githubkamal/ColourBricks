"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useSearchParams } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { listProjects } from "@/features/projects/api";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { useDebouncedValue } from "@/lib/use-debounced-value";
import { formatDate, formatINR } from "@/lib/format";
import {
  bulkExclude,
  holdTransaction,
  mapDebit,
  pairInternalTransfer,
  reconciliationQueue,
  reconcileCredit,
  transferSuggestions,
  unholdTransaction,
  unreconcile,
  type ReconciliationAllocationTarget,
  type ReconciliationRow,
} from "./api";

const round3 = (n: number) => Math.round((n + Number.EPSILON) * 1000) / 1000;
const BRD_COLUMNS = [
  "Date",
  "Bank",
  "Description",
  "Type",
  "Credit",
  "Debit",
  "Client/Vendor",
  "Project(s)",
  "Allocated",
  "Difference",
  "Status",
  "Action",
];

export function ReconciliationPage() {
  const queryClient = useQueryClient();
  const searchParams = useSearchParams();
  const { confirm, dialog } = useConfirmDialog();
  const [accountId, setAccountId] = useState<number | "">("");
  // Deep-linkable via `?status=` (e.g. the Reconciled/Excluded Transactions nav
  // items) so a link can drop the user straight onto a filtered view of this
  // same queue instead of always defaulting to Pending.
  const [status, setStatus] = useState(() => searchParams?.get("status") || "Pending");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");
  const [search, setSearch] = useState("");
  const debouncedSearch = useDebouncedValue(search, 300);
  const [selected, setSelected] = useState<Set<number>>(new Set());
  const [mapRow, setMapRow] = useState<ReconciliationRow | null>(null);

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", "recon"],
    queryFn: () => listAccounts(),
  });
  const { data: queue } = useQuery({
    queryKey: ["reconciliation", accountId, status, dateFrom, dateTo, debouncedSearch],
    queryFn: () =>
      reconciliationQueue({
        accountId: accountId || null,
        status: status || null,
        dateFrom: dateFrom || null,
        dateTo: dateTo || null,
        search: debouncedSearch || null,
      }),
  });
  const { data: transfers = [] } = useQuery({
    queryKey: ["transfer-suggestions", accountId],
    queryFn: () => transferSuggestions(accountId || null),
  });

  const refresh = () => {
    void queryClient.invalidateQueries({ queryKey: ["reconciliation"] });
    void queryClient.invalidateQueries({ queryKey: ["transfer-suggestions"] });
    setSelected(new Set());
    setMapRow(null);
  };

  const excludeMut = useMutation({
    mutationFn: (reason: string) => bulkExclude([...selected], reason),
    onSuccess: () => {
      toast.success("Excluded");
      refresh();
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not exclude"),
  });

  const deleteOneMut = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) => bulkExclude([id], reason),
    onSuccess: () => {
      toast.success("Deleted");
      refresh();
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not delete"),
  });

  const holdMut = useMutation({
    mutationFn: (id: number) => holdTransaction(id),
    onSuccess: () => {
      toast.success("On hold");
      refresh();
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not hold"),
  });

  const unholdMut = useMutation({
    mutationFn: (id: number) => unholdTransaction(id),
    onSuccess: () => {
      toast.success("Unheld");
      refresh();
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not unhold"),
  });

  const unrecMut = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) => unreconcile(id, reason),
    onSuccess: () => {
      toast.success("Unreconciled");
      refresh();
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not unreconcile"),
  });

  const pairMut = useMutation({
    mutationFn: ({ d, c }: { d: number; c: number }) => pairInternalTransfer(d, c),
    onSuccess: () => {
      toast.success("Marked as internal transfer");
      refresh();
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not pair"),
  });

  const rows = queue?.items ?? [];

  return (
    <div className="max-w-6xl space-y-6">
      {dialog}
      <h1 className="text-lg font-semibold">Bank reconciliation</h1>

      <div className="flex flex-wrap items-end gap-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">Account</span>
          <select
            className="bg-card block rounded border px-3 py-1.5 text-sm"
            aria-label="Account"
            value={accountId}
            onChange={(e) => setAccountId(e.target.value ? Number(e.target.value) : "")}
          >
            <option value="">All accounts</option>
            {accounts.map((a) => (
              <option key={a.id} value={a.id}>
                {a.name}
              </option>
            ))}
          </select>
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Status</span>
          <select
            className="bg-card block rounded border px-3 py-1.5 text-sm"
            aria-label="Status"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            {["", "Pending", "InReview", "Reconciled", "Excluded", "InternalTransfer"].map((s) => (
              <option key={s} value={s}>
                {s || "Any"}
              </option>
            ))}
          </select>
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">From</span>
          <Input
            type="date"
            aria-label="From"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">To</span>
          <Input
            type="date"
            aria-label="To"
            value={dateTo}
            onChange={(e) => setDateTo(e.target.value)}
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Search</span>
          <Input aria-label="Search" value={search} onChange={(e) => setSearch(e.target.value)} />
        </label>
      </div>

      {selected.size > 0 && (
        <BulkExcludeBar count={selected.size} onExclude={(r) => excludeMut.mutate(r)} />
      )}

      {transfers.length > 0 && (
        <div className="border-attention/40 bg-attention/5 space-y-2 rounded border p-3 text-sm">
          <p className="font-medium">Suggested internal transfers</p>
          {transfers.map((t) => (
            <div
              key={`${t.debitTransactionId}-${t.creditTransactionId}`}
              className="flex items-center gap-3"
            >
              <span>
                {formatINR(t.amount)} on {formatDate(t.date)} — debit #{t.debitTransactionId} ↔
                credit #{t.creditTransactionId}
              </span>
              <Button
                type="button"
                className="h-7 px-2 text-xs"
                onClick={() =>
                  pairMut.mutate({ d: t.debitTransactionId, c: t.creditTransactionId })
                }
              >
                Mark as transfer
              </Button>
            </div>
          ))}
        </div>
      )}

      <div className="bg-card overflow-x-auto rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2" />
              {BRD_COLUMNS.map((c) => (
                <th key={c} className="p-2 font-medium">
                  {c}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr>
                <td
                  colSpan={BRD_COLUMNS.length + 1}
                  className="text-muted-foreground p-3 text-center"
                >
                  No transactions match the filters.
                </td>
              </tr>
            )}
            {rows.map((row) => (
              <QueueRow
                key={row.id}
                row={row}
                selected={selected.has(row.id)}
                onToggleSelect={() =>
                  setSelected((s) => {
                    const n = new Set(s);
                    if (n.has(row.id)) n.delete(row.id);
                    else n.add(row.id);
                    return n;
                  })
                }
                onMap={() => setMapRow(row)}
                onDelete={async () => {
                  const { confirmed, value: reason } = await confirm({
                    title: "Delete this row?",
                    description: "This cannot be undone.",
                    inputLabel: "Reason",
                    destructive: true,
                    confirmLabel: "Delete",
                  });
                  if (!confirmed) return;
                  deleteOneMut.mutate({ id: row.id, reason: reason ?? "" });
                }}
                onHold={() => holdMut.mutate(row.id)}
                onUnhold={() => unholdMut.mutate(row.id)}
                onUnreconcile={(reason) => unrecMut.mutate({ id: row.id, reason })}
              />
            ))}
          </tbody>
        </table>
      </div>

      <Dialog open={mapRow !== null} onOpenChange={(open) => !open && setMapRow(null)}>
        <DialogContent>
          {mapRow && (
            <>
              <DialogTitle>
                Map {mapRow.type.toLowerCase()} — {formatINR(mapRow.debit || mapRow.credit)}
              </DialogTitle>
              <div className="mt-3">
                <MapPanel row={mapRow} onDone={refresh} />
              </div>
            </>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}

function BulkExcludeBar({
  count,
  onExclude,
}: {
  count: number;
  onExclude: (reason: string) => void;
}) {
  const [reason, setReason] = useState("");
  return (
    <div className="bg-card flex items-center gap-3 rounded border p-3 text-sm">
      <span className="font-medium">{count} selected</span>
      <Input
        aria-label="Exclude reason"
        placeholder="Reason"
        value={reason}
        onChange={(e) => setReason(e.target.value)}
      />
      <Button
        type="button"
        variant="destructive"
        disabled={reason.trim() === ""}
        onClick={() => onExclude(reason.trim())}
      >
        Exclude {count}
      </Button>
    </div>
  );
}

function QueueRow({
  row,
  selected,
  onToggleSelect,
  onMap,
  onDelete,
  onHold,
  onUnhold,
  onUnreconcile,
}: {
  row: ReconciliationRow;
  selected: boolean;
  onToggleSelect: () => void;
  onMap: () => void;
  onDelete: () => void;
  onHold: () => void;
  onUnhold: () => void;
  onUnreconcile: (reason: string) => void;
}) {
  const { confirm, dialog } = useConfirmDialog();
  const mappable = row.status === "Pending" || row.status === "InReview";
  return (
    <>
      {dialog}
      <tr className="border-b last:border-0">
        <td className="p-2">
          {row.status !== "Reconciled" && (
            <input
              type="checkbox"
              aria-label={`Select row ${row.id}`}
              checked={selected}
              onChange={onToggleSelect}
            />
          )}
        </td>
        <td className="p-2">{formatDate(row.date)}</td>
        <td className="p-2">{row.bank}</td>
        <td className="p-2">{row.description}</td>
        <td className="p-2">{row.type}</td>
        <td className="p-2 tabular-nums">{row.credit > 0 ? formatINR(row.credit) : "—"}</td>
        <td className="p-2 tabular-nums">{row.debit > 0 ? formatINR(row.debit) : "—"}</td>
        <td className="p-2">{row.counterparty ?? "—"}</td>
        <td className="p-2">{row.projects || "—"}</td>
        <td className="p-2 tabular-nums">{formatINR(row.allocated)}</td>
        <td
          className={
            Math.abs(row.difference) < 0.0005
              ? "p-2 tabular-nums"
              : "text-attention p-2 font-medium tabular-nums"
          }
          data-testid={`difference-${row.id}`}
        >
          {formatINR(row.difference)}
        </td>
        <td className="p-2">{row.status === "InReview" ? "On hold" : row.status}</td>
        <td className="p-2">
          <div className="flex flex-wrap gap-2">
            {mappable && (
              <>
                <button type="button" className="text-xs underline" onClick={onMap}>
                  Map
                </button>
                <button type="button" className="text-negative text-xs" onClick={onDelete}>
                  Delete
                </button>
                {row.status === "InReview" ? (
                  <button type="button" className="text-xs underline" onClick={onUnhold}>
                    Unhold
                  </button>
                ) : (
                  <button type="button" className="text-xs underline" onClick={onHold}>
                    Hold
                  </button>
                )}
              </>
            )}
            {row.status === "Reconciled" && (
              <button
                type="button"
                className="text-negative text-xs"
                onClick={async () => {
                  const { confirmed, value: reason } = await confirm({
                    title: "Unreconcile this transaction?",
                    inputLabel: "Reason",
                    destructive: true,
                    confirmLabel: "Unreconcile",
                  });
                  if (!confirmed) return;
                  onUnreconcile(reason ?? "");
                }}
              >
                Unreconcile
              </button>
            )}
          </div>
        </td>
      </tr>
    </>
  );
}

function MapPanel({ row, onDone }: { row: ReconciliationRow; onDone: () => void }) {
  if (row.type === "Credit") {
    return <CreditMapForm row={row} onDone={onDone} />;
  }
  return <DebitSplitForm row={row} onDone={onDone} />;
}

function CreditMapForm({ row, onDone }: { row: ReconciliationRow; onDone: () => void }) {
  const [client, setClient] = useState<PartySearchItem | null>(null);
  const [projectId, setProjectId] = useState<number | "">("");

  const { data: projects } = useQuery({
    queryKey: ["projects", "recon-panel"],
    queryFn: () => listProjects({ status: "Ongoing", pageSize: 200 }),
  });

  const creditMut = useMutation({
    mutationFn: () =>
      reconcileCredit(row.id, { clientId: client!.id, projectId: Number(projectId) }),
    onSuccess: () => {
      toast.success("Reconciled");
      onDone();
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Reconcile failed"),
  });

  return (
    <div className="flex flex-wrap items-end gap-3">
      <PartyPicker type="Client" label="Client" selected={client} onSelect={setClient} />
      <label className="space-y-1">
        <span className="text-sm font-medium">Project</span>
        <select
          className="bg-card block rounded border px-3 py-1.5 text-sm"
          aria-label="Project"
          value={projectId}
          onChange={(e) => setProjectId(e.target.value ? Number(e.target.value) : "")}
        >
          <option value="">Select…</option>
          {(projects?.items ?? []).map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      </label>
      <Button
        type="button"
        disabled={!client || projectId === "" || creditMut.isPending}
        onClick={() => creditMut.mutate()}
      >
        Reconcile {formatINR(row.credit)}
      </Button>
    </div>
  );
}

interface SplitLine {
  target: ReconciliationAllocationTarget;
  amount: string;
  description: string;
  project: { id: number; name: string } | null;
  vendor: PartySearchItem | null;
}

function emptyLine(): SplitLine {
  return { target: "Vendor", amount: "", description: "", project: null, vendor: null };
}

// Vendor and FieldOfficer both need a party (and an optional project — no project
// means an advance); CustomWork needs both, mandatorily; BankCharges needs a project
// but no party (a bank fee has no counterparty); the rest (Personal/Office/Savings/
// Custom) are plain company buckets with neither.
const isPartyTarget = (t: ReconciliationAllocationTarget) => t === "Vendor" || t === "FieldOfficer";
const showsPartyPicker = (t: ReconciliationAllocationTarget) =>
  isPartyTarget(t) || t === "CustomWork";
const showsProjectPicker = (t: ReconciliationAllocationTarget) =>
  showsPartyPicker(t) || t === "BankCharges";
const projectRequired = (t: ReconciliationAllocationTarget) =>
  t === "CustomWork" || t === "BankCharges";

// A single bank debit split across a vendor or field officer (with/without a project)
// and/or Personal/Office/Savings/Custom common expenses (client request, 2026-09-04).
// Each split line has its own description, and the lines must sum to exactly the bank
// debit (rule 48).
function DebitSplitForm({ row, onDone }: { row: ReconciliationRow; onDone: () => void }) {
  const [lines, setLines] = useState<SplitLine[]>(
    row.projectDetail.length > 0
      ? row.projectDetail.map((h) => ({
          target: "Vendor" as const,
          amount: String(h.amount),
          description: "",
          project: { id: h.projectId, name: h.projectName },
          vendor: null,
        }))
      : [emptyLine()],
  );

  const { data: projects } = useQuery({
    queryKey: ["projects", "recon-panel"],
    queryFn: () => listProjects({ status: "Ongoing", pageSize: 200 }),
  });

  const update = (i: number, patch: Partial<SplitLine>) =>
    setLines((cur) => cur.map((l, li) => (li === i ? { ...l, ...patch } : l)));

  const allocated = round3(lines.reduce((s, l) => s + (Number(l.amount) || 0), 0));
  const difference = round3(row.debit - allocated);
  const balanced = Math.abs(difference) < 0.0005;
  const linesValid = lines.every(
    (l) =>
      Number(l.amount) > 0 &&
      l.description.trim() !== "" &&
      (!showsPartyPicker(l.target) || l.vendor !== null) &&
      (!projectRequired(l.target) || l.project !== null),
  );

  const mapMut = useMutation({
    mutationFn: () =>
      mapDebit(
        row.id,
        lines.map((l) => ({
          target: l.target,
          amount: Number(l.amount),
          description: l.description.trim(),
          projectId: showsProjectPicker(l.target) ? (l.project?.id ?? null) : null,
          vendorId: showsPartyPicker(l.target) ? (l.vendor?.id ?? null) : null,
        })),
      ),
    onSuccess: () => {
      toast.success("Mapped");
      onDone();
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Map failed"),
  });

  return (
    <div className="space-y-3">
      {lines.map((l, i) => (
        <div key={i} className="bg-card space-y-2 rounded border p-2">
          <div className="flex flex-wrap items-end gap-2">
            <label className="space-y-1">
              <span className="text-sm font-medium">Target</span>
              <select
                className="bg-card block rounded border px-3 py-1.5 text-sm"
                aria-label={`Target ${i + 1}`}
                value={l.target}
                onChange={(e) =>
                  update(i, {
                    target: e.target.value as ReconciliationAllocationTarget,
                    project: null,
                    vendor: null,
                  })
                }
              >
                <option value="Vendor">Vendor</option>
                <option value="FieldOfficer">Field officer</option>
                <option value="CustomWork">Custom work</option>
                <option value="BankCharges">Bank charges</option>
                <option value="Personal">Personal</option>
                <option value="Office">Office</option>
                <option value="Savings">Savings</option>
                <option value="Custom">Custom</option>
              </select>
            </label>
            <AmountInput
              className="w-32"
              aria-label={`Amount ${i + 1}`}
              placeholder="Amount"
              value={l.amount}
              onChange={(v) => update(i, { amount: v })}
            />
            <Input
              className="w-56"
              aria-label={`Description ${i + 1}`}
              placeholder="Description"
              value={l.description}
              onChange={(e) => update(i, { description: e.target.value })}
            />
            {lines.length > 1 && (
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={() => setLines((cur) => cur.filter((_, li) => li !== i))}
              >
                Remove
              </Button>
            )}
          </div>
          {(showsPartyPicker(l.target) || showsProjectPicker(l.target)) && (
            <div className="flex flex-wrap items-end gap-2">
              {showsPartyPicker(l.target) && (
                <PartyPicker
                  type={
                    l.target === "FieldOfficer"
                      ? "FieldOfficer"
                      : l.target === "Vendor"
                        ? "Vendor"
                        : undefined
                  }
                  label={`${l.target === "FieldOfficer" ? "Field officer" : l.target === "CustomWork" ? "Party" : "Vendor"} ${i + 1}`}
                  selected={l.vendor}
                  onSelect={(v) => update(i, { vendor: v })}
                />
              )}
              {showsProjectPicker(l.target) && (
                <label className="space-y-1">
                  <span className="text-sm font-medium">
                    {projectRequired(l.target) ? "Project" : "Project (optional — none = advance)"}
                  </span>
                  <select
                    className="bg-card block rounded border px-3 py-1.5 text-sm"
                    aria-label={`Project ${i + 1}`}
                    value={l.project?.id ?? ""}
                    onChange={(e) => {
                      const p = (projects?.items ?? []).find(
                        (x) => x.id === Number(e.target.value),
                      );
                      update(i, { project: p ? { id: p.id, name: p.name } : null });
                    }}
                  >
                    <option value="">
                      {projectRequired(l.target) ? "Select…" : "No project (advance)"}
                    </option>
                    {(projects?.items ?? []).map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.name}
                      </option>
                    ))}
                  </select>
                </label>
              )}
            </div>
          )}
        </div>
      ))}

      <Button
        type="button"
        variant="secondary"
        size="sm"
        onClick={() => setLines((cur) => [...cur, emptyLine()])}
      >
        Add split
      </Button>

      <p
        className={balanced ? "text-muted-foreground text-xs" : "text-attention text-xs"}
        data-testid="split-difference"
      >
        allocated {formatINR(allocated)} of {formatINR(row.debit)} — difference{" "}
        {formatINR(difference)}
      </p>

      <Button
        type="button"
        disabled={!balanced || !linesValid || lines.length === 0 || mapMut.isPending}
        title={balanced ? undefined : "The split must equal the bank debit"}
        onClick={() => mapMut.mutate()}
      >
        Submit
      </Button>
    </div>
  );
}
