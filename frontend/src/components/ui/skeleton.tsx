import { cn } from "@/lib/utils";

/**
 * A shimmering placeholder block for content that's still loading — swaps the
 * flat "Loading…" text banners for something that reads as motion, not a dead
 * page (client request, 2026-09-20).
 */
export function Skeleton({ className, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-slot="skeleton"
      className={cn(
        "from-muted via-muted-foreground/15 to-muted animate-[shimmer_1.6s_ease-in-out_infinite] rounded-md bg-gradient-to-r bg-[length:200%_100%]",
        className,
      )}
      {...props}
    />
  );
}
