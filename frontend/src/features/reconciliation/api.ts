import { apiClient } from "@/lib/api";

export interface ProjectAllocation {
  projectId: number;
  projectName: string;
  amount: number;
}

export interface ReconciliationRow {
  id: number;
  date: string;
  bank: string;
  description: string;
  type: "Credit" | "Debit";
  credit: number;
  debit: number;
  counterparty: string | null;
  projects: string;
  allocated: number;
  difference: number;
  status: string;
  projectDetail: ProjectAllocation[];
}

export interface ReconciliationQueue {
  items: ReconciliationRow[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface MatchSuggestion {
  settlementId: number;
  partyId: number | null;
  partyName: string;
  date: string;
  amount: number;
  referenceNo: string | null;
  description: string;
  score: number;
  reasons: string[];
}

export interface MatchSuggestions {
  bankTransactionId: number;
  autoSelectThreshold: number;
  autoSelectSettlementId: number | null;
  suggestions: MatchSuggestion[];
}

export interface ReconciliationProposal {
  bankTransactionId: number;
  type: "Credit" | "Debit";
  amount: number;
  hints: ProjectAllocation[];
  fifoProposal: ProjectAllocation[];
}

export interface TransferSuggestion {
  debitTransactionId: number;
  creditTransactionId: number;
  fromAccountId: number;
  toAccountId: number;
  amount: number;
  date: string;
}

export interface QueueFilters {
  accountId?: number | null;
  status?: string | null;
  dateFrom?: string | null;
  dateTo?: string | null;
  amountMin?: number | null;
  amountMax?: number | null;
  matched?: boolean | null;
  search?: string | null;
  page?: number;
}

export function reconciliationQueue(f: QueueFilters): Promise<ReconciliationQueue> {
  const q = new URLSearchParams();
  if (f.accountId) q.set("accountId", String(f.accountId));
  if (f.status) q.set("status", f.status);
  if (f.dateFrom) q.set("dateFrom", f.dateFrom);
  if (f.dateTo) q.set("dateTo", f.dateTo);
  if (f.amountMin != null) q.set("amountMin", String(f.amountMin));
  if (f.amountMax != null) q.set("amountMax", String(f.amountMax));
  if (f.matched != null) q.set("matched", String(f.matched));
  if (f.search) q.set("search", f.search);
  q.set("page", String(f.page ?? 1));
  q.set("pageSize", "100");
  return apiClient.get<ReconciliationQueue>(`/reconciliation?${q.toString()}`);
}

export function matchSuggestions(bankTxId: number): Promise<MatchSuggestions> {
  return apiClient.get<MatchSuggestions>(`/bank-transactions/${bankTxId}/match-suggestions`);
}

export function reconciliationProposal(
  bankTxId: number,
  vendorId?: number | null,
): Promise<ReconciliationProposal> {
  const q = vendorId ? `?vendorId=${vendorId}` : "";
  return apiClient.get<ReconciliationProposal>(
    `/bank-transactions/${bankTxId}/reconciliation-proposal${q}`,
  );
}

export function reconcileCredit(
  bankTxId: number,
  body: {
    clientId: number;
    projectId: number;
    incomeType?: string;
    existingReceiptId?: number | null;
  },
): Promise<unknown> {
  return apiClient.post(`/bank-transactions/${bankTxId}/reconcile-credit`, body);
}

export function reconcileDebit(
  bankTxId: number,
  body: {
    vendorId?: number | null;
    allocations?: { projectId: number; amount: number }[];
    existingPaymentId?: number | null;
  },
): Promise<unknown> {
  return apiClient.post(`/bank-transactions/${bankTxId}/reconcile-debit`, body);
}

/** One line of a manual debit split (client request, 2026-09-04). */
export type ReconciliationAllocationTarget =
  | "Vendor"
  | "Personal"
  | "Office"
  | "Savings"
  | "Custom"
  | "FieldOfficer"
  | "CustomWork"
  | "BankCharges";

export interface ReconciliationSplitLine {
  target: ReconciliationAllocationTarget;
  amount: number;
  description: string;
  projectId?: number | null;
  vendorId?: number | null;
}

export interface MapDebitResult {
  bankTransactionId: number;
  status: string;
  settlementIds: number[];
  commonExpenseIds: number[];
}

/** Splits a debit across a vendor (with/without a project) and/or Personal/Office/Savings. */
export function mapDebit(
  bankTxId: number,
  allocations: ReconciliationSplitLine[],
): Promise<MapDebitResult> {
  return apiClient.post<MapDebitResult>(`/bank-transactions/${bankTxId}/map-debit`, {
    allocations,
  });
}

/** Flags a Pending row "deal with later" — no ledger impact. */
export function holdTransaction(bankTxId: number): Promise<void> {
  return apiClient.post<void>(`/bank-transactions/${bankTxId}/hold`, {});
}

/** Reverts a held row back to Pending. */
export function unholdTransaction(bankTxId: number): Promise<void> {
  return apiClient.post<void>(`/bank-transactions/${bankTxId}/unhold`, {});
}

export function unreconcile(bankTxId: number, reason: string): Promise<void> {
  return apiClient.post<void>(`/bank-transactions/${bankTxId}/unreconcile`, { reason });
}

export function bulkExclude(ids: number[], reason: string): Promise<void> {
  return apiClient.post<void>("/bank-transactions/bulk-exclude", { ids, reason });
}

export function transferSuggestions(accountId?: number | null): Promise<TransferSuggestion[]> {
  const q = accountId ? `?accountId=${accountId}` : "";
  return apiClient.get<TransferSuggestion[]>(`/internal-transfers/suggestions${q}`);
}

export function pairInternalTransfer(
  fromTransactionId: number,
  toTransactionId: number,
): Promise<unknown> {
  return apiClient.post("/internal-transfers", { fromTransactionId, toTransactionId });
}
