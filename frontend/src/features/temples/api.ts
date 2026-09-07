import { ApiError, apiClient } from "@/lib/api";

export interface TempleSummary {
  id: number;
  name: string;
  isActive: boolean;
}

export interface TempleDetail {
  id: number;
  name: string;
  isActive: boolean;
  concurrencyStamp: string;
}

export function listTemples(search?: string): Promise<TempleSummary[]> {
  const params = search ? `?search=${encodeURIComponent(search)}` : "";
  return apiClient.get<TempleSummary[]>(`/temples${params}`);
}

export function getTemple(id: number): Promise<TempleDetail> {
  return apiClient.get<TempleDetail>(`/temples/${id}`);
}

export function updateTemple(
  id: number,
  input: { name: string; isActive: boolean; concurrencyStamp: string },
): Promise<TempleDetail> {
  return apiClient.put<TempleDetail>(`/temples/${id}`, input);
}

export type CreateTempleOutcome =
  | { kind: "created"; temple: TempleSummary }
  | { kind: "needs-confirmation" }
  | { kind: "exact-duplicate"; existingId: number };

export async function createTemple(name: string, confirm: boolean): Promise<CreateTempleOutcome> {
  try {
    const body = await apiClient.post<TempleSummary | { requiresConfirmation: true }>(
      `/temples?confirm=${confirm}`,
      { name },
    );
    if ("requiresConfirmation" in body) return { kind: "needs-confirmation" };
    return { kind: "created", temple: body };
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      return { kind: "exact-duplicate", existingId: Number(error.problem?.["existingId"] ?? 0) };
    }
    throw error;
  }
}
