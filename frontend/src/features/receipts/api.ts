import { apiClient } from "@/lib/api";
import type { Receipt, RecordReceiptInput } from "./types";

export function recordReceipt(input: RecordReceiptInput): Promise<Receipt> {
  return apiClient.post<Receipt>("/receipts", input);
}

export function listReceipts(projectId: number, from?: string, to?: string): Promise<Receipt[]> {
  const params = new URLSearchParams();
  if (from) params.set("from", from);
  if (to) params.set("to", to);
  const q = params.toString();
  return apiClient.get<Receipt[]>(`/projects/${projectId}/receipts${q ? `?${q}` : ""}`);
}

export function projectIncomeTotal(
  projectId: number,
): Promise<{ projectId: number; total: number }> {
  return apiClient.get(`/projects/${projectId}/income-total`);
}

export function reverseReceipt(id: number, reason: string): Promise<void> {
  return apiClient.post<void>(`/receipts/${id}/reverse`, { reason });
}
