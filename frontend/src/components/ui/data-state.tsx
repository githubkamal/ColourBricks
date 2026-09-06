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
  if (isPending) return <Banner label={loadingLabel} />;
  if (isError) return <Banner label={errorLabel} tone="negative" />;
  if (isEmpty) return <Banner label={emptyLabel} />;
  return null;
}

function Banner({ label, tone }: { label: string; tone?: "negative" }) {
  return (
    <div
      className={cn(
        "rounded-lg border border-dashed p-10 text-center text-sm",
        tone === "negative" ? "text-negative border-negative/30" : "text-muted-foreground",
      )}
    >
      {label}
    </div>
  );
}
