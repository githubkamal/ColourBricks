"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { dataState } from "@/components/ui/data-state";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatusBadge } from "@/components/ui/status-badge";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import { useQueryParamNumber } from "@/lib/use-query-param";
import { generateSchedule, getSchedule, listLoans, regenerateSchedule } from "./api";

/** EMI Schedule (BRD §48/§49): pick a loan, generate/regenerate, and see every instalment. */
export function LoanSchedulePage() {
  const queryClient = useQueryClient();
  const { data: loans = [] } = useQuery({ queryKey: ["loans"], queryFn: () => listLoans() });
  const [loanId, setLoanId] = useQueryParamNumber("loanId", 0);

  const { data: schedule = [], isPending } = useQuery({
    queryKey: ["loan-schedule", loanId],
    queryFn: () => getSchedule(loanId),
    enabled: loanId !== 0,
  });
  const { pageRows: pagedSchedule, page, setPage, pageCount, total } = usePagination(schedule, 20);

  const [emiAmount, setEmiAmount] = useState("");
  const [newRate, setNewRate] = useState("");

  const generate = useMutation({
    mutationFn: () => generateSchedule(loanId, emiAmount ? Number(emiAmount) : null),
    onSuccess: () => {
      toast.success("Schedule generated");
      setEmiAmount("");
      void queryClient.invalidateQueries({ queryKey: ["loan-schedule", loanId] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not generate the schedule"),
  });

  const regenerate = useMutation({
    mutationFn: () =>
      regenerateSchedule(loanId, Number(newRate), emiAmount ? Number(emiAmount) : null),
    onSuccess: () => {
      toast.success("Schedule regenerated");
      setNewRate("");
      setEmiAmount("");
      void queryClient.invalidateQueries({ queryKey: ["loan-schedule", loanId] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not regenerate the schedule"),
  });

  return (
    <div className="max-w-4xl space-y-6">
      <PageHeader title="EMI Schedule" />

      <label className="block max-w-sm space-y-1">
        <span className="text-sm font-medium">Loan</span>
        <Select
          className="w-full"
          value={loanId || ""}
          aria-label="Loan"
          onChange={(e) => setLoanId(e.target.value ? Number(e.target.value) : 0)}
        >
          <option value="">Select a loan…</option>
          {loans.map((l) => (
            <option key={l.id} value={l.id}>
              {l.lenderName} — {formatINR(l.principalAmount)} ({l.status})
            </option>
          ))}
        </Select>
      </label>

      {loanId !== 0 && (
        <>
          <div className="bg-card flex flex-wrap items-end gap-3 rounded border p-4">
            <label className="space-y-1">
              <span className="text-sm font-medium">EMI amount (optional)</span>
              <AmountInput value={emiAmount} aria-label="EMI amount" onChange={setEmiAmount} />
            </label>
            <Button
              type="button"
              variant="outline"
              onClick={() => generate.mutate()}
              disabled={generate.isPending}
            >
              Generate schedule
            </Button>

            <span className="bg-border mx-1 h-6 w-px" aria-hidden="true" />

            <label className="space-y-1">
              <span className="text-sm font-medium">New annual rate % (regenerate)</span>
              <AmountInput value={newRate} aria-label="New annual rate %" onChange={setNewRate} />
            </label>
            <Button
              type="button"
              variant="outline"
              onClick={() => regenerate.mutate()}
              disabled={regenerate.isPending || !(Number(newRate) > 0)}
            >
              Regenerate pending tail
            </Button>
          </div>

          {dataState({
            isPending,
            isEmpty: !isPending && schedule.length === 0,
            emptyLabel: "No schedule generated yet.",
          }) ?? (
            <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
              <table className="w-full text-sm">
                <thead className="bg-secondary/60 text-muted-foreground">
                  <tr className="border-b text-left">
                    <th className="p-2 font-medium">#</th>
                    <th className="p-2 font-medium">Due date</th>
                    <th className="p-2 font-medium">EMI</th>
                    <th className="p-2 font-medium">Principal</th>
                    <th className="p-2 font-medium">Interest</th>
                    <th className="p-2 font-medium">Closing principal</th>
                    <th className="p-2 font-medium">Status</th>
                  </tr>
                </thead>
                <tbody>
                  {pagedSchedule.map((i) => (
                    <tr key={i.id} className="border-b last:border-0">
                      <td className="p-2">{i.instalmentNo}</td>
                      <td className="p-2">{formatDate(i.dueDate)}</td>
                      <td className="p-2 tabular-nums">{formatINR(i.emiAmount)}</td>
                      <td className="p-2 tabular-nums">{formatINR(i.principalComponent)}</td>
                      <td className="p-2 tabular-nums">{formatINR(i.interestComponent)}</td>
                      <td className="p-2 tabular-nums">{formatINR(i.closingPrincipal)}</td>
                      <td className="p-2">
                        <StatusBadge status={i.status} />
                        {i.overdue && <span className="text-negative"> · overdue</span>}
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
              <div className="p-3">
                <PaginationBar
                  page={page}
                  pageCount={pageCount}
                  total={total}
                  onPageChange={setPage}
                  itemLabel="instalments"
                />
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
