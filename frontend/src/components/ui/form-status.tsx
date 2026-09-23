"use client";

import { CheckCircle2, Loader2, XCircle } from "lucide-react";

import { ApiError } from "@/lib/api";
import { cn } from "@/lib/utils";

/**
 * The slice of a TanStack `useMutation` result this component reads. Typed
 * structurally so any mutation — whatever its data/variables — can be passed
 * without a generic dance at every call site.
 */
export interface MutationStatusLike {
  isPending: boolean;
  isSuccess: boolean;
  isError: boolean;
  error?: unknown;
}

/** First field-level message if the API sent one, else the plain message. */
export function mutationErrorMessage(error: unknown, fallback = "Something went wrong"): string {
  if (error instanceof ApiError) {
    const firstField = Object.values(error.fieldErrors)[0]?.[0];
    return firstField ?? error.message ?? fallback;
  }
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

/**
 * The inline "working… / saved / failed" line that sits next to an action
 * button (client request, 2026-09-23). A toast disappears; this stays put next
 * to the control the user just pressed, so there is always a visible answer to
 * "did that work?". Renders nothing while the mutation is idle.
 */
export function FormStatus({
  mutation,
  pendingLabel = "Working…",
  successLabel = "Done",
  errorLabel = "Something went wrong",
  className,
}: {
  mutation: MutationStatusLike;
  pendingLabel?: string;
  successLabel?: string;
  errorLabel?: string;
  className?: string;
}) {
  const state = mutation.isPending
    ? "pending"
    : mutation.isError
      ? "error"
      : mutation.isSuccess
        ? "success"
        : "idle";

  if (state === "idle") return null;

  const Icon = state === "pending" ? Loader2 : state === "success" ? CheckCircle2 : XCircle;
  const label =
    state === "pending"
      ? pendingLabel
      : state === "success"
        ? successLabel
        : mutationErrorMessage(mutation.error, errorLabel);

  return (
    <p
      role="status"
      aria-live="polite"
      data-form-status={state}
      className={cn(
        "inline-flex min-w-0 items-center gap-1.5 text-sm",
        state === "pending" && "text-muted-foreground",
        state === "success" && "text-positive",
        state === "error" && "text-negative",
        className,
      )}
    >
      <Icon
        aria-hidden="true"
        className={cn("size-4 shrink-0", state === "pending" && "animate-spin")}
      />
      <span className="min-w-0 break-words">{label}</span>
    </p>
  );
}
