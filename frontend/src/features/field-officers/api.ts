import { apiClient } from "@/lib/api";

/** Personal, Office, Savings or Custom — a field officer's no-project bill (client request, 2026-09-04). */
export type FieldOfficerExpenseType = "Personal" | "Office" | "Savings" | "Custom";

export interface FieldOfficerExpense {
  id: number;
  fieldOfficerId: number;
  type: FieldOfficerExpenseType;
  date: string;
  amount: number;
  referenceNo: string | null;
  description: string | null;
  status: string;
}

export interface RecordFieldOfficerExpenseInput {
  fieldOfficerId: number;
  type: FieldOfficerExpenseType;
  date: string;
  amount: number;
  referenceNo?: string | null;
  description?: string | null;
}

/**
 * Posts a payable to the officer (not an instant company expense) — it nets against
 * whatever advance he already holds and shows up in his outstanding, the same way a
 * vendor purchase does.
 */
export function recordFieldOfficerExpense(
  input: RecordFieldOfficerExpenseInput,
): Promise<FieldOfficerExpense> {
  return apiClient.post<FieldOfficerExpense>("/field-officer-expenses", input);
}

export function listFieldOfficerExpenses(fieldOfficerId: number): Promise<FieldOfficerExpense[]> {
  return apiClient.get<FieldOfficerExpense[]>(
    `/field-officer-expenses?fieldOfficerId=${fieldOfficerId}`,
  );
}

export function reverseFieldOfficerExpense(id: number, reason: string): Promise<void> {
  return apiClient.post<void>(`/field-officer-expenses/${id}/reverse`, { reason });
}
