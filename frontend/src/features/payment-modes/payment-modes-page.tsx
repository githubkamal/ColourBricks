"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { ApiError } from "@/lib/api";
import { createPaymentMode, listPaymentModes, updatePaymentMode } from "./api";
import type { PaymentModeDto } from "./types";

export function PaymentModesPage() {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");

  const { data, isPending } = useQuery({
    queryKey: ["payment-modes", { includeInactive: true }],
    queryFn: () => listPaymentModes(true),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["payment-modes"] });

  const add = useMutation({
    mutationFn: () => createPaymentMode({ name: name.trim() }),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.mode.name} added`);
        setName("");
        void invalidate();
      } else {
        toast.message("That payment mode already exists");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add the payment mode"),
  });

  const toggle = useMutation({
    mutationFn: (mode: PaymentModeDto) =>
      updatePaymentMode(mode.id, {
        name: mode.name,
        requiresAccount: mode.requiresAccount,
        requiresReference: mode.requiresReference,
        isActive: !mode.isActive,
        concurrencyStamp: mode.concurrencyStamp,
      }),
    onSuccess: () => void invalidate(),
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the payment mode"),
  });

  return (
    <div className="max-w-2xl space-y-4">
      <h1 className="text-lg font-semibold">Payment Modes</h1>

      <form
        className="flex gap-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (name.trim()) add.mutate();
        }}
      >
        <Input
          value={name}
          onChange={(e) => setName(e.target.value)}
          placeholder="e.g. Cheque"
          aria-label="New payment mode"
        />
        <Button type="submit" disabled={add.isPending || !name.trim()}>
          Add
        </Button>
      </form>

      <div className="rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              <th className="p-2 font-medium">Name</th>
              <th className="p-2 font-medium">Reference</th>
              <th className="p-2 font-medium">Account</th>
              <th className="p-2 font-medium">Status</th>
              <th className="p-2 font-medium" />
            </tr>
          </thead>
          <tbody>
            {isPending && (
              <tr>
                <td colSpan={5} className="text-muted-foreground p-3 text-center">
                  Loading…
                </td>
              </tr>
            )}
            {data?.map((mode) => (
              <tr key={mode.id} className="border-b last:border-0">
                <td className="p-2">{mode.name}</td>
                <td className="text-muted-foreground p-2">
                  {mode.requiresReference ? "Required" : "—"}
                </td>
                <td className="text-muted-foreground p-2">
                  {mode.requiresAccount ? "Required" : "—"}
                </td>
                <td className="text-muted-foreground p-2">
                  {mode.isActive ? "Active" : "Inactive"}
                </td>
                <td className="p-2 text-right">
                  <Button
                    type="button"
                    variant="ghost"
                    size="xs"
                    disabled={toggle.isPending}
                    onClick={() => toggle.mutate(mode)}
                  >
                    {mode.isActive ? "Deactivate" : "Reactivate"}
                  </Button>
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
