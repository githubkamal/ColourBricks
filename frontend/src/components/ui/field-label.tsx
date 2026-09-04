import * as React from "react";
import { cn } from "@/lib/utils";

/**
 * A form field's label, with an optional required marker and inline error text below
 * the field it wraps (client request, 2026-09-04 — a required field or bad value must
 * say so, not just leave the submit button silently disabled).
 */
export function FieldLabel({
  children,
  required,
  error,
  className,
}: {
  children: React.ReactNode;
  required?: boolean;
  error?: string;
  className?: string;
}) {
  return (
    <span className={cn("text-sm font-medium", className)}>
      {children}
      {required && (
        <span className="text-destructive" aria-hidden="true">
          {" "}
          *
        </span>
      )}
      {error && <span className="text-destructive block text-xs font-normal">{error}</span>}
    </span>
  );
}
