import { apiClient } from "@/lib/api";

export interface ExpenseCategory {
  id: number;
  name: string;
  slug: string;
  bucket: string;
  isCost: boolean;
  isActive: boolean;
  isSystem: boolean;
  concurrencyStamp: string;
}

export interface CreateExpenseCategoryInput {
  name: string;
  bucket: string;
  isCost?: boolean;
}

export interface UpdateExpenseCategoryInput {
  name: string;
  isActive: boolean;
  concurrencyStamp: string;
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
  /** Present only when paid immediately through an account — lets it be linked to a bank transaction. */
  settlementId: number | null;
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

export function listExpenseCategories(includeInactive = false): Promise<ExpenseCategory[]> {
  return apiClient.get<ExpenseCategory[]>(
    `/expense-categories${includeInactive ? "?includeInactive=true" : ""}`,
  );
}

export function createExpenseCategory(input: CreateExpenseCategoryInput): Promise<ExpenseCategory> {
  return apiClient.post<ExpenseCategory>("/expense-categories", input);
}

export function updateExpenseCategory(
  id: number,
  input: UpdateExpenseCategoryInput,
): Promise<ExpenseCategory> {
  return apiClient.put<ExpenseCategory>(`/expense-categories/${id}`, input);
}

export function listProjectExpenses(projectId: number): Promise<DirectExpense[]> {
  return apiClient.get<DirectExpense[]>(`/projects/${projectId}/expenses`);
}

export function recordDirectExpense(input: RecordDirectExpenseInput): Promise<DirectExpense> {
  return apiClient.post<DirectExpense>("/project-expenses", input);
}

export interface PayDirectExpenseInput {
  date: string;
  paymentModeId: number;
  accountId: number;
  referenceNo?: string | null;
}

export function payDirectExpense(id: number, input: PayDirectExpenseInput): Promise<DirectExpense> {
  return apiClient.post<DirectExpense>(`/project-expenses/${id}/pay`, input);
}

export function reverseDirectExpense(id: number, reason: string): Promise<void> {
  return apiClient.post<void>(`/project-expenses/${id}/reverse`, { reason });
}
