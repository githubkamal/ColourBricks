"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { listLoans } from "./api";
import { Input } from "@/components/ui/input";
import { formatDate, formatINR } from "@/lib/format";
import {
  loanReportDateWise,
  loanReportEmiPaid,
  loanReportEmiPending,
  loanReportOutstanding,
  loanReportPrincipalVsInterest,
  loanReportProjectWise,
  loanReportSchedule,
} from "./api";

const REPORTS = [
  { key: "project-wise", title: "Project-wise Loans" },
  { key: "outstanding", title: "Outstanding" },
  { key: "schedule", title: "EMI Schedule" },
  { key: "emi-paid", title: "EMI Paid" },
  { key: "emi-pending", title: "EMI Pending" },
  { key: "principal-vs-interest", title: "Principal vs Interest" },
  { key: "date-wise", title: "Date-wise EMI Payments" },
] as const;
type ReportKey = (typeof REPORTS)[number]["key"];

interface Column {
  header: string;
  render: (row: Record<string, unknown>) => string;
}

const money =
  (key: string): Column["render"] =>
  (row) =>
    formatINR((row[key] as number) ?? 0);
const date =
  (key: string): Column["render"] =>
  (row) =>
    formatDate(row[key] as string);
const plain =
  (key: string): Column["render"] =>
  (row) =>
    String(row[key] ?? "");

const COLUMNS: Record<ReportKey, Column[]> = {
  "project-wise": [
    { header: "Project", render: (r) => (r.projectId ? String(r.projectName) : "Company-wide") },
    { header: "Loans", render: plain("loanCount") },
    { header: "Loan amount", render: money("loanAmount") },
    { header: "Principal paid", render: money("principalPaid") },
    { header: "Outstanding", render: money("outstandingPrincipal") },
    { header: "Interest paid", render: money("interestPaid") },
  ],
  outstanding: [
    { header: "Loan", render: plain("loanId") },
    { header: "Lender", render: plain("lenderName") },
    { header: "Project", render: (r) => (r.projectId ? String(r.projectName) : "Company-wide") },
    { header: "Loan amount", render: money("loanAmount") },
    { header: "Principal paid", render: money("principalPaid") },
    { header: "Interest paid", render: money("interestPaid") },
    { header: "Outstanding", render: money("outstandingPrincipal") },
    {
      header: "Next due",
      render: (r) => (r.nextDueDate ? formatDate(r.nextDueDate as string) : "—"),
    },
    { header: "Status", render: plain("status") },
  ],
  schedule: [
    { header: "#", render: plain("instalmentNo") },
    { header: "Due date", render: date("dueDate") },
    { header: "EMI", render: money("emiAmount") },
    { header: "Principal", render: money("principalComponent") },
    { header: "Interest", render: money("interestComponent") },
    { header: "Closing principal", render: money("closingPrincipal") },
    { header: "Status", render: plain("status") },
  ],
  "emi-paid": [
    { header: "Date", render: date("date") },
    { header: "Lender", render: plain("lenderName") },
    { header: "Amount", render: money("amount") },
    { header: "Principal", render: money("principalPaid") },
    { header: "Interest", render: money("interestPaid") },
    { header: "Type", render: (r) => (r.isPrepayment ? "Prepayment" : "EMI") },
    { header: "Status", render: plain("status") },
  ],
  "emi-pending": [
    { header: "Lender", render: plain("lenderName") },
    { header: "#", render: plain("instalmentNo") },
    { header: "Due date", render: date("dueDate") },
    { header: "EMI", render: money("emiAmount") },
    { header: "Amount due", render: money("amountDue") },
    { header: "Days overdue", render: plain("daysOverdue") },
  ],
  "principal-vs-interest": [
    { header: "Lender", render: plain("lenderName") },
    { header: "Loan amount", render: money("loanAmount") },
    { header: "Principal paid", render: money("principalPaid") },
    { header: "Interest paid", render: money("interestPaid") },
    { header: "Outstanding", render: money("outstandingPrincipal") },
  ],
  "date-wise": [
    { header: "Date", render: date("date") },
    { header: "Payments", render: plain("paymentCount") },
    { header: "Amount", render: money("amount") },
    { header: "Principal", render: money("principalPaid") },
    { header: "Interest", render: money("interestPaid") },
  ],
};

