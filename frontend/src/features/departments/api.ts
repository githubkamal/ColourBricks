import { ApiError, apiClient } from "@/lib/api";
import type { CreateDepartmentInput, DepartmentDto } from "./types";

export function listDepartments(includeInactive = false): Promise<DepartmentDto[]> {
  const params = includeInactive ? "?includeInactive=true" : "";
  return apiClient.get<DepartmentDto[]>(`/departments${params}`);
}

export type CreateDepartmentOutcome =
  { kind: "created"; department: DepartmentDto } | { kind: "duplicate"; existingId: number };

export async function createDepartment(
  input: CreateDepartmentInput,
): Promise<CreateDepartmentOutcome> {
  try {
    const department = await apiClient.post<DepartmentDto>("/departments", input);
    return { kind: "created", department };
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      return { kind: "duplicate", existingId: Number(error.problem?.["existingId"] ?? 0) };
    }
    throw error;
  }
}

export function updateDepartment(
  id: number,
  input: { name: string; isActive: boolean; concurrencyStamp: string },
): Promise<DepartmentDto> {
  return apiClient.put<DepartmentDto>(`/departments/${id}`, input);
}
