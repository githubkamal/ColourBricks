import { ApiError, apiClient } from "@/lib/api";
import type { AccountDetail, AccountListItem, AccountType, CreateAccountInput } from "./types";

export function listAccounts(
  type?: AccountType,
  includeInactive = false,
): Promise<AccountListItem[]> {
  const params = new URLSearchParams();
  if (type) params.set("type", type);
  if (includeInactive) params.set("includeInactive", "true");
  const query = params.toString();
  return apiClient.get<AccountListItem[]>(`/accounts${query ? `?${query}` : ""}`);
}

export function getAccount(id: number): Promise<AccountDetail> {
  return apiClient.get<AccountDetail>(`/accounts/${id}`);
}

export type CreateAccountOutcome =
  { kind: "created"; account: AccountDetail } | { kind: "duplicate"; existingId: number };

export async function createAccount(input: CreateAccountInput): Promise<CreateAccountOutcome> {
  try {
    const account = await apiClient.post<AccountDetail>("/accounts", input);
    return { kind: "created", account };
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      return { kind: "duplicate", existingId: Number(error.problem?.["existingId"] ?? 0) };
    }
    throw error;
  }
}

export function updateAccount(
  id: number,
  input: CreateAccountInput & { isActive: boolean; concurrencyStamp: string },
): Promise<AccountDetail> {
  return apiClient.put<AccountDetail>(`/accounts/${id}`, input);
}
