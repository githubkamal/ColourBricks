import { apiClient, type PagedResult } from "@/lib/api";

export interface AuditLog {
  id: number;
  userId: number | null;
  timestampUtc: string;
  module: string;
  action: string;
  entityType: string | null;
  recordId: string;
  oldValues: string | null;
  newValues: string | null;
  details: string | null;
}

export interface AuditLogQuery {
  userId?: number;
  module?: string;
  action?: string;
  recordId?: string;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}

export function listAuditLogs(query: AuditLogQuery): Promise<PagedResult<AuditLog>> {
  const params = new URLSearchParams();
  if (query.userId) params.set("userId", String(query.userId));
  if (query.module) params.set("module", query.module);
  if (query.action) params.set("action", query.action);
  if (query.recordId) params.set("recordId", query.recordId);
  if (query.dateFrom) params.set("dateFrom", query.dateFrom);
  if (query.dateTo) params.set("dateTo", query.dateTo);
  params.set("page", String(query.page ?? 1));
  params.set("pageSize", String(query.pageSize ?? 50));
  return apiClient.list<AuditLog>(`/admin/audit-logs?${params.toString()}`);
}
