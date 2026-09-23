"use client";

import { Button, type ButtonProps } from "./button";
import { FormStatus, type MutationStatusLike } from "./form-status";
import { cn } from "@/lib/utils";

export interface SubmitButtonProps extends Omit<ButtonProps, "loading"> {
  /** The mutation this button fires — drives both the spinner and the status line. */
  mutation: MutationStatusLike;
  pendingLabel?: string;
  successLabel?: string;
  errorLabel?: string;
  /** Extra classes for the wrapper, not the button. */
  wrapperClassName?: string;
  /** Put the status line under the button instead of beside it. */
  stacked?: boolean;
}

/**
 * An action button that answers for itself: a spinner while the request is in
 * flight and a success/failure line right beside it afterwards (client request,
 * 2026-09-23). Drop-in for `<Button>` — pass the mutation and drop the
 * `|| x.isPending` from `disabled`, which this handles.
 */
export function SubmitButton({
  mutation,
  pendingLabel,
  successLabel = "Done",
  errorLabel,
  wrapperClassName,
  stacked = false,
  children,
  ...buttonProps
}: SubmitButtonProps) {
  return (
    <span
      className={cn(
        "inline-flex min-w-0 gap-2",
        stacked ? "flex-col items-start" : "flex-wrap items-center",
        wrapperClassName,
      )}
    >
      <Button {...buttonProps} loading={mutation.isPending}>
        {children}
      </Button>
      <FormStatus
        mutation={mutation}
        pendingLabel={pendingLabel ?? "Working…"}
        successLabel={successLabel}
        errorLabel={errorLabel ?? "Could not complete that"}
      />
    </span>
  );
}
