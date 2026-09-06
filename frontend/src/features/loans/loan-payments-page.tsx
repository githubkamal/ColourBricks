"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { reconcileDebit, type ReconciliationRow } from "@/features/reconciliation/api";
import { BankTransactionPicker } from "@/features/reconciliation/bank-transaction-picker";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import { useQueryParamNumber } from "@/lib/use-query-param";
import {
  getSchedule,
  listEmiPayments,
  listLoans,
  payEmi,
  prepayLoan,
  reverseEmiPayment,
  type LoanEmiPayment,
} from "./api";

/** EMI Payments (BRD §49): settle a scheduled instalment or pay ahead of schedule. */
export function LoanPaymentsPage() {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();
  const { data: loans = [] } = useQuery({ queryKey: ["loans"], queryFn: () => listLoans() });
  const [loanId, setLoanId] = useQueryParamNumber("loanId", 0);

  const { data: schedule = [] } = useQuery({
    queryKey: ["loan-schedule", loanId],
    queryFn: () => getSchedule(loanId),
    enabled: loanId !== 0,
  });
  const { data: payments = [], isPending } = useQuery({
    queryKey: ["loan-emi-payments", loanId],
    queryFn: () => listEmiPayments(loanId),
    enabled: loanId !== 0,
  });
  const {
    pageRows: pagedPayments,
    page: paymentsPage,
    setPage: setPaymentsPage,
    pageCount: paymentsPageCount,
    total: paymentsTotal,
  } = usePagination(payments, 20);

  const pendingInstalments = schedule.filter((i) => i.status !== "Paid");

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", { all: true }],
    queryFn: () => listAccounts(),
  });

  const [instalmentId, setInstalmentId] = useState<number | "">("");
  const [date, setDate] = useState("");
  const [amount, setAmount] = useState("");
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | null>(null);
  const [referenceNo, setReferenceNo] = useState("");
  const [bankTx, setBankTx] = useState<ReconciliationRow | null>(null);

  const [prepayAmount, setPrepayAmount] = useState("");
  const [prepayDate, setPrepayDate] = useState("");
  const [prepayModeId, setPrepayModeId] = useState<number | null>(null);
  const [prepayAccountId, setPrepayAccountId] = useState<number | null>(null);
  const [prepayBankTx, setPrepayBankTx] = useState<ReconciliationRow | null>(null);

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["loan-schedule", loanId] });
    void queryClient.invalidateQueries({ queryKey: ["loan-emi-payments", loanId] });
    void queryClient.invalidateQueries({ queryKey: ["loans"] });
  };

  const pay = useMutation({
    mutationFn: () =>
      payEmi(loanId, {
        instalmentId: instalmentId as number,
        date,
        paymentModeId: paymentModeId!,
        amount: amount ? Number(amount) : null,
        accountId,
        referenceNo: referenceNo.trim() || null,
      }),
    onSuccess: async (result) => {
      toast.success("EMI payment recorded");
      setInstalmentId("");
      setDate("");
      setAmount("");
      setPaymentModeId(null);
      setAccountId(null);
      setReferenceNo("");
      invalidate();
      if (bankTx && result.settlementId) {
        try {
          await reconcileDebit(bankTx.id, { existingPaymentId: result.settlementId });
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
      setBankTx(null);
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not record the payment"),
  });

  const prepay = useMutation({
    mutationFn: () =>
      prepayLoan(loanId, {
        amount: Number(prepayAmount),
        date: prepayDate,
        paymentModeId: prepayModeId!,
        accountId: prepayAccountId,
      }),
    onSuccess: async (result) => {
      toast.success("Prepayment recorded — pending instalments re-amortised");
      setPrepayAmount("");
      setPrepayDate("");
      setPrepayModeId(null);
      setPrepayAccountId(null);
      invalidate();
      if (prepayBankTx && result.settlementId) {
        try {
          await reconcileDebit(prepayBankTx.id, { existingPaymentId: result.settlementId });
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
      setPrepayBankTx(null);
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not record the prepayment"),
  });

  const reverse = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) => reverseEmiPayment(id, reason),
    onSuccess: () => {
      toast.success("Payment reversed");
      invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not reverse the payment"),
  });

  const payReady = instalmentId !== "" && date !== "" && paymentModeId !== null;
  const prepayReady = Number(prepayAmount) > 0 && prepayDate !== "" && prepayModeId !== null;

  async function handleReverse(id: number) {
    const { confirmed, value: reason } = await confirm({
      title: "Reverse this payment?",
      description: "This cannot be undone.",
      inputLabel: "Reason",
      destructive: true,
      confirmLabel: "Reverse",
    });
    if (!confirmed || !reason) return;
    reverse.mutate({ id, reason });
  }

  return (
    <div className="max-w-4xl space-y-6">
      {dialog}
      <PageHeader title="EMI Payments" />

      <label className="block max-w-sm space-y-1">
        <span className="text-sm font-medium">Loan</span>
        <select
          className="bg-card w-full rounded border px-3 py-1.5 text-sm"
          value={loanId || ""}
          aria-label="Loan"
          onChange={(e) => setLoanId(e.target.value ? Number(e.target.value) : 0)}
        >
          <option value="">Select a loan…</option>
          {loans.map((l) => (
            <option key={l.id} value={l.id}>
              {l.lenderName} — {formatINR(l.principalAmount)} ({l.status})
            </option>
          ))}
        </select>
      </label>

      {loanId !== 0 && (
        <>
          <form
            className="bg-card space-y-3 rounded border p-4"
            onSubmit={(e) => {
              e.preventDefault();
              if (payReady) pay.mutate();
            }}
          >
            <h2 className="text-sm font-semibold">Pay an EMI instalment</h2>
            <label className="block space-y-1">
              <span className="text-sm font-medium">Instalment</span>
              <select
                className="bg-card w-full rounded border px-3 py-1.5 text-sm"
                value={instalmentId}
                aria-label="Instalment"
                onChange={(e) => setInstalmentId(e.target.value ? Number(e.target.value) : "")}
              >
                <option value="">Select…</option>
                {pendingInstalments.map((i) => (
                  <option key={i.id} value={i.id}>
                    #{i.instalmentNo} — due {formatDate(i.dueDate)} — {formatINR(i.emiAmount)}
                  </option>
                ))}
              </select>
            </label>
            <div className="grid grid-cols-2 gap-3">
              <label className="space-y-1">
                <span className="text-sm font-medium">Date</span>
                <Input
                  type="date"
                  value={date}
                  aria-label="Payment date"
                  onChange={(e) => setDate(e.target.value)}
                />
              </label>
              <label className="space-y-1">
                <span className="text-sm font-medium">Amount (blank pays the full EMI)</span>
                <AmountInput value={amount} aria-label="Amount" onChange={setAmount} />
              </label>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <PaymentModeSelect
                value={paymentModeId}
                onChange={(m) => setPaymentModeId(m?.id ?? null)}
              />
              <label className="space-y-1">
                <span className="text-sm font-medium">Reference no.</span>
                <Input
                  value={referenceNo}
                  aria-label="Reference no."
                  onChange={(e) => setReferenceNo(e.target.value)}
                />
              </label>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <label className="space-y-1">
                <span className="text-sm font-medium">Account</span>
                <select
                  className="bg-card w-full rounded border px-3 py-1.5 text-sm"
                  value={accountId ?? ""}
                  aria-label="Account"
                  onChange={(e) => setAccountId(e.target.value ? Number(e.target.value) : null)}
                >
                  <option value="">None</option>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
                </select>
              </label>
              <BankTransactionPicker selected={bankTx} onSelect={setBankTx} accountId={accountId} />
            </div>
            <Button type="submit" disabled={!payReady || pay.isPending}>
              Record payment
            </Button>
          </form>

          <form
            className="bg-card space-y-3 rounded border p-4"
            onSubmit={(e) => {
              e.preventDefault();
              if (prepayReady) prepay.mutate();
            }}
          >
            <h2 className="text-sm font-semibold">Prepay (extra principal, ahead of schedule)</h2>
            <div className="grid grid-cols-2 gap-3">
              <label className="space-y-1">
                <span className="text-sm font-medium">Amount</span>
                <AmountInput
                  value={prepayAmount}
                  aria-label="Prepayment amount"
                  onChange={setPrepayAmount}
                />
              </label>
              <label className="space-y-1">
                <span className="text-sm font-medium">Date</span>
                <Input
                  type="date"
                  value={prepayDate}
                  aria-label="Prepayment date"
                  onChange={(e) => setPrepayDate(e.target.value)}
                />
              </label>
            </div>
            <div className="grid grid-cols-2 gap-3">
              <PaymentModeSelect
                value={prepayModeId}
                onChange={(m) => setPrepayModeId(m?.id ?? null)}
              />
              <label className="space-y-1">
                <span className="text-sm font-medium">Account</span>
                <select
                  className="bg-card w-full rounded border px-3 py-1.5 text-sm"
                  value={prepayAccountId ?? ""}
                  aria-label="Prepayment account"
                  onChange={(e) => setPrepayAccountId(e.target.value ? Number(e.target.value) : null)}
                >
                  <option value="">None</option>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
                </select>
              </label>
            </div>
            <BankTransactionPicker
              selected={prepayBankTx}
              onSelect={setPrepayBankTx}
              accountId={prepayAccountId}
            />
            <Button type="submit" variant="outline" disabled={!prepayReady || prepay.isPending}>
              Record prepayment
            </Button>
          </form>

          {dataState({
            isPending,
            isEmpty: !isPending && payments.length === 0,
            emptyLabel: "No payments recorded yet.",
          }) ?? (
            <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
              <table className="w-full text-sm">
                <thead className="bg-secondary/60 text-muted-foreground">
                  <tr className="border-b text-left">
                    <th className="p-2 font-medium">Date</th>
                    <th className="p-2 font-medium">Amount</th>
                    <th className="p-2 font-medium">Principal</th>
                    <th className="p-2 font-medium">Interest</th>
                    <th className="p-2 font-medium">Type</th>
                    <th className="p-2 font-medium">Status</th>
                    <th />
                  </tr>
                </thead>
                <tbody>
                  {pagedPayments.map((p) => (
                    <PaymentRow key={p.id} payment={p} onReverse={() => void handleReverse(p.id)} />
                  ))}
                </tbody>
              </table>
              <div className="p-3">
                <PaginationBar
                  page={paymentsPage}
                  pageCount={paymentsPageCount}
                  total={paymentsTotal}
                  onPageChange={setPaymentsPage}
                  itemLabel="payments"
                />
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}

function PaymentRow({ payment, onReverse }: { payment: LoanEmiPayment; onReverse: () => void }) {
  return (
    <tr className="border-b last:border-0">
      <td className="p-2">{formatDate(payment.date)}</td>
      <td className="p-2 tabular-nums">{formatINR(payment.amount)}</td>
      <td className="p-2 tabular-nums">{formatINR(payment.principalPaid)}</td>
      <td className="p-2 tabular-nums">{formatINR(payment.interestPaid)}</td>
      <td className="p-2">{payment.isPrepayment ? "Prepayment" : "EMI"}</td>
      <td className="p-2">
        <StatusBadge status={payment.status} />
      </td>
      <td className="p-2">
        {payment.status !== "Reversed" && (
          <Button type="button" variant="ghost" size="xs" onClick={onReverse}>
            Reverse
          </Button>
        )}
      </td>
    </tr>
  );
}
