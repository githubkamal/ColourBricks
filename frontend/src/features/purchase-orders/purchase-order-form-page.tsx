"use client";

import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { ItemPicker } from "@/features/items/item-picker";
import type { ItemSearchItem } from "@/features/items/types";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { listProjects } from "@/features/projects/api";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { createPurchaseOrder, type PurchaseOrderLineInput } from "./api";

interface Row {
  projectId: number | "";
  item: ItemSearchItem | null;
  quantity: string;
  unit: string;
}

const emptyRow = (): Row => ({ projectId: "", item: null, quantity: "", unit: "" });

export function PurchaseOrderFormPage() {
  const router = useRouter();
  const [vendor, setVendor] = useState<PartySearchItem | null>(null);
  const [orderDate, setOrderDate] = useState("");
  const [notes, setNotes] = useState("");
  const [rows, setRows] = useState<Row[]>([emptyRow()]);

  const { data: projects } = useQuery({
    queryKey: ["projects", { forPurchaseOrders: true }],
    queryFn: () => listProjects({ pageSize: 200 }),
  });

  const create = useMutation({
    mutationFn: () =>
      createPurchaseOrder({
        vendorId: vendor!.id,
        orderDate,
        notes: notes.trim() || null,
        lines: rows.map<PurchaseOrderLineInput>((r) => ({
          projectId: Number(r.projectId),
          itemId: r.item?.id ?? null,
          itemName: r.item!.name,
          quantity: Number(r.quantity),
          unit: r.unit.trim(),
        })),
      }),
    onSuccess: (po) => {
      toast.success(`${po.poNumber} saved as draft`);
      router.push(`/vendors/purchase-orders/${po.id}`);
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError
          ? (error.fieldErrors?.lines?.[0] ?? error.message)
          : "Could not save the order",
      ),
  });

  const ready =
    vendor !== null &&
    orderDate !== "" &&
    rows.every(
      (r) => r.projectId !== "" && r.item !== null && Number(r.quantity) > 0 && r.unit.trim(),
    );

  return (
    <div className="max-w-3xl space-y-6">
      <h1 className="text-lg font-semibold">New Purchase Order</h1>

      <form
        className="bg-card space-y-4 rounded border p-4"
        onSubmit={(e) => {
          e.preventDefault();
          if (ready) create.mutate();
        }}
      >
        <PartyPicker type="Vendor" label="Vendor" selected={vendor} onSelect={setVendor} />

        <div className="flex flex-wrap gap-3">
          <label className="space-y-1">
            <span className="text-sm font-medium">Order date</span>
            <Input
              type="date"
              aria-label="Order date"
              value={orderDate}
              onChange={(e) => setOrderDate(e.target.value)}
            />
          </label>
          <label className="flex-1 space-y-1">
            <span className="text-sm font-medium">Notes</span>
            <Input aria-label="Notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
          </label>
        </div>

        <p className="text-muted-foreground text-xs">
          Prices aren&apos;t known yet — record what was ordered, per project. You&apos;ll add price
          when the vendor&apos;s invoice arrives.
        </p>

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
                  <select
                    className="bg-card w-full rounded border px-2 py-1.5 text-sm"
                    aria-label={`Project ${i + 1}`}
                    value={row.projectId}
                    onChange={(e) =>
                      setRows((rs) =>
                        rs.map((r, j) =>
                          j === i
                            ? { ...r, projectId: e.target.value ? Number(e.target.value) : "" }
                            : r,
                        ),
                      )
                    }
                  >
                    <option value="">Select…</option>
                    {projects?.items.map((p) => (
                      <option key={p.id} value={p.id}>
                        {p.code} — {p.name}
                      </option>
                    ))}
                  </select>
                </td>
                <td className="py-1 pr-2">
                  <ItemPicker
                    selected={row.item}
                    ariaLabel={`itemName ${i + 1}`}
                    onSelect={(item) =>
                      setRows((rs) =>
                        rs.map((r, j) =>
                          j === i ? { ...r, item, unit: r.unit || (item?.unit ?? "") } : r,
                        ),
                      )
                    }
                  />
                </td>
                {(["quantity", "unit"] as const).map((field) =>
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

        <Button
          type="button"
          variant="ghost"
          size="xs"
          onClick={() => setRows((rs) => [...rs, emptyRow()])}
        >
          + Add line
        </Button>

        <div>
          <Button type="submit" disabled={!ready || create.isPending}>
            Save as draft
          </Button>
        </div>
      </form>
    </div>
  );
}
