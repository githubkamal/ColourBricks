import { cva, type VariantProps } from "class-variance-authority";
import type { LucideIcon } from "lucide-react";

import { cn } from "@/lib/utils";

const statValueVariants = cva("num mt-1 text-xl font-semibold", {
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
    <div className={cn("bg-card border-border rounded-xl border p-4 shadow-xs", className)}>
      <div className="flex items-center gap-1.5">
        {Icon && (
          <Icon
            aria-hidden="true"
            className="text-muted-foreground size-3.5 shrink-0 stroke-[2.5]"
          />
        )}
        <p className="text-muted-foreground text-xs font-semibold">{label}</p>
      </div>
      <p className={statValueVariants({ tone })}>{value}</p>
    </div>
  );
}
