"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import Link from "next/link";
import { useState } from "react";
import { toast } from "sonner";
import { AmountInput } from "@/components/ui/amount-input";
import { SubmitButton } from "@/components/ui/submit-button";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatusBadge } from "@/components/ui/status-badge";
import { ApiError } from "@/lib/api";
import { formatINR, parseAmount } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import { createAccount, listAccounts } from "./api";
import type { AccountType } from "./types";

const today = () => new Date().toISOString().slice(0, 10);

export function AccountsPage({ type }: { type: AccountType }) {
  const queryClient = useQueryClient();
  const [name, setName] = useState("");
  const [bankName, setBankName] = useState("");
  const [accountNumber, setAccountNumber] = useState("");
  const [openingBalance, setOpeningBalance] = useState("");
  const [openingBalanceDate, setOpeningBalanceDate] = useState(today);

  const { data, isPending } = useQuery({
    queryKey: ["accounts", { type }],
    queryFn: () => listAccounts(type, true),
  });
  const { pageRows, page, setPage, pageCount, total } = usePagination(data, 20);

  const add = useMutation({
    mutationFn: () =>
      createAccount({
        name: name.trim(),
        type,
        openingBalance: openingBalance.trim() ? parseAmount(openingBalance) : 0,
        openingBalanceDate,
        bankName: type === "Bank" ? bankName.trim() || null : null,
        accountNumber: type === "Bank" ? accountNumber.trim() || null : null,
      }),
    onSuccess: (outcome) => {
      if (outcome.kind === "created") {
        toast.success(`${outcome.account.name} added`);
        setName("");
        setBankName("");
        setAccountNumber("");
        setOpeningBalance("");
        setOpeningBalanceDate(today());
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
        className="bg-card border-border grid gap-3 rounded-xl border p-4 shadow-xs sm:grid-cols-2"
        onSubmit={(e) => {
          e.preventDefault();
          if (name.trim()) add.mutate();
        }}
      >
        <label className="space-y-1 sm:col-span-2">
          <span className="text-sm font-medium">Name</span>
          <Input
            value={name}
            onChange={(e) => setName(e.target.value)}
            placeholder={type === "Cash" ? "e.g. Office Cash" : "e.g. HDFC"}
            aria-label={`New ${type.toLowerCase()} account name`}
          />
        </label>
        {type === "Bank" && (
          <>
            <label className="space-y-1">
              <span className="text-sm font-medium">Bank</span>
              <Input
                value={bankName}
                onChange={(e) => setBankName(e.target.value)}
                aria-label="Bank name"
              />
            </label>
            <label className="space-y-1">
              <span className="text-sm font-medium">Account number</span>
              <Input
                value={accountNumber}
                onChange={(e) => setAccountNumber(e.target.value)}
                aria-label="Account number"
              />
            </label>
          </>
        )}
        <label className="space-y-1">
          <span className="text-sm font-medium">Opening balance</span>
          <AmountInput
            value={openingBalance}
            onChange={setOpeningBalance}
            placeholder="0.00"
            aria-label="Opening balance"
          />
        </label>
        <label className="space-y-1">
          <span className="text-sm font-medium">As of</span>
          <Input
            type="date"
            value={openingBalanceDate}
            onChange={(e) => setOpeningBalanceDate(e.target.value)}
            aria-label="Opening balance date"
          />
        </label>
        <div className="flex justify-end sm:col-span-2">
          <SubmitButton type="submit" disabled={!name.trim() || !openingBalanceDate} mutation={add}>
            Add
          </SubmitButton>
        </div>
      </form>

      {dataState({
        isPending,
        isEmpty: !isPending && (data?.length ?? 0) === 0,
        emptyLabel: `No ${type.toLowerCase()} accounts found.`,
      }) ?? (
        <div
          data-table-scroll
          className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs"
        >
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Name</th>
                {type === "Bank" && <th className="p-2 font-medium">Bank</th>}
                {type === "Bank" && <th className="p-2 font-medium">Number</th>}
                {type === "Bank" && <th className="p-2 text-right font-medium">Opening</th>}
                {type === "Bank" && (
                  <th
                    className="p-2 text-right font-medium"
                    title="Opening + statement credits − debits"
                  >
                    Balance
                  </th>
                )}
                <th className="p-2 font-medium">Status</th>
                <th className="p-2 font-medium" />
              </tr>
            </thead>
            <tbody>
              {pageRows.map((account) => (
                <tr key={account.id} className="border-b last:border-0">
                  <td className="p-2">{account.name}</td>
                  {type === "Bank" && (
                    <td className="text-muted-foreground p-2">{account.bankName ?? "—"}</td>
                  )}
                  {type === "Bank" && (
                    <td className="text-muted-foreground p-2">{account.accountNumber ?? "—"}</td>
                  )}
                  {type === "Bank" && (
                    <td className="text-muted-foreground p-2 text-right tabular-nums">
                      {formatINR(account.openingBalance)}
                    </td>
                  )}
                  {type === "Bank" && (
                    <td className="p-2 text-right font-medium tabular-nums">
                      {formatINR(account.statementBalance)}
                    </td>
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

      <PaginationBar
        page={page}
        pageCount={pageCount}
        total={total}
        onPageChange={setPage}
        itemLabel="accounts"
      />
    </div>
  );
}
