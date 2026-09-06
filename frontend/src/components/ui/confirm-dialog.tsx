"use client";

import { useCallback, useState } from "react";
import { Button } from "@/components/ui/button";
import {
  AlertDialog,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";
import { Input } from "@/components/ui/input";

export interface ConfirmOptions {
  title: string;
  description?: string;
  confirmLabel?: string;
  cancelLabel?: string;
  destructive?: boolean;
  /** Shows a text field (e.g. a reason or a temporary password) the user must fill in to confirm. */
  inputLabel?: string;
  inputType?: "text" | "password";
  inputPlaceholder?: string;
}

export interface ConfirmResult {
  confirmed: boolean;
  value?: string;
}

interface PendingConfirm extends ConfirmOptions {
  resolve: (result: ConfirmResult) => void;
}

/**
 * Replaces `window.confirm`/`window.prompt` for destructive actions with an
 * accessible, themeable dialog. Render `{dialog}` once in the component tree
 * and `await confirm({ ... })` from an event handler.
 */
export function useConfirmDialog() {
  const [pending, setPending] = useState<PendingConfirm | null>(null);
  const [value, setValue] = useState("");

  const confirm = useCallback((options: ConfirmOptions) => {
    return new Promise<ConfirmResult>((resolve) => {
      setValue("");
      setPending({ ...options, resolve });
    });
  }, []);

  function close(confirmed: boolean) {
    if (!pending) return;
    const trimmed = value.trim();
    if (confirmed && pending.inputLabel && trimmed === "") return;
    pending.resolve({ confirmed, value: pending.inputLabel ? trimmed : undefined });
    setPending(null);
  }

  const dialog = (
    <AlertDialog
      open={pending !== null}
      onOpenChange={(open) => {
        if (!open) close(false);
      }}
    >
      {pending && (
        <AlertDialogContent>
          <AlertDialogTitle>{pending.title}</AlertDialogTitle>
          {pending.description && (
            <AlertDialogDescription>{pending.description}</AlertDialogDescription>
          )}
          {pending.inputLabel && (
            <div className="mt-3">
              <label htmlFor="confirm-dialog-input" className="text-sm font-medium">
                {pending.inputLabel}
              </label>
              <Input
                id="confirm-dialog-input"
                autoFocus
                type={pending.inputType ?? "text"}
                placeholder={pending.inputPlaceholder}
                value={value}
                onChange={(event) => setValue(event.target.value)}
                className="mt-1"
                onKeyDown={(event) => {
                  if (event.key === "Enter") close(true);
                }}
              />
            </div>
          )}
          <div className="mt-4 flex justify-end gap-2">
            <Button variant="outline" onClick={() => close(false)}>
              {pending.cancelLabel ?? "Cancel"}
            </Button>
            <Button
              variant={pending.destructive ? "destructive" : "default"}
              onClick={() => close(true)}
              disabled={Boolean(pending.inputLabel) && value.trim() === ""}
            >
              {pending.confirmLabel ?? "Confirm"}
            </Button>
          </div>
        </AlertDialogContent>
      )}
    </AlertDialog>
  );

  return { confirm, dialog };
}
