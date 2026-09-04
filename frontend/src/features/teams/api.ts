import { ApiError, apiClient } from "@/lib/api";
import type { CreateTeamInput, TeamDto, TeamGroup } from "./types";

export function listTeams(departmentId?: number, includeInactive = false): Promise<TeamDto[]> {
  const params = new URLSearchParams();
  if (departmentId) params.set("departmentId", String(departmentId));
  if (includeInactive) params.set("includeInactive", "true");
  const query = params.toString();
  return apiClient.get<TeamDto[]>(`/teams${query ? `?${query}` : ""}`);
}

export function listTeamsGrouped(includeInactive = false): Promise<TeamGroup[]> {
  const params = includeInactive ? "?includeInactive=true" : "";
  return apiClient.get<TeamGroup[]>(`/teams/grouped${params}`);
}

export type CreateTeamOutcome =
  | { kind: "created"; team: TeamDto }
  | { kind: "needs-confirmation" }
  | { kind: "exact-duplicate"; existingId: number };

export async function createTeam(
  input: CreateTeamInput,
  confirm: boolean,
): Promise<CreateTeamOutcome> {
  try {
    const body = await apiClient.post<TeamDto | { requiresConfirmation: true }>(
      `/teams?confirm=${confirm}`,
      input,
    );
    if ("requiresConfirmation" in body) return { kind: "needs-confirmation" };
    return { kind: "created", team: body };
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      return { kind: "exact-duplicate", existingId: Number(error.problem?.["existingId"] ?? 0) };
    }
    throw error;
  }
}
