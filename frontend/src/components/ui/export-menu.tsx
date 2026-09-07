"use client";

import { Download } from "lucide-react";
import { useRef, useState } from "react";
import { buttonVariants } from "@/components/ui/button";
import {
  exportTableCsv,
  exportTableExcel,
  exportTablePdf,
  type ExportTable,
} from "@/lib/export-table";
import { cn } from "@/lib/utils";

/**
 * CSV / Excel / PDF export dropdown, shared by every screen that offers an
 * export (client request, 2026-09-07 — Excel and a real generated PDF next
 * to CSV, everywhere an export button already exists). `table` is called
 * lazily on click so callers don't have to build the export payload when
 * there's nothing to export yet.
 */
export function ExportMenu({
  table,
  disabled,
}: {
  table: () => ExportTable | null;
  disabled?: boolean;
}) {
  const [open, setOpen] = useState(false);
  const detailsRef = useRef<HTMLDetailsElement>(null);

  async function handle(kind: "csv" | "excel" | "pdf") {
    const t = table();
    if (!t) return;
    if (kind === "csv") exportTableCsv(t);
    else if (kind === "excel") await exportTableExcel(t);
    else await exportTablePdf(t);
    setOpen(false);
    if (detailsRef.current) detailsRef.current.open = false;
  }

  return (
    <details
      ref={detailsRef}
      className="relative"
      open={open}
      onToggle={(e) => setOpen(e.currentTarget.open)}
    >
      <summary
        className={cn(
          buttonVariants({ variant: "export" }),
          disabled && "pointer-events-none opacity-50",
          "list-none [&::-webkit-details-marker]:hidden",
        )}
      >
        <Download />
        Export
      </summary>
      <div className="bg-background absolute z-10 mt-1 min-w-32 space-y-0.5 rounded border p-1 shadow">
        <button
          type="button"
          className="hover:bg-muted w-full rounded px-2 py-1.5 text-left text-sm"
          onClick={() => void handle("csv")}
        >
          CSV
        </button>
        <button
          type="button"
          className="hover:bg-muted w-full rounded px-2 py-1.5 text-left text-sm"
          onClick={() => void handle("excel")}
        >
          Excel
        </button>
        <button
          type="button"
          className="hover:bg-muted w-full rounded px-2 py-1.5 text-left text-sm"
          onClick={() => void handle("pdf")}
        >
          PDF
        </button>
      </div>
    </details>
  );
}
