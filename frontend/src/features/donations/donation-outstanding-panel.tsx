"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { reconcileDebit, type ReconciliationRow } from "@/features/reconciliation/api";
import { BankTransactionPicker } from "@/features/reconciliation/bank-transaction-picker";
import { Button } from "@/components/ui/button";
import { AmountInput } from "@/components/ui/amount-input";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { formatINR } from "@/lib/format";
import { donationOutstanding, payDonationTemple } from "./api";

export function DonationOutstandingPanel({ projectId }: { projectId: number }) {
  const queryClient = useQueryClient();
  const { data: rows = [] } = useQuery({
    queryKey: ["donation-outstanding", projectId],
    queryFn: () => donationOutstanding(projectId),
  });

  if (rows.length === 0) return null;

  return (
    <div className="space-y-2">
      <h2 className="text-sm font-semibold">Donation outstanding by temple</h2>
      <div className="bg-card rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Temple</th>
              <th className="p-2 font-medium">Allocated</th>
              <th className="p-2 font-medium">Paid</th>
              <th className="p-2 font-medium">Outstanding</th>
              <th className="p-2 font-medium" />
            </tr>
          </thead>
          <tbody>
            {rows.map((row) => (
              <TempleRow
                key={row.templeId}
                projectId={projectId}
                templeId={row.templeId}
                templeName={row.templeName}
                allocated={row.allocated}
                paid={row.paid}
                outstanding={row.outstanding}
                onPaid={() =>
                  queryClient.invalidateQueries({ queryKey: ["donation-outstanding", projectId] })
                }
              />
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

function TempleRow({
  projectId,
  templeId,
  templeName,
  allocated,
  paid,
  outstanding,
  onPaid,
}: {
  projectId: number;
  templeId: number;
  templeName: string;
  allocated: number;
  paid: number;
  outstanding: number;
  onPaid: () => void;
}) {
  const queryClient = useQueryClient();
  const [open, setOpen] = useState(false);
  const [date, setDate] = useState("");
  const [amount, setAmount] = useState("");
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | "">("");
  const [bankTx, setBankTx] = useState<ReconciliationRow | null>(null);

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", { all: true }],
    queryFn: () => listAccounts(),
  });

  const pay = useMutation({
    mutationFn: () =>
      payDonationTemple(projectId, templeId, {
        date,
        amount: Number(amount),
        paymentModeId: paymentModeId!,
        accountId: accountId === "" ? null : accountId,
      }),
    onSuccess: async (result) => {
      toast.success("Donation payment recorded");
      setOpen(false);
      setAmount("");
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
      onPaid();
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError ? (error.fieldErrors?.amount?.[0] ?? error.message) : "Failed",
      ),
  });

  return (
    <>
      <tr className="border-b last:border-0">
        <td className="p-2">{templeName}</td>
        <td className="p-2 tabular-nums">{formatINR(allocated)}</td>
        <td className="text-muted-foreground p-2 tabular-nums">{formatINR(paid)}</td>
        <td
          className="p-2 font-medium tabular-nums"
          data-testid={`donation-outstanding-${templeId}`}
        >
          {formatINR(outstanding)}
        </td>
        <td className="p-2 text-right">
          {outstanding > 0 && (
            <Button type="button" variant="ghost" size="xs" onClick={() => setOpen((v) => !v)}>
              {open ? "Cancel" : "Pay"}
            </Button>
          )}
        </td>
      </tr>
      {open && (
        <tr>
          <td colSpan={5} className="bg-secondary/30 p-3">
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
                  aria-label={`Donation pay date ${templeId}`}
                />
              </label>
              <label className="space-y-1">
                <span className="text-xs">Amount</span>
                <AmountInput
                  className="w-28"
                  value={amount}
                  onChange={setAmount}
                  aria-label={`Donation pay amount ${templeId}`}
                />
              </label>
              <PaymentModeSelect
                value={paymentModeId}
                onChange={(m) => setPaymentModeId(m?.id ?? null)}
              />
              <label className="space-y-1">
                <span className="text-xs">Account</span>
                <Select
                  value={accountId}
                  aria-label={`Donation pay account ${templeId}`}
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
              <Button type="submit" size="xs" disabled={pay.isPending}>
                Save payment
              </Button>
            </form>
          </td>
        </tr>
      )}
    </>
  );
}
