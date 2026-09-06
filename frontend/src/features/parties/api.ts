import { ApiError, apiClient, type PagedResult } from "@/lib/api";
import type {
  CreatePartyInput,
  NearDuplicate,
  PartyDto,
  PartySearchItem,
  PartyType,
  UpdatePartyInput,
} from "./types";

export function searchParties(
  query: string,
  type?: PartyType,
  limit = 20,
): Promise<PartySearchItem[]> {
  const params = new URLSearchParams({ q: query, limit: String(limit) });
  if (type) params.set("type", type);
  return apiClient.get<PartySearchItem[]>(`/parties/search?${params.toString()}`);
}

export function getParty(id: number): Promise<PartyDto> {
  return apiClient.get<PartyDto>(`/parties/${id}`);
}

export function updateParty(id: number, input: UpdatePartyInput): Promise<PartyDto> {
  return apiClient.put<PartyDto>(`/parties/${id}`, input);
}

export function listParties(
  type?: PartyType,
  search?: string,
  page = 1,
): Promise<PagedResult<PartySearchItem>> {
  const params = new URLSearchParams({ page: String(page), pageSize: "20" });
  if (type) params.set("type", type);
  if (search) params.set("search", search);
  return apiClient.list<PartySearchItem>(`/parties?${params.toString()}`);
}

export type CreatePartyOutcome =
  | { kind: "created"; party: PartyDto }
  | { kind: "needs-confirmation"; nearDuplicates: NearDuplicate[] }
  | { kind: "exact-duplicate"; existingId: number };

export async function createParty(
  input: CreatePartyInput,
  confirm: boolean,
): Promise<CreatePartyOutcome> {
  try {
    const body = await apiClient.post<
      PartyDto | { requiresConfirmation: true; nearDuplicates: NearDuplicate[] }
    >(`/parties?confirm=${confirm}`, input);

    if ("requiresConfirmation" in body) {
      return { kind: "needs-confirmation", nearDuplicates: body.nearDuplicates };
    }
    return { kind: "created", party: body };
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      const existingId = Number(error.problem?.["existingId"] ?? 0);
      if (existingId > 0) return { kind: "exact-duplicate", existingId };
    }
    throw error;
  }
}
