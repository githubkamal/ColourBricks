"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { formatDate, formatINR } from "@/lib/format";
import { useQueryParam } from "@/lib/use-query-param";
import {
  settlementAllocationHistory,
  vendorPaymentAllocationReport,
  type VendorPaymentAllocationReportRow,
} from "./api";

export function VendorPaymentAllocationReportPage() {
  const [vendorId, setVendorId] = useQueryParam("vendorId", "");
  const [vendorName, setVendorName] = useQueryParam("vendorName", "");
  const [dateFrom, setDateFrom] = useQueryParam("dateFrom", "");
  const [dateTo, setDateTo] = useQueryParam("dateTo", "");
  const [openSettlement, setOpenSettlement] = useState<number | null>(null);

  const vendor: PartySearchItem | null = vendorId
    ? { id: Number(vendorId), name: vendorName, types: [], category: null }
    : null;

  function setVendor(party: PartySearchItem | null) {
    setVendorId(party ? String(party.id) : "");
    setVendorName(party ? party.name : "");
  }

  const { data: rows = [] } = useQuery({
    queryKey: ["allocation-report", vendor?.id ?? null, dateFrom, dateTo],
    queryFn: () =>
      vendorPaymentAllocationReport({
        vendorId: vendor?.id ?? null,
        dateFrom: dateFrom || null,
        dateTo: dateTo || null,
      }),
  });

  const detail = useQuery({
    queryKey: ["allocation-history", openSettlement],
    queryFn: () => settlementAllocationHistory(openSettlement!),
    enabled: openSettlement !== null,
  });

  return (
    <div className="max-w-4xl space-y-6">
      <PageHeader title="Vendor Payment Allocation Report" />

      <div className="flex flex-wrap items-end gap-3">
        <div className="min-w-64">
          <PartyPicker type="Vendor" label="Vendor" selected={vendor} onSelect={setVendor} />
        </div>
        <label className="space-y-1">
          <span className="text-sm font-medium">From</span>
          <Input
            type="date"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
            aria-label="From"
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">To</span>
          <Input
            type="date"
            value={dateTo}
            onChange={(e) => setDateTo(e.target.value)}
            aria-label="To"
          />
        </label>
      </div>

      {dataState({
        isEmpty: rows.length === 0,
        emptyLabel: "No allocations match the filters.",
      }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Date</th>
                <th className="p-2 font-medium">Vendor</th>
                <th className="p-2 font-medium">Total payment</th>
                <th className="p-2 font-medium">Project</th>
                <th className="p-2 font-medium">Allocated</th>
                <th className="p-2 font-medium">Status</th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r: VendorPaymentAllocationReportRow, i) => {
                const firstOfGroup = i === 0 || rows[i - 1].settlementId !== r.settlementId;
                return (
                  <tr
                    key={`${r.settlementId}-${r.projectId ?? "adv"}-${i}`}
                    className={firstOfGroup ? "border-t" : "border-b-0"}
                  >
                    <td className="p-2">{firstOfGroup ? formatDate(r.date) : ""}</td>
                    <td className="p-2">{firstOfGroup ? r.vendorName : ""}</td>
                    <td className="p-2 tabular-nums">
                      {firstOfGroup ? (
                        <button
                          type="button"
                          className="underline underline-offset-2"
                          onClick={() =>
                            setOpenSettlement((cur) =>
                              cur === r.settlementId ? null : r.settlementId,
                            )
                          }
                        >
                          {formatINR(r.totalPayment)}
                        </button>
                      ) : (
                        ""
                      )}
                    </td>
                    <td className="p-2">{r.projectName}</td>
                    <td className="p-2 font-medium tabular-nums">{formatINR(r.allocated)}</td>
                    <td className="p-2">
                      <StatusBadge status={r.status} />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}

      {openSettlement !== null && detail.data && (
        <div
          className="bg-card border-border rounded-xl border p-3 text-sm shadow-xs"
          data-testid="allocation-detail"
        >
          <p className="mb-2 font-medium">
            Settlement #{detail.data.settlementId} — {detail.data.vendorName} —{" "}
            {formatINR(detail.data.totalPayment)} ({detail.data.status})
          </p>
          <ul className="space-y-1">
            {detail.data.lines.map((l, i) => (
              <li key={i} className="flex justify-between">
                <span>
                  {l.projectName}
                  {l.obligationReference ? ` · ${l.obligationReference}` : ""} · {l.method}
                </span>
                <span className="font-medium">{formatINR(l.amount)}</span>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}
