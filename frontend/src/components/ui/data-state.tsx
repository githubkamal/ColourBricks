import { AlertTriangle, Inbox } from "lucide-react";

import { Skeleton } from "@/components/ui/skeleton";
import { cn } from "@/lib/utils";

/**
 * A loading/error/empty banner shared by list and dashboard pages, replacing the
 * hand-rolled `<tr><td colSpan>…</td></tr>` and `<p>` variants scattered across
 * features. Returns `null` when none of the three states apply, so callers do
 * `dataState(...) ?? <table>…</table>`.
 */
export function dataState({
  isPending,
  isError,
  isEmpty,
  loadingLabel = "Loading…",
  errorLabel = "Could not load this data.",
  emptyLabel = "Nothing here yet.",
}: {
  isPending?: boolean;
  isError?: boolean;
  isEmpty?: boolean;
  loadingLabel?: string;
  errorLabel?: string;
  emptyLabel?: string;
}): React.ReactNode | null {
  if (isPending) return <LoadingRows label={loadingLabel} />;
  if (isError) return <Banner label={errorLabel} tone="negative" />;
  if (isEmpty) return <Banner label={emptyLabel} />;
  return null;
}

/** A few shimmering row placeholders — reads as "still working", not a dead page. */
function LoadingRows({ label }: { label: string }) {
  return (
    <div
      className="bg-card border-border space-y-3 rounded-xl border p-5 shadow-xs"
      aria-busy="true"
      aria-live="polite"
    >
      <span className="sr-only">{label}</span>
      {[100, 84, 92, 70].map((width, i) => (
        <div key={i} className="flex items-center gap-3">
          <Skeleton className="size-8 shrink-0 rounded-lg" />
          <Skeleton className="h-3.5 flex-1 rounded-full" style={{ maxWidth: `${width}%` }} />
        </div>
      ))}
    </div>
  );
}

function Banner({ label, tone }: { label: string; tone?: "negative" }) {
  const Icon = tone === "negative" ? AlertTriangle : Inbox;
  return (
    <div
      className={cn(
        "flex flex-col items-center gap-2 rounded-xl border border-dashed p-10 text-center text-sm",
        tone === "negative"
          ? "text-negative border-negative/30 bg-negative/5"
          : "text-muted-foreground bg-muted/30",
      )}
    >
      <Icon aria-hidden="true" className="size-5 opacity-70" />
      {label}
    </div>
  );
}
