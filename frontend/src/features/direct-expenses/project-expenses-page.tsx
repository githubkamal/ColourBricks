"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useRef, useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { getProject } from "@/features/projects/api";
import { ProjectPicker } from "@/features/projects/project-picker";
import type { ProjectListItem } from "@/features/projects/types";
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
import { useQueryParamNumber } from "@/lib/use-query-param";
import { listExpenseCategories, listProjectExpenses, recordDirectExpense } from "./api";

export function ProjectExpensesPage() {
  const [projectId, setProjectId] = useQueryParamNumber("projectId", 0);
  // `undefined` = the user hasn't picked in this session yet, so a deep-linked
  // `?projectId=` still needs restoring (just the id survives a URL, not the
  // project's code/name the picker displays).
  const [manualProject, setManualProject] = useState<ProjectListItem | null | undefined>(undefined);
  const { data: restoredProject } = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => getProject(projectId),
    enabled: projectId > 0 && manualProject === undefined,
  });
  const project = manualProject !== undefined ? manualProject : (restoredProject ?? null);

  function handleSelect(next: ProjectListItem | null) {
    setManualProject(next);
    setProjectId(next?.id ?? 0);
  }

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">Project Expenses</h1>

      <div className="bg-card max-w-xs rounded border p-4">
        <ProjectPicker selected={project} onSelect={handleSelect} label="Project" />
      </div>

      {project && <ExpenseForm projectId={project.id} />}
    </div>
  );
}

export function ExpenseForm({ projectId }: { projectId: number }) {
  const queryClient = useQueryClient();
  const [categoryId, setCategoryId] = useState<number | "">("");
  const [date, setDate] = useState("");
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [paidImmediately, setPaidImmediately] = useState(false);
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | "">("");
  const [bankTx, setBankTx] = useState<ReconciliationRow | null>(null);
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
  const {
    pageRows: pagedExpenses,
    page: expensesPage,
    setPage: setExpensesPage,
    pageCount: expensesPageCount,
    total: expensesTotal,
  } = usePagination(expenses, 20);

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
    onSuccess: async (result) => {
      toast.success("Expense recorded");
      setAmount("");
      setDescription("");
      setTouchedDate(false);
      setTouchedAmount(false);
      void queryClient.invalidateQueries({ queryKey: ["project-expenses", projectId] });
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
          <Select
            ref={categoryRef}
            className="w-full"
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
          </Select>
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
              <Select
                className="w-full"
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
              </Select>
            </label>
            <div className="col-span-2">
              <BankTransactionPicker
                selected={bankTx}
                onSelect={setBankTx}
                accountId={accountId === "" ? null : accountId}
              />
            </div>
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
            {pagedExpenses.map((x) => (
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

      <PaginationBar
        page={expensesPage}
        pageCount={expensesPageCount}
        total={expensesTotal}
        onPageChange={setExpensesPage}
        itemLabel="expenses"
      />
    </div>
  );
}
