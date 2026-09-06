"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { ProjectPicker } from "@/features/projects/project-picker";
import type { ProjectListItem } from "@/features/projects/types";
import { reconcileDebit, type ReconciliationRow } from "@/features/reconciliation/api";
import { BankTransactionPicker } from "@/features/reconciliation/bank-transaction-picker";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { FieldLabel } from "@/components/ui/field-label";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApiError } from "@/lib/api";
import { usePagination } from "@/lib/use-pagination";
import { formatDate, formatINR } from "@/lib/format";
import {
  listCustomWork,
  payCustomWork,
  recordCustomWork,
  reverseCustomWork,
  type CustomWork,
} from "./api";

export function CustomWorkPage() {
  const [project, setProject] = useState<ProjectListItem | null>(null);

  return (
    <div className="max-w-3xl space-y-6">
      <PageHeader title="Customized / Ad-hoc Work" />

      <div className="bg-card max-w-xs rounded border p-4">
        <ProjectPicker selected={project} onSelect={setProject} label="Project" />
      </div>

      {project && <CustomWorkForm projectId={project.id} />}
    </div>
  );
}

function CustomWorkForm({ projectId }: { projectId: number }) {
  const queryClient = useQueryClient();
  const [party, setParty] = useState<PartySearchItem | null>(null);
  const [date, setDate] = useState("");
  const [workType, setWorkType] = useState("");
  const [estimatedCost, setEstimatedCost] = useState("");
  const [actualCost, setActualCost] = useState("");
  const [description, setDescription] = useState("");
  const [touchedDate, setTouchedDate] = useState(false);
  const [touchedActual, setTouchedActual] = useState(false);

  const { data: entries = [] } = useQuery({
    queryKey: ["custom-work", projectId],
    queryFn: () => listCustomWork(projectId),
  });
  const { pageRows: pagedEntries, page, setPage, pageCount, total } = usePagination(entries, 20);

  const record = useMutation({
    mutationFn: () =>
      recordCustomWork({
        projectId,
        date,
        estimatedCost: Number(estimatedCost) || 0,
        actualCost: Number(actualCost),
        partyId: party?.id ?? null,
        workType: workType.trim() || null,
        description: description.trim() || null,
      }),
    onSuccess: () => {
      toast.success("Custom work recorded");
      setActualCost("");
      setEstimatedCost("");
      setWorkType("");
      setDescription("");
      setTouchedDate(false);
      setTouchedActual(false);
      void queryClient.invalidateQueries({ queryKey: ["custom-work", projectId] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not record the work"),
  });

  const variancePreview = (Number(actualCost) || 0) - (Number(estimatedCost) || 0);
  const ready = date !== "" && Number(actualCost) > 0;

  return (
    <div className="space-y-6">
      <form
        className="bg-card space-y-3 rounded border p-4"
        onSubmit={(e) => {
          e.preventDefault();
          if (ready) record.mutate();
        }}
      >
        <PartyPicker
          label="Vendor / subcontractor (optional)"
          selected={party}
          onSelect={setParty}
        />
        <div className="flex flex-wrap gap-3">
          <label className="space-y-1">
            <FieldLabel required error={touchedDate && date === "" ? "Required" : undefined}>
              Work date
            </FieldLabel>
            <Input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              onBlur={() => setTouchedDate(true)}
              aria-label="Work date"
            />
          </label>
          <label className="space-y-1">
            <FieldLabel>Work type</FieldLabel>
            <Input
              value={workType}
              onChange={(e) => setWorkType(e.target.value)}
              aria-label="Work type"
            />
          </label>
          <label className="space-y-1">
            <FieldLabel>Estimated cost</FieldLabel>
            <AmountInput
              value={estimatedCost}
              onChange={setEstimatedCost}
              aria-label="Estimated cost"
            />
          </label>
          <label className="space-y-1">
            <FieldLabel
              required
              error={
                touchedActual && Number(actualCost) <= 0 ? "Must be greater than zero" : undefined
              }
            >
              Actual cost
            </FieldLabel>
            <AmountInput
              value={actualCost}
              onChange={setActualCost}
              onBlur={() => setTouchedActual(true)}
              aria-label="Actual cost"
            />
          </label>
        </div>
        <label className="block space-y-1">
          <FieldLabel>Description</FieldLabel>
          <Input
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            aria-label="Description"
          />
        </label>
        <p className="text-muted-foreground text-xs" data-testid="variance-preview">
          Variance (actual − estimated): {formatINR(variancePreview)}
        </p>
        <Button type="submit" disabled={!ready || record.isPending}>
          Record custom work
        </Button>
      </form>

      {dataState({ isEmpty: entries.length === 0, emptyLabel: "No custom work yet." }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Date</th>
                <th className="p-2 font-medium">Work type</th>
                <th className="p-2 font-medium">Party</th>
                <th className="p-2 font-medium">Estimated</th>
                <th className="p-2 font-medium">Actual</th>
                <th className="p-2 font-medium">Variance</th>
                <th className="p-2 font-medium">Status</th>
                <th className="p-2" />
              </tr>
            </thead>
            <tbody>
              {pagedEntries.map((w) => (
                <CustomWorkRow key={w.id} work={w} projectId={projectId} />
              ))}
            </tbody>
          </table>
        </div>
      )}

      <PaginationBar
        page={page}
        pageCount={pageCount}
        total={total}
        onPageChange={setPage}
        itemLabel="entries"
      />
    </div>
  );
}

function CustomWorkRow({ work: w, projectId }: { work: CustomWork; projectId: number }) {
  const queryClient = useQueryClient();
  const [paying, setPaying] = useState(false);
  const { confirm, dialog } = useConfirmDialog();

  const invalidate = () =>
    void queryClient.invalidateQueries({ queryKey: ["custom-work", projectId] });

  const reverse = useMutation({
    mutationFn: (reason: string) => reverseCustomWork(w.id, reason),
    onSuccess: () => {
      toast.success("Reversed");
      invalidate();
    },
    onError: (error) => {
      toast.error(error instanceof ApiError ? error.message : "Could not reverse");
    },
  });

  return (
    <>
      {dialog}
      <tr className="border-b last:border-0">
        <td className="p-2">{formatDate(w.date)}</td>
        <td className="p-2">{w.workType ?? "—"}</td>
        <td className="p-2">{w.partyName ?? "—"}</td>
        <td className="text-muted-foreground p-2 tabular-nums">{formatINR(w.estimatedCost)}</td>
        <td className="p-2 tabular-nums">{formatINR(w.actualCost)}</td>
        <td
          className={
            w.variance > 0
              ? "text-attention p-2 tabular-nums"
              : "text-muted-foreground p-2 tabular-nums"
          }
        >
          {formatINR(w.variance)}
        </td>
        <td className="p-2">
          <StatusBadge status={w.status} />
        </td>
        <td className="p-2">
          {w.status === "Active" && (
            <div className="flex gap-2">
              {w.partyId && (
                <button
                  type="button"
                  className="text-xs underline"
                  onClick={() => setPaying((v) => !v)}
                >
                  {paying ? "Close" : "Pay"}
                </button>
              )}
              <button
                type="button"
                className="text-negative text-xs"
                onClick={async () => {
                  const { confirmed, value: reason } = await confirm({
                    title: "Reverse this custom work record?",
                    description: "This cannot be undone.",
                    inputLabel: "Reason",
                    destructive: true,
                    confirmLabel: "Reverse",
                  });
                  if (confirmed && reason) reverse.mutate(reason);
                }}
              >
                Reverse
              </button>
            </div>
          )}
        </td>
      </tr>
      {paying && w.partyId && (
        <tr className="bg-secondary/30 border-b">
          <td colSpan={8} className="p-3">
            <CustomWorkPaymentForm
              projectId={projectId}
              partyId={w.partyId}
              customWorkId={w.id}
              onDone={() => {
                setPaying(false);
                invalidate();
              }}
            />
          </td>
        </tr>
      )}
    </>
  );
}

function CustomWorkPaymentForm({
  projectId,
  partyId,
  customWorkId,
  onDone,
}: {
  projectId: number;
  partyId: number;
  customWorkId: number;
  onDone: () => void;
}) {
  const queryClient = useQueryClient();
  const [amount, setAmount] = useState("");
  const [date, setDate] = useState("");
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | "">("");
  const [bankTx, setBankTx] = useState<ReconciliationRow | null>(null);
  const [touchedDate, setTouchedDate] = useState(false);
  const [touchedAmount, setTouchedAmount] = useState(false);

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", { all: true }],
    queryFn: () => listAccounts(),
  });

  const pay = useMutation({
    mutationFn: () =>
      payCustomWork({
        projectId,
        partyId,
        customWorkId,
        date,
        amount: Number(amount),
        paymentModeId: paymentModeId!,
        accountId: accountId === "" ? null : accountId,
      }),
    onSuccess: async (result) => {
      toast.success(`Paid ${formatINR(result.amount)}`);
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
      }
      onDone();
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError
          ? (error.fieldErrors?.amount?.[0] ?? error.message)
          : "Payment failed",
      ),
  });

  const ready = date !== "" && Number(amount) > 0 && paymentModeId !== null;

  return (
    <div className="flex flex-wrap items-end gap-3">
      <label className="space-y-1">
        <FieldLabel required error={touchedDate && date === "" ? "Required" : undefined}>
          Date
        </FieldLabel>
        <Input
          type="date"
          aria-label="Payment date"
          value={date}
          onChange={(e) => setDate(e.target.value)}
          onBlur={() => setTouchedDate(true)}
        />
      </label>
      <label className="space-y-1">
        <FieldLabel
          required
          error={touchedAmount && Number(amount) <= 0 ? "Must be greater than zero" : undefined}
        >
          Amount
        </FieldLabel>
        <AmountInput
          className="w-32"
          aria-label="Payment amount"
          value={amount}
          onChange={setAmount}
          onBlur={() => setTouchedAmount(true)}
        />
      </label>
      <PaymentModeSelect value={paymentModeId} onChange={(m) => setPaymentModeId(m?.id ?? null)} />
      <label className="space-y-1">
        <span className="text-sm font-medium">Account</span>
        <select
          className="bg-card block rounded border px-3 py-1.5 text-sm"
          aria-label="Payment account"
          value={accountId}
          onChange={(e) => setAccountId(e.target.value ? Number(e.target.value) : "")}
        >
          <option value="">None</option>
          {accounts.map((a) => (
            <option key={a.id} value={a.id}>
              {a.name}
            </option>
          ))}
        </select>
      </label>
      <div className="w-56">
        <BankTransactionPicker
          selected={bankTx}
          onSelect={setBankTx}
          accountId={accountId === "" ? null : accountId}
        />
      </div>
      <Button type="button" disabled={!ready || pay.isPending} onClick={() => pay.mutate()}>
        Record payment
      </Button>
    </div>
  );
}
