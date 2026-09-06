import {
  AlertTriangle,
  Banknote,
  Building2,
  CheckCircle2,
  Clock,
  Landmark,
  PiggyBank,
  Receipt,
  Scale,
  TrendingDown,
  TrendingUp,
  Wallet,
  type LucideIcon,
} from "lucide-react";

/**
 * Picks a small icon for a dashboard stat tile by what its key means (client
 * request, 2026-09-07) — same substring-matching approach as `tileTone`, and
 * deliberately shared logic so a tile's icon and colour always agree (a
 * profit tile is never red with a "good news" icon, for instance).
 */
export function tileIcon(key: string): LucideIcon {
  const k = key.toLowerCase();
  if (k.includes("profit")) return Scale;
  if (k === "savings") return PiggyBank;
  if (k.includes("income") || k.includes("revenue")) return TrendingUp;
  if (k.includes("expense") || k.includes("cost")) return TrendingDown;
  if (k.includes("outstanding")) return AlertTriangle;
  if (k === "pendingreconciliation") return Clock;
  if (k === "cashbankposition" || k === "currentbalance") return Landmark;
  if (k.includes("ongoing")) return Building2;
  if (k.includes("completed")) return CheckCircle2;
  if (k.includes("paid")) return Banknote;
  if (k === "projectvalue") return Wallet;
  return Receipt;
}
