"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import { toast } from "sonner";
import { AttachmentPanel } from "@/features/attachments/attachment-panel";
import { ItemPicker } from "@/features/items/item-picker";
import type { ItemSearchItem } from "@/features/items/types";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem, PartyType } from "@/features/parties/types";
import { ProjectPicker } from "@/features/projects/project-picker";
import type { ProjectListItem } from "@/features/projects/types";
import { Button } from "@/components/ui/button";
import { AmountInput } from "@/components/ui/amount-input";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import {
  listVendorPurchases,
  recordVendorPurchase,
  reverseVendorPurchase,
  vendorOutstanding,
  type PurchaseLineInput,
} from "./api";

const round3 = (n: number) => Math.round((n + Number.EPSILON) * 1000) / 1000;

interface Row {
  item: ItemSearchItem | null;
  quantity: string;
  unit: string;
  rate: string;
  taxAmount: string;
}

const emptyRow = (): Row => ({ item: null, quantity: "", unit: "", rate: "", taxAmount: "" });

export function VendorPurchasePage() {
  const [project, setProject] = useState<ProjectListItem | null>(null);
  // A field officer's project purchase posts through this exact same screen — he's
  // billed directly, no draft/PO stage, since he's already bought the item (client
  // request, 2026-09-04). "vendorId" throughout stays the party id either way.
  const [partyType, setPartyType] = useState<PartyType>("Vendor");

  return (
    <div className="max-w-4xl space-y-6">
      <PageHeader title="Material Purchases" />

      <div className="bg-card flex flex-wrap items-end gap-3 rounded border p-4">
        <label className="space-y-1">
          <span className="text-sm font-medium">Bought by</span>
          <select
            className="bg-card text-foreground block rounded border px-3 py-1.5 text-sm"
            aria-label="Bought by"
            value={partyType}
            onChange={(e) => setPartyType(e.target.value as PartyType)}
          >
            <option value="Vendor">Vendor</option>
            <option value="FieldOfficer">Field officer</option>
          </select>
        </label>
        <div className="min-w-56 flex-1">
          <ProjectPicker selected={project} onSelect={setProject} label="Project" />
        </div>
      </div>

      {project && (
        <PurchaseForm
          key={`${project.id}-${partyType}`}
          projectId={project.id}
          partyType={partyType}
        />
      )}
    </div>
  );
}

