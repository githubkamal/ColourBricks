type Tone = "neutral" | "positive" | "negative" | "attention";

/**
 * Colours a dashboard stat tile by what its key means, not just a flat neutral
 * gray for everything (client request, 2026-09-07). Shared between the Company
 * and Project dashboards so the same kind of figure always reads the same way:
 * profit/balance-type tiles flip green/red on sign, outstanding-type tiles
 * amber when non-zero, income-like tiles green, cost-like tiles red.
 */
export function tileTone(key: string, value: number): Tone {
  const k = key.toLowerCase();
  if (k.includes("profit") || k === "currentbalance" || k === "cashbankposition") {
    return value < 0 ? "negative" : "positive";
  }
  if (k.includes("outstanding") || k === "pendingreconciliation") {
    return value > 0 ? "attention" : "neutral";
  }
  if (k.includes("income") || k.includes("revenue") || k.includes("receipt") || k === "savings") {
    return "positive";
  }
  if (k.includes("expense") || k.includes("cost")) {
    return "negative";
  }
  return "neutral";
}
