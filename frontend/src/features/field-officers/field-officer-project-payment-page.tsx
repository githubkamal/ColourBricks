"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { toast } from "sonner";
import { AmountInput } from "@/components/ui/amount-input";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { FieldLabel } from "@/components/ui/field-label";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { Select } from "@/components/ui/select";
import { StatusBadge } from "@/components/ui/status-badge";
import { SubmitButton } from "@/components/ui/submit-button";
import { listExpenseCategories } from "@/features/direct-expenses/api";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { ProjectPicker } from "@/features/projects/project-picker";
import type { ProjectListItem } from "@/features/projects/types";
import { vendorOutstandingSummary } from "@/features/vendor-payments/api";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import {
  listFieldOfficerProjectPayments,
  recordFieldOfficerExpense,
  reverseFieldOfficerExpense,
} from "./api";

/**
 * A field officer spent money on a particular thing for a particular project (client
 * request, 2026-09-28). Saving posts the amount to that project's expenses and to the
 * officer's payable — the project's spend, P&L and ledger all pick it up.
 */
export function FieldOfficerProjectPaymentPage() {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();
  const [officer, setOfficer] = useState<PartySearchItem | null>(null);
  const [project, setProject] = useState<ProjectListItem | null>(null);
  const [categoryId, setCategoryId] = useState<number | "">("");
  const [date, setDate] = useState("");
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [referenceNo, setReferenceNo] = useState("");

  const { data: categories = [] } = useQuery({
    queryKey: ["expense-categories"],
    queryFn: () => listExpenseCategories(),
  });
  const costCategories = useMemo(
    () => categories.filter((c) => c.isCost && c.isActive),
    [categories],
  );

  const { data: summary } = useQuery({
    queryKey: ["field-officer-summary", officer?.id],
    queryFn: () => vendorOutstandingSummary(officer!.id),
    enabled: officer !== null,
  });
  const { data: payments = [] } = useQuery({
    queryKey: ["field-officer-project-payments", officer?.id],
    queryFn: () => listFieldOfficerProjectPayments(officer!.id),
    enabled: officer !== null,
  });
  const { pageRows, page, setPage, pageCount, total } = usePagination(payments, 20);

  function refresh() {
    void queryClient.invalidateQueries({ queryKey: ["field-officer-summary", officer?.id] });
    void queryClient.invalidateQueries({ queryKey: ["field-officer-project-payments", officer?.id] });
    // The mapped project's spend, P&L, ledger and budget-vs-actual all derive from the
    // ledger — refetch them rather than guess which cached screen shows the figure.
    void queryClient.invalidateQueries({ queryKey: ["project"] });
    void queryClient.invalidateQueries({ queryKey: ["projects"] });
    void queryClient.invalidateQueries({ queryKey: ["project-expenses"] });
    void queryClient.invalidateQueries({ queryKey: ["reports"] });
  }

  const record = useMutation({
    mutationFn: () =>
      recordFieldOfficerExpense({
        fieldOfficerId: officer!.id,
        type: "Custom",
        date,
        amount: Number(amount),
        projectId: project!.id,
        categoryId: categoryId === "" ? null : categoryId,
        description: description.trim() || null,
        referenceNo: referenceNo.trim() || null,
      }),
    onSuccess: (bill) => {
      toast.success(`${formatINR(bill.amount)} added to ${bill.projectName ?? "the project"}`);
      setAmount("");
      setDescription("");
      setReferenceNo("");
      refresh();
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError
          ? (error.fieldErrors?.amount?.[0] ?? error.message)
          : "Could not record the payment",
      ),
  });

  const reverse = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) =>
      reverseFieldOfficerExpense(id, reason),
    onSuccess: () => {
      toast.success("Reversed");
      refresh();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not reverse"),
  });

  async function handleReverse(id: number) {
    const { confirmed, value: reason } = await confirm({
      title: "Reverse this payment?",
      description: "The amount is taken back off the project. This cannot be undone.",
      inputLabel: "Reason",
      destructive: true,
      confirmLabel: "Reverse",
    });
    if (!confirmed || !reason) return;
    reverse.mutate({ id, reason });
  }

  const ready = officer !== null && project !== null && date !== "" && Number(amount) > 0;

  // What this officer has put on each project — active payments only.
  const byProject = useMemo(() => {
    const totals = new Map<number, { name: string; amount: number }>();
    for (const p of payments) {
      if (p.status !== "Active" || p.projectId === null) continue;
      const row = totals.get(p.projectId) ?? { name: p.projectName ?? "", amount: 0 };
      row.amount += p.amount;
      totals.set(p.projectId, row);
    }
    return [...totals.entries()]
      .map(([id, v]) => ({ id, ...v }))
      .sort((a, b) => a.name.localeCompare(b.name));
  }, [payments]);

  return (
    <div className="max-w-3xl space-y-6">
      {dialog}
      <PageHeader
        title="Field Officer Project Payments"
        description="Record what a field officer spent on a project. The amount is added to that project's expenses."
      />

      <PartyPicker
        type="FieldOfficer"
        label="Field officer"
        selected={officer}
        onSelect={setOfficer}
      />

      {officer && summary && (
        <div className="bg-card border-border rounded-xl border p-3 shadow-xs">
          <p className="text-muted-foreground text-xs">Total outstanding (owed to him)</p>
          <p className="text-2xl font-semibold tabular-nums">{formatINR(summary.total)}</p>
        </div>
      )}

      {officer && (
        <form
          className="bg-card border-border space-y-3 rounded-xl border p-4 shadow-xs"
          onSubmit={(e) => {
            e.preventDefault();
            if (ready) record.mutate();
          }}
        >
          <ProjectPicker
            label="Project"
            selected={project}
            onSelect={setProject}
            status="Ongoing"
          />
          <div className="flex flex-wrap items-end gap-3">
            <label className="space-y-1">
              <FieldLabel required>Date</FieldLabel>
              <Input
                type="date"
                aria-label="Date"
                value={date}
                onChange={(e) => setDate(e.target.value)}
              />
            </label>
            <label className="space-y-1">
              <FieldLabel required>Amount</FieldLabel>
              <AmountInput
                className="w-32"
                aria-label="Amount"
                value={amount}
                onChange={setAmount}
              />
            </label>
            <label className="space-y-1">
              <FieldLabel>Category</FieldLabel>
              <Select
                aria-label="Category"
                value={categoryId}
                onChange={(e) => setCategoryId(e.target.value ? Number(e.target.value) : "")}
              >
                <option value="">Other expenses</option>
                {costCategories.map((c) => (
                  <option key={c.id} value={c.id}>
                    {c.name}
                  </option>
                ))}
              </Select>
            </label>
            <label className="space-y-1">
              <FieldLabel>Reference</FieldLabel>
              <Input
                aria-label="Reference"
                value={referenceNo}
                onChange={(e) => setReferenceNo(e.target.value)}
              />
            </label>
          </div>
          <label className="block space-y-1">
            <FieldLabel>What was it for?</FieldLabel>
            <Input
              aria-label="Description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </label>
          <SubmitButton type="submit" disabled={!ready} mutation={record}>
            Save payment
          </SubmitButton>
        </form>
      )}

      {officer && byProject.length > 0 && (
        <div className="bg-card border-border rounded-xl border p-3 text-sm shadow-xs">
          <p className="font-medium">Spent by project</p>
          <ul className="text-muted-foreground mt-1">
            {byProject.map((p) => (
              <li key={p.id}>
                {p.name}: {formatINR(p.amount)}
              </li>
            ))}
          </ul>
        </div>
      )}

      {officer &&
        (dataState({ isEmpty: payments.length === 0, emptyLabel: "No project payments yet." }) ?? (
          <div
            data-table-scroll
            className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs"
          >
            <table className="w-full text-sm">
              <thead className="bg-secondary/60 text-muted-foreground">
                <tr className="border-b text-left">
                  <th className="p-2 font-medium">Date</th>
                  <th className="p-2 font-medium">Project</th>
                  <th className="p-2 font-medium">Amount</th>
                  <th className="p-2 font-medium">For</th>
                  <th className="p-2 font-medium">Status</th>
                  <th className="p-2" />
                </tr>
              </thead>
              <tbody>
                {pageRows.map((b) => (
                  <tr key={b.id} className="border-b last:border-0">
                    <td data-nowrap className="p-2">
                      {formatDate(b.date)}
                    </td>
                    <td className="p-2">{b.projectName ?? "—"}</td>
                    <td className="p-2 tabular-nums">{formatINR(b.amount)}</td>
                    <td className="p-2">{b.description ?? "—"}</td>
                    <td className="p-2">
                      <StatusBadge status={b.status} />
                    </td>
                    <td className="p-2">
                      {b.status === "Active" && (
                        <button
                          type="button"
                          className="text-negative text-xs"
                          onClick={() => void handleReverse(b.id)}
                        >
                          Reverse
                        </button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        ))}

      {officer && payments.length > 0 && (
        <PaginationBar
          page={page}
          pageCount={pageCount}
          total={total}
          onPageChange={setPage}
          itemLabel="payments"
        />
      )}
    </div>
  );
}
