import { cn } from "@/lib/utils";

/**
 * The card a data table sits in. Every grid in the app used to hand-roll the
 * same `bg-card … overflow-x-auto rounded-xl border` div, and the handful that
 * forgot the `overflow-x-auto` let wide rows — a long narration, a fifteen-digit
 * amount — push the figures out past the card's own border (client report,
 * 2026-09-23). One component, so a table can't be built without its scroller.
 *
 * `data-table-scroll` is what globals.css hangs the responsive cell rules off:
 * text cells wrap, figures stay whole, and whatever is left over scrolls here
 * instead of overflowing the page.
 */
export function TableScroll({ className, children, ...props }: React.ComponentProps<"div">) {
  return (
    <div
      data-table-scroll
      className={cn(
        "bg-card border-border w-full max-w-full overflow-x-auto overscroll-x-contain rounded-xl border shadow-xs",
        className,
      )}
      {...props}
    >
      {children}
    </div>
  );
}
