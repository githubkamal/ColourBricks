import { apiClient } from "@/lib/api";

export interface AllocationLine {
  projectId: number;
  projectName: string;
  obligationId: number | null;
  obligationReference: string | null;
  outstandingBefore: number;
  allocated: number;
  outstandingAfter: number;
}

export interface AllocationProposal {
  partyId: number;
  amount: number;
  method: string;
  lines: AllocationLine[];
  totalAllocated: number;
  advance: number;
}

export interface MultiProjectPayment {
  settlementId: number;
  vendorId: number;
  amount: number;
  method: string;
  applied: AllocationLine[];
  advance: number;
}

export function proposeAllocation(vendorId: number, amount: number): Promise<AllocationProposal> {
  return apiClient.post<AllocationProposal>("/vendor-payments/propose", { vendorId, amount });
}

export interface AllocateInput {
  vendorId: number;
  date: string;
  amount: number;
  paymentModeId: number;
  accountId?: number | null;
  referenceNo?: string | null;
  allocations?: { projectId: number; obligationId: number | null; amount: number }[];
  overrideReason?: string | null;
}

export function allocatePayment(input: AllocateInput): Promise<MultiProjectPayment> {
  return apiClient.post<MultiProjectPayment>("/vendor-payments/allocate", input);
}

export interface AllocationHistoryLine {
  projectId: number | null;
  projectName: string;
  obligationId: number | null;
  obligationReference: string | null;
  amount: number;
  method: string;
}

export interface SettlementAllocationHistory {
  settlementId: number;
  date: string;
  vendorId: number;
  vendorName: string;
  totalPayment: number;
  status: string;
  lines: AllocationHistoryLine[];
}

export interface VendorPaymentAllocationReportRow {
  date: string;
  vendorId: number;
  vendorName: string;
  settlementId: number;
  totalPayment: number;
  projectId: number | null;
  projectName: string;
  allocated: number;
  method: string;
  status: string;
}

export interface AllocationReportFilters {
  vendorId?: number | null;
  projectId?: number | null;
  dateFrom?: string | null;
  dateTo?: string | null;
}

export function settlementAllocationHistory(
  settlementId: number,
): Promise<SettlementAllocationHistory> {
  return apiClient.get<SettlementAllocationHistory>(`/settlements/${settlementId}/allocations`);
}

export function vendorPaymentAllocationReport(
  filters: AllocationReportFilters = {},
): Promise<VendorPaymentAllocationReportRow[]> {
  const params = new URLSearchParams();
  if (filters.vendorId) params.set("vendorId", String(filters.vendorId));
  if (filters.projectId) params.set("projectId", String(filters.projectId));
  if (filters.dateFrom) params.set("dateFrom", filters.dateFrom);
  if (filters.dateTo) params.set("dateTo", filters.dateTo);
  const q = params.toString();
  return apiClient.get<VendorPaymentAllocationReportRow[]>(
    `/reports/vendor-payment-allocations${q ? `?${q}` : ""}`,
  );
}
