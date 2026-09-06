"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { Dialog, DialogContent, DialogTitle } from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { getAccount, updateAccount } from "./api";
import type { AccountDetail as AccountDetailDto } from "./types";

export function AccountDetail({ id }: { id: number }) {
  const queryClient = useQueryClient();
  const { confirm, dialog: confirmDialog } = useConfirmDialog();
  const [editing, setEditing] = useState(false);

  const {
    data: account,
    isPending,
    isError,
  } = useQuery({
    queryKey: ["accounts", id],
    queryFn: () => getAccount(id),
  });

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["accounts"] });

  const save = useMutation({
    mutationFn: (input: {
      name: string;
      bankName: string | null;
      accountNumber: string | null;
      ifsc: string | null;
    }) =>
      updateAccount(id, {
        name: input.name,
        type: account!.type,
        openingBalance: account!.openingBalance,
        openingBalanceDate: account!.openingBalanceDate,
        bankName: input.bankName,
        accountNumber: input.accountNumber,
        ifsc: input.ifsc,
        isActive: account!.isActive,
        concurrencyStamp: account!.concurrencyStamp,
      }),
    onSuccess: () => {
      toast.success("Account updated");
      setEditing(false);
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the account"),
  });

  const toggleActive = useMutation({
    mutationFn: () =>
      updateAccount(id, {
        name: account!.name,
        type: account!.type,
        openingBalance: account!.openingBalance,
        openingBalanceDate: account!.openingBalanceDate,
        bankName: account!.bankName,
        accountNumber: account!.accountNumber,
        ifsc: account!.ifsc,
        isActive: !account!.isActive,
        concurrencyStamp: account!.concurrencyStamp,
      }),
    onSuccess: (updated) => {
      toast.success(updated.isActive ? "Account reactivated" : "Account deactivated");
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not update the account"),
  });

  async function handleToggle() {
    if (!account) return;
    const result = await confirm(
      account.isActive
        ? {
            title: `Deactivate "${account.name}"?`,
            description: "It will no longer show up when picking an account for a transaction.",
            confirmLabel: "Deactivate",
            destructive: true,
          }
        : { title: `Reactivate "${account.name}"?`, confirmLabel: "Reactivate" },
    );
    if (result.confirmed) toggleActive.mutate();
  }

  if (isPending || isError || !account) {
    return dataState({
      isPending,
      isError: isError || !account,
      errorLabel: "Account not found.",
    });
  }

  const backHref = account.type === "Cash" ? "/accounts/cash" : "/accounts/bank";

  return (
    <div className="max-w-xl space-y-6">
      <div>
        <Link className="text-primary text-sm underline" href={backHref}>
          ← {account.type} accounts
        </Link>
        <div className="mt-1 flex items-center gap-2">
          <h1 className="font-heading text-2xl font-semibold">{account.name}</h1>
          <StatusBadge status={account.isActive ? "Active" : "Inactive"} />
          <div className="ml-auto flex gap-2">
            <Button type="button" variant="outline" size="xs" onClick={() => setEditing(true)}>
              Edit
            </Button>
            <Button type="button" variant="outline" size="xs" onClick={() => void handleToggle()}>
              {account.isActive ? "Deactivate" : "Reactivate"}
            </Button>
          </div>
        </div>
      </div>

      <div className="bg-card border-border rounded-xl border p-4 shadow-xs">
        <p className="text-muted-foreground text-xs uppercase">Balance (derived from ledger)</p>
        <p className="text-2xl font-semibold">{formatINR(account.balance)}</p>
        <p className="text-muted-foreground mt-1 text-xs">
          Opening {formatINR(account.openingBalance)} on {formatDate(account.openingBalanceDate)} ·
          computed, never stored
        </p>
      </div>

      <div className="bg-card border-border rounded-xl border p-5 shadow-xs">
        <dl className="grid grid-cols-2 gap-x-4 gap-y-2 text-sm">
          <dt className="text-muted-foreground">Type</dt>
          <dd>{account.type}</dd>
          {account.type === "Bank" && (
            <>
              <dt className="text-muted-foreground">Bank</dt>
              <dd>{account.bankName ?? "—"}</dd>
              <dt className="text-muted-foreground">Account number</dt>
              <dd>{account.accountNumber ?? "—"}</dd>
              <dt className="text-muted-foreground">IFSC</dt>
              <dd>{account.ifsc ?? "—"}</dd>
            </>
          )}
          <dt className="text-muted-foreground">Opening balance</dt>
          <dd>
            {formatINR(account.openingBalance)}
            {account.openingBalanceLocked && (
              <span className="text-muted-foreground"> · locked (has transactions)</span>
            )}
          </dd>
        </dl>
      </div>

      <Dialog open={editing} onOpenChange={setEditing}>
        <DialogContent>
          <DialogTitle>Edit account</DialogTitle>
          <EditAccountForm
            account={account}
            saving={save.isPending}
            onSave={(input) => save.mutate(input)}
          />
        </DialogContent>
      </Dialog>

      {confirmDialog}
    </div>
  );
}

function EditAccountForm({
  account,
  onSave,
  saving,
}: {
  account: AccountDetailDto;
  onSave: (input: {
    name: string;
    bankName: string | null;
    accountNumber: string | null;
    ifsc: string | null;
  }) => void;
  saving: boolean;
}) {
  const [name, setName] = useState(account.name);
  const [bankName, setBankName] = useState(account.bankName ?? "");
  const [accountNumber, setAccountNumber] = useState(account.accountNumber ?? "");
  const [ifsc, setIfsc] = useState(account.ifsc ?? "");

  return (
    <form
      className="mt-3 space-y-3"
      onSubmit={(e) => {
        e.preventDefault();
        if (!name.trim()) return;
        onSave({
          name: name.trim(),
          bankName: bankName.trim() || null,
          accountNumber: accountNumber.trim() || null,
          ifsc: ifsc.trim() || null,
        });
      }}
    >
      <div className="space-y-1">
        <label className="text-sm font-medium">Name</label>
        <Input value={name} onChange={(e) => setName(e.target.value)} autoFocus />
      </div>
      {account.type === "Bank" && (
        <>
          <div className="space-y-1">
            <label className="text-sm font-medium">Bank</label>
            <Input value={bankName} onChange={(e) => setBankName(e.target.value)} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">Account number</label>
            <Input value={accountNumber} onChange={(e) => setAccountNumber(e.target.value)} />
          </div>
          <div className="space-y-1">
            <label className="text-sm font-medium">IFSC</label>
            <Input value={ifsc} onChange={(e) => setIfsc(e.target.value)} />
          </div>
        </>
      )}
      <div className="flex justify-end gap-2 pt-2">
        <Button type="submit" disabled={saving || !name.trim()}>
          Save
        </Button>
      </div>
    </form>
  );
}
