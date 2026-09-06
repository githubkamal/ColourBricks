"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { ProjectPicker, type ProjectPickerSelection } from "@/features/projects/project-picker";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import {
  cancelPurchaseOrder,
  getPurchaseOrder,
  submitPurchaseOrder,
  updatePurchaseOrder,
  type PurchaseOrder,
  type PurchaseOrderLineInput,
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
  taxAmount: string;
}

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
      <div className="bg-card rounded border p-4">
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
          <Button
            type="button"
            disabled={!linesReady || save.isPending}
            onClick={() => save.mutate()}
          >
            Save changes
          </Button>
        </div>
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
      taxAmount: "0",
    })),
  );

  const round3 = (n: number) => Math.round((n + Number.EPSILON) * 1000) / 1000;
  const lineTotal = (r: SubmitRow) =>
    round3((Number(r.quantity) || 0) * (Number(r.rate) || 0) + (Number(r.taxAmount) || 0));
  const total = round3(rows.reduce((s, r) => s + lineTotal(r), 0));

  const submit = useMutation({
    mutationFn: () =>
      submitPurchaseOrder(po.id, {
        invoiceNumber: invoiceNumber.trim(),
        lines: rows.map<SubmitPurchaseOrderLineInput>((r) => ({
          lineId: r.lineId,
          quantity: Number(r.quantity),
          rate: Number(r.rate),
          taxAmount: Number(r.taxAmount) || 0,
        })),
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

  const ready =
    invoiceNumber.trim() !== "" &&
    rows.every((r) => Number(r.quantity) > 0 && Number(r.rate) >= 0 && Number(r.taxAmount) >= 0);

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

      <table className="w-full text-sm">
        <thead className="text-muted-foreground">
          <tr className="text-left">
            <th className="py-1 font-medium">Project</th>
            <th className="py-1 font-medium">Item</th>
            <th className="py-1 font-medium">Qty</th>
            <th className="py-1 font-medium">Rate</th>
            <th className="py-1 font-medium">Tax (GST)</th>
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
              {(["quantity", "rate", "taxAmount"] as const).map((field) => (
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
              <td className="py-1 pr-2 tabular-nums">{formatINR(lineTotal(row))}</td>
            </tr>
          ))}
        </tbody>
      </table>

      <div className="flex items-center justify-between">
        <p className="text-sm font-semibold" data-testid="submit-total">
          Total: {formatINR(total)}
        </p>
        <div className="flex gap-2">
          <Button type="button" variant="ghost" onClick={onCancel}>
            Back
          </Button>
          <Button
            type="button"
            disabled={!ready || submit.isPending}
            onClick={() => submit.mutate()}
          >
            Submit order
          </Button>
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
        <p className="text-lg font-semibold">{formatINR(po.total)}</p>
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
      ))}
    </div>
  );
}
