"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { vendorOutstandingSummary } from "@/features/vendor-payments/api";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { FieldLabel } from "@/components/ui/field-label";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import {
  listFieldOfficerExpenses,
  recordFieldOfficerExpense,
  reverseFieldOfficerExpense,
  type FieldOfficerExpenseType,
} from "./api";

const TYPES: FieldOfficerExpenseType[] = ["Personal", "Office", "Savings", "Custom"];

export function FieldOfficerExpensePage() {
  const queryClient = useQueryClient();
  const [officer, setOfficer] = useState<PartySearchItem | null>(null);
  const [type, setType] = useState<FieldOfficerExpenseType>("Personal");
  const [date, setDate] = useState("");
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");
  const [referenceNo, setReferenceNo] = useState("");
  const [touchedDate, setTouchedDate] = useState(false);
  const [touchedAmount, setTouchedAmount] = useState(false);

  const { data: summary } = useQuery({
    queryKey: ["field-officer-summary", officer?.id],
    queryFn: () => vendorOutstandingSummary(officer!.id),
    enabled: officer !== null,
  });

  const { data: bills = [] } = useQuery({
    queryKey: ["field-officer-expenses", officer?.id],
    queryFn: () => listFieldOfficerExpenses(officer!.id),
    enabled: officer !== null,
  });

  const record = useMutation({
    mutationFn: () =>
      recordFieldOfficerExpense({
        fieldOfficerId: officer!.id,
        type,
        date,
        amount: Number(amount),
        description: description.trim() || null,
        referenceNo: referenceNo.trim() || null,
      }),
    onSuccess: (bill) => {
      toast.success(`Bill recorded: ${formatINR(bill.amount)}`);
      setAmount("");
      setDescription("");
      setReferenceNo("");
      setTouchedDate(false);
      setTouchedAmount(false);
      void queryClient.invalidateQueries({ queryKey: ["field-officer-summary", officer?.id] });
      void queryClient.invalidateQueries({ queryKey: ["field-officer-expenses", officer?.id] });
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError
          ? (error.fieldErrors?.amount?.[0] ?? error.message)
          : "Could not record the bill",
      ),
  });

  const reverse = useMutation({
    mutationFn: (id: number) => {
      const reason = window.prompt("Reason to reverse this bill?");
      if (!reason) return Promise.reject(new Error("cancelled"));
      return reverseFieldOfficerExpense(id, reason);
    },
    onSuccess: () => {
      toast.success("Reversed");
      void queryClient.invalidateQueries({ queryKey: ["field-officer-summary", officer?.id] });
      void queryClient.invalidateQueries({ queryKey: ["field-officer-expenses", officer?.id] });
    },
    onError: (error) => {
      if (error instanceof Error && error.message === "cancelled") return;
      toast.error(error instanceof ApiError ? error.message : "Could not reverse");
    },
  });

  const ready = officer !== null && date !== "" && Number(amount) > 0;

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">Field Officer Bills</h1>
      <p className="text-muted-foreground text-sm">
        For a bill tied to a project, use Material Purchases instead — pick &ldquo;Field
        officer&rdquo; there. This page is for bills with no project: Personal, Office, Savings or
        Custom.
      </p>

      <PartyPicker
        type="FieldOfficer"
        label="Field officer"
        selected={officer}
        onSelect={setOfficer}
      />

      {officer && summary && (
        <div className="rounded border p-3">
          <p className="text-muted-foreground text-xs">Total outstanding (owed to him)</p>
          <p className="text-2xl font-semibold" data-testid="field-officer-outstanding">
            {formatINR(summary.total)}
          </p>
          {summary.advance > 0 && (
            <p className="text-attention text-xs" data-testid="field-officer-advance">
              He is holding {formatINR(summary.advance)} of float / advance
            </p>
          )}
        </div>
      )}

      {officer && (
        <form
          className="space-y-3 rounded border p-4"
          onSubmit={(e) => {
            e.preventDefault();
            if (ready) record.mutate();
          }}
        >
          <div className="flex flex-wrap items-end gap-3">
            <label className="space-y-1">
              <FieldLabel required>Type</FieldLabel>
              <select
                className="block rounded border bg-transparent px-3 py-1.5 text-sm"
                aria-label="Type"
                value={type}
                onChange={(e) => setType(e.target.value as FieldOfficerExpenseType)}
              >
                {TYPES.map((t) => (
                  <option key={t} value={t}>
                    {t}
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
                error={
                  touchedAmount && Number(amount) <= 0 ? "Must be greater than zero" : undefined
                }
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
            <FieldLabel>Description</FieldLabel>
            <Input
              aria-label="Description"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </label>
          <Button type="submit" disabled={!ready || record.isPending}>
            Record bill
          </Button>
        </form>
      )}

      {officer && (
        <div className="rounded border">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Date</th>
                <th className="p-2 font-medium">Type</th>
                <th className="p-2 font-medium">Amount</th>
                <th className="p-2 font-medium">Description</th>
                <th className="p-2 font-medium">Status</th>
                <th className="p-2" />
              </tr>
            </thead>
            <tbody>
              {bills.length === 0 && (
                <tr>
                  <td colSpan={6} className="text-muted-foreground p-3 text-center">
                    No bills yet.
                  </td>
                </tr>
              )}
              {bills.map((b) => (
                <tr key={b.id} className="border-b last:border-0">
                  <td className="p-2">{formatDate(b.date)}</td>
                  <td className="p-2">{b.type}</td>
                  <td className="p-2">{formatINR(b.amount)}</td>
                  <td className="p-2">{b.description ?? "—"}</td>
                  <td className="p-2">{b.status}</td>
                  <td className="p-2">
                    {b.status === "Active" && (
                      <button
                        type="button"
                        className="text-negative text-xs"
                        onClick={() => reverse.mutate(b.id)}
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
      )}
    </div>
  );
}
