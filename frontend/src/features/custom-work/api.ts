import { apiClient } from "@/lib/api";

export interface CustomWork {
  id: number;
  projectId: number;
  partyId: number | null;
  partyName: string | null;
  departmentId: number | null;
  date: string;
  workType: string | null;
  description: string | null;
  estimatedCost: number;
  actualCost: number;
  variance: number;
  status: string;
}

export interface RecordCustomWorkInput {
  projectId: number;
  date: string;
  estimatedCost: number;
  actualCost: number;
  partyId?: number | null;
  workType?: string | null;
  description?: string | null;
}

export function recordCustomWork(input: RecordCustomWorkInput): Promise<CustomWork> {
  return apiClient.post<CustomWork>("/custom-work", input);
}

export function listCustomWork(projectId: number): Promise<CustomWork[]> {
  return apiClient.get<CustomWork[]>(`/projects/${projectId}/custom-work`);
}

/** Reverses the custom-work record itself (client request, 2026-09-04). */
export function reverseCustomWork(id: number, reason: string): Promise<void> {
  return apiClient.post<void>(`/custom-work/${id}/reverse`, { reason });
}

export interface CustomWorkPayment {
  id: number;
  projectId: number;
  partyId: number;
  date: string;
  amount: number;
  paymentModeId: number;
  accountId: number | null;
  referenceNo: string | null;
  status: string;
  outstandingAfter: number;
}

export interface PayCustomWorkInput {
  projectId: number;
  partyId: number;
  date: string;
  amount: number;
  paymentModeId: number;
  accountId?: number | null;
  referenceNo?: string | null;
  customWorkId?: number | null;
}

/** Settles a custom-work obligation (client request, 2026-09-04). */
export function payCustomWork(input: PayCustomWorkInput): Promise<CustomWorkPayment> {
  return apiClient.post<CustomWorkPayment>("/custom-work-payments", input);
}

export function reverseCustomWorkPayment(id: number, reason: string): Promise<void> {
  return apiClient.post<void>(`/custom-work-payments/${id}/reverse`, { reason });
}
