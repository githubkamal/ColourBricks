import { ApiError, apiClient } from "@/lib/api";
import type { CreateUserInput, RoleOption, UpdateUserInput, UserListItem } from "./types";

export function listUsers(includeInactive = true): Promise<UserListItem[]> {
  const params = includeInactive ? "?includeInactive=true" : "";
  return apiClient.get<UserListItem[]>(`/users${params}`);
}

export function listRoles(): Promise<RoleOption[]> {
  return apiClient.get<RoleOption[]>("/users/roles");
}

export type CreateUserOutcome =
  { kind: "created"; user: UserListItem } | { kind: "duplicate"; existingId: number };

export async function createUser(input: CreateUserInput): Promise<CreateUserOutcome> {
  try {
    const user = await apiClient.post<UserListItem>("/users", input);
    return { kind: "created", user };
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      return { kind: "duplicate", existingId: Number(error.problem?.["existingId"] ?? 0) };
    }
    throw error;
  }
}

export function updateUser(id: number, input: UpdateUserInput): Promise<UserListItem> {
  return apiClient.put<UserListItem>(`/users/${id}`, input);
}

export function deleteUser(id: number, reason: string): Promise<void> {
  return apiClient.del<void>(`/users/${id}?reason=${encodeURIComponent(reason)}`);
}

export function assignProjects(id: number, projectIds: number[]): Promise<UserListItem> {
  return apiClient.put<UserListItem>(`/users/${id}/projects`, { projectIds });
}

export function resetPassword(id: number, newPassword: string): Promise<void> {
  return apiClient.post<void>(`/users/${id}/reset-password`, { newPassword });
}
