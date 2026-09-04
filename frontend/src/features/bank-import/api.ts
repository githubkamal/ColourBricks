import { apiBaseUrl } from "@/lib/config";
import { ApiError, apiClient } from "@/lib/api";

export interface StagedProjectAllocation {
  projectId: number;
  projectName: string;
  amount: number;
}

export interface StagedBankRow {
  id: number;
  sourceLineNo: number;
  valueDate: string | null;
  narration: string;
  debit: number;
  credit: number;
  balance: number | null;
  bankReference: string | null;
  parseState: "Parsed" | "Error";
  parseError: string | null;
  duplicateOfBankTransactionId: number | null;
  isRemoved: boolean;
  allocations: StagedProjectAllocation[];
  allocatedTotal: number;
  readyToCommit: boolean;
  blockedReason: string | null;
}

export interface BankImportCounts {
  total: number;
  mapped: number;
  unmapped: number;
  removed: number;
  duplicate: number;
  parseError: number;
  committed: number;
}

export interface BankImportBatch {
  id: number;
  accountId: number;
  fileName: string;
  status: "Draft" | "Committed" | "Discarded";
  uploadedAtUtc: string;
  uploadedByUserId: number | null;
  counts: BankImportCounts;
  rows: StagedBankRow[];
}

export interface BankImportCommitResult {
  batchId: number;
  committed: number;
  removed: number;
  duplicate: number;
  parseError: number;
}

export function getBankImport(batchId: number): Promise<BankImportBatch> {
  return apiClient.get<BankImportBatch>(`/bank-imports/${batchId}`);
}

export function setRowAllocations(
  batchId: number,
  rowId: number,
  allocations: { projectId: number; amount: number }[],
): Promise<StagedBankRow> {
  return apiClient.put<StagedBankRow>(`/bank-imports/${batchId}/rows/${rowId}/allocations`, {
    allocations,
  });
}

export function removeStagedRow(batchId: number, rowId: number, reason: string): Promise<void> {
  return apiClient.del<void>(
    `/bank-imports/${batchId}/rows/${rowId}?reason=${encodeURIComponent(reason)}`,
  );
}

export function commitBankImport(batchId: number): Promise<BankImportCommitResult> {
  return apiClient.post<BankImportCommitResult>(`/bank-imports/${batchId}/commit`, {});
}

export function discardBankImport(batchId: number): Promise<void> {
  return apiClient.post<void>(`/bank-imports/${batchId}/discard`, {});
}

// ── statement profiles + upload ────────────────────────────────────────────────

export interface BankStatementProfile {
  id: number;
  accountId: number;
  name: string;
  headerRowIndex: number;
  delimiter: string;
  dateColumn: number;
  narrationColumn: number;
  referenceColumn: number | null;
  balanceColumn: number | null;
  singleAmountColumn: boolean;
  amountColumn: number | null;
  debitColumn: number | null;
  creditColumn: number | null;
  debitSign: "Negative" | "Positive";
  dateFormats: string;
}

export interface DetectedColumns {
  headers: string[];
  sampleRows: string[][];
}

export type CreateProfileInput = Omit<BankStatementProfile, "id" | "debitSign"> & {
  debitSign?: "Negative" | "Positive";
};

export function listBankStatementProfiles(accountId: number): Promise<BankStatementProfile[]> {
  return apiClient.get<BankStatementProfile[]>(`/accounts/${accountId}/bank-statement-profiles`);
}

export function createBankStatementProfile(
  input: CreateProfileInput,
): Promise<BankStatementProfile> {
  return apiClient.post<BankStatementProfile>("/bank-statement-profiles", input);
}

async function postForm<T>(path: string, body: FormData): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: "POST",
    credentials: "include",
    body,
  });
  if (!response.ok) {
    let detail = `Request failed (${response.status})`;
    try {
      const problem = (await response.json()) as {
        detail?: string;
        errors?: Record<string, string[]>;
      };
      detail = problem.detail ?? Object.values(problem.errors ?? {})[0]?.[0] ?? detail;
    } catch {
      /* keep default */
    }
    throw new ApiError(response.status, detail);
  }
  return (await response.json()) as T;
}

export function detectStatementColumns(
  file: File,
  headerRowIndex: number,
): Promise<DetectedColumns> {
  const body = new FormData();
  body.set("file", file);
  body.set("headerRowIndex", String(headerRowIndex));
  return postForm<DetectedColumns>("/bank-imports/detect-columns", body);
}

export function uploadStatement(
  accountId: number,
  profileId: number,
  file: File,
): Promise<BankImportBatch> {
  const body = new FormData();
  body.set("accountId", String(accountId));
  body.set("profileId", String(profileId));
  body.set("file", file);
  return postForm<BankImportBatch>("/bank-imports/upload", body);
}