function PurchaseForm({ projectId, partyType }: { projectId: number; partyType: PartyType }) {
  const queryClient = useQueryClient();
  const { confirm, dialog: confirmDialog } = useConfirmDialog();
  const [vendor, setVendor] = useState<PartySearchItem | null>(null);
  const [date, setDate] = useState("");
  const [invoiceNumber, setInvoiceNumber] = useState("");
  const [rows, setRows] = useState<Row[]>([emptyRow()]);

  const { data: purchases = [] } = useQuery({
    queryKey: ["vendor-purchases", projectId],
    queryFn: () => listVendorPurchases(projectId),
  });
  const {
    pageRows: pagedPurchases,
    page: purchasesPage,
    setPage: setPurchasesPage,
    pageCount: purchasesPageCount,
    total: purchasesTotal,
  } = usePagination(purchases, 20);
  const { data: outstanding } = useQuery({
    queryKey: ["vendor-outstanding", vendor?.id],
    queryFn: () => vendorOutstanding(vendor!.id),
    enabled: vendor !== null,
  });

  const lineTotal = (r: Row) =>
    round3((Number(r.quantity) || 0) * (Number(r.rate) || 0) + (Number(r.taxAmount) || 0));
  const total = useMemo(() => round3(rows.reduce((s, r) => s + lineTotal(r), 0)), [rows]);

  const record = useMutation({
    mutationFn: () =>
      recordVendorPurchase({
        projectId,
        vendorId: vendor!.id,
        date,
        total,
        invoiceNumber: invoiceNumber.trim() || null,
        lines: rows.map<PurchaseLineInput>((r) => ({
          itemId: r.item?.id ?? null,
          itemName: r.item!.name,
          quantity: Number(r.quantity),
          unit: r.unit.trim(),
          rate: Number(r.rate),
          taxAmount: Number(r.taxAmount) || 0,
        })),
      }),
    onSuccess: (result) => {
      if (result.duplicateInvoiceWarning) {
        toast.warning("An invoice with this number already exists for this vendor.");
      }
      toast.success(`Purchase recorded: ${formatINR(result.purchase.total)}`);
      setRows([emptyRow()]);
      setInvoiceNumber("");
      void queryClient.invalidateQueries({ queryKey: ["vendor-purchases", projectId] });
      void queryClient.invalidateQueries({ queryKey: ["vendor-outstanding", vendor?.id] });
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError
          ? (error.fieldErrors?.total?.[0] ?? error.message)
          : "Could not record",
      ),
  });

  const reverse = useMutation({
    mutationFn: (input: { id: number; reason: string }) =>
      reverseVendorPurchase(input.id, input.reason),
    onSuccess: () => {
      toast.success("Purchase reversed");
      void queryClient.invalidateQueries({ queryKey: ["vendor-purchases", projectId] });
      void queryClient.invalidateQueries({ queryKey: ["vendor-outstanding", vendor?.id] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not reverse the purchase"),
  });

  async function handleReverse(id: number) {
    const result = await confirm({
      title: "Reverse this purchase?",
      description: "This posts a reversing entry — the original record stays for the audit trail.",
      inputLabel: "Reason",
      inputPlaceholder: "e.g. Entered against the wrong project",
      confirmLabel: "Reverse",
      destructive: true,
    });
    if (result.confirmed && result.value) reverse.mutate({ id, reason: result.value });
  }

  const ready =
    vendor !== null &&
    date !== "" &&
    total > 0 &&
    rows.every((r) => r.item !== null && Number(r.quantity) > 0 && r.unit.trim());

  const isDirty =
    vendor !== null ||
    date !== "" ||
    invoiceNumber !== "" ||
    rows.length > 1 ||
    rows.some((r) => r.item || r.quantity || r.unit || r.rate || r.taxAmount);

  useEffect(() => {
    if (!isDirty) return;
    function handler(e: BeforeUnloadEvent) {
      e.preventDefault();
    }
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [isDirty]);

  return (
    <div className="space-y-6">
      <form
        className="bg-card space-y-3 rounded border p-4"
        onSubmit={(e) => {
          e.preventDefault();
          if (ready) record.mutate();
        }}
      >
        <PartyPicker
          type={partyType}
          label={partyType === "FieldOfficer" ? "Field officer" : "Vendor"}
          selected={vendor}
          onSelect={setVendor}
        />
        {outstanding && (
          <p className="text-muted-foreground text-xs">
            Current vendor outstanding: {formatINR(outstanding.outstanding)}
          </p>
        )}

        <div className="flex gap-3">
          <label className="space-y-1">
            <span className="text-sm font-medium">Purchase date</span>
            <Input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              aria-label="Purchase date"
            />
          </label>
          <label className="space-y-1">
            <span className="text-sm font-medium">Invoice number</span>
            <Input
              value={invoiceNumber}
              onChange={(e) => setInvoiceNumber(e.target.value)}
              aria-label="Invoice number"
            />
          </label>
        </div>

        <table className="w-full text-sm">
          <thead className="text-muted-foreground">
            <tr className="text-left">
              <th className="py-1 font-medium">Item</th>
              <th className="py-1 font-medium">Qty</th>
              <th className="py-1 font-medium">Unit</th>
              <th className="py-1 font-medium">Rate</th>
              <th className="py-1 font-medium">Tax</th>
              <th className="py-1 font-medium">Line total</th>
              <th />
            </tr>
          </thead>
          <tbody>
            {rows.map((row, i) => (
              <tr key={i}>
                <td className="py-1 pr-2">
                  <ItemPicker
                    selected={row.item}
                    ariaLabel={`itemName ${i + 1}`}
                    onSelect={(item) =>
                      setRows((rs) => rs.map((r, j) => (j === i ? { ...r, item } : r)))
                    }
                  />
                </td>
                {(["quantity", "unit", "rate", "taxAmount"] as const).map((field) => {
                  const numeric = field === "quantity" || field === "rate" || field === "taxAmount";
                  return (
                    <td key={field} className="py-1 pr-2">
                      {numeric ? (
                        <AmountInput
                          value={row[field]}
                          aria-label={`${field} ${i + 1}`}
                          onChange={(v) =>
                            setRows((rs) => rs.map((r, j) => (j === i ? { ...r, [field]: v } : r)))
                          }
                        />
                      ) : (
                        <Input
                          value={row[field]}
                          aria-label={`${field} ${i + 1}`}
                          onChange={(e) =>
                            setRows((rs) =>
                              rs.map((r, j) => (j === i ? { ...r, [field]: e.target.value } : r)),
                            )
                          }
                        />
                      )}
                    </td>
                  );
                })}
                <td className="py-1 pr-2 tabular-nums">{formatINR(lineTotal(row))}</td>
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

        <div className="flex items-center justify-between">
          <Button
            type="button"
            variant="ghost"
            size="xs"
            onClick={() => setRows((rs) => [...rs, emptyRow()])}
          >
            + Add line
          </Button>
          <p className="text-sm font-semibold" data-testid="purchase-total">
            Total: {formatINR(total)}
          </p>
        </div>

        <Button type="submit" disabled={!ready || record.isPending}>
          Record purchase
        </Button>
      </form>

      <div className="space-y-3">
        {dataState({ isEmpty: purchases.length === 0, emptyLabel: "No purchases yet." })}
        {pagedPurchases.map((p) => (
          <div
            key={p.id}
            className="bg-card border-border space-y-2 rounded-xl border p-3 text-sm shadow-xs"
          >
            <div className="flex items-center justify-between">
              <span>
                {formatDate(p.date)} · {p.vendorName}
                {p.invoiceNumber && (
                  <span className="text-muted-foreground"> · {p.invoiceNumber}</span>
                )}
                <span className="ml-2">
                  <StatusBadge status={p.status} />
                </span>
              </span>
              <span className="flex items-center gap-2">
                {formatINR(p.total)}
                <span className="text-muted-foreground"> · paid {formatINR(p.partPaid)}</span>
                {p.status.toLowerCase() === "active" && (
                  <Button
                    type="button"
                    variant="ghost"
                    size="xs"
                    disabled={reverse.isPending}
                    onClick={() => void handleReverse(p.id)}
                  >
                    Reverse
                  </Button>
                )}
              </span>
            </div>
            <AttachmentPanel ownerType="VendorPurchase" ownerId={p.id} />
          </div>
        ))}
      </div>

      <PaginationBar
        page={purchasesPage}
        pageCount={purchasesPageCount}
        total={purchasesTotal}
        onPageChange={setPurchasesPage}
        itemLabel="purchases"
      />

      {confirmDialog}
    </div>
  );
}
