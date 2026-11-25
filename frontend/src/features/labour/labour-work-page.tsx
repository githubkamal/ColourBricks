"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { getProject } from "@/features/projects/api";
import { reconcileDebit, type ReconciliationRow } from "@/features/reconciliation/api";
import { BankTransactionPicker } from "@/features/reconciliation/bank-transaction-picker";
import { SubmitButton } from "@/components/ui/submit-button";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { AmountInput } from "@/components/ui/amount-input";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { Select } from "@/components/ui/select";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import { cn } from "@/lib/utils";
import { useQueryParamNumber } from "@/lib/use-query-param";
import {
  deleteWork,
  listWork,
  payWork,
  PAYMENT_FREQUENCIES,
  type PaymentFrequency,
  type WorkEntry,
} from "./api";
import { WorkEntryForm } from "./work-entry-form";

export function LabourWorkPage() {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();
  const [projectId] = useQueryParamNumber("projectId", 0);
  const { data: deepLinkedProject } = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => getProject(projectId),
    enabled: projectId > 0,
  });

  const [editing, setEditing] = useState<WorkEntry | null>(null);
  // Bumped after each save so a fresh (empty) form mounts.
  const [formKey, setFormKey] = useState(0);
  const [search, setSearch] = useState("");
  const formRef = useRef<HTMLDivElement>(null);

  const { data: work = [], isPending } = useQuery({
    queryKey: ["labour-work", "all"],
    queryFn: () => listWork(),
  });

  // Deleted entries (and the old versions of edited ones) are history, not work to show.
  const term = search.trim().toLowerCase();
  const active = work.filter((w) => w.status === "Active");
  const shown = term
    ? active.filter((w) =>
        [w.referenceNumber, w.teamName, w.projectName, w.projectCode, w.workType, w.description]
          .filter((v): v is string => !!v)
          .some((v) => v.toLowerCase().includes(term)),
      )
    : active;
  const { pageRows, page, setPage, pageCount, total } = usePagination(shown, 20);

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["labour-work"] });
    void queryClient.invalidateQueries({ queryKey: ["team-statement"] });
  };

  function startEdit(entry: WorkEntry) {
    setEditing(entry);
    formRef.current?.scrollIntoView({ behavior: "smooth", block: "start" });
  }

  async function remove(entry: WorkEntry) {
    const { confirmed } = await confirm({
      title: `Delete ${entry.referenceNumber ?? "this work entry"}?`,
      description:
        entry.totalPaid > 0
          ? `This also deletes the ${formatINR(entry.totalPaid)} paid against it. The books keep a reversal record.`
          : "The books keep a reversal record.",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (!confirmed) return;
    try {
      await deleteWork(entry.id);
      toast.success(`${entry.referenceNumber ?? "Work entry"} deleted`);
      if (editing?.id === entry.id) setEditing(null);
      invalidate();
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Could not delete the work entry");
    }
  }

  const defaultProject = deepLinkedProject
    ? { id: deepLinkedProject.id, name: deepLinkedProject.name, code: deepLinkedProject.code }
    : null;

  return (
    <div className="max-w-4xl space-y-6">
      {dialog}
      <PageHeader title="Labour & Subcontractor Work" />

      <div ref={formRef} className="scroll-mt-4">
        <WorkEntryForm
          key={editing ? `edit-${editing.id}` : `new-${formKey}-${defaultProject?.id ?? 0}`}
          entry={editing}
          defaultProject={defaultProject}
          onSaved={() => {
            setEditing(null);
            setFormKey((k) => k + 1);
            invalidate();
          }}
          onCancel={editing ? () => setEditing(null) : undefined}
        />
      </div>

      <div className="space-y-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h2 className="text-base font-semibold">Work entries</h2>
          <Input
            className="w-64"
            value={search}
            onChange={(e) => {
              setSearch(e.target.value);
              setPage(1);
            }}
            placeholder="Search ref no, team, project…"
            aria-label="Search work entries"
          />
        </div>
        {dataState({
          isPending,
          isEmpty: shown.length === 0,
          emptyLabel: term ? "No work entries match." : "No work entries yet.",
        })}
        {pageRows.map((entry) => (
          <WorkRow
            key={entry.id}
            entry={entry}
            isEditing={editing?.id === entry.id}
            onPaid={invalidate}
            onEdit={startEdit}
            onDelete={remove}
          />
        ))}
      </div>

      <PaginationBar
        page={page}
        pageCount={pageCount}
        total={total}
        onPageChange={setPage}
        itemLabel="work entries"
      />
    </div>
  );
}

function WorkRow({
  entry,
  isEditing,
  onPaid,
  onEdit,
  onDelete,
}: {
  entry: WorkEntry;
  isEditing: boolean;
  onPaid: () => void;
  onEdit: (entry: WorkEntry) => void;
  onDelete: (entry: WorkEntry) => void;
}) {
  const queryClient = useQueryClient();
  const [paying, setPaying] = useState(false);
  const [amount, setAmount] = useState("");
  const [frequency, setFrequency] = useState<PaymentFrequency>("Weekly");
  const [date, setDate] = useState("");
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | "">("");
  const [bankTx, setBankTx] = useState<ReconciliationRow | null>(null);

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", { all: true }],
    queryFn: () => listAccounts(),
  });

  const pay = useMutation({
    mutationFn: () =>
      payWork(entry.id, {
        date,
        amount: Number(amount),
        frequency,
        paymentModeId: paymentModeId!,
        accountId: accountId === "" ? null : accountId,
      }),
    onSuccess: async (result) => {
      toast.success("Payment recorded");
      setPaying(false);
      setAmount("");
      onPaid();
      if (bankTx) {
        try {
          await reconcileDebit(bankTx.id, { existingPaymentId: result.id });
          toast.success("Linked to the bank transaction");
          void queryClient.invalidateQueries({ queryKey: ["reconciliation"] });
        } catch (error) {
          toast.error(
            error instanceof ApiError
              ? `Payment saved, but couldn't link the bank transaction: ${error.message}`
              : "Payment saved, but couldn't link the bank transaction — link it from the Reconciliation Queue instead.",
          );
        }
        setBankTx(null);
      }
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError ? (error.fieldErrors?.amount?.[0] ?? error.message) : "Failed",
      ),
  });

  return (
    <div
      className={cn(
        "bg-card border-border rounded-xl border p-3 text-sm shadow-xs",
        isEditing && "ring-ring/40 ring-2",
      )}
    >
      <div className="flex flex-wrap items-center justify-between gap-2">
        <span className="space-x-1">
          {entry.referenceNumber && (
            <Badge variant="primary" data-testid={`ref-${entry.id}`}>
              {entry.referenceNumber}
            </Badge>
          )}
          <span>
            {formatDate(entry.date)} · {entry.teamName}
          </span>
          {entry.projectName && (
            <span className="text-muted-foreground">
              · {entry.projectCode ? `${entry.projectCode} — ` : ""}
              {entry.projectName}
            </span>
          )}
          {entry.workType && <span className="text-muted-foreground"> · {entry.workType}</span>}
        </span>
        <span className="flex items-center gap-2 tabular-nums">
          Agreed {formatINR(entry.agreedValue)} · Paid {formatINR(entry.totalPaid)}
          <Badge
            variant={entry.outstanding > 0 ? "amber" : "teal"}
            data-testid={`outstanding-${entry.id}`}
          >
            Outstanding {formatINR(entry.outstanding)}
          </Badge>
        </span>
      </div>
      {entry.description && <p className="text-muted-foreground mt-1">{entry.description}</p>}
      {entry.status === "Active" && (
        <div className="mt-2">
          {paying && entry.outstanding > 0 ? (
            <form
              className="flex flex-wrap items-end gap-2"
              onSubmit={(e) => {
                e.preventDefault();
                if (date && Number(amount) > 0 && paymentModeId !== null) pay.mutate();
              }}
            >
              <label className="space-y-1">
                <span className="text-xs">Date</span>
                <Input
                  type="date"
                  value={date}
                  onChange={(e) => setDate(e.target.value)}
                  aria-label={`Pay date ${entry.id}`}
                />
              </label>
              <label className="space-y-1">
                <span className="text-xs">Amount</span>
                <AmountInput
                  className="w-28"
                  value={amount}
                  onChange={setAmount}
                  aria-label={`Pay amount ${entry.id}`}
                />
              </label>
              <label className="space-y-1">
                <span className="text-xs">Frequency</span>
                <Select
                  value={frequency}
                  aria-label={`Pay frequency ${entry.id}`}
                  onChange={(e) => setFrequency(e.target.value as PaymentFrequency)}
                >
                  {PAYMENT_FREQUENCIES.map((f) => (
                    <option key={f} value={f}>
                      {f}
                    </option>
                  ))}
                </Select>
              </label>
              <PaymentModeSelect
                value={paymentModeId}
                onChange={(m) => setPaymentModeId(m?.id ?? null)}
              />
              <label className="space-y-1">
                <span className="text-xs">Account</span>
                <Select
                  value={accountId}
                  aria-label={`Pay account ${entry.id}`}
                  onChange={(e) => setAccountId(e.target.value ? Number(e.target.value) : "")}
                >
                  <option value="">None</option>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
                </Select>
              </label>
              <div className="w-56">
                <BankTransactionPicker
                  selected={bankTx}
                  onSelect={setBankTx}
                  accountId={accountId === "" ? null : accountId}
                />
              </div>
              <SubmitButton type="submit" size="xs" mutation={pay}>
                Save payment
              </SubmitButton>
              <Button type="button" size="xs" variant="ghost" onClick={() => setPaying(false)}>
                Cancel
              </Button>
            </form>
          ) : (
            <div className="flex gap-1">
              {entry.outstanding > 0 && (
                <Button type="button" size="xs" variant="ghost" onClick={() => setPaying(true)}>
                  Add payment
                </Button>
              )}
              <Button type="button" size="xs" variant="ghost" onClick={() => onEdit(entry)}>
                Edit
              </Button>
              <Button
                type="button"
                size="xs"
                variant="ghost"
                className="text-destructive"
                onClick={() => onDelete(entry)}
              >
                Delete
              </Button>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
