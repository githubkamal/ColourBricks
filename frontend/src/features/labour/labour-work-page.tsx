"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PaymentModeSelect } from "@/features/payment-modes/payment-mode-select";
import { getProject } from "@/features/projects/api";
import { ProjectPicker } from "@/features/projects/project-picker";
import type { ProjectListItem } from "@/features/projects/types";
import { reconcileDebit, type ReconciliationRow } from "@/features/reconciliation/api";
import { BankTransactionPicker } from "@/features/reconciliation/bank-transaction-picker";
import { TeamPicker } from "@/features/teams/team-picker";
import type { TeamDto } from "@/features/teams/types";
import { Button } from "@/components/ui/button";
import { AmountInput } from "@/components/ui/amount-input";
import { dataState } from "@/components/ui/data-state";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { ApiError } from "@/lib/api";
import { formatDate, formatINR } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import { useQueryParamNumber } from "@/lib/use-query-param";
import {
  listWork,
  payWork,
  recordWork,
  PAYMENT_FREQUENCIES,
  type PaymentFrequency,
  type WorkEntry,
} from "./api";

export function LabourWorkPage() {
  const [projectId, setProjectId] = useQueryParamNumber("projectId", 0);
  const [manualProject, setManualProject] = useState<ProjectListItem | null | undefined>(
    undefined,
  );
  const { data: restoredProject } = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => getProject(projectId),
    enabled: projectId > 0 && manualProject === undefined,
  });
  const project = manualProject !== undefined ? manualProject : (restoredProject ?? null);

  function handleSelect(next: ProjectListItem | null) {
    setManualProject(next);
    setProjectId(next?.id ?? 0);
  }

  return (
    <div className="max-w-3xl space-y-6">
      <PageHeader title="Labour & Subcontractor Work" />

      <div className="bg-card max-w-xs rounded border p-4">
        <ProjectPicker selected={project} onSelect={handleSelect} label="Project" />
      </div>

      {project && <WorkList projectId={project.id} />}
    </div>
  );
}

