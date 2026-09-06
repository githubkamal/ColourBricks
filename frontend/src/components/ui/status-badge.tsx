import type { VariantProps } from "class-variance-authority";
import { Badge, badgeVariants } from "./badge";

type BadgeVariant = VariantProps<typeof badgeVariants>["variant"];

/**
 * The status vocabulary recurring across vendor, labour, materials, loan and
 * account pages (Active/Reversed/Closed/Draft/Submitted/Cancelled/Paid/Pending/
 * Inactive, …). Falls back to "neutral" for anything not in the map, so a page
 * can pass any status string without throwing.
 */
const STATUS_VARIANT: Record<string, BadgeVariant> = {
  active: "positive",
  paid: "positive",
  reconciled: "positive",
  approved: "positive",
  completed: "positive",
  closed: "neutral",
  inactive: "neutral",
  draft: "neutral",
  excluded: "neutral",
  ongoing: "primary",
  submitted: "primary",
  open: "primary",
  pending: "attention",
  onhold: "attention",
  "on hold": "attention",
  overdue: "negative",
  reversed: "negative",
  cancelled: "negative",
  rejected: "negative",
};

export function StatusBadge({ status }: { status: string }) {
  const variant = STATUS_VARIANT[status.toLowerCase()] ?? "neutral";
  return <Badge variant={variant}>{status}</Badge>;
}
