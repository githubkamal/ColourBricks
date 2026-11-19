"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatusBadge } from "@/components/ui/status-badge";
import { Select } from "@/components/ui/select";
import { toast } from "sonner";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import { deletePurchaseOrder, listPurchaseOrders, type PurchaseOrder } from "./api";

const STATUSES = ["Draft", "Submitted", "Cancelled"] as const;

export function PurchaseOrderListPage() {
  const [vendor, setVendor] = useState<PartySearchItem | null>(null);
  const [status, setStatus] = useState<string>("");
  const queryClient = useQueryClient();
  const { confirm, dialog: confirmDialog } = useConfirmDialog();

  const remove = useMutation({
    mutationFn: (po: PurchaseOrder) => deletePurchaseOrder(po.id),
    onSuccess: () => {
      toast.success("Purchase order deleted");
      void queryClient.invalidateQueries({ queryKey: ["purchase-orders"] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not delete the order"),
  });

  async function handleDelete(po: PurchaseOrder) {
    const { confirmed } = await confirm({
      title: `Delete ${po.poNumber}?`,
      description: "The order is permanently removed. This cannot be undone.",
      confirmLabel: "Delete",
      destructive: true,
    });
    if (confirmed) remove.mutate(po);
  }

  const { data: orders = [] } = useQuery({
    queryKey: ["purchase-orders", vendor?.id, status],
    queryFn: () => listPurchaseOrders(vendor?.id, status || undefined),
  });
  const { pageRows: pagedOrders, page, setPage, pageCount, total } = usePagination(orders, 20);

  return (
    <div className="max-w-4xl space-y-6">
      <PageHeader
        title="Purchase Orders"
        action={
          <Link href="/vendors/purchase-orders/new" className="text-primary text-sm underline">
            New purchase order
          </Link>
        }
      />

      <div className="bg-card border-border flex flex-wrap items-end gap-3 rounded-xl border p-3 shadow-xs">
        <PartyPicker type="Vendor" label="Vendor" selected={vendor} onSelect={setVendor} />
        <label className="space-y-1">
          <span className="text-sm font-medium">Status</span>
          <Select aria-label="Status" value={status} onChange={(e) => setStatus(e.target.value)}>
            <option value="">All</option>
            {STATUSES.map((s) => (
              <option key={s} value={s}>
                {s}
              </option>
            ))}
          </Select>
        </label>
      </div>

      {dataState({ isEmpty: orders.length === 0, emptyLabel: "No purchase orders yet." }) ?? (
        <div
          data-table-scroll
          className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs"
        >
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">PO Number</th>
                <th className="p-2 font-medium">Vendor</th>
                <th className="p-2 font-medium">Date</th>
                <th className="p-2 font-medium">Status</th>
                <th className="p-2 font-medium">Projects</th>
                <th className="p-2 font-medium">Total</th>
                <th className="p-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {pagedOrders.map((po) => (
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
                  <td data-nowrap className="p-2">
                    {formatDate(po.orderDate)}
                  </td>
                  <td className="p-2">
                    <StatusBadge status={po.status} />
                  </td>
                  <td className="p-2">{new Set(po.lines.map((l) => l.projectId)).size}</td>
                  <td className="p-2">{po.status === "Draft" ? "—" : formatINR(po.total)}</td>
                  <td className="p-2 text-right">
                    {po.status !== "Submitted" && (
                      <Button
                        type="button"
                        variant="ghost"
                        size="xs"
                        className="text-destructive"
                        disabled={remove.isPending}
                        onClick={() => void handleDelete(po)}
                      >
                        Delete
                      </Button>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <PaginationBar
        page={page}
        pageCount={pageCount}
        total={total}
        onPageChange={setPage}
        itemLabel="purchase orders"
      />
      {confirmDialog}
    </div>
  );
}
