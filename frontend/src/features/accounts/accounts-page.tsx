"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApiError } from "@/lib/api";
import { createAccount, listAccounts } from "./api";
import type { AccountType } from "./types";

export function AccountsPage({ type }: { type: AccountType }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");

  const { data, isPending } = useQuery({
    queryKey: ["accounts", { type }],
    queryFn: () => listAccounts(type, true),
  });

  const add = useMutation({
    mutationFn: () =>
      createAccount({
        name: name.trim(),
        type,
        openingBalance: 0,
        openingBalanceDate: new Date().toISOString().slice(0, 10),
      }),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.account.name} added`);
        setName("");
        void queryClient.invalidateQueries({ queryKey: ["accounts"] });
      } else {
        toast.message("That account already exists");
      }
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not add the account"),
  });

  return (
    <div className="max-w-2xl space-y-4">
      <PageHeader title={`${type} Accounts`} />

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
          placeholder={type === "Cash" ? "e.g. Office Cash" : "e.g. HDFC"}
          aria-label={`New ${type.toLowerCase()} account name`}
        />
        <Button type="submit" disabled={add.isPending || !name.trim()}>
          Add
        </Button>
      </form>

      {dataState({
        isPending,
        isEmpty: !isPending && (data?.length ?? 0) === 0,
        emptyLabel: `No ${type.toLowerCase()} accounts found.`,
      }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Name</th>
                {type === "Bank" && <th className="p-2 font-medium">Bank</th>}
                {type === "Bank" && <th className="p-2 font-medium">Number</th>}
                <th className="p-2 font-medium">Status</th>
                <th className="p-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {data?.map((account) => (
                <tr key={account.id} className="border-b last:border-0">
                  <td className="p-2">{account.name}</td>
                  {type === "Bank" && (
                    <td className="text-muted-foreground p-2">{account.bankName ?? "—"}</td>
                  )}
                  {type === "Bank" && (
                    <td className="text-muted-foreground p-2">{account.accountNumber ?? "—"}</td>
                  )}
                  <td className="p-2">
                    <StatusBadge status={account.isActive ? "Active" : "Inactive"} />
                  </td>
                  <td className="p-2 text-right">
                    <Link className="text-primary underline" href={`/accounts/${account.id}`}>
                      View
                    </Link>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}
