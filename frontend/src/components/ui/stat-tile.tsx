import { cva, type VariantProps } from "class-variance-authority";
import type { LucideIcon } from "lucide-react";

import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

const statValueVariants = cva("num mt-2 text-xl font-semibold", {
  variants: {
    tone: {
      neutral: "text-foreground",
      positive: "text-positive",
      negative: "text-negative",
      attention: "text-attention",
    },
  },
  defaultVariants: {
    tone: "neutral",
  },
});

// A tinted icon badge per tone (client request, 2026-09-20 — "add colour to
// the cards") so a tile's meaning reads at a glance, not just its number.
const iconBadgeVariants = cva("flex size-8 shrink-0 items-center justify-center rounded-lg", {
  variants: {
    tone: {
      neutral: "bg-muted text-muted-foreground",
      positive: "bg-positive/10 text-positive",
      negative: "bg-negative/10 text-negative",
      attention: "bg-attention/10 text-attention",
    },
  },
  defaultVariants: {
    tone: "neutral",
  },
});

// A slim tone-coloured strip along the top edge — the same colour story as the
// icon badge, so the card itself carries a touch of colour instead of staying
// a flat white rectangle.
const topAccentVariants = cva("absolute inset-x-0 top-0 h-1", {
  variants: {
    tone: {
      neutral: "bg-border",
      positive: "bg-positive",
      negative: "bg-negative",
      attention: "bg-attention",
    },
  },
  defaultVariants: {
    tone: "neutral",
  },
});

export function StatTile({
  label,
  value,
  tone,
  icon: Icon,
  className,
}: {
  label: string;
  value: React.ReactNode;
  icon?: LucideIcon;
} & VariantProps<typeof statValueVariants> & { className?: string }) {
  return (
    <div
      className={cn(
        "bg-card border-border relative overflow-hidden rounded-xl border p-4 shadow-xs",
        className,
      )}
    >
      <div aria-hidden="true" className={topAccentVariants({ tone })} />
      <div className="flex items-center gap-2">
        {Icon && (
          <div className={iconBadgeVariants({ tone })}>
            <Icon aria-hidden="true" className="size-4 shrink-0 stroke-[2.5]" />
          </div>
        )}
        <p className="text-muted-foreground text-xs font-semibold">{label}</p>
      </div>
      <p className={statValueVariants({ tone })}>{value}</p>
    </div>
  );
}

/** Placeholder shape for a `StatTile` grid while its query is pending. */
export function StatTileSkeleton() {
  return (
    <div
      className="bg-card border-border relative overflow-hidden rounded-xl border p-4 shadow-xs"
      aria-hidden="true"
    >
      <div className="bg-border absolute inset-x-0 top-0 h-1" />
      <div className="flex items-center gap-2">
        <Skeleton className="size-8 shrink-0 rounded-lg" />
        <Skeleton className="h-3 w-20 rounded-full" />
      </div>
      <Skeleton className="mt-2 h-6 w-24 rounded-md" />
    </div>
  );
}
