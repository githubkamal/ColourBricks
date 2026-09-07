"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";
import { listAccounts } from "@/features/accounts/api";
import { PartyPicker } from "@/features/parties/party-picker";
import type { PartySearchItem } from "@/features/parties/types";
import { ProjectPicker } from "@/features/projects/project-picker";
import type { ProjectListItem } from "@/features/projects/types";
import { AmountInput } from "@/components/ui/amount-input";
import { Button } from "@/components/ui/button";
import { useConfirmDialog } from "@/components/ui/confirm-dialog";
import { dataState } from "@/components/ui/data-state";
import { FieldLabel } from "@/components/ui/field-label";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { PaginationBar } from "@/components/ui/pagination-bar";
import { StatusBadge } from "@/components/ui/status-badge";
import { Select } from "@/components/ui/select";
import { ApiError } from "@/lib/api";
import { formatINR } from "@/lib/format";
import { usePagination } from "@/lib/use-pagination";
import { listLoans, recordLoan, reverseLoan, type Loan } from "./api";

/** Loan Master (BRD §48): record a loan and see every loan on file. */
export function LoanMasterPage() {
  const queryClient = useQueryClient();
  const { confirm, dialog } = useConfirmDialog();
  const { data: accounts } = useQuery({ queryKey: ["accounts"], queryFn: () => listAccounts() });
  const { data: loans = [], isPending } = useQuery({
    queryKey: ["loans"],
    queryFn: () => listLoans(),
  });
  const { pageRows: pagedLoans, page, setPage, pageCount, total } = usePagination(loans, 20);

  const [lender, setLender] = useState<PartySearchItem | null>(null);
  const [project, setProject] = useState<ProjectListItem | null>(null);
  const [principal, setPrincipal] = useState("");
  const [rate, setRate] = useState("");
  const [startDate, setStartDate] = useState("");
  const [tenureMonths, setTenureMonths] = useState("");
  const [emiAmount, setEmiAmount] = useState("");
  const [emiStartDate, setEmiStartDate] = useState("");
  const [disbursementAccountId, setDisbursementAccountId] = useState<number | "">("");
  const [disbursementDate, setDisbursementDate] = useState("");
  const [reference, setReference] = useState("");
  const [notes, setNotes] = useState("");
  const [touched, setTouched] = useState(false);

  const principalRef = useRef<HTMLInputElement>(null);
  const rateRef = useRef<HTMLInputElement>(null);
  const startDateRef = useRef<HTMLInputElement>(null);
  const tenureRef = useRef<HTMLInputElement>(null);
  const emiStartDateRef = useRef<HTMLInputElement>(null);
  const disbursementAccountRef = useRef<HTMLSelectElement>(null);
  const disbursementDateRef = useRef<HTMLInputElement>(null);

  const isDirty =
    lender !== null ||
    principal !== "" ||
    rate !== "" ||
    startDate !== "" ||
    tenureMonths !== "" ||
    emiAmount !== "" ||
    emiStartDate !== "" ||
    disbursementAccountId !== "" ||
    disbursementDate !== "" ||
    reference !== "" ||
    notes !== "";

  useEffect(() => {
    if (!isDirty) return;
    function handler(e: BeforeUnloadEvent) {
      e.preventDefault();
    }
    window.addEventListener("beforeunload", handler);
    return () => window.removeEventListener("beforeunload", handler);
  }, [isDirty]);

  const record = useMutation({
    mutationFn: () =>
      recordLoan({
        lenderId: lender!.id,
        principalAmount: Number(principal),
        annualInterestRatePercent: Number(rate),
        startDate,
        tenureMonths: Number(tenureMonths),
        emiStartDate,
        disbursementAccountId: Number(disbursementAccountId),
        disbursementDate,
        projectId: project?.id ?? null,
        emiAmount: emiAmount ? Number(emiAmount) : null,
        reference: reference.trim() || null,
        notes: notes.trim() || null,
      }),
    onSuccess: (loan) => {
      toast.success(`Loan recorded: ${formatINR(loan.principalAmount)} from ${loan.lenderName}`);
      setLender(null);
      setProject(null);
      setPrincipal("");
      setRate("");
      setStartDate("");
      setTenureMonths("");
      setEmiAmount("");
      setEmiStartDate("");
      setDisbursementAccountId("");
      setDisbursementDate("");
      setReference("");
      setNotes("");
      setTouched(false);
      void queryClient.invalidateQueries({ queryKey: ["loans"] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not record the loan"),
  });

  const reverse = useMutation({
    mutationFn: ({ id, reason }: { id: number; reason: string }) => reverseLoan(id, reason),
    onSuccess: () => {
      toast.success("Loan reversed");
      void queryClient.invalidateQueries({ queryKey: ["loans"] });
    },
    onError: (error) =>
      toast.error(error instanceof ApiError ? error.message : "Could not reverse the loan"),
  });

  const ready =
    lender !== null &&
    Number(principal) > 0 &&
    Number(rate) > 0 &&
    startDate !== "" &&
    Number(tenureMonths) > 0 &&
    emiStartDate !== "" &&
    disbursementAccountId !== "" &&
    disbursementDate !== "";

  function focusFirstInvalid() {
    if (!(Number(principal) > 0)) {
      principalRef.current?.focus();
    } else if (!(Number(rate) > 0)) {
      rateRef.current?.focus();
    } else if (startDate === "") {
      startDateRef.current?.focus();
    } else if (!(Number(tenureMonths) > 0)) {
      tenureRef.current?.focus();
    } else if (emiStartDate === "") {
      emiStartDateRef.current?.focus();
    } else if (disbursementAccountId === "") {
      disbursementAccountRef.current?.focus();
    } else if (disbursementDate === "") {
      disbursementDateRef.current?.focus();
    }
  }

  async function handleReverse(loan: Loan) {
    const { confirmed, value: reason } = await confirm({
      title: "Reverse this loan?",
      description: "This cannot be undone.",
      inputLabel: "Reason",
      destructive: true,
      confirmLabel: "Reverse",
    });
    if (!confirmed || !reason) return;
    reverse.mutate({ id: loan.id, reason });
  }

  return (
    <div className="max-w-4xl space-y-6">
      {dialog}
      <PageHeader title="Loan Master" />

      <form
        className="bg-card space-y-3 rounded border p-4"
        onSubmit={(e) => {
          e.preventDefault();
          setTouched(true);
          if (ready) {
            record.mutate();
          } else {
            focusFirstInvalid();
          }
        }}
      >
        <PartyPicker type="Lender" label="Lender" selected={lender} onSelect={setLender} />

        <ProjectPicker
          selected={project}
          onSelect={setProject}
          label="Project (optional — leave blank for a company-wide loan)"
        />

        <div className="grid grid-cols-2 gap-3">
          <label className="space-y-1">
            <FieldLabel
              required
              error={touched && !(Number(principal) > 0) ? "Required" : undefined}
            >
              Principal amount
            </FieldLabel>
            <AmountInput
              ref={principalRef}
              value={principal}
              aria-label="Principal amount"
              onChange={setPrincipal}
            />
          </label>
          <label className="space-y-1">
            <FieldLabel required error={touched && !(Number(rate) > 0) ? "Required" : undefined}>
              Annual interest rate %
            </FieldLabel>
            <AmountInput
              ref={rateRef}
              value={rate}
              aria-label="Annual interest rate %"
              onChange={setRate}
            />
          </label>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <label className="space-y-1">
            <FieldLabel required error={touched && startDate === "" ? "Required" : undefined}>
              Start date
            </FieldLabel>
            <Input
              ref={startDateRef}
              type="date"
              value={startDate}
              aria-label="Start date"
              onChange={(e) => setStartDate(e.target.value)}
            />
          </label>
          <label className="space-y-1">
            <FieldLabel
              required
              error={touched && !(Number(tenureMonths) > 0) ? "Required" : undefined}
            >
              Tenure (months)
            </FieldLabel>
            <Input
              ref={tenureRef}
              inputMode="numeric"
              value={tenureMonths}
              aria-label="Tenure (months)"
              onChange={(e) => setTenureMonths(e.target.value.replace(/\D/g, ""))}
            />
          </label>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <label className="space-y-1">
            <span className="text-sm font-medium">
              EMI amount (optional — auto-calculated if blank)
            </span>
            <AmountInput value={emiAmount} aria-label="EMI amount" onChange={setEmiAmount} />
          </label>
          <label className="space-y-1">
            <FieldLabel required error={touched && emiStartDate === "" ? "Required" : undefined}>
              EMI start date
            </FieldLabel>
            <Input
              ref={emiStartDateRef}
              type="date"
              value={emiStartDate}
              aria-label="EMI start date"
              onChange={(e) => setEmiStartDate(e.target.value)}
            />
          </label>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <label className="space-y-1">
            <FieldLabel
              required
              error={touched && disbursementAccountId === "" ? "Required" : undefined}
            >
              Disbursement account
            </FieldLabel>
            <Select
              ref={disbursementAccountRef}
              className="w-full"
              value={disbursementAccountId}
              aria-label="Disbursement account"
              onChange={(e) =>
                setDisbursementAccountId(e.target.value ? Number(e.target.value) : "")
              }
            >
              <option value="">Select…</option>
              {accounts?.map((a) => (
                <option key={a.id} value={a.id}>
                  {a.name} ({a.type})
                </option>
              ))}
            </Select>
          </label>
          <label className="space-y-1">
            <FieldLabel
              required
              error={touched && disbursementDate === "" ? "Required" : undefined}
            >
              Disbursement date
            </FieldLabel>
            <Input
              ref={disbursementDateRef}
              type="date"
              value={disbursementDate}
              aria-label="Disbursement date"
              onChange={(e) => setDisbursementDate(e.target.value)}
            />
          </label>
        </div>

        <div className="grid grid-cols-2 gap-3">
          <label className="space-y-1">
            <span className="text-sm font-medium">Reference</span>
            <Input
              value={reference}
              aria-label="Reference"
              onChange={(e) => setReference(e.target.value)}
            />
          </label>
          <label className="space-y-1">
            <span className="text-sm font-medium">Notes</span>
            <Input value={notes} aria-label="Notes" onChange={(e) => setNotes(e.target.value)} />
          </label>
        </div>

        <Button type="submit" disabled={record.isPending}>
          Record loan
        </Button>
      </form>

      {dataState({
        isPending,
        isEmpty: !isPending && loans.length === 0,
        emptyLabel: "No loans recorded yet.",
      }) ?? (
        <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
          <table className="w-full text-sm">
            <thead className="bg-secondary/60 text-muted-foreground">
              <tr className="border-b text-left">
                <th className="p-2 font-medium">Lender</th>
                <th className="p-2 font-medium">Project</th>
                <th className="p-2 font-medium">Principal</th>
                <th className="p-2 font-medium">Rate</th>
                <th className="p-2 font-medium">Outstanding</th>
                <th className="p-2 font-medium">Status</th>
                <th />
              </tr>
            </thead>
            <tbody>
              {pagedLoans.map((loan) => (
                <LoanRow key={loan.id} loan={loan} onReverse={() => void handleReverse(loan)} />
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
        itemLabel="loans"
      />
    </div>
  );
}

function LoanRow({ loan, onReverse }: { loan: Loan; onReverse: () => void }) {
  return (
    <tr className="border-b last:border-0">
      <td className="p-2">
        {loan.lenderName}
        {loan.reference && <span className="text-muted-foreground"> · {loan.reference}</span>}
      </td>
      <td className="p-2">{loan.projectId ? `#${loan.projectId}` : "Company-wide"}</td>
      <td className="p-2 tabular-nums">{formatINR(loan.principalAmount)}</td>
      <td className="p-2 tabular-nums">{loan.annualInterestRatePercent}%</td>
      <td className="p-2 tabular-nums">{formatINR(loan.outstandingPrincipal)}</td>
      <td className="p-2">
        <StatusBadge status={loan.status} />
      </td>
      <td className="p-2">
        {loan.status !== "Reversed" && (
          <Button type="button" variant="ghost" size="xs" onClick={onReverse}>
            Reverse
          </Button>
        )}
      </td>
    </tr>
  );
}
