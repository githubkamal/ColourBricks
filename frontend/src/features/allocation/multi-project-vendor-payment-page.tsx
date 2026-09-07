"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { useCurrentUser } from "@/features/shell/user-context";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { formatINR } from "@/lib/format";
import { hasPermission } from "@/lib/permissions";
import { allocatePayment, proposeAllocation, type AllocationProposal } from "./api";

const round3 = (n: number) => Math.round((n + Number.EPSILON) * 1000) / 1000;

export function MultiProjectVendorPaymentPage() {
  const user = useCurrentUser();
  const canOverride = hasPermission(user.permissions, "vendor_payment_allocation.approve");

  const [vendor, setVendor] = useState<PartySearchItem | null>(null);
  const [amount, setAmount] = useState("");
  const [date, setDate] = useState("");
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | "">("");
  const [proposal, setProposal] = useState<AllocationProposal | null>(null);
  const [overrides, setOverrides] = useState<Record<number, string>>({});
  const [reason, setReason] = useState("");

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", { all: true }],
    queryFn: () => listAccounts(),
  });

  const propose = useMutation({
    mutationFn: () => proposeAllocation(vendor!.id, Number(amount)),
    onSuccess: (p) => {
      setProposal(p);
      setOverrides({});
      setReason("");
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not propose the allocation"),
  });

  const allocatedFor = (projectId: number, fallback: number) =>
    overrides[projectId] !== undefined ? Number(overrides[projectId]) || 0 : fallback;

  const effectiveLines =
    proposal?.lines.map((l) => ({
      ...l,
      allocated: allocatedFor(l.projectId, l.allocated),
    })) ?? [];

  const isOverridden =
    proposal !== null &&
    effectiveLines.some((l, i) => round3(l.allocated) !== round3(proposal.lines[i].allocated));

  const totalAllocated = round3(effectiveLines.reduce((s, l) => s + l.allocated, 0));
  const difference = round3(Number(amount) - totalAllocated);

  const apply = useMutation({
    mutationFn: () =>
      allocatePayment({
        vendorId: vendor!.id,
        date,
        amount: Number(amount),
        paymentModeId: paymentModeId!,
        accountId: accountId === "" ? null : accountId,
        allocations: isOverridden
          ? effectiveLines.map((l) => ({
              projectId: l.projectId,
              obligationId: l.obligationId,
              amount: round3(l.allocated),
            }))
          : undefined,
        referenceNo: null,
        overrideReason: isOverridden ? reason.trim() : null,
      }),
    onSuccess: (result) => {
      toast.success(
        result.advance > 0
          ? `Payment applied across ${result.applied.length} project(s); ${formatINR(result.advance)} recorded as advance`
          : `Payment applied across ${result.applied.length} project(s)`,
      );
      setProposal(null);
      setAmount("");
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError ? (error.fieldErrors?.amount?.[0] ?? error.message) : "Failed",
      ),
  });

  // A FIFO proposal may leave a positive remainder — that is booked as a vendor
  // advance (P3-T05), so it does not have to balance. A manual override must.
  const canApply =
    proposal !== null &&
    date !== "" &&
    paymentModeId !== null &&
    (isOverridden
      ? Math.abs(difference) < 0.0005 && reason.trim().length > 0
      : difference > -0.0005);

  return (
    <div className="max-w-3xl space-y-6">
      <PageHeader title="Multi-project Vendor Payment" />

      <PartyPicker type="Vendor" label="Vendor" selected={vendor} onSelect={setVendor} />

      {vendor && (
        <>
          <form
            className="flex flex-wrap items-end gap-3"
            onSubmit={(e) => {
              e.preventDefault();
              if (Number(amount) > 0) propose.mutate();
            }}
          >
            <label className="space-y-1">
              <span className="text-sm font-medium">Payment amount</span>
              <AmountInput value={amount} onChange={setAmount} aria-label="Payment amount" />
            </label>
            <Button type="submit" disabled={propose.isPending || !(Number(amount) > 0)}>
              Propose FIFO allocation
            </Button>
          </form>

          {proposal && (
            <div className="space-y-3">
              <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
                <table className="w-full text-sm">
                  <thead className="bg-secondary/60 text-muted-foreground">
                    <tr className="border-b text-left">
                      <th className="p-2 font-medium">Project</th>
                      <th className="p-2 font-medium">Outstanding before</th>
                      <th className="p-2 font-medium">Allocated</th>
                      <th className="p-2 font-medium">Outstanding after</th>
                    </tr>
                  </thead>
                  <tbody>
                    {proposal.lines.map((line) => {
                      const allocated = allocatedFor(line.projectId, line.allocated);
                      return (
                        <tr key={line.projectId} className="border-b last:border-0">
                          <td className="p-2">{line.projectName}</td>
                          <td className="p-2 tabular-nums">{formatINR(line.outstandingBefore)}</td>
                          <td className="p-2 font-medium tabular-nums">
                            {canOverride ? (
                              <AmountInput
                                className="w-28"
                                aria-label={`Allocated ${line.projectName}`}
                                value={overrides[line.projectId] ?? String(line.allocated)}
                                onChange={(v) =>
                                  setOverrides((o) => ({
                                    ...o,
                                    [line.projectId]: v,
                                  }))
                                }
                              />
                            ) : (
                              formatINR(line.allocated)
                            )}
                          </td>
                          <td className="p-2 tabular-nums">
                            {formatINR(round3(line.outstandingBefore - allocated))}
                          </td>
                        </tr>
                      );
                    })}
                  </tbody>
                  <tfoot>
                    <tr className="border-t">
                      <td className="p-2 font-medium">Total</td>
                      <td />
                      <td className="p-2 font-semibold tabular-nums" data-testid="total-allocated">
                        {formatINR(totalAllocated)}
                      </td>
                      <td className="p-2">
                        {proposal.advance > 0 && !isOverridden && (
                          <span className="text-attention">
                            advance {formatINR(proposal.advance)}
                          </span>
                        )}
                      </td>
                    </tr>
                  </tfoot>
                </table>
              </div>

              <p
                className={
                  Math.abs(difference) < 0.0005 || (!isOverridden && difference > 0)
                    ? "text-muted-foreground text-sm"
                    : "text-attention text-sm"
                }
                data-testid="difference"
              >
                Difference (payment − allocated): {formatINR(difference)}
                {!isOverridden && difference > 0.0005 && " — recorded as a vendor advance"}
              </p>

              {isOverridden && (
                <label className="block space-y-1">
                  <span className="text-sm font-medium">
                    Override reason <span className="text-attention">*</span>
                  </span>
                  <Input
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                    aria-label="Override reason"
                  />
                </label>
              )}

              <div className="flex flex-wrap items-end gap-3">
                <label className="space-y-1">
                  <span className="text-sm font-medium">Date</span>
                  <Input
                    type="date"
                    value={date}
                    onChange={(e) => setDate(e.target.value)}
                    aria-label="Date"
                  />
                </label>
                <PaymentModeSelect
                  value={paymentModeId}
                  onChange={(m) => setPaymentModeId(m?.id ?? null)}
                />
                <label className="space-y-1">
                  <span className="text-sm font-medium">Account</span>
                  <Select
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
                <Button
                  type="button"
                  disabled={!canApply || apply.isPending}
                  onClick={() => apply.mutate()}
                >
                  {isOverridden
                    ? "Apply override"
                    : difference > 0.0005
                      ? "Apply payment + advance"
                      : "Apply payment"}
                </Button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
