"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { listProjects } from "@/features/projects/api";
import { Button } from "@/components/ui/button";
import { AmountInput } from "@/components/ui/amount-input";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { listReceipts, projectIncomeTotal, recordReceipt, reverseReceipt } from "./api";
import { INCOME_TYPES, INCOME_TYPE_LABELS, type IncomeType } from "./types";

export function ProjectIncomePage() {
  const [projectId, setProjectId] = useState<number | "">("");

  const { data: projects } = useQuery({
    queryKey: ["projects", { forIncome: true }],
    queryFn: () => listProjects({ pageSize: 100 }),
  });

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">Project Income</h1>

      <label className="block space-y-1">
        <span className="text-sm font-medium">Project</span>
        <select
          className="w-full rounded border bg-transparent px-3 py-1.5 text-sm"
          value={projectId}
          aria-label="Project"
          onChange={(e) => setProjectId(e.target.value ? Number(e.target.value) : "")}
        >
          <option value="">Select a project…</option>
          {projects?.items.map((p) => (
            <option key={p.id} value={p.id}>
              {p.code} — {p.name}
            </option>
          ))}
        </select>
      </label>

      {projectId !== "" && <ProjectIncome projectId={projectId} />}
    </div>
  );
}

function ProjectIncome({ projectId }: { projectId: number }) {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();
  const [type, setType] = useState<IncomeType>("ClientAdvance");
  const [date, setDate] = useState("");
  const [amount, setAmount] = useState("");
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [requiresReference, setRequiresReference] = useState(false);
  const [accountId, setAccountId] = useState<number | "">("");
  const [referenceNo, setReferenceNo] = useState("");

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", { all: true }],
    queryFn: () => listAccounts(),
  });
  const { data: receipts = [] } = useQuery({
    queryKey: ["receipts", projectId],
    queryFn: () => listReceipts(projectId),
  });
  const { data: total } = useQuery({
    queryKey: ["income-total", projectId],
    queryFn: () => projectIncomeTotal(projectId),
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["receipts", projectId] });
    void queryClient.invalidateQueries({ queryKey: ["income-total", projectId] });
  };

  const record = useMutation({
    mutationFn: () =>
      recordReceipt({
        projectId,
        type,
        date,
        amount: Number(amount),
        paymentModeId: paymentModeId!,
        accountId: accountId === "" ? null : accountId,
        referenceNo: referenceNo.trim() || null,
      }),
    onSuccess: (receipt) => {
      toast.success(`Recorded ${formatINR(receipt.amount)}`);
      setAmount("");
      setReferenceNo("");
      invalidate();
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError
          ? (error.fieldErrors?.referenceNo?.[0] ?? error.message)
          : "Could not record the receipt",
      ),
  });

  const reverse = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) => reverseReceipt(id, reason),
    onSuccess: () => {
      toast.success("Receipt reversed");
      invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not reverse the receipt"),
  });

  async function handleReverse(id: number) {
    const { confirmed, value: reason } = await confirm({
      title: "Reverse this receipt?",
      description: "This cannot be undone.",
      inputLabel: "Reason",
      destructive: true,
      confirmLabel: "Reverse",
    });
    if (!confirmed) return;
    reverse.mutate({ id, reason: reason ?? "" });
  }

  const ready = date && Number(amount) > 0 && paymentModeId !== null;

  return (
    <div className="space-y-6">
      {dialog}
      <form
        className="grid grid-cols-2 gap-3 rounded border p-4"
        onSubmit={(e) => {
          e.preventDefault();
          if (ready) record.mutate();
        }}
      >
        <label className="space-y-1">
          <span className="text-sm font-medium">Transaction type</span>
          <select
            className="block w-full rounded border bg-transparent px-3 py-1.5 text-sm"
            value={type}
            aria-label="Transaction type"
            onChange={(e) => setType(e.target.value as IncomeType)}
          >
            {INCOME_TYPES.map((t) => (
              <option key={t} value={t}>
                {INCOME_TYPE_LABELS[t]}
              </option>
            ))}
          </select>
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Date</span>
          <Input
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            aria-label="Date"
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">Amount</span>
          <AmountInput value={amount} onChange={setAmount} aria-label="Amount" />
        </label>
        <PaymentModeSelect
          value={paymentModeId}
          onChange={(mode) => {
            setPaymentModeId(mode?.id ?? null);
            setRequiresReference(mode?.requiresReference ?? false);
          }}
        />
        <label className="space-y-1">
          <span className="text-sm font-medium">Account</span>
          <select
            className="block w-full rounded border bg-transparent px-3 py-1.5 text-sm"
            value={accountId}
            aria-label="Account"
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
        <label className="space-y-1">
          <span className="text-sm font-medium">
            Reference {requiresReference && <span className="text-attention">*</span>}
          </span>
          <Input
            value={referenceNo}
            onChange={(e) => setReferenceNo(e.target.value)}
            aria-label="Reference"
            required={requiresReference}
          />
        </label>
        <div className="col-span-2">
          <Button type="submit" disabled={!ready || record.isPending}>
            Record receipt
          </Button>
        </div>
      </form>

      <p className="text-sm" data-testid="income-total">
        Total income: <span className="font-semibold">{formatINR(total?.total ?? 0)}</span>
      </p>

      <div className="rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Date</th>
              <th className="p-2 font-medium">Type</th>
              <th className="p-2 font-medium">Amount</th>
              <th className="p-2 font-medium">Mode</th>
              <th className="p-2 font-medium">Status</th>
              <th className="p-2 font-medium" />
            </tr>
          </thead>
          <tbody>
            {receipts.length === 0 && (
              <tr>
                <td colSpan={6} className="text-muted-foreground p-3 text-center">
                  No receipts yet.
                </td>
              </tr>
            )}
            {receipts.map((r) => (
              <tr key={r.id} className="border-b last:border-0">
                <td className="p-2">{formatDate(r.date)}</td>
                <td className="text-muted-foreground p-2">{INCOME_TYPE_LABELS[r.type]}</td>
                <td className="p-2">{formatINR(r.amount)}</td>
                <td className="text-muted-foreground p-2">{r.paymentModeName}</td>
                <td className="text-muted-foreground p-2">{r.status}</td>
                <td className="p-2 text-right">
                  {r.status === "Active" && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      disabled={reverse.isPending}
                      onClick={() => handleReverse(r.id)}
                    >
                      Reverse
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
