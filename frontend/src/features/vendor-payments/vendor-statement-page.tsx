"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem, PartyType } from "@/features/parties/types";
import { listVendorPurchases } from "@/features/vendor-purchases/api";
import { Button } from "@/components/ui/button";
import { AmountInput } from "@/components/ui/amount-input";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { applyVendorAdvance, vendorOutstandingSummary, vendorStatement } from "./api";

export function VendorStatementPage({
  partyType = "Vendor",
  title = "Vendor Statement",
  pickerLabel = "Vendor",
}: {
  partyType?: PartyType;
  title?: string;
  pickerLabel?: string;
}) {
  const [vendor, setVendor] = useState<PartySearchItem | null>(null);
  const [purchaseId, setPurchaseId] = useState<number | "">("");
  const [amount, setAmount] = useState("");
  const queryClient = useQueryClient();

  const { data: rows = [] } = useQuery({
    queryKey: ["vendor-statement", vendor?.id],
    queryFn: () => vendorStatement(vendor!.id),
    enabled: vendor !== null,
  });

  const { data: summary } = useQuery({
    queryKey: ["vendor-advance-summary", vendor?.id],
    queryFn: () => vendorOutstandingSummary(vendor!.id),
    enabled: vendor !== null,
  });

  const { data: purchases = [] } = useQuery({
    queryKey: ["vendor-open-purchases", vendor?.id],
    queryFn: () => listVendorPurchases(undefined, vendor!.id),
    enabled: vendor !== null && (summary?.advance ?? 0) > 0,
  });

  const advance = summary?.advance ?? 0;
  const openPurchases = purchases.filter(
    (p) => p.status === "Active" && p.total - p.partPaid > 0.005,
  );

  const apply = useMutation({
    mutationFn: () =>
      applyVendorAdvance({
        vendorId: vendor!.id,
        obligationId: Number(purchaseId),
        amount: Number(amount),
        date: new Date().toISOString().slice(0, 10),
      }),
    onSuccess: (result) => {
      toast.success(`Applied ${formatINR(result.applied)} of advance`);
      setPurchaseId("");
      setAmount("");
      void queryClient.invalidateQueries({ queryKey: ["vendor-statement", vendor?.id] });
      void queryClient.invalidateQueries({ queryKey: ["vendor-advance-summary", vendor?.id] });
      void queryClient.invalidateQueries({ queryKey: ["vendor-open-purchases", vendor?.id] });
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError
          ? (error.fieldErrors?.amount?.[0] ?? error.message)
          : "Could not apply the advance",
      ),
  });

  const canApply = purchaseId !== "" && Number(amount) > 0 && Number(amount) <= advance + 0.005;

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">{title}</h1>
      <PartyPicker type={partyType} label={pickerLabel} selected={vendor} onSelect={setVendor} />

      {vendor && summary && (
        <div className="grid gap-3 rounded border p-3 sm:grid-cols-[auto_1fr]">
          <div>
            <p className="text-muted-foreground text-xs">Total outstanding</p>
            <p className="text-2xl font-semibold" data-testid="vendor-total-outstanding">
              {formatINR(summary.total)}
            </p>
            {summary.advance > 0 && (
              <p className="text-attention text-xs">
                Includes {formatINR(summary.advance)} advance held
              </p>
            )}
          </div>
          {summary.byProject.length > 0 && (
            <div>
              <p className="text-muted-foreground mb-1 text-xs">By project</p>
              <table className="w-full text-sm">
                <tbody>
                  {summary.byProject.map((p) => (
                    <tr key={p.projectId} className="border-b last:border-0">
                      <td className="py-1">{p.projectName}</td>
                      <td className="py-1 text-right tabular-nums">{formatINR(p.outstanding)}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </div>
      )}

      {vendor && advance > 0 && (
        <div className="border-attention/40 bg-attention/5 space-y-3 rounded border p-3">
          <p className="text-sm font-medium" data-testid="advance-balance">
            Advance / credit balance: <span className="text-attention">{formatINR(advance)}</span>
          </p>
          <div className="flex flex-wrap items-end gap-3">
            <label className="space-y-1">
              <span className="text-sm font-medium">Apply to purchase</span>
              <select
                className="block rounded border bg-transparent px-3 py-1.5 text-sm"
                value={purchaseId}
                aria-label="Apply to purchase"
                onChange={(e) => setPurchaseId(e.target.value ? Number(e.target.value) : "")}
              >
                <option value="">Select a purchase…</option>
                {openPurchases.map((p) => (
                  <option key={p.id} value={p.id}>
                    #{p.id} — {formatDate(p.date)} — {formatINR(p.total - p.partPaid)} open
                  </option>
                ))}
              </select>
            </label>
            <label className="space-y-1">
              <span className="text-sm font-medium">Amount</span>
              <AmountInput
                className="w-32"
                value={amount}
                onChange={setAmount}
                aria-label="Advance amount to apply"
              />
            </label>
            <Button
              type="button"
              disabled={!canApply || apply.isPending}
              onClick={() => apply.mutate()}
            >
              Apply advance
            </Button>
          </div>
        </div>
      )}

      {vendor && (
        <div className="rounded border">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Date</th>
                <th className="p-2 font-medium">Entry</th>
                <th className="p-2 font-medium">Purchase</th>
                <th className="p-2 font-medium">Paid</th>
                <th className="p-2 font-medium">Running outstanding</th>
              </tr>
            </thead>
            <tbody>
              {rows.length === 0 && (
                <tr>
                  <td colSpan={5} className="text-muted-foreground p-3 text-center">
                    No activity yet.
                  </td>
                </tr>
              )}
              {rows.map((r, i) => (
                <tr key={i} className="border-b last:border-0">
                  <td className="p-2">{formatDate(r.date)}</td>
                  <td className="text-muted-foreground p-2">
                    {r.kind} — {r.reference}
                  </td>
                  <td className="p-2">
                    {r.purchaseAmount > 0 ? formatINR(r.purchaseAmount) : "—"}
                  </td>
                  <td className="p-2">{r.paid > 0 ? formatINR(r.paid) : "—"}</td>
                  <td className="p-2 font-medium">{formatINR(r.runningOutstanding)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
