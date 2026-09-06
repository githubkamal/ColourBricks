import { cva, type VariantProps } from "class-variance-authority";

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
  className,
}: {
  label: string;
  value: React.ReactNode;
} & VariantProps<typeof statValueVariants> & { className?: string }) {
  return (
    <div className={cn("bg-card border-border rounded-xl border p-4 shadow-xs", className)}>
      <p className="text-muted-foreground text-xs">{label}</p>
      <p className={statValueVariants({ tone })}>{value}</p>
    </div>
  );
}
