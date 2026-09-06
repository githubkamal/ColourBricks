import { apiClient } from "@/lib/api";

export interface PurchaseLine {
  id: number;
  itemId: number | null;
  itemName: string;
  quantity: number;
  unit: string;
  rate: number;
  taxAmount: number;
  lineTotal: number;
}

export interface VendorPurchase {
  id: number;
  projectId: number;
  vendorId: number;
  vendorName: string;
  date: string;
  invoiceNumber: string | null;
  total: number;
  partPaid: number;
  vendorOutstandingAfter: number;
  status: string;
  lines: PurchaseLine[];
}

export interface PurchaseLineInput {
  itemId?: number | null;
  itemName: string;
  quantity: number;
  unit: string;
  rate: number;
  taxAmount?: number;
}

export interface RecordVendorPurchaseInput {
  projectId: number;
  vendorId: number;
  date: string;
  total: number;
  lines: PurchaseLineInput[];
  invoiceNumber?: string | null;
  partPayment?: number | null;
  partPaymentModeId?: number | null;
  partPaymentAccountId?: number | null;
}

export function recordVendorPurchase(
  input: RecordVendorPurchaseInput,
): Promise<{ purchase: VendorPurchase; duplicateInvoiceWarning: boolean }> {
  return apiClient.post("/vendor-purchases", input);
}

export function listVendorPurchases(
  projectId?: number,
  vendorId?: number,
): Promise<VendorPurchase[]> {
  const params = new URLSearchParams();
  if (projectId) params.set("projectId", String(projectId));
  if (vendorId) params.set("vendorId", String(vendorId));
  const q = params.toString();
  return apiClient.get<VendorPurchase[]>(`/vendor-purchases${q ? `?${q}` : ""}`);
}

export function vendorOutstanding(
  vendorId: number,
): Promise<{ vendorId: number; outstanding: number }> {
  return apiClient.get(`/vendors/${vendorId}/outstanding`);
}

export function reverseVendorPurchase(id: number, reason: string): Promise<void> {
  return apiClient.post(`/vendor-purchases/${id}/reverse`, { reason });
}
