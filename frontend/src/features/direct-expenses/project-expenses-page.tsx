"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { listProjects } from "@/features/projects/api";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { FieldLabel } from "@/components/ui/field-label";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { useQueryParamNumber } from "@/lib/use-query-param";
import { listExpenseCategories, listProjectExpenses, recordDirectExpense } from "./api";

export function ProjectExpensesPage() {
  const [projectId, setProjectId] = useQueryParamNumber("projectId", 0);

  const { data: projects } = useQuery({
    queryKey: ["projects", { forExpenses: true }],
    queryFn: () => listProjects({ pageSize: 100 }),
  });

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">Project Expenses</h1>

      <div className="bg-card max-w-xs rounded border p-4">
        <label className="block space-y-1">
          <span className="text-sm font-medium">Project</span>
          <select
            className="bg-card w-full rounded border px-3 py-1.5 text-sm"
            value={projectId || ""}
            aria-label="Project"
            onChange={(e) => setProjectId(e.target.value ? Number(e.target.value) : 0)}
          >
            <option value="">Select a project…</option>
            {projects?.items.map((p) => (
              <option key={p.id} value={p.id}>
                {p.code} — {p.name}
              </option>
            ))}
          </select>
        </label>
      </div>

      {projectId !== 0 && <ExpenseForm projectId={projectId} />}
    </div>
  );
}

function ExpenseForm({ projectId }: { projectId: number }) {
  const queryClient = useQueryClient();
  const [categoryId, setCategoryId] = useState<number | "">("");
  const [date, setDate] = useState("");
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [paidImmediately, setPaidImmediately] = useState(false);
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | "">("");
  const [touchedDate, setTouchedDate] = useState(false);
  const [touchedAmount, setTouchedAmount] = useState(false);
  const categoryRef = useRef<HTMLSelectElement>(null);
  const dateRef = useRef<HTMLInputElement>(null);
  const amountRef = useRef<HTMLInputElement>(null);

  const { data: categories = [] } = useQuery({
    queryKey: ["expense-categories"],
    queryFn: listExpenseCategories,
  });
  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", { all: true }],
    queryFn: () => listAccounts(),
  });
  const { data: expenses = [] } = useQuery({
    queryKey: ["project-expenses", projectId],
    queryFn: () => listProjectExpenses(projectId),
  });

  const record = useMutation({
    mutationFn: () =>
      recordDirectExpense({
        projectId,
        categoryId: Number(categoryId),
        date,
        amount: Number(amount),
        paidImmediately,
        paymentModeId: paidImmediately ? paymentModeId : null,
        accountId: paidImmediately && accountId !== "" ? accountId : null,
        description: description.trim() || null,
      }),
    onSuccess: () => {
      toast.success("Expense recorded");
      setAmount("");
      setDescription("");
      setTouchedDate(false);
      setTouchedAmount(false);
      void queryClient.invalidateQueries({ queryKey: ["project-expenses", projectId] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not record the expense"),
  });

  const ready =
    categoryId !== "" &&
    date !== "" &&
    Number(amount) > 0 &&
    (!paidImmediately || paymentModeId !== null);

  function focusFirstInvalid() {
    if (categoryId === "") {
      categoryRef.current?.focus();
    } else if (date === "") {
      dateRef.current?.focus();
    } else if (!(Number(amount) > 0)) {
      amountRef.current?.focus();
    }
  }

  return (
    <div className="space-y-6">
      <form
        className="bg-card grid grid-cols-2 gap-3 rounded border p-4"
        onSubmit={(e) => {
          e.preventDefault();
          setTouchedDate(true);
          setTouchedAmount(true);
          if (ready) {
            record.mutate();
          } else {
            focusFirstInvalid();
          }
        }}
      >
        <label className="space-y-1">
          <span className="text-sm font-medium">Category</span>
          <select
            ref={categoryRef}
            className="bg-card block w-full rounded border px-3 py-1.5 text-sm"
            value={categoryId}
            aria-label="Category"
            onChange={(e) => setCategoryId(e.target.value ? Number(e.target.value) : "")}
          >
            <option value="">Select…</option>
            {categories
              .filter((c) => c.isCost)
              .map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
          </select>
        </label>
        <label className="space-y-1">
          <FieldLabel required error={touchedDate && date === "" ? "Required" : undefined}>
            Date
          </FieldLabel>
          <Input
            ref={dateRef}
            type="date"
            value={date}
            onChange={(e) => setDate(e.target.value)}
            onBlur={() => setTouchedDate(true)}
            aria-label="Date"
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
            ref={amountRef}
            value={amount}
            onChange={setAmount}
            onBlur={() => setTouchedAmount(true)}
            aria-label="Amount"
          />
        </label>
        <label className="space-y-1">
          <FieldLabel>Description</FieldLabel>
          <Input
            value={description}
            onChange={(e) => setDescription(e.target.value)}
            aria-label="Description"
          />
        </label>
        <label className="col-span-2 flex items-center gap-2 text-sm">
          <input
            type="checkbox"
            checked={paidImmediately}
            onChange={(e) => setPaidImmediately(e.target.checked)}
          />
          Paid immediately
        </label>
        {paidImmediately && (
          <>
            <PaymentModeSelect
              value={paymentModeId}
              onChange={(mode) => setPaymentModeId(mode?.id ?? null)}
            />
            <label className="space-y-1">
              <span className="text-sm font-medium">Account</span>
              <select
                className="bg-card block w-full rounded border px-3 py-1.5 text-sm"
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
          </>
        )}
        <div className="col-span-2">
          <Button type="submit" disabled={!ready || record.isPending}>
            Record expense
          </Button>
        </div>
      </form>

      <div className="bg-card rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Date</th>
              <th className="p-2 font-medium">Category</th>
              <th className="p-2 font-medium">Bucket</th>
              <th className="p-2 font-medium">Amount</th>
              <th className="p-2 font-medium">Paid</th>
            </tr>
          </thead>
          <tbody>
            {expenses.length === 0 && (
              <tr>
                <td colSpan={5} className="text-muted-foreground p-3 text-center">
                  No expenses yet.
                </td>
              </tr>
            )}
            {expenses.map((x) => (
              <tr key={x.id} className="border-b last:border-0">
                <td className="p-2">{formatDate(x.date)}</td>
                <td className="p-2">{x.categoryName}</td>
                <td className="text-muted-foreground p-2">{x.bucket}</td>
                <td className="p-2 tabular-nums">{formatINR(x.amount)}</td>
                <td className="text-muted-foreground p-2">
                  {x.paidImmediately ? "Yes" : "Payable"}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
