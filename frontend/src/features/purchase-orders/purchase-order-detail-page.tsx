"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { AttachmentPanel } from "@/features/attachments/attachment-panel";
import { ProjectPicker, type ProjectPickerSelection } from "@/features/projects/project-picker";
import { SubmitButton } from "@/components/ui/submit-button";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Input } from "@/components/ui/input";
import { Select } from "@/components/ui/select";
import { TableScroll } from "@/components/ui/table-scroll";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import {
  cancelPurchaseOrder,
  getPurchaseOrder,
  submitPurchaseOrder,
  updatePurchaseOrder,
  type PurchaseOrder,
  type PurchaseOrderLineInput,
  type PurchaseOrderChargeInput,
  type PurchaseOrderTaxType,
  type SubmitPurchaseOrderLineInput,
} from "./api";

interface DraftRow {
  id?: number;
  project: ProjectPickerSelection | null;
  itemName: string;
  quantity: string;
  unit: string;
}

interface SubmitRow {
  lineId: number;
  projectName: string;
  itemName: string;
  unit: string;
  quantity: string;
  rate: string;
  taxType: PurchaseOrderTaxType;
  /** The GST % when taxType is Percentage, or the flat currency figure when Amount. */
  taxValue: string;
}

/**
 * An extra charge on the vendor's invoice that isn't one of the ordered items —
 * transport, handling, loading (client request, 2026-09-23). Wholly optional:
 * an invoice with none simply has no rows here, and a row's GST may be zero.
 */
interface ChargeRow {
  /** Stable across re-renders so React keeps each row's inputs focused. */
  key: number;
  chargeType: string;
  amount: string;
  taxType: PurchaseOrderTaxType;
  /** The GST % when taxType is Percentage, or the flat currency figure when Amount. */
  taxValue: string;
}

/** What vendors usually bill on top of the goods; the field still accepts anything typed. */
const CHARGE_TYPES = [
  "Transport",
  "Freight",
  "Handling",
  "Loading",
  "Unloading",
  "Packing",
  "Insurance",
  "Other",
];

let nextChargeKey = 1;
const newChargeRow = (): ChargeRow => ({
  key: nextChargeKey++,
  chargeType: "Transport",
  amount: "",
  taxType: "Amount",
  taxValue: "0",
});

