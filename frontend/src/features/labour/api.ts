import { apiClient } from "@/lib/api";

export const PAYMENT_FREQUENCIES = ["Daily", "Weekly", "Monthly", "Milestone", "AdHoc"] as const;
export type PaymentFrequency = (typeof PAYMENT_FREQUENCIES)[number];

export interface WorkEntry {
  id: number;
  projectId: number;
  departmentId: number | null;
  teamId: number;
  teamName: string;
  date: string;
  workType: string | null;
  description: string | null;
  agreedValue: number;
  totalPaid: number;
  outstanding: number;
  status: string;
  /** System reference number, e.g. LW-2026-00001 — kept across edits. */
  referenceNumber: string | null;
  projectName: string | null;
  projectCode: string | null;
}

export interface TeamStatementRow {
  date: string;
  kind: "Work" | "Payment";
  reference: string;
  workValue: number;
  paid: number;
  runningOutstanding: number;
  /** The work entry (for a payment row: the entry it pays). */
  workEntryId: number | null;
  /** Set on payment rows only. */
  paymentId: number | null;
  referenceNumber: string | null;
  projectName: string | null;
}

export interface RecordWorkInput {
  projectId: number;
  teamId: number;
  date: string;
  agreedValue: number;
  departmentId?: number | null;
  workType?: string | null;
  description?: string | null;
}

export interface PayWorkInput {
  date: string;
  amount: number;
  frequency: PaymentFrequency;
  paymentModeId: number;
  accountId?: number | null;
  referenceNo?: string | null;
}

export function recordWork(input: RecordWorkInput): Promise<WorkEntry> {
  return apiClient.post<WorkEntry>("/labour/work", input);
}

export function listWork(projectId?: number, teamId?: number): Promise<WorkEntry[]> {
  const params = new URLSearchParams();
  if (projectId) params.set("projectId", String(projectId));
  if (teamId) params.set("teamId", String(teamId));
  const q = params.toString();
  return apiClient.get<WorkEntry[]>(`/labour/work${q ? `?${q}` : ""}`);
}

export interface WorkPayment {
  id: number;
  workEntryId: number;
  date: string;
  amount: number;
  frequency: PaymentFrequency;
  paymentModeId: number;
  accountId: number | null;
  referenceNo: string | null;
}

export function payWork(workId: number, input: PayWorkInput): Promise<WorkPayment> {
  return apiClient.post<WorkPayment>(`/labour/work/${workId}/payments`, input);
}

export function reverseWork(id: number, reason: string): Promise<void> {
  return apiClient.post<void>(`/labour/work/${id}/reverse`, { reason });
}

export function teamStatement(teamId: number): Promise<TeamStatementRow[]> {
  return apiClient.get<TeamStatementRow[]>(`/labour/teams/${teamId}/statement`);
}

export type UpdateWorkInput = RecordWorkInput;

export function updateWork(id: number, input: UpdateWorkInput): Promise<WorkEntry> {
  return apiClient.put<WorkEntry>(`/labour/work/${id}`, input);
}

export function getWork(id: number): Promise<WorkEntry> {
  return apiClient.get<WorkEntry>(`/labour/work/${id}`);
}

export function deleteWork(id: number, reason?: string): Promise<void> {
  const q = reason ? `?reason=${encodeURIComponent(reason)}` : "";
  return apiClient.del<void>(`/labour/work/${id}${q}`);
}

export function deleteWorkPayment(paymentId: number, reason?: string): Promise<void> {
  const q = reason ? `?reason=${encodeURIComponent(reason)}` : "";
  return apiClient.del<void>(`/labour/payments/${paymentId}${q}`);
}
