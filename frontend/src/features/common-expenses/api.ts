import { apiClient } from "@/lib/api";

export type CommonExpenseType = "Personal" | "Office" | "Savings";

export const SUB_CATEGORIES: Record<CommonExpenseType, string[]> = {
  Personal: ["Personal withdrawals", "Personal purchases", "Other personal expenses"],
  Office: [
    "Office rent",
    "Electricity",
    "Internet",
    "Salaries",
    "Stationery",
    "Travel",
    "Software",
    "Maintenance",
  ],
  Savings: ["Savings allocation", "Savings transactions"],
};

export interface CommonExpense {
  id: number;
  type: CommonExpenseType;
  subCategory: string;
  date: string;
  amount: number;
  paymentModeId: number;
  accountId: number | null;
  referenceNo: string | null;
  description: string | null;
  status: string;
  /** Present only when paid through an account — lets it be linked to a bank transaction. */
  settlementId: number | null;
}

export interface CommonExpenseSummary {
  personal: number;
  office: number;
  savings: number;
  total: number;
}

export interface RecordCommonExpenseInput {
  type: CommonExpenseType;
  subCategory: string;
  date: string;
  amount: number;
  paymentModeId: number;
  accountId?: number | null;
  referenceNo?: string | null;
  description?: string | null;
}

export function recordCommonExpense(input: RecordCommonExpenseInput): Promise<CommonExpense> {
  return apiClient.post<CommonExpense>("/common-expenses", input);
}

export interface CommonExpenseFilter {
  type?: CommonExpenseType;
  subCategory?: string;
  dateFrom?: string;
  dateTo?: string;
}

function qs(params: Record<string, string | undefined>): string {
  const entries = Object.entries(params).filter(([, v]) => v !== undefined && v !== "");
  return entries.length ? `?${new URLSearchParams(entries as [string, string][]).toString()}` : "";
}

export function listCommonExpenses(
  filter: CommonExpenseFilter | CommonExpenseType = {},
): Promise<CommonExpense[]> {
  const f: CommonExpenseFilter = typeof filter === "string" ? { type: filter } : filter;
  return apiClient.get<CommonExpense[]>(`/common-expenses${qs({ ...f })}`);
}

export function commonExpenseSummary(
  range: { dateFrom?: string; dateTo?: string } = {},
): Promise<CommonExpenseSummary> {
  return apiClient.get<CommonExpenseSummary>(`/common-expenses/summary${qs({ ...range })}`);
}

// ── allocation (P6-T02..T05) ──────────────────────────────────────────────────

export type AllocationMethod = "Equal" | "Percentage" | "Manual";

export interface AllocationLine {
  projectId: number;
  projectName: string;
  before: number;
  allocated: number;
  after: number;
  percent: number | null;
}

export interface AllocationPreview {
  periodFrom: string;
  periodTo: string;
  method: AllocationMethod;
  poolAmount: number;
  totalAllocated: number;
  balances: boolean;
  lines: AllocationLine[];
}

export interface AllocationRun {
  id: number;
  periodFrom: string;
  periodTo: string;
  types: string;
  method: AllocationMethod;
  poolAmount: number;
  status: string;
  note: string | null;
  createdAtUtc: string;
  lines: AllocationLine[];
}

export interface AllocationReportRow {
  runId: number;
  periodFrom: string;
  periodTo: string;
  types: string;
  method: AllocationMethod;
  poolAmount: number;
  projectId: number;
  projectName: string;
  allocated: number;
  status: string;
}

export interface AllocationRequest {
  periodFrom: string;
  periodTo: string;
  types: CommonExpenseType[];
  method: AllocationMethod;
  shares?: { projectId: number; value: number }[];
  note?: string | null;
}

export function previewAllocation(req: AllocationRequest): Promise<AllocationPreview> {
  return apiClient.post<AllocationPreview>("/common-expense-allocations/preview", req);
}
export function commitAllocation(req: AllocationRequest): Promise<AllocationRun> {
  return apiClient.post<AllocationRun>("/common-expense-allocations", req);
}
export function listAllocationRuns(): Promise<AllocationRun[]> {
  return apiClient.get<AllocationRun[]>("/common-expense-allocations");
}
export function reverseAllocationRun(runId: number): Promise<void> {
  return apiClient.post<void>(`/common-expense-allocations/${runId}/reverse`, {});
}
export interface AllocationReportFilter {
  dateFrom?: string;
  dateTo?: string;
  type?: CommonExpenseType;
}

export function commonExpenseAllocationReport(
  filter: AllocationReportFilter = {},
): Promise<AllocationReportRow[]> {
  return apiClient.get<AllocationReportRow[]>(
    `/reports/common-expense-allocations${qs({ ...filter })}`,
  );
}