export function PurchaseOrderDetailPage({ id }: { id: number }) {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();

  const { data: po } = useQuery({
    queryKey: ["purchase-order", id],
    queryFn: () => getPurchaseOrder(id),
  });

  const invalidate = () => {
    void queryClient.invalidateQueries({ queryKey: ["purchase-order", id] });
    void queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
  };

  const cancel = useMutation({
    mutationFn: () => cancelPurchaseOrder(id),
    onSuccess: () => {
      toast.success("Purchase order cancelled");
      invalidate();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Could not cancel"),
  });

  async function handleCancel() {
    const { confirmed } = await confirm({
      title: "Cancel this purchase order?",
      description: "This cannot be undone.",
      confirmLabel: "Cancel order",
      destructive: true,
    });
    if (!confirmed) return;
    cancel.mutate();
  }

  if (!po) {
    return <p className="text-muted-foreground text-sm">Loading…</p>;
  }

  return (
    <div className="max-w-4xl space-y-6">
      {dialog}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-lg font-semibold">{po.poNumber}</h1>
          <p className="text-muted-foreground text-sm">
            {po.vendorName} · {formatDate(po.orderDate)} ·{" "}
            <span data-testid="po-status">{po.status}</span>
          </p>
        </div>
        {po.status === "Draft" && (
          <Button type="button" variant="destructive" size="sm" onClick={handleCancel}>
            Cancel order
          </Button>
        )}
      </div>

      {po.status === "Draft" && <DraftEditor po={po} onSaved={invalidate} />}
      {po.status === "Submitted" && <SubmittedView po={po} />}
      {po.status === "Cancelled" && (
        <p className="text-muted-foreground rounded border p-3 text-sm">
          This order was cancelled while still a draft — nothing was posted.
        </p>
      )}

      <div className="pt-2">
        <Link href="/vendors/purchase-orders" className="text-primary text-sm underline">
          ← All purchase orders
        </Link>
      </div>
    </div>
  );
}

function DraftEditor({ po, onSaved }: { po: PurchaseOrder; onSaved: () => void }) {
  const [rows, setRows] = useState<DraftRow[]>(
    po.lines.map((l) => ({
      id: l.id,
      project: { id: l.projectId, name: l.projectName },
      itemName: l.itemName,
      quantity: String(l.quantity),
      unit: l.unit,
    })),
  );
  const [submitting, setSubmitting] = useState(false);

  const save = useMutation({
    mutationFn: () =>
      updatePurchaseOrder(po.id, {
        orderDate: po.orderDate,
        notes: po.notes,
        concurrencyStamp: po.concurrencyStamp,
        lines: rows.map<PurchaseOrderLineInput>((r) => ({
          projectId: r.project!.id,
          itemName: r.itemName.trim(),
          quantity: Number(r.quantity),
          unit: r.unit.trim(),
        })),
      }),
    onSuccess: () => {
      toast.success("Draft saved");
      onSaved();
    },
    onError: (error) => toast.error(error instanceof ApiError ? error.message : "Could not save"),
  });

  const linesReady = rows.every(
    (r) => r.project !== null && r.itemName.trim() && Number(r.quantity) > 0 && r.unit.trim(),
  );

  return (
    <div className="space-y-4">
      <div data-table-scroll className="bg-card overflow-x-auto rounded border p-4">
        <table className="w-full text-sm">
          <thead className="text-muted-foreground">
            <tr className="text-left">
              <th className="py-1 font-medium">Project</th>
              <th className="py-1 font-medium">Item</th>
              <th className="py-1 font-medium">Qty</th>
              <th className="py-1 font-medium">Unit</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => (
              <tr key={i}>
                <td className="py-1 pr-2">
                  <ProjectPicker
                    selected={row.project}
                    ariaLabel={`Project ${i + 1}`}
                    onSelect={(project) =>
                      setRows((rs) => rs.map((r, j) => (j === i ? { ...r, project } : r)))
                    }
                  />
                </td>
                {(["itemName", "quantity", "unit"] as const).map((field) =>
                  field === "quantity" ? (
                    <td key={field} className="py-1 pr-2">
                      <AmountInput
                        value={row[field]}
                        aria-label={`${field} ${i + 1}`}
                        onChange={(v) =>
                          setRows((rs) => rs.map((r, j) => (j === i ? { ...r, quantity: v } : r)))
                        }
                      />
                    </td>
                  ) : (
                    <td key={field} className="py-1 pr-2">
                      <Input
                        value={row[field]}
                        aria-label={`${field} ${i + 1}`}
                        onChange={(e) =>
                          setRows((rs) =>
                            rs.map((r, j) => (j === i ? { ...r, [field]: e.target.value } : r)),
                          )
                        }
                      />
                    </td>
                  ),
                )}
                <td className="py-1">
                  {rows.length > 1 && (
                    <Button
                      type="button"
                      variant="ghost"
                      size="xs"
                      aria-label={`Remove line ${i + 1}`}
                      onClick={() => setRows((rs) => rs.filter((_, j) => j !== i))}
                    >
                      ✕
                    </Button>
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
        <div className="mt-2 flex items-center justify-between">
          <Button
            type="button"
            variant="ghost"
            size="xs"
            onClick={() =>
              setRows((rs) => [...rs, { project: null, itemName: "", quantity: "", unit: "" }])
            }
          >
            + Add line
          </Button>
          <SubmitButton
            type="button"
            disabled={!linesReady}
            mutation={save}
            onClick={() => save.mutate()}
          >
            Save changes
          </SubmitButton>
        </div>
      </div>

      <div className="bg-card rounded border p-4">
        <p className="mb-2 text-sm font-medium">Vendor invoice / attachments</p>
        <AttachmentPanel ownerType="PurchaseOrder" ownerId={po.id} />
      </div>

      {!submitting ? (
        <Button type="button" variant="secondary" onClick={() => setSubmitting(true)}>
          Vendor invoice received — submit
        </Button>
      ) : (
        <SubmitForm po={po} onDone={onSaved} onCancel={() => setSubmitting(false)} />
      )}
    </div>
  );
}

function SubmitForm({
  po,
  onDone,
  onCancel,
}: {
  po: PurchaseOrder;
  onDone: () => void;
  onCancel: () => void;
}) {
  const [invoiceNumber, setInvoiceNumber] = useState("");
  const [rows, setRows] = useState<SubmitRow[]>(
    po.lines.map((l) => ({
      lineId: l.id,
      projectName: l.projectName,
      itemName: l.itemName,
      unit: l.unit,
      quantity: String(l.quantity),
      rate: "",
      taxType: "Amount",
      taxValue: "0",
    })),
  );

  const [charges, setCharges] = useState<ChargeRow[]>([]);
  // Typed, not derived: a round-off exists precisely because the vendor's own
  // total disagrees with the arithmetic by a few paise. "-" alone is allowed
  // mid-typing so a negative adjustment can be entered left to right.
  const [roundOffText, setRoundOffText] = useState("");

  const round3 = (n: number) => Math.round((n + Number.EPSILON) * 1000) / 1000;
  const subtotal = (r: SubmitRow) => round3((Number(r.quantity) || 0) * (Number(r.rate) || 0));
  const taxAmount = (r: SubmitRow) => {
    const value = Number(r.taxValue) || 0;
    return r.taxType === "Percentage" ? round3(subtotal(r) * (value / 100)) : round3(value);
  };
  const lineTotal = (r: SubmitRow) => round3(subtotal(r) + taxAmount(r));
  const subtotalTotal = round3(rows.reduce((s, r) => s + subtotal(r), 0));
  const taxTotal = round3(rows.reduce((s, r) => s + taxAmount(r), 0));

  const chargeAmount = (c: ChargeRow) => round3(Number(c.amount) || 0);
  const chargeTax = (c: ChargeRow) => {
    const value = Number(c.taxValue) || 0;
    return c.taxType === "Percentage" ? round3(chargeAmount(c) * (value / 100)) : round3(value);
  };
  const chargeTotal = (c: ChargeRow) => round3(chargeAmount(c) + chargeTax(c));
  const chargesSubtotal = round3(charges.reduce((s, c) => s + chargeAmount(c), 0));
  const chargesTax = round3(charges.reduce((s, c) => s + chargeTax(c), 0));

  const roundOff = roundOffText === "" || roundOffText === "-" ? 0 : round3(Number(roundOffText));
  const total = round3(subtotalTotal + taxTotal + chargesSubtotal + chargesTax + roundOff);

  const submit = useMutation({
    mutationFn: () =>
      submitPurchaseOrder(po.id, {
        invoiceNumber: invoiceNumber.trim(),
        lines: rows.map<SubmitPurchaseOrderLineInput>((r) => ({
          lineId: r.lineId,
          quantity: Number(r.quantity),
          rate: Number(r.rate),
          taxType: r.taxType,
          taxRate: r.taxType === "Percentage" ? Number(r.taxValue) || 0 : null,
          taxAmount: taxAmount(r),
        })),
        charges: charges.map<PurchaseOrderChargeInput>((c) => ({
          chargeType: c.chargeType.trim(),
          amount: chargeAmount(c),
          taxType: c.taxType,
          taxRate: c.taxType === "Percentage" ? Number(c.taxValue) || 0 : null,
          taxAmount: chargeTax(c),
        })),
        roundOff,
      }),
    onSuccess: (submitted) => {
      toast.success(
        `Submitted — posted to ${new Set(submitted.lines.map((l) => l.projectId)).size} project(s)`,
      );
      onDone();
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError
          ? (error.fieldErrors?.lines?.[0] ?? error.message)
          : "Could not submit",
      ),
  });

  const roundOffValid = roundOffText === "" || /^-?\d*(\.\d{0,3})?$/.test(roundOffText);

  const ready =
    invoiceNumber.trim() !== "" &&
    roundOffValid &&
    total > 0 &&
    rows.every(
      (r) =>
        Number(r.quantity) > 0 &&
        Number(r.rate) >= 0 &&
        Number(r.taxValue) >= 0 &&
        (r.taxType !== "Percentage" || Number(r.taxValue) >= 0),
    ) &&
    charges.every((c) => c.chargeType.trim() !== "" && Number(c.amount) >= 0);

  return (
    <div className="bg-card space-y-3 rounded border p-4">
      <p className="text-sm font-medium">Price against the vendor&apos;s invoice</p>
      <p className="text-muted-foreground text-xs">
        Quantity, rate and tax can all be corrected here to match what the invoice actually says.
        The whole order submits at once.
      </p>

      <label className="block max-w-xs space-y-1">
        <span className="text-sm font-medium">Vendor invoice number</span>
        <Input
          aria-label="Vendor invoice number"
          value={invoiceNumber}
          onChange={(e) => setInvoiceNumber(e.target.value)}
        />
      </label>

      <div data-table-scroll className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead className="text-muted-foreground">
            <tr className="text-left">
              <th className="py-1 font-medium">Project</th>
              <th className="py-1 font-medium">Item</th>
              <th className="py-1 font-medium">Qty</th>
              <th className="py-1 font-medium">Rate</th>
              <th className="py-1 font-medium">Subtotal</th>
              <th className="py-1 font-medium">Tax type</th>
              <th className="py-1 font-medium">Tax (GST)</th>
              <th className="py-1 font-medium">GST amount</th>
              <th className="py-1 font-medium">Line total</th>
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => (
              <tr key={row.lineId}>
                <td className="py-1 pr-2">{row.projectName}</td>
                <td className="py-1 pr-2">
                  {row.itemName} <span className="text-muted-foreground">({row.unit})</span>
                </td>
                {(["quantity", "rate"] as const).map((field) => (
                  <td key={field} className="py-1 pr-2">
                    <AmountInput
                      className="w-24"
                      value={row[field]}
                      aria-label={`${field} ${i + 1}`}
                      onChange={(v) =>
                        setRows((rs) => rs.map((r, j) => (j === i ? { ...r, [field]: v } : r)))
                      }
                    />
                  </td>
                ))}
                <td className="py-1 pr-2 tabular-nums">{formatINR(subtotal(row))}</td>
                <td className="py-1 pr-2">
                  <Select
                    className="w-28"
                    value={row.taxType}
                    aria-label={`taxType ${i + 1}`}
                    onChange={(e) =>
                      setRows((rs) =>
                        rs.map((r, j) =>
                          j === i ? { ...r, taxType: e.target.value as PurchaseOrderTaxType } : r,
                        ),
                      )
                    }
                  >
                    <option value="Amount">₹ Amount</option>
                    <option value="Percentage">% Percentage</option>
                  </Select>
                </td>
                <td className="py-1 pr-2">
                  <AmountInput
                    className="w-24"
                    value={row.taxValue}
                    aria-label={`taxValue ${i + 1}`}
                    onChange={(v) =>
                      setRows((rs) => rs.map((r, j) => (j === i ? { ...r, taxValue: v } : r)))
                    }
                  />
                </td>
                <td className="py-1 pr-2 tabular-nums">{formatINR(taxAmount(row))}</td>
                <td className="py-1 pr-2 tabular-nums">{formatINR(lineTotal(row))}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      <div className="space-y-2 border-t pt-3">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <p className="text-sm font-medium">Other charges</p>
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() => setCharges((cs) => [...cs, newChargeRow()])}
          >
            + Add charge
          </Button>
        </div>
        <p className="text-muted-foreground text-xs">
          Transport, handling and anything else the invoice bills on top of the goods — all
          optional, and so is each charge&apos;s GST. On an order spanning several projects each
          charge is split between them in proportion to what they ordered.
        </p>

        {charges.length > 0 && (
          <TableScroll className="rounded border-none shadow-none">
            <table className="w-full text-sm">
              <thead className="text-muted-foreground">
                <tr className="text-left">
                  <th className="py-1 pr-2 font-medium">Type</th>
                  <th className="py-1 pr-2 font-medium">Amount</th>
                  <th className="py-1 pr-2 font-medium">Tax type</th>
                  <th className="py-1 pr-2 font-medium">Tax (GST)</th>
                  <th className="py-1 pr-2 font-medium">GST amount</th>
                  <th className="py-1 pr-2 font-medium">Total</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {charges.map((charge, i) => (
                  <tr key={charge.key}>
                    <td className="py-1 pr-2">
                      <Select
                        className="w-36"
                        value={charge.chargeType}
                        aria-label={`chargeType ${i + 1}`}
                        onChange={(e) =>
                          setCharges((cs) =>
                            cs.map((c, j) => (j === i ? { ...c, chargeType: e.target.value } : c)),
                          )
                        }
                      >
                        {CHARGE_TYPES.map((t) => (
                          <option key={t} value={t}>
                            {t}
                          </option>
                        ))}
                      </Select>
                    </td>
                    <td className="py-1 pr-2">
                      <AmountInput
                        className="w-28"
                        value={charge.amount}
                        aria-label={`chargeAmount ${i + 1}`}
                        onChange={(v) =>
                          setCharges((cs) => cs.map((c, j) => (j === i ? { ...c, amount: v } : c)))
                        }
                      />
                    </td>
                    <td className="py-1 pr-2">
                      <Select
                        className="w-28"
                        value={charge.taxType}
                        aria-label={`chargeTaxType ${i + 1}`}
                        onChange={(e) =>
                          setCharges((cs) =>
                            cs.map((c, j) =>
                              j === i
                                ? { ...c, taxType: e.target.value as PurchaseOrderTaxType }
                                : c,
                            ),
                          )
                        }
                      >
                        <option value="Amount">₹ Amount</option>
                        <option value="Percentage">% Percentage</option>
                      </Select>
                    </td>
                    <td className="py-1 pr-2">
                      <AmountInput
                        className="w-24"
                        value={charge.taxValue}
                        aria-label={`chargeTaxValue ${i + 1}`}
                        onChange={(v) =>
                          setCharges((cs) =>
                            cs.map((c, j) => (j === i ? { ...c, taxValue: v } : c)),
                          )
                        }
                      />
                    </td>
                    <td className="py-1 pr-2 tabular-nums">{formatINR(chargeTax(charge))}</td>
                    <td className="py-1 pr-2 tabular-nums">{formatINR(chargeTotal(charge))}</td>
                    <td className="py-1">
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        aria-label={`Remove charge ${i + 1}`}
                        onClick={() => setCharges((cs) => cs.filter((_, j) => j !== i))}
                      >
                        ✕
                      </Button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </TableScroll>
        )}

        <label className="flex flex-wrap items-center gap-2 pt-1">
          <span className="text-sm font-medium">Round off</span>
          <Input
            className="w-28"
            inputMode="decimal"
            aria-label="Round off"
            placeholder="0.000"
            value={roundOffText}
            onChange={(e) => setRoundOffText(e.target.value)}
          />
          <span className="text-muted-foreground text-xs">
            Optional, and may be negative — whatever it takes to match the invoice&apos;s own total.
          </span>
        </label>
        {!roundOffValid && (
          <p className="text-destructive text-xs">
            Enter a number, optionally negative, with at most three decimals.
          </p>
        )}
      </div>

      <div className="flex flex-wrap items-center justify-between gap-3 border-t pt-3">
        <div className="space-y-1 text-sm">
          <p className="text-muted-foreground">
            Subtotal (excl. GST): <span className="tabular-nums">{formatINR(subtotalTotal)}</span>
          </p>
          <p className="text-muted-foreground">
            GST: <span className="tabular-nums">{formatINR(taxTotal)}</span>
          </p>
          {charges.length > 0 && (
            <p className="text-muted-foreground">
              Other charges (incl. GST):{" "}
              <span className="tabular-nums" data-testid="submit-charges-total">
                {formatINR(round3(chargesSubtotal + chargesTax))}
              </span>
            </p>
          )}
          {roundOff !== 0 && (
            <p className="text-muted-foreground">
              Round off:{" "}
              <span className="tabular-nums" data-testid="submit-round-off">
                {formatINR(roundOff)}
              </span>
            </p>
          )}
          <p className="font-semibold" data-testid="submit-total">
            Total: {formatINR(total)}
          </p>
        </div>
        <div className="flex gap-2">
          <Button type="button" variant="ghost" onClick={onCancel}>
            Back
          </Button>
          <SubmitButton
            type="button"
            disabled={!ready}
            mutation={submit}
            onClick={() => submit.mutate()}
          >
            Submit order
          </SubmitButton>
        </div>
      </div>
    </div>
  );
}

function SubmittedView({ po }: { po: PurchaseOrder }) {
  const byProject = new Map<number, { name: string; lines: typeof po.lines }>();
  for (const line of po.lines) {
    const bucket = byProject.get(line.projectId) ?? { name: line.projectName, lines: [] };
    bucket.lines.push(line);
    byProject.set(line.projectId, bucket);
  }

  return (
    <div className="space-y-4">
      <div className="bg-card rounded border p-3 text-sm">
        <p>
          Invoice <span className="font-medium">{po.invoiceNumber}</span> · submitted{" "}
          {po.submittedDate ? formatDate(po.submittedDate) : ""}
        </p>
        <p className="text-muted-foreground">Subtotal (excl. GST): {formatINR(po.subtotalTotal)}</p>
        <p className="text-muted-foreground">GST: {formatINR(po.taxTotal)}</p>
        {po.charges.map((c) => (
          <p key={c.id} className="text-muted-foreground">
            {c.chargeType}: {formatINR(c.amount)}
            {c.taxAmount !== 0 && ` + GST ${formatINR(c.taxAmount)}`}
          </p>
        ))}
        {po.roundOff !== 0 && (
          <p className="text-muted-foreground">Round off: {formatINR(po.roundOff)}</p>
        )}
        <p className="text-lg font-semibold">Total: {formatINR(po.total)}</p>
      </div>

      <div className="bg-card rounded border p-3">
        <p className="mb-2 text-sm font-medium">Vendor invoice / attachments</p>
        <AttachmentPanel ownerType="PurchaseOrder" ownerId={po.id} />
      </div>

      {[...byProject.entries()].map(([projectId, group]) => (
        <div key={projectId} className="bg-card rounded border p-3">
          <div className="mb-2 flex items-center justify-between">
            <Link href={`/projects/${projectId}`} className="text-primary font-medium underline">
              {group.name}
            </Link>
            <span className="font-medium">
              {formatINR(group.lines.reduce((s, l) => s + (l.lineTotal ?? 0), 0))}
            </span>
          </div>
          <div data-table-scroll className="overflow-x-auto">
            <table className="w-full text-sm">
              <thead className="text-muted-foreground">
                <tr className="text-left">
                  <th className="py-1 font-medium">Item</th>
                  <th className="py-1 font-medium">Qty</th>
                  <th className="py-1 font-medium">Rate</th>
                  <th className="py-1 font-medium">Tax</th>
                  <th className="py-1 font-medium">Total</th>
                </tr>
              </thead>
              <tbody>
                {group.lines.map((l) => (
                  <tr key={l.id} className="border-t">
                    <td className="py-1">
                      {l.itemName} <span className="text-muted-foreground">({l.unit})</span>
                    </td>
                    <td className="py-1">{l.quantity}</td>
                    <td className="py-1">{formatINR(l.rate ?? 0)}</td>
                    <td className="py-1">{formatINR(l.taxAmount ?? 0)}</td>
                    <td className="py-1">{formatINR(l.lineTotal ?? 0)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </div>
      ))}
    </div>
  );
}
