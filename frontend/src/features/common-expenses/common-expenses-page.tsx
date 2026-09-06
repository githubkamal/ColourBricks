"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { FieldLabel } from "@/components/ui/field-label";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
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
    onSuccess: () => {
      toast.success(`${type} expense recorded`);
      setAmount("");
      setTouchedDate(false);
      setTouchedAmount(false);
      void queryClient.invalidateQueries({ queryKey: ["common-expenses", type] });
      void queryClient.invalidateQueries({ queryKey: ["common-expense-summary"] });
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

      <div className="flex flex-wrap items-end gap-3 rounded border p-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">Sub-category</span>
          <select
            className="block rounded border bg-background text-foreground px-3 py-1.5 text-sm"
            aria-label="Sub-category"
            value={subCategory}
            onChange={(e) => setSubCategory(e.target.value)}
          >
            {SUB_CATEGORIES[type].map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
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
          <select
            className="block rounded border bg-background text-foreground px-3 py-1.5 text-sm"
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
          </select>
        </label>
        <Button type="button" disabled={!canSave || save.isPending} onClick={() => save.mutate()}>
          Record
        </Button>
      </div>

      <div className="rounded border">
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
            {rows.map((r) => (
              <tr key={r.id} className="border-b last:border-0">
                <td className="p-2">{formatDate(r.date)}</td>
                <td className="p-2">{r.subCategory}</td>
                <td className="p-2 tabular-nums">{formatINR(r.amount)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
