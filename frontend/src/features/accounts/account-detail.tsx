"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { getAccount } from "./api";
import { formatDate, formatINR } from "@/lib/format";

export function AccountDetail({ id }: { id: number }) {
  const {
    data: account,
    isPending,
    isError,
  } = useQuery({
    queryKey: ["accounts", id],
    queryFn: () => getAccount(id),
  });

  if (isPending) return <p className="text-muted-foreground text-sm">Loading…</p>;
  if (isError || !account) return <p className="text-destructive text-sm">Account not found.</p>;

  const backHref = account.type === "Cash" ? "/accounts/cash" : "/accounts/bank";

  return (
    <div className="max-w-xl space-y-6">
      <div>
        <Link className="text-primary text-sm underline" href={backHref}>
          ← {account.type} accounts
        </Link>
        <h1 className="mt-1 text-lg font-semibold">{account.name}</h1>
      </div>

      <div className="rounded border p-4">
        <p className="text-muted-foreground text-xs uppercase">Balance (derived from ledger)</p>
        <p className="text-2xl font-semibold">{formatINR(account.balance)}</p>
        <p className="text-muted-foreground mt-1 text-xs">
          Opening {formatINR(account.openingBalance)} on {formatDate(account.openingBalanceDate)} ·
          computed, never stored
        </p>
      </div>

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
        <dt className="text-muted-foreground">Status</dt>
        <dd>{account.isActive ? "Active" : "Inactive"}</dd>
      </dl>
    </div>
  );
}
