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
}
export interface ProjectLedgerView {
  projectId: number;
  openingBalance: number;
  closingBalance: number;
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

export const projectDashboard = (id: number) =>
  apiClient.get<ProjectDashboard>(`/projects/${id}/dashboard`);
export const companyDashboard = () => apiClient.get<CompanyDashboard>(`/dashboard`);
export const budgetVsActual = (id: number) =>
  apiClient.get<BudgetVsActual>(`/projects/${id}/budget-vs-actual`);
export const projectFinancialLedger = (id: number) =>
  apiClient.get<ProjectLedgerView>(`/projects/${id}/financial-ledger`);
export const projectPnl = (id: number, revenueBasis: "Contract" | "Receipts") =>
  apiClient.get<ProjectPnl>(`/projects/${id}/pnl?revenueBasis=${revenueBasis}`);
