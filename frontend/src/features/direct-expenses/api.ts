import { apiClient } from "@/lib/api";

export interface ExpenseCategory {
  id: number;
  name: string;
  slug: string;
  bucket: string;
  isCost: boolean;
  isActive: boolean;
}

export interface DirectExpense {
  id: number;
  projectId: number;
  categoryId: number;
  categoryName: string;
  bucket: string;
  partyId: number | null;
  date: string;
  amount: number;
  paidImmediately: boolean;
  description: string | null;
  status: string;
}

export interface RecordDirectExpenseInput {
  projectId: number;
  categoryId: number;
  date: string;
  amount: number;
  paidImmediately?: boolean;
  paymentModeId?: number | null;
  accountId?: number | null;
  description?: string | null;
}

export function listExpenseCategories(): Promise<ExpenseCategory[]> {
  return apiClient.get<ExpenseCategory[]>("/expense-categories");
}

export function listProjectExpenses(projectId: number): Promise<DirectExpense[]> {
  return apiClient.get<DirectExpense[]>(`/projects/${projectId}/expenses`);
}

export function recordDirectExpense(input: RecordDirectExpenseInput): Promise<DirectExpense> {
  return apiClient.post<DirectExpense>("/project-expenses", input);
}
