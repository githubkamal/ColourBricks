"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { formatDate, formatINR } from "@/lib/format";
import { listPurchaseOrders } from "./api";

const STATUSES = ["Draft", "Submitted", "Cancelled"] as const;

export function PurchaseOrderListPage() {
  const [vendor, setVendor] = useState<PartySearchItem | null>(null);
  const [status, setStatus] = useState<string>("");

  const { data: orders = [] } = useQuery({
    queryKey: ["purchase-orders", vendor?.id, status],
    queryFn: () => listPurchaseOrders(vendor?.id, status || undefined),
  });

  return (
    <div className="max-w-4xl space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-lg font-semibold">Purchase Orders</h1>
        <Link href="/vendors/purchase-orders/new" className="text-primary text-sm underline">
          New purchase order
        </Link>
      </div>

      <div className="flex flex-wrap items-end gap-3 rounded border p-3">
        <PartyPicker type="Vendor" label="Vendor" selected={vendor} onSelect={setVendor} />
        <label className="space-y-1">
          <span className="text-sm font-medium">Status</span>
          <select
            className="block rounded border bg-transparent px-3 py-1.5 text-sm"
            aria-label="Status"
            value={status}
            onChange={(e) => setStatus(e.target.value)}
          >
            <option value="">All</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </select>
        </label>
      </div>

      <div className="rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">PO Number</th>
              <th className="p-2 font-medium">Vendor</th>
              <th className="p-2 font-medium">Date</th>
              <th className="p-2 font-medium">Status</th>
              <th className="p-2 font-medium">Projects</th>
              <th className="p-2 font-medium">Total</th>
            </tr>
          </thead>
          <tbody>
            {orders.length === 0 && (
              <tr>
                <td colSpan={6} className="text-muted-foreground p-3 text-center">
                  No purchase orders yet.
                </td>
              </tr>
            )}
            {orders.map((po) => (
              <tr key={po.id} className="border-b last:border-0">
                <td className="p-2">
                  <Link
                    href={`/vendors/purchase-orders/${po.id}`}
                    className="text-primary underline"
                  >
                    {po.poNumber}
                  </Link>
                </td>
                <td className="p-2">{po.vendorName}</td>
                <td className="p-2">{formatDate(po.orderDate)}</td>
                <td className="p-2">{po.status}</td>
                <td className="p-2">{new Set(po.lines.map((l) => l.projectId)).size}</td>
                <td className="p-2">{po.status === "Draft" ? "—" : formatINR(po.total)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
