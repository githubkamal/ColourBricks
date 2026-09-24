"use client";

import { formatINR } from "@/lib/format";

const round3 = (n: number) => Math.round((n + Number.EPSILON) * 1000) / 1000;

/**
 * Opening/current bank balance, plus this statement's credits, minus its debits, gives
 * the balance once imported (client request, 2026-09-24). `currentBalance` is the
 * account's statement balance before these rows land.
 */
export function BalanceSummary({
  currentBalance,
  rows,
  currentLabel = "Current balance",
  resultLabel = "Balance after import",
}: {
  currentBalance: number;
  rows: { debit: number; credit: number }[];
  currentLabel?: string;
  resultLabel?: string;
}) {
  const credits = round3(rows.reduce((s, r) => s + r.credit, 0));
  const debits = round3(rows.reduce((s, r) => s + r.debit, 0));
  const closing = round3(currentBalance + credits - debits);

  const items: [string, string, string][] = [
    [currentLabel, formatINR(currentBalance), ""],
    ["+ Credits", formatINR(credits), "text-positive"],
    ["− Debits", formatINR(debits), "text-negative"],
    [resultLabel, formatINR(closing), "font-semibold"],
  ];

  return (
    <dl
      className="bg-card grid grid-cols-2 gap-3 rounded border p-3 text-sm sm:grid-cols-4"
      data-testid="balance-summary"
    >
      {items.map(([label, value, tone]) => (
        <div key={label}>
          <dt className="text-muted-foreground text-xs">{label}</dt>
          <dd className={`tabular-nums ${tone}`}>{value}</dd>
        </div>
      ))}
    </dl>
  );
}
