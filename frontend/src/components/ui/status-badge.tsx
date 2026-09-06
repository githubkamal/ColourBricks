import type { VariantProps } from "class-variance-authority";
import { Badge, badgeVariants } from "./badge";

type BadgeVariant = VariantProps<typeof badgeVariants>["variant"];

/**
 * The status vocabulary recurring across vendor, labour, materials, loan and
 * account pages (Active/Reversed/Closed/Draft/Submitted/Cancelled/Paid/Pending/
 * Inactive, …). Falls back to "neutral" for anything not in the map, so a page
 * can pass any status string without throwing.
 *
 * Each status gets its own fixed, theme-independent colour (client request,
 * 2026-09-06) — no two statuses share a colour, and none of them move when
 * the accent picker changes (only "primary" tracks that, and nothing here
 * uses it). Loosely grouped warm/cool by good-vs-bad, but every entry is its
 * own distinct hue, not a shared bucket.
 */
const STATUS_VARIANT: Record<string, BadgeVariant> = {
  active: "emerald",
  paid: "teal",
  reconciled: "cyan",
  approved: "sky",
  completed: "green",
  closed: "slate",
  inactive: "zinc",
  draft: "blue",
  excluded: "neutral",
  ongoing: "sky",
  submitted: "green",
  open: "violet",
  pending: "amber",
  onhold: "yellow",
  "on hold": "yellow",
  overdue: "red",
  reversed: "rose",
  cancelled: "red",
  rejected: "fuchsia",
};

export function StatusBadge({ status }: { status: string }) {
  const variant = STATUS_VARIANT[status.toLowerCase()] ?? "neutral";
  return <Badge variant={variant}>{status}</Badge>;
}
