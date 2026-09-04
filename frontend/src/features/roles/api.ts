import { ApiError, apiClient } from "@/lib/api";

export interface RoleSummary {
  id: number;
  name: string;
  description: string | null;
  isSystem: boolean;
  isActive: boolean;
  permissionCount: number;
}

export interface RoleDetail extends RoleSummary {
  permissionKeys: string[];
  concurrencyStamp: string;
}

export interface PermissionCatalogue {
  modules: { key: string; name: string }[];
  actions: string[];
}

export function listRoles(): Promise<RoleSummary[]> {
  return apiClient.get<RoleSummary[]>("/roles");
}

export function getRole(id: number): Promise<RoleDetail> {
  return apiClient.get<RoleDetail>(`/roles/${id}`);
}

export function getPermissionCatalogue(): Promise<PermissionCatalogue> {
  return apiClient.get<PermissionCatalogue>("/roles/catalogue");
}

export type CreateRoleOutcome =
  { kind: "created"; role: RoleDetail } | { kind: "duplicate"; existingId: number };

export async function createRole(name: string, description?: string): Promise<CreateRoleOutcome> {
  try {
    const role = await apiClient.post<RoleDetail>("/roles", { name, description });
    return { kind: "created", role };
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      return { kind: "duplicate", existingId: Number(error.problem?.["existingId"] ?? 0) };
    }
    throw error;
  }
}

export function setRolePermissions(id: number, permissionKeys: string[]): Promise<RoleDetail> {
  return apiClient.put<RoleDetail>(`/roles/${id}/permissions`, { permissionKeys });
}

export function deleteRole(id: number, reason: string): Promise<void> {
  return apiClient.del<void>(`/roles/${id}?reason=${encodeURIComponent(reason)}`);
}
