import { cva, type VariantProps } from "class-variance-authority";

import { cn } from "@/lib/utils";

const badgeVariants = cva(
  "inline-flex shrink-0 items-center gap-1 rounded-full px-2 py-0.5 text-xs font-medium whitespace-nowrap",
  {
    variants: {
      variant: {
        neutral: "bg-muted text-muted-foreground",
        primary: "bg-primary/10 text-primary",
        positive: "bg-positive/10 text-positive",
        negative: "bg-negative/10 text-negative",
        attention: "bg-attention/10 text-attention",

        // Fixed, theme-independent status colours (client request, 2026-09-06)
        // — plain Tailwind palette utilities, not our --primary/accent tokens,
        // so a status's colour never changes when someone picks a different
        // accent. Each status word across the app gets its own of these, so
        // no two statuses read as the same colour (see StatusBadge).
        slate: "bg-slate-100 text-slate-700 dark:bg-slate-500/15 dark:text-slate-300",
        zinc: "bg-zinc-100 text-zinc-700 dark:bg-zinc-500/15 dark:text-zinc-300",
        stone: "bg-stone-100 text-stone-700 dark:bg-stone-500/15 dark:text-stone-300",
        red: "bg-red-100 text-red-700 dark:bg-red-500/15 dark:text-red-300",
        orange: "bg-orange-100 text-orange-700 dark:bg-orange-500/15 dark:text-orange-300",
        amber: "bg-amber-100 text-amber-700 dark:bg-amber-500/15 dark:text-amber-300",
        yellow: "bg-yellow-100 text-yellow-800 dark:bg-yellow-500/15 dark:text-yellow-300",
        green: "bg-green-100 text-green-700 dark:bg-green-500/15 dark:text-green-300",
        emerald: "bg-emerald-100 text-emerald-700 dark:bg-emerald-500/15 dark:text-emerald-300",
        teal: "bg-teal-100 text-teal-700 dark:bg-teal-500/15 dark:text-teal-300",
        cyan: "bg-cyan-100 text-cyan-700 dark:bg-cyan-500/15 dark:text-cyan-300",
        sky: "bg-sky-100 text-sky-700 dark:bg-sky-500/15 dark:text-sky-300",
        blue: "bg-blue-100 text-blue-700 dark:bg-blue-500/15 dark:text-blue-300",
        indigo: "bg-indigo-100 text-indigo-700 dark:bg-indigo-500/15 dark:text-indigo-300",
        violet: "bg-violet-100 text-violet-700 dark:bg-violet-500/15 dark:text-violet-300",
        fuchsia: "bg-fuchsia-100 text-fuchsia-700 dark:bg-fuchsia-500/15 dark:text-fuchsia-300",
        pink: "bg-pink-100 text-pink-700 dark:bg-pink-500/15 dark:text-pink-300",
        rose: "bg-rose-100 text-rose-700 dark:bg-rose-500/15 dark:text-rose-300",
      },
    },
    defaultVariants: {
      variant: "neutral",
    },
  },
);

function Badge({
  className,
  variant,
  ...props
}: React.ComponentProps<"span"> & VariantProps<typeof badgeVariants>) {
  return (
    <span data-slot="badge" className={cn(badgeVariants({ variant, className }))} {...props} />
  );
}

export { Badge, badgeVariants };
