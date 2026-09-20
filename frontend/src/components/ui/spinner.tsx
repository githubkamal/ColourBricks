import { Loader2 } from "lucide-react";

import { cn } from "@/lib/utils";

/** A branded (primary-coloured) spinner for full-page and inline loading states. */
export function Spinner({ className }: { className?: string }) {
  return (
    <Loader2
      aria-hidden="true"
      className={cn("text-primary size-5 animate-spin", className)}
    />
  );
}