function WorkList({ projectId }: { projectId: number }) {
  const queryClient = useQueryClient();
  const [team, setTeam] = useState<TeamDto | null>(null);
  const [date, setDate] = useState("");
  const [agreedValue, setAgreedValue] = useState("");
  const [workType, setWorkType] = useState("");

  const { data: work = [] } = useQuery({
    queryKey: ["labour-work", projectId],
    queryFn: () => listWork(projectId),
  });
  const { pageRows: pagedWork, page, setPage, pageCount, total } = usePagination(work, 20);

  const invalidate = () => queryClient.invalidateQueries({ queryKey: ["labour-work", projectId] });

  const add = useMutation({
    mutationFn: () =>
      recordWork({
        projectId,
        teamId: team!.id,
        date,
        agreedValue: Number(agreedValue),
        workType: workType.trim() || null,
      }),
    onSuccess: () => {
      toast.success("Work entry recorded");
      setAgreedValue("");
      setWorkType("");
      void invalidate();
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not record the work entry"),
  });

  const ready = team !== null && date !== "" && Number(agreedValue) > 0;

  return (
    <div className="space-y-6">
      <form
        className="bg-card space-y-3 rounded border p-4"
        onSubmit={(e) => {
          e.preventDefault();
          if (ready) add.mutate();
        }}
      >
        <TeamPicker selected={team} onSelect={setTeam} label="Team" />
        <div className="flex gap-3">
          <label className="space-y-1">
            <span className="text-sm font-medium">Work date</span>
            <Input
              type="date"
              value={date}
              onChange={(e) => setDate(e.target.value)}
              aria-label="Work date"
            />
          </label>
          <label className="space-y-1">
            <span className="text-sm font-medium">Agreed work value</span>
            <AmountInput
              value={agreedValue}
              onChange={setAgreedValue}
              aria-label="Agreed work value"
            />
          </label>
          <label className="space-y-1">
            <span className="text-sm font-medium">Work type</span>
            <Input
              value={workType}
              onChange={(e) => setWorkType(e.target.value)}
              aria-label="Work type"
            />
          </label>
        </div>
        <Button type="submit" disabled={!ready || add.isPending}>
          Record work entry
        </Button>
      </form>

      <div className="space-y-3">
        {dataState({ isEmpty: work.length === 0, emptyLabel: "No work entries yet." })}
        {pagedWork.map((entry) => (
          <WorkRow key={entry.id} entry={entry} onPaid={invalidate} />
        ))}
      </div>

      <PaginationBar
        page={page}
        pageCount={pageCount}
        total={total}
        onPageChange={setPage}
        itemLabel="work entries"
      />
    </div>
  );
}

function WorkRow({ entry, onPaid }: { entry: WorkEntry; onPaid: () => void }) {
  const queryClient = useQueryClient();
  const [paying, setPaying] = useState(false);
  const [amount, setAmount] = useState("");
  const [frequency, setFrequency] = useState<PaymentFrequency>("Weekly");
  const [date, setDate] = useState("");
  const [paymentModeId, setPaymentModeId] = useState<number | null>(null);
  const [accountId, setAccountId] = useState<number | "">("");
  const [bankTx, setBankTx] = useState<ReconciliationRow | null>(null);

  const { data: accounts = [] } = useQuery({
    queryKey: ["accounts", { all: true }],
    queryFn: () => listAccounts(),
  });

  const pay = useMutation({
    mutationFn: () =>
      payWork(entry.id, {
        date,
        amount: Number(amount),
        frequency,
        paymentModeId: paymentModeId!,
        accountId: accountId === "" ? null : accountId,
      }),
    onSuccess: async (result) => {
      toast.success("Payment recorded");
      setPaying(false);
      setAmount("");
      onPaid();
      if (bankTx) {
        try {
          await reconcileDebit(bankTx.id, { existingPaymentId: result.id });
          toast.success("Linked to the bank transaction");
          void queryClient.invalidateQueries({ queryKey: ["reconciliation"] });
        } catch (error) {
          toast.error(
            error instanceof ApiError
              ? `Payment saved, but couldn't link the bank transaction: ${error.message}`
              : "Payment saved, but couldn't link the bank transaction — link it from the Reconciliation Queue instead.",
          );
        }
        setBankTx(null);
      }
    },
    onError: (error) =>
      toast.error(
        error instanceof ApiError ? (error.fieldErrors?.amount?.[0] ?? error.message) : "Failed",
      ),
  });

  return (
    <div className="bg-card border-border rounded-xl border p-3 text-sm shadow-xs">
      <div className="flex items-center justify-between">
        <span>
          {formatDate(entry.date)} · {entry.teamName}
          {entry.workType && <span className="text-muted-foreground"> · {entry.workType}</span>}
        </span>
        <span className="tabular-nums">
          Agreed {formatINR(entry.agreedValue)} · Paid {formatINR(entry.totalPaid)} ·{" "}
          <span className="font-semibold" data-testid={`outstanding-${entry.id}`}>
            Outstanding {formatINR(entry.outstanding)}
          </span>
        </span>
      </div>
      {entry.outstanding > 0 && (
        <div className="mt-2">
          {paying ? (
            <form
              className="flex flex-wrap items-end gap-2"
              onSubmit={(e) => {
                e.preventDefault();
                if (date && Number(amount) > 0 && paymentModeId !== null) pay.mutate();
              }}
            >
              <label className="space-y-1">
                <span className="text-xs">Date</span>
                <Input
                  type="date"
                  value={date}
                  onChange={(e) => setDate(e.target.value)}
                  aria-label={`Pay date ${entry.id}`}
                />
              </label>
              <label className="space-y-1">
                <span className="text-xs">Amount</span>
                <AmountInput
                  className="w-28"
                  value={amount}
                  onChange={setAmount}
                  aria-label={`Pay amount ${entry.id}`}
                />
              </label>
              <label className="space-y-1">
                <span className="text-xs">Frequency</span>
                <select
                  className="bg-card block rounded border px-2 py-1.5 text-sm"
                  value={frequency}
                  aria-label={`Pay frequency ${entry.id}`}
                  onChange={(e) => setFrequency(e.target.value as PaymentFrequency)}
                >
                  {PAYMENT_FREQUENCIES.map((f) => (
                    <option key={f} value={f}>
                      {f}
                    </option>
                  ))}
                </select>
              </label>
              <PaymentModeSelect
                value={paymentModeId}
                onChange={(m) => setPaymentModeId(m?.id ?? null)}
              />
              <label className="space-y-1">
                <span className="text-xs">Account</span>
                <select
                  className="bg-card block rounded border px-2 py-1.5 text-sm"
                  value={accountId}
                  aria-label={`Pay account ${entry.id}`}
                  onChange={(e) => setAccountId(e.target.value ? Number(e.target.value) : "")}
                >
                  <option value="">None</option>
                  {accounts.map((a) => (
                    <option key={a.id} value={a.id}>
                      {a.name}
                    </option>
                  ))}
                </select>
              </label>
              <div className="w-56">
                <BankTransactionPicker
                  selected={bankTx}
                  onSelect={setBankTx}
                  accountId={accountId === "" ? null : accountId}
                />
              </div>
              <Button type="submit" size="xs" disabled={pay.isPending}>
                Save payment
              </Button>
              <Button type="button" size="xs" variant="ghost" onClick={() => setPaying(false)}>
                Cancel
              </Button>
            </form>
          ) : (
            <Button type="button" size="xs" variant="ghost" onClick={() => setPaying(true)}>
              Add payment
            </Button>
          )}
        </div>
      )}
    </div>
  );
}
