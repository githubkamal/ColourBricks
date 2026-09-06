"use client";

import { useQuery } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { formatDate, formatINR } from "@/lib/format";
import { reconciliationQueue, type ReconciliationRow } from "./api";

export interface BankTransactionPickerProps {
  selected: ReconciliationRow | null;
  onSelect: (row: ReconciliationRow | null) => void;
  /** Scopes the search to one account, if the payment form already has one picked. */
  accountId?: number | null;
  label?: string;
}

/**
 * Optional "link this payment to a bank transaction" picker (client request,
 * 2026-09-06): every payment form should offer it, without requiring it. Reuses
 * the existing reconciliation queue as a search source rather than a new
 * endpoint — picking a row here doesn't reconcile anything by itself, the
 * caller still has to call `reconcileDebit(row.id, { existingPaymentId })`
 * once its own payment mutation has succeeded and produced a settlement id.
 */
export function BankTransactionPicker({
  selected,
  onSelect,
  accountId,
  label = "Link bank transaction (optional)",
}: BankTransactionPickerProps) {
  const [open, setOpen] = useState(false);
  const [search, setSearch] = useState("");
  const containerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function onClickAway(event: MouseEvent) {
      if (!containerRef.current?.contains(event.target as Node)) setOpen(false);
    }
    document.addEventListener("mousedown", onClickAway);
    return () => document.removeEventListener("mousedown", onClickAway);
  }, []);

  const { data, isFetching } = useQuery({
    queryKey: ["bank-transaction-picker", accountId, search],
    queryFn: () => reconciliationQueue({ accountId, status: "Pending", search, page: 1 }),
    enabled: open,
  });

  const debits = (data?.items ?? []).filter((r) => r.type === "Debit");

  return (
    <div ref={containerRef} className="relative space-y-1">
      <span className="text-sm font-medium">{label}</span>

      {selected ? (
        <div className="bg-card flex items-center justify-between rounded border px-3 py-1.5 text-sm">
          <span className="truncate">
            {formatDate(selected.date)} · {selected.description} ·{" "}
            <span className="tabular-nums">{formatINR(selected.debit)}</span>
          </span>
          <Button type="button" variant="ghost" size="xs" onClick={() => onSelect(null)}>
            Remove
          </Button>
        </div>
      ) : (
        <Input
          value={search}
          onChange={(e) => {
            setSearch(e.target.value);
            setOpen(true);
          }}
          onFocus={() => setOpen(true)}
          placeholder="Search pending bank debits…"
          aria-label={label}
        />
      )}

      {open && !selected && (
        <div className="bg-popover absolute z-20 mt-1 max-h-64 w-full overflow-y-auto rounded border p-1 text-sm shadow-md">
          {isFetching && <p className="text-muted-foreground p-2">Searching…</p>}
          {!isFetching && debits.length === 0 && (
            <p className="text-muted-foreground p-2">No pending bank debits found.</p>
          )}
          {debits.map((row) => (
            <button
              key={row.id}
              type="button"
              className="hover:bg-secondary block w-full rounded px-2 py-1 text-left"
              onClick={() => {
                onSelect(row);
                setOpen(false);
                setSearch("");
              }}
            >
              {formatDate(row.date)} · {row.description} ·{" "}
              <span className="tabular-nums">{formatINR(row.debit)}</span>
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
