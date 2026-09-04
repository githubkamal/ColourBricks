import { apiClient } from "@/lib/api";

export interface ProjectOutstandingLine {
  projectId: number;
  projectName: string;
  outstanding: number;
}

export interface VendorOutstandingSummary {
  vendorId: number;
  total: number;
  byProject: ProjectOutstandingLine[];
  advance: number;
}

export interface VendorPayment {
  id: number;
  vendorId: number;
  projectId: number;
  date: string;
  amount: number;
  paymentModeId: number;
  accountId: number | null;
  referenceNo: string | null;
  status: string;
  vendorProjectOutstandingAfter: number;
  advanceCreated: number;
}

export interface VendorStatementRow {
  date: string;
  kind: "Purchase" | "Payment" | "Advance";
  reference: string;
  purchaseAmount: number;
  paid: number;
  runningOutstanding: number;
}

export interface ApplyVendorAdvanceInput {
  vendorId: number;
  obligationId: number;
  amount: number;
  date: string;
}

export interface ApplyVendorAdvanceResult {
  obligationId: number;
  applied: number;
  advanceRemaining: number;
  obligationOutstandingAfter: number;
}

export function applyVendorAdvance(
  input: ApplyVendorAdvanceInput,
): Promise<ApplyVendorAdvanceResult> {
  return apiClient.post<ApplyVendorAdvanceResult>("/vendor-payments/apply-advance", input);
}

export interface RecordVendorPaymentInput {
  vendorId: number;
  projectId: number;
  date: string;
  amount: number;
  paymentModeId: number;
  accountId?: number | null;
  referenceNo?: string | null;
}

export function vendorOutstandingSummary(vendorId: number): Promise<VendorOutstandingSummary> {
  return apiClient.get<VendorOutstandingSummary>(`/vendors/${vendorId}/outstanding-summary`);
}

export function recordVendorPayment(input: RecordVendorPaymentInput): Promise<VendorPayment> {
  return apiClient.post<VendorPayment>("/vendor-payments", input);
}

export function listVendorPayments(vendorId: number): Promise<VendorPayment[]> {
  return apiClient.get<VendorPayment[]>(`/vendors/${vendorId}/payments`);
}

export function vendorStatement(vendorId: number): Promise<VendorStatementRow[]> {
  return apiClient.get<VendorStatementRow[]>(`/vendors/${vendorId}/statement`);
}

export function reverseVendorPayment(id: number, reason: string): Promise<void> {
  return apiClient.post<void>(`/vendor-payments/${id}/reverse`, { reason });
}
