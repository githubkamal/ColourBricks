"use client";

import * as React from "react";
import { Input } from "./input";
import { cn } from "@/lib/utils";

/**
 * Empty, or digits with at most one decimal point — never negative, never a letter.
 * Capped at 15 integer digits and 3 decimal digits, matching every amount column's
 * DECIMAL(18,3) type (raised from DECIMAL(18,2) — client request, 2026-09-04) — the
 * largest value the database can hold is 999,999,999,999,999.999, and that's also
 * the largest value the field will let you type, not a database error later.
 */
const DECIMAL_PATTERN = /^\d{0,15}(\.\d{0,3})?$/;

export interface AmountInputProps extends Omit<
  React.ComponentProps<typeof Input>,
  "onChange" | "type" | "inputMode"
> {
  value: string;
  onChange: (value: string) => void;
  /** Inline error shown below the field (e.g. "Required" or "Must be greater than zero"). */
  error?: string;
}

/**
 * A numeric amount field that only ever accepts digits and a single decimal point — a
 * keystroke that would produce anything else is simply dropped, not just left to fail
 * validation later (client request, 2026-09-04: decimal values must still be accepted,
 * only non-numeric input is blocked). Also caps length at the database's DECIMAL(18,3)
 * limit, so a value too large to save is rejected here, not as a backend error. Pair
 * with `error` for an inline message instead of a silently-disabled submit button.
 */
export function AmountInput({ value, onChange, error, className, ...props }: AmountInputProps) {
  return (
    <div className="space-y-1">
      <Input
        {...props}
        inputMode="decimal"
        value={value}
        aria-invalid={!!error}
        className={cn(error && "border-destructive", className)}
        onChange={(e) => {
          const next = e.target.value;
          if (DECIMAL_PATTERN.test(next)) {
            onChange(next);
          }
        }}
      />
      {error && <p className="text-destructive text-xs">{error}</p>}
    </div>
  );
}
