"use client";

import { useQuery } from "@tanstack/react-query";
import { listPaymentModes } from "./api";
import type { PaymentModeDto } from "./types";

export interface PaymentModeSelectProps {
  value: number | null;
  onChange: (mode: PaymentModeDto | null) => void;
  label?: string;
}

/**
 * Reusable payment-mode picker (BRD §28). Only active modes are offered; a
 * deactivated mode never appears here but historical transactions still resolve
 * it by id. The parent form uses the selected mode's <c>requiresReference</c> /
 * <c>requiresAccount</c> flags to decide which fields to require.
 */
export function PaymentModeSelect({
  value,
  onChange,
  label = "Payment mode",
}: PaymentModeSelectProps) {
  const { data: modes = [], isPending } = useQuery({
    queryKey: ["payment-modes", { includeInactive: false }],
    queryFn: () => listPaymentModes(false),
  });

  return (
    <label className="block space-y-1">
      <span className="text-sm font-medium">{label}</span>
      <select
        className="w-full rounded border bg-transparent px-3 py-1.5 text-sm"
        value={value ?? ""}
        disabled={isPending}
        aria-label={label}
        onChange={(e) => {
          const id = e.target.value ? Number(e.target.value) : null;
          onChange(id === null ? null : (modes.find((m) => m.id === id) ?? null));
        }}
      >
        <option value="">Select…</option>
        {modes.map((mode) => (
          <option key={mode.id} value={mode.id}>
            {mode.name}
          </option>
        ))}
      </select>
    </label>
  );
}
