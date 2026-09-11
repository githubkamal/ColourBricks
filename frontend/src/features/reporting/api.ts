import { apiClient } from "@/lib/api";

export interface DashboardTile {
  key: string;
  label: string;
  value: number;
  drillUrl: string | null;
}
export interface ExpenseBreakdownRow {
  bucket: string;
  amount: number;
}
export interface ProjectDashboard {
  projectId: number;
  projectName: string;
  summary: DashboardTile[];
  expenseBreakdown: ExpenseBreakdownRow[];
  totalExpenses: number;
  monthlyFlow: MonthlyFlow[];
}
export interface CompanyProjectProfitRow {
  projectId: number;
  projectName: string;
  revenue: number;
  actualCost: number;
  profit: number;
}
export interface MonthlyFlow {
  month: string;
  income: number;
  expense: number;
}
export interface CompanyDashboard {
  tiles: DashboardTile[];
  projectProfitability: CompanyProjectProfitRow[];
  monthlyFlow: MonthlyFlow[];
}

export interface BudgetVsActualRow {
  categoryId: number;
  categoryName: string;
  bucket: string;
  budget: number;
  actual: number;
  variance: number;
  variancePercent: number | null;
  status: "Within" | "Approaching" | "Exceeded";
}
export interface BudgetVsActual {
  projectId: number;
  budgetRevisionNumber: number | null;
  estimatedCost: number;
  actualCost: number;
  approachingThresholdPercent: number;
  overrunMessage: string;
  rows: BudgetVsActualRow[];
}

export interface ProjectLedgerLine {
  entryId: number;
  date: string;
  description: string;
  credit: number;
  debit: number;
  runningBalance: number;
  sourceType: string;
  sourceId: number;
  isReversal: boolean;
  categoryId: number;
  categoryName: string;
  partyId: number | null;
  partyName: string | null;
  /** Accounting bucket for the line's category — "Cost" | "Income" | "Liability" | "Asset". */
  bucket: string;
}
export interface ProjectLedgerView {
  projectId: number;
  openingBalance: number;
  closingBalance: number;
  totalCredit: number;
  totalDebit: number;
  lines: ProjectLedgerLine[];
}

export interface ProjectPnl {
  projectId: number;
  projectName: string;
  revenueBasis: "Contract" | "Receipts";
  revenue: number;
  estimatedCost: number;
  actualCost: number;
  grossProfit: number;
  profitPercent: number;
  budgetVariance: number;
}

/**
 * Scopes the dashboard's flow figures — "Entire" = all time, default "Monthly"
 * = this month, "Custom" = the caller-supplied `dateFrom`/`dateTo`.
 */
export type DashboardPeriod = "Weekly" | "Monthly" | "Yearly" | "Entire" | "Custom";

export const projectDashboard = (id: number) =>
  apiClient.get<ProjectDashboard>(`/projects/${id}/dashboard`);
export function companyDashboard(
  period: DashboardPeriod = "Monthly",
  dateFrom?: string,
  dateTo?: string,
): Promise<CompanyDashboard> {
  const params = new URLSearchParams({ period });
  if (period === "Custom") {
    if (dateFrom) params.set("dateFrom", dateFrom);
    if (dateTo) params.set("dateTo", dateTo);
  }
  return apiClient.get<CompanyDashboard>(`/dashboard?${params.toString()}`);
}
export const budgetVsActual = (id: number) =>
  apiClient.get<BudgetVsActual>(`/projects/${id}/budget-vs-actual`);
export interface ProjectLedgerFilter {
  dateFrom?: string;
  dateTo?: string;
  categoryId?: number;
}

export function projectFinancialLedger(
  id: number,
  filter: ProjectLedgerFilter = {},
): Promise<ProjectLedgerView> {
  const params = new URLSearchParams();
  if (filter.dateFrom) params.set("dateFrom", filter.dateFrom);
  if (filter.dateTo) params.set("dateTo", filter.dateTo);
  if (filter.categoryId) params.set("categoryId", String(filter.categoryId));
  const q = params.toString();
  return apiClient.get<ProjectLedgerView>(`/projects/${id}/financial-ledger${q ? `?${q}` : ""}`);
}
export const projectPnl = (id: number, revenueBasis: "Contract" | "Receipts") =>
  apiClient.get<ProjectPnl>(`/projects/${id}/pnl?revenueBasis=${revenueBasis}`);
