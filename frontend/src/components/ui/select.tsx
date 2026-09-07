import * as React from "react";
import { cn } from "@/lib/utils";

function Select({ className, ...props }: React.ComponentProps<"select">) {
  return (
    <select
      data-slot="select"
      className={cn(
        "border-input bg-card h-9 rounded-md border px-3 py-1 text-sm shadow-xs transition-shadow outline-none",
        "focus-visible:border-ring focus-visible:ring-ring/40 focus-visible:ring-3",
        "disabled:cursor-not-allowed disabled:opacity-50",
        className,
      )}
      {...props}
    />
  );
}

export { Select };
