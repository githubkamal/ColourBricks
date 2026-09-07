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
import { FieldLabel } from "@/components/ui/field-label";
import { Input } from "@/components/ui/input";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import {
  SUB_CATEGORIES,
  commonExpenseSummary,
  listCommonExpenses,
  recordCommonExpense,
  type CommonExpenseType,
} from "./api";

export function CommonExpensesPage({ type }: { type: CommonExpenseType }) {
  const queryClient = useQueryClient();
  const [subCategory, setSubCategory] = useState(SUB_CATEGORIES[type][0]);
  const [date, setDate] = useState("");
  const [amount, setAmount] = useState("");
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | "">("");
  const [bankTx, setBankTx] = useState<ReconciliationRow | null>(null);
  const [touchedDate, setTouchedDate] = useState(false);
  const [touchedAmount, setTouchedAmount] = useState(false);

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", "common-expense"],
    queryFn: () => listAccounts(),
  });
  const { data: rows = [] } = useQuery({
    queryKey: ["common-expenses", type],
    queryFn: () => listCommonExpenses(type),
  });
  const { pageRows, page, setPage, pageCount, total } = usePagination(rows, 20);
  const { data: summary } = useQuery({
    queryKey: ["common-expense-summary"],
    queryFn: () => commonExpenseSummary(),
  });

  const save = useMutation({
    mutationFn: () =>
      recordCommonExpense({
        type,
        subCategory,
        date,
        amount: Number(amount),
        paymentModeId: paymentModeId!,
        accountId: accountId === "" ? null : accountId,
      }),
    onSuccess: async (result) => {
      toast.success(`${type} expense recorded`);
      setAmount("");
      setTouchedDate(false);
      setTouchedAmount(false);
      void queryClient.invalidateQueries({ queryKey: ["common-expenses", type] });
      void queryClient.invalidateQueries({ queryKey: ["common-expense-summary"] });
      if (bankTx && result.settlementId) {
        try {
          await reconcileDebit(bankTx.id, { existingPaymentId: result.settlementId });
          toast.success("Linked to the bank transaction");
          void queryClient.invalidateQueries({ queryKey: ["reconciliation"] });
        } catch (error) {
          toast.error(
            error instanceof ApiError
              ? `Expense saved, but couldn't link the bank transaction: ${error.message}`
              : "Expense saved, but couldn't link the bank transaction — link it from the Reconciliation Queue instead.",
          );
        }
      }
      setBankTx(null);
    },
    onError: (e) => toast.error(e instanceof ApiError ? e.message : "Could not save"),
  });

  const canSave = date !== "" && Number(amount) > 0 && paymentModeId !== null;

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">{type} expenses</h1>
      {summary && (
        <p className="text-muted-foreground text-sm" data-testid="ce-summary">
          Company totals — Personal {formatINR(summary.personal)}, Office{" "}
          {formatINR(summary.office)}, Savings {formatINR(summary.savings)} (Savings is not counted
          as an expense)
        </p>
      )}

      <div className="bg-card flex flex-wrap items-end gap-3 rounded border p-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">Sub-category</span>
          <Select
            aria-label="Sub-category"
            value={subCategory}
            onChange={(e) => setSubCategory(e.target.value)}
          >
            {SUB_CATEGORIES[type].map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </Select>
        </label>
        <label className="space-y-1">
          <FieldLabel required error={touchedDate && date === "" ? "Required" : undefined}>
            Date
          </FieldLabel>
          <Input
            type="date"
            aria-label="Date"
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
            aria-label="Amount"
            value={amount}
            onChange={setAmount}
            onBlur={() => setTouchedAmount(true)}
          />
        </label>
        <PaymentModeSelect
          value={paymentModeId}
          onChange={(m) => setPaymentModeId(m?.id ?? null)}
        />
        <label className="space-y-1">
          <span className="text-sm font-medium">Account</span>
          <Select
            aria-label="Account"
            value={accountId}
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
        <BankTransactionPicker
          selected={bankTx}
          onSelect={setBankTx}
          accountId={accountId === "" ? null : accountId}
        />
        <Button type="button" disabled={!canSave || save.isPending} onClick={() => save.mutate()}>
          Record
        </Button>
      </div>

      <div className="bg-card rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Date</th>
              <th className="p-2 font-medium">Sub-category</th>
              <th className="p-2 font-medium">Amount</th>
            </tr>
          </thead>
          <tbody>
            {rows.length === 0 && (
              <tr>
                <td colSpan={3} className="text-muted-foreground p-3 text-center">
                  No {type.toLowerCase()} expenses yet.
                </td>
              </tr>
            )}
            {pageRows.map((r) => (
              <tr key={r.id} className="border-b last:border-0">
                <td className="p-2">{formatDate(r.date)}</td>
                <td className="p-2">{r.subCategory}</td>
                <td className="p-2 tabular-nums">{formatINR(r.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <PaginationBar
        page={page}
        pageCount={pageCount}
        total={total}
        onPageChange={setPage}
        itemLabel="expenses"
      />
    </div>
  );
}
