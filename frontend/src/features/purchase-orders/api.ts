import { apiClient } from "@/lib/api";

export type PurchaseOrderTaxType = "Percentage" | "Amount";

export interface PurchaseOrderLine {
  id: number;
  projectId: number;
  projectName: string;
  itemId: number | null;
  itemName: string;
  quantity: number;
  unit: string;
  rate: number | null;
  subtotal: number | null;
  taxType: PurchaseOrderTaxType;
  taxRate: number | null;
  taxAmount: number | null;
  lineTotal: number | null;
}

/**
 * An invoice charge that isn't one of the ordered items — transport, handling,
 * loading (client request, 2026-09-23). Order-wide, optional, and taxed the same
 * two ways a line is.
 */
export interface PurchaseOrderCharge {
  id: number;
  chargeType: string;
  amount: number;
  taxType: PurchaseOrderTaxType;
  taxRate: number | null;
  taxAmount: number;
  total: number;
}

export interface PurchaseOrder {
  id: number;
  poNumber: string;
  vendorId: number;
  vendorName: string;
  orderDate: string;
  status: "Draft" | "Submitted" | "Cancelled";
  invoiceNumber: string | null;
  submittedDate: string | null;
  notes: string | null;
  subtotalTotal: number;
  taxTotal: number;
  total: number;
  lines: PurchaseOrderLine[];
  charges: PurchaseOrderCharge[];
  /** Sum of every charge's amount, before its GST. */
  chargesSubtotal: number;
  /** Sum of every charge's GST amount. */
  chargesTax: number;
  /** The manual adjustment folded into `total`; may be negative. */
  roundOff: number;
  obligationIds: number[];
  concurrencyStamp: string;
}

export interface PurchaseOrderLineInput {
  projectId: number;
  itemId?: number | null;
  itemName: string;
  quantity: number;
  unit: string;
}

export interface CreatePurchaseOrderInput {
  vendorId: number;
  orderDate: string;
  lines: PurchaseOrderLineInput[];
  notes?: string | null;
}

export interface UpdatePurchaseOrderInput {
  orderDate: string;
  lines: PurchaseOrderLineInput[];
  concurrencyStamp: string;
  notes?: string | null;
}

export interface SubmitPurchaseOrderLineInput {
  lineId: number;
  quantity: number;
  rate: number;
  taxType: PurchaseOrderTaxType;
  taxRate?: number | null;
  taxAmount: number;
}

export interface PurchaseOrderChargeInput {
  chargeType: string;
  amount: number;
  taxType: PurchaseOrderTaxType;
  taxRate?: number | null;
  taxAmount: number;
}

export interface SubmitPurchaseOrderInput {
  invoiceNumber: string;
  lines: SubmitPurchaseOrderLineInput[];
  /** Extra charges — omitted entirely when the invoice has none. */
  charges?: PurchaseOrderChargeInput[];
  /** Manual adjustment to the invoice total; may be negative. */
  roundOff?: number;
}

export function createPurchaseOrder(input: CreatePurchaseOrderInput): Promise<PurchaseOrder> {
  return apiClient.post("/purchase-orders", input);
}

export function listPurchaseOrders(vendorId?: number, status?: string): Promise<PurchaseOrder[]> {
  const params = new URLSearchParams();
  if (vendorId) params.set("vendorId", String(vendorId));
  if (status) params.set("status", status);
  const q = params.toString();
  return apiClient.get<PurchaseOrder[]>(`/purchase-orders${q ? `?${q}` : ""}`);
}

export function getPurchaseOrder(id: number): Promise<PurchaseOrder> {
  return apiClient.get(`/purchase-orders/${id}`);
}

export function updatePurchaseOrder(
  id: number,
  input: UpdatePurchaseOrderInput,
): Promise<PurchaseOrder> {
  return apiClient.put(`/purchase-orders/${id}`, input);
}

export function submitPurchaseOrder(
  id: number,
  input: SubmitPurchaseOrderInput,
): Promise<PurchaseOrder> {
  return apiClient.post(`/purchase-orders/${id}/submit`, input);
}

export function cancelPurchaseOrder(id: number): Promise<void> {
  return apiClient.post(`/purchase-orders/${id}/cancel`, {});
}
