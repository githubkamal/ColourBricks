"use client";

import { useQuery } from "@tanstack/react-query";
import { listLoans } from "./api";
import { Input } from "@/components/ui/input";
import { PageHeader } from "@/components/ui/page-header";
import { Select } from "@/components/ui/select";
import { formatDate, formatINR } from "@/lib/format";
import { useQueryParam, useQueryParamNumber } from "@/lib/use-query-param";
import { cn } from "@/lib/utils";
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

interface ColumnRender {
  (row: Record<string, unknown>): string;
  isMoney?: boolean;
}

interface Column {
  header: string;
  render: ColumnRender;
}

const money = (key: string): ColumnRender => {
  const render: ColumnRender = (row) => formatINR((row[key] as number) ?? 0);
  render.isMoney = true;
  return render;
};
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

const REPORT_KEYS = REPORTS.map((r) => r.key);
function isReportKey(value: string): value is ReportKey {
  return (REPORT_KEYS as string[]).includes(value);
}

/** Loan Reports (BRD §54) — a dedicated controller, so this is its own small explorer. */
export function LoanReportsPage() {
  const [reportKeyRaw, setReportKeyRaw] = useQueryParam("report", "project-wise");
  const reportKey = isReportKey(reportKeyRaw) ? reportKeyRaw : "project-wise";
  const setReportKey = (next: ReportKey) => setReportKeyRaw(next);
  const [loanId, setLoanId] = useQueryParamNumber("loanId", 0);
  const [projectId, setProjectId] = useQueryParamNumber("projectId", 0);
  const [dateFrom, setDateFrom] = useQueryParam("dateFrom", "");
  const [dateTo, setDateTo] = useQueryParam("dateTo", "");

  const { data: loans = [] } = useQuery({ queryKey: ["loans"], queryFn: () => listLoans() });

  const needsLoan = reportKey === "schedule";
  const canRun = !needsLoan || loanId !== 0;

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
          return loanReportSchedule(loanId) as unknown as Promise<Record<string, unknown>[]>;
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
      <PageHeader title="Loan Reports" />

      <div className="bg-card flex flex-wrap items-end gap-3 rounded border p-3">
        <label className="space-y-1">
          <span className="text-sm font-medium">Report</span>
          <Select
            aria-label="Report"
            value={reportKey}
            onChange={(e) => setReportKey(e.target.value as ReportKey)}
          >
            {REPORTS.map((r) => (
              <option key={r.key} value={r.key}>
                {r.title}
              </option>
            ))}
          </Select>
        </label>

        {showLoanFilter && (
          <label className="space-y-1">
            <span className="text-sm font-medium">Loan{needsLoan ? "" : " (optional)"}</span>
            <Select
              aria-label="Loan"
              value={loanId || ""}
              onChange={(e) => setLoanId(e.target.value ? Number(e.target.value) : 0)}
            >
              <option value="">{needsLoan ? "Select a loan…" : "All loans"}</option>
              {loans.map((l) => (
                <option key={l.id} value={l.id}>
                  {l.lenderName} — {formatINR(l.principalAmount)}
                </option>
              ))}
            </Select>
          </label>
        )}

        {showProjectFilter && (
          <label className="space-y-1">
            <span className="text-sm font-medium">Project id (optional)</span>
            <Input
              className="w-28"
              inputMode="numeric"
              aria-label="Project id"
              value={projectId || ""}
              onChange={(e) => setProjectId(e.target.value ? Number(e.target.value) : 0)}
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

      <div className="bg-card border-border overflow-x-auto rounded-xl border shadow-xs">
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
            {needsLoan && loanId === 0 && (
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
                  <td key={c.header} className={cn("p-2", c.render.isMoney && "tabular-nums")}>
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
