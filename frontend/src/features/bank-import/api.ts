import { apiBaseUrl } from "@/lib/config";
import { ApiError, apiClient } from "@/lib/api";

/** What one slice of an imported row is mapped to — a project, a party or a common bucket. */
export const MAPPING_TARGETS = [
  "Project",
  "Vendor",
  "FieldOfficer",
  "Labour",
  "Client",
  "Personal",
  "Office",
  "Savings",
  "Other",
] as const;
export type MappingTarget = (typeof MAPPING_TARGETS)[number];

export interface StagedProjectAllocation {
  target: MappingTarget;
  projectId: number | null;
  projectName: string;
  partyId: number | null;
  partyName: string;
  amount: number;
}

export interface RowMappingInput {
  target: MappingTarget;
  amount: number;
  projectId?: number | null;
  partyId?: number | null;
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
  allocations: RowMappingInput[],
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

export function updateBankStatementProfile(
  id: number,
  input: Omit<CreateProfileInput, "accountId">,
): Promise<BankStatementProfile> {
  return apiClient.put<BankStatementProfile>(`/bank-statement-profiles/${id}`, input);
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

// ── upload-wizard preview (dry run — nothing persisted) ─────────────────────────

/** One row from a dry-run parse. Nothing behind this has been saved; `existsInDb` is
 * only meaningful when the preview was fetched with `checkExisting: true`. */
export interface PreviewBankImportRow {
  sourceLineNo: number;
  valueDate: string | null;
  narration: string | null;
  debit: number;
  credit: number;
  balance: number | null;
  bankReference: string | null;
  parseError: string | null;
  existsInDb: boolean;
}

export interface PreviewBankImportInput {
  accountId: number;
  file: File;
  headerRowIndex: number;
  delimiter?: string;
  dateColumn: number;
  narrationColumn: number;
  referenceColumn?: number;
  balanceColumn?: number;
  singleAmountColumn: boolean;
  amountColumn?: number;
  debitColumn?: number;
  creditColumn?: number;
  debitSign?: "Negative" | "Positive";
  dateFormats: string;
  /** When true, also flags rows whose date+amount+narration+reference signature already
   * exists in the ledger for this account — used by the wizard's Validate step. */
  checkExisting: boolean;
}

export function previewBankImport(input: PreviewBankImportInput): Promise<PreviewBankImportRow[]> {
  const body = new FormData();
  body.set("accountId", String(input.accountId));
  body.set("headerRowIndex", String(input.headerRowIndex));
  body.set("delimiter", input.delimiter ?? ",");
  body.set("dateColumn", String(input.dateColumn));
  body.set("narrationColumn", String(input.narrationColumn));
  if (input.referenceColumn !== undefined)
    body.set("referenceColumn", String(input.referenceColumn));
  if (input.balanceColumn !== undefined) body.set("balanceColumn", String(input.balanceColumn));
  body.set("singleAmountColumn", String(input.singleAmountColumn));
  if (input.amountColumn !== undefined) body.set("amountColumn", String(input.amountColumn));
  if (input.debitColumn !== undefined) body.set("debitColumn", String(input.debitColumn));
  if (input.creditColumn !== undefined) body.set("creditColumn", String(input.creditColumn));
  body.set("debitSign", input.debitSign ?? "Negative");
  body.set("dateFormats", input.dateFormats);
  body.set("checkExisting", String(input.checkExisting));
  body.set("file", input.file);
  return postForm<PreviewBankImportRow[]>("/bank-imports/preview", body);
}

/** Stages only the rows handed to it (the checked survivors of the preview grid) as a
 * Draft batch, without re-parsing the file server-side. */
export function createBankImportFromRows(
  accountId: number,
  fileName: string,
  rows: PreviewBankImportRow[],
): Promise<BankImportBatch> {
  return apiClient.post<BankImportBatch>("/bank-imports", {
    accountId,
    fileName,
    rows: rows.map((r) => ({
      sourceLineNo: r.sourceLineNo,
      valueDate: r.valueDate,
      narration: r.narration,
      debit: r.debit,
      credit: r.credit,
      balance: r.balance,
      bankReference: r.bankReference,
    })),
  });
}
