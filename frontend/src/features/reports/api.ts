import { apiClient } from "@/lib/api";

export const DATE_PRESETS = [
  "Custom",
  "Today",
  "Yesterday",
  "ThisWeek",
  "PreviousWeek",
  "ThisMonth",
  "PreviousMonth",
  "CurrentYear",
  "PreviousYear",
  "ThisFinancialYear",
  "PreviousFinancialYear",
] as const;

export type DatePreset = (typeof DATE_PRESETS)[number];

export interface ReportColumn {
  key: string;
  header: string;
  numeric: boolean;
  defaultVisible: boolean;
  total: string | null;
  drillThrough: string | null;
  /** How a numeric value should be formatted: "currency" (₹) or "quantity" (plain number). */
  unit: "currency" | "quantity";
}

export interface ReportCatalogEntry {
  key: string;
  title: string;
  columns: ReportColumn[];
  supportedFilters: string[];
  /** Column keys BRD §57's Sort function actually resorts by — not necessarily every column. */
  sortableColumnKeys: string[];
}

export interface ReportResult {
  key: string;
  title: string;
  columns: ReportColumn[];
  rows: Record<string, unknown>[];
  totals: Record<string, number>;
  rangeFrom: string | null;
  rangeTo: string | null;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ReportFilterState {
  datePreset: DatePreset;
  dateFrom?: string;
  dateTo?: string;
  projectId?: string;
  vendorId?: string;
  subcontractorId?: string;
  departmentId?: string;
  itemId?: string;
  categoryId?: string;
  paymentModeId?: string;
  accountId?: string;
  paymentStatus?: string;
  transactionType?: string;
  reconciliationStatus?: string;
  search?: string;
  sortBy?: string;
  sortDir?: "asc" | "desc";
  page: number;
  pageSize: number;
}

export const emptyFilter: ReportFilterState = { datePreset: "Custom", page: 1, pageSize: 50 };

function toQuery(filter: ReportFilterState): string {
  const params = new URLSearchParams();
  for (const [key, value] of Object.entries(filter)) {
    if (value !== undefined && value !== "" && value !== null) {
      params.set(key, String(value));
    }
  }
  return params.toString();
}

export function reportCatalog(): Promise<ReportCatalogEntry[]> {
  return apiClient.get<ReportCatalogEntry[]>("/reports/catalog");
}

export function runReport(key: string, filter: ReportFilterState): Promise<ReportResult> {
  return apiClient.get<ReportResult>(`/reports/run/${key}?${toQuery(filter)}`);
}
