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
  /** Set when the bill was mapped to a project; null for a no-project bill. */
  projectId: number | null;
  projectName: string | null;
  categoryId: number | null;
}

export interface RecordFieldOfficerExpenseInput {
  fieldOfficerId: number;
  type: FieldOfficerExpenseType;
  date: string;
  amount: number;
  referenceNo?: string | null;
  description?: string | null;
  /** Maps the bill to a project — its amount then counts as that project's spend. */
  projectId?: number | null;
  categoryId?: number | null;
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

/** Project payments recorded for an officer — the bills that carry a project. */
export async function listFieldOfficerProjectPayments(
  fieldOfficerId: number,
): Promise<FieldOfficerExpense[]> {
  const rows = await listFieldOfficerExpenses(fieldOfficerId);
  return rows.filter((r) => r.projectId !== null);
}

export function reverseFieldOfficerExpense(id: number, reason: string): Promise<void> {
  return apiClient.post<void>(`/field-officer-expenses/${id}/reverse`, { reason });
}