/** Loan Reports (BRD §54) — a dedicated controller, so this is its own small explorer. */
export function LoanReportsPage() {
  const [reportKey, setReportKey] = useState<ReportKey>("project-wise");
  const [loanId, setLoanId] = useState<number | "">("");
  const [projectId, setProjectId] = useState<number | "">("");
  const [dateFrom, setDateFrom] = useState("");
  const [dateTo, setDateTo] = useState("");

  const { data: loans = [] } = useQuery({ queryKey: ["loans"], queryFn: () => listLoans() });

  const needsLoan = reportKey === "schedule";
  const canRun = !needsLoan || loanId !== "";

  const { data: rows = [], isPending } = useQuery({
    queryKey: ["loan-report", reportKey, loanId, projectId, dateFrom, dateTo],
    enabled: canRun,
    queryFn: async (): Promise<Record<string, unknown>[]> => {
      switch (reportKey) {
        case "project-wise":
          return loanReportProjectWise() as unknown as Promise<Record<string, unknown>[]>;
        case "outstanding":
          return loanReportOutstanding(projectId || undefined) as unknown as Promise<
            Record<string, unknown>[]
          >;
        case "schedule":
          return loanReportSchedule(loanId as number) as unknown as Promise<
            Record<string, unknown>[]
          >;
        case "emi-paid":
          return loanReportEmiPaid(
            loanId || undefined,
            dateFrom || undefined,
            dateTo || undefined,
          ) as unknown as Promise<Record<string, unknown>[]>;
        case "emi-pending":
          return loanReportEmiPending(loanId || undefined) as unknown as Promise<
            Record<string, unknown>[]
          >;
        case "principal-vs-interest":
          return loanReportPrincipalVsInterest(loanId || undefined) as unknown as Promise<
            Record<string, unknown>[]
          >;
        case "date-wise":
          return loanReportDateWise(
            dateFrom || undefined,
            dateTo || undefined,
          ) as unknown as Promise<Record<string, unknown>[]>;
      }
    },
  });

  const columns = COLUMNS[reportKey];
  const showLoanFilter = ["schedule", "emi-paid", "emi-pending", "principal-vs-interest"].includes(
    reportKey,
  );
  const showProjectFilter = reportKey === "outstanding";
  const showDateFilter = reportKey === "emi-paid" || reportKey === "date-wise";

  return (
    <div className="space-y-4">
      <h1 className="text-lg font-semibold">Loan Reports</h1>

      <div className="flex flex-wrap items-end gap-3 rounded border p-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">Report</span>
          <select
            className="rounded border bg-transparent px-3 py-1.5 text-sm"
            aria-label="Report"
            value={reportKey}
            onChange={(e) => setReportKey(e.target.value as ReportKey)}
          >
            {REPORTS.map((r) => (
              <option key={r.key} value={r.key}>
                {r.title}
              </option>
            ))}
          </select>
        </label>

        {showLoanFilter && (
          <label className="space-y-1">
            <span className="text-sm font-medium">Loan{needsLoan ? "" : " (optional)"}</span>
            <select
              className="rounded border bg-transparent px-3 py-1.5 text-sm"
              aria-label="Loan"
              value={loanId}
              onChange={(e) => setLoanId(e.target.value ? Number(e.target.value) : "")}
            >
              <option value="">{needsLoan ? "Select a loan…" : "All loans"}</option>
              {loans.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.lenderName} — {formatINR(l.principalAmount)}
                </option>
              ))}
            </select>
          </label>
        )}

        {showProjectFilter && (
          <label className="space-y-1">
            <span className="text-sm font-medium">Project id (optional)</span>
            <Input
              className="w-28"
              inputMode="numeric"
              aria-label="Project id"
              value={projectId}
              onChange={(e) => setProjectId(e.target.value ? Number(e.target.value) : "")}
            />
          </label>
        )}

        {showDateFilter && (
          <>
            <label className="space-y-1">
              <span className="text-sm font-medium">From</span>
              <Input
                type="date"
                aria-label="From"
                value={dateFrom}
                onChange={(e) => setDateFrom(e.target.value)}
              />
            </label>
            <label className="space-y-1">
              <span className="text-sm font-medium">To</span>
              <Input
                type="date"
                aria-label="To"
                value={dateTo}
                onChange={(e) => setDateTo(e.target.value)}
              />
            </label>
          </>
        )}
      </div>

      <div className="overflow-x-auto rounded border">
        <table className="w-full text-sm">
          <thead className="bg-secondary/60 text-muted-foreground">
            <tr className="border-b text-left">
              {columns.map((c) => (
                <th key={c.header} className="p-2 font-medium">
                  {c.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {needsLoan && loanId === "" && (
              <tr>
                <td colSpan={columns.length} className="text-muted-foreground p-3 text-center">
                  Select a loan to see its schedule.
                </td>
              </tr>
            )}
            {canRun && isPending && (
              <tr>
                <td colSpan={columns.length} className="text-muted-foreground p-3 text-center">
                  Loading…
                </td>
              </tr>
            )}
            {canRun && !isPending && rows.length === 0 && (
              <tr>
                <td colSpan={columns.length} className="text-muted-foreground p-3 text-center">
                  No rows.
                </td>
              </tr>
            )}
            {rows.map((row, i) => (
              <tr key={i} className="border-b last:border-0">
                {columns.map((c) => (
                  <td key={c.header} className="p-2">
                    {c.render(row)}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
