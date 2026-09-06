"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { Button } from "@/components/ui/button";
import { AmountInput } from "@/components/ui/amount-input";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import {
  listVendorPayments,
  recordVendorPayment,
  reverseVendorPayment,
  vendorOutstandingSummary,
} from "./api";

export function VendorPaymentsPage() {
  const [vendor, setVendor] = useState<PartySearchItem | null>(null);

  return (
    <div className="max-w-3xl space-y-6">
      <PageHeader title="Vendor Payments" />
      <PartyPicker type="Vendor" label="Vendor" selected={vendor} onSelect={setVendor} />
      {vendor && <VendorPay vendorId={vendor.id} vendorName={vendor.name} />}
    </div>
  );
}

function VendorPay({ vendorId, vendorName }: { vendorId: number; vendorName: string }) {
  const queryClient = useQueryClient();
  const [pickedProjectId, setPickedProjectId] = useState<number | "">("");
  const [amount, setAmount] = useState("");
  const [date, setDate] = useState("");
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | "">("");
  const { confirm, dialog } = useConfirmDialog();

  const { data: summary } = useQuery({
    queryKey: ["vendor-outstanding-summary", vendorId],
    queryFn: () => vendorOutstandingSummary(vendorId),
  });
  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", { all: true }],
    queryFn: () => listAccounts(),
  });
  const { data: payments = [] } = useQuery({
    queryKey: ["vendor-payments", vendorId],
    queryFn: () => listVendorPayments(vendorId),
  });

  // Default to the vendor's first outstanding project without a state-in-effect.
  const projectId: number | "" =
    pickedProjectId !== "" ? pickedProjectId : (summary?.byProject[0]?.projectId ?? "");
  const setProjectId = setPickedProjectId;

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["vendor-outstanding-summary", vendorId] });
    void queryClient.invalidateQueries({ queryKey: ["vendor-payments", vendorId] });
  };

  const pay = useMutation({
    mutationFn: () =>
      recordVendorPayment({
        vendorId,
        projectId: Number(projectId),
        date,
        amount: Number(amount),
        paymentModeId: paymentModeId!,
        accountId: accountId === "" ? null : accountId,
      }),
    onSuccess: () => {
      toast.success("Payment recorded");
      setAmount("");
      invalidate();
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError ? (error.fieldErrors?.amount?.[0] ?? error.message) : "Failed",
      ),
  });

  const reverse = useMutation({
    mutationFn: (id: number) => reverseVendorPayment(id, "reversed from vendor payments screen"),
    onSuccess: () => {
      toast.success("Payment reversed");
      invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not reverse the payment"),
  });

  const ready = projectId !== "" && date !== "" && Number(amount) > 0 && paymentModeId !== null;

  return (
    <div className="space-y-6">
      {dialog}
      <div className="bg-card border-border rounded-xl border p-3 text-sm shadow-xs">
        <p className="font-medium">
          {vendorName} — total outstanding {formatINR(summary?.total ?? 0)}
        </p>
        <ul className="text-muted-foreground mt-1">
          {summary?.byProject.map((line) => (
            <li key={line.projectId}>
              {line.projectName}: {formatINR(line.outstanding)}
            </li>
          ))}
        </ul>
      </div>

      <form
        className="bg-card border-border grid grid-cols-2 gap-3 rounded-xl border p-4 shadow-xs"
        onSubmit={(e) => {
          e.preventDefault();
          if (ready) pay.mutate();
        }}
      >
        <label className="space-y-1">
          <span className="text-sm font-medium">Project</span>
          <select
            className="bg-background text-foreground block w-full rounded border px-3 py-1.5 text-sm"
            value={projectId}
            aria-label="Project"
            onChange={(e) => setProjectId(e.target.value ? Number(e.target.value) : "")}
          >
            <option value="">Select…</option>
            {summary?.byProject.map((line) => (
              <option key={line.projectId} value={line.projectId}>
                {line.projectName} ({formatINR(line.outstanding)})
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
          onChange={(m) => setPaymentModeId(m?.id ?? null)}
        />
        <label className="space-y-1">
          <span className="text-sm font-medium">Account</span>
          <select
            className="bg-background text-foreground block w-full rounded border px-3 py-1.5 text-sm"
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
        <div className="col-span-2">
          <Button type="submit" disabled={!ready || pay.isPending}>
            Record payment
          </Button>
        </div>
      </form>

      {dataState({ isEmpty: payments.length === 0, emptyLabel: "No payments yet." }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Date</th>
                <th className="p-2 font-medium">Amount</th>
                <th className="p-2 font-medium">Status</th>
                <th className="p-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {payments.map((p) => (
                <tr key={p.id} className="border-b last:border-0">
                  <td className="p-2">{formatDate(p.date)}</td>
                  <td className="p-2">{formatINR(p.amount)}</td>
                  <td className="p-2">
                    <StatusBadge status={p.status} />
                  </td>
                  <td className="p-2 text-right">
                    {p.status === "Active" && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        disabled={reverse.isPending}
                        onClick={() => {
                          void confirm({
                            title: "Reverse this payment?",
                            description: "This cannot be undone.",
                            destructive: true,
                            confirmLabel: "Reverse",
                          }).then(({ confirmed }) => {
                            if (confirmed) reverse.mutate(p.id);
                          });
                        }}
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
      )}
    </div>
  );
}
