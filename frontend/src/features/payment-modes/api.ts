import { ApiError, apiClient } from "@/lib/api";
import type { CreatePaymentModeInput, PaymentModeDto } from "./types";

export function listPaymentModes(includeInactive = false): Promise<PaymentModeDto[]> {
  const params = includeInactive ? "?includeInactive=true" : "";
  return apiClient.get<PaymentModeDto[]>(`/payment-modes${params}`);
}

export type CreatePaymentModeOutcome =
  { kind: "created"; mode: PaymentModeDto } | { kind: "duplicate"; existingId: number };

export async function createPaymentMode(
  input: CreatePaymentModeInput,
): Promise<CreatePaymentModeOutcome> {
  try {
    const mode = await apiClient.post<PaymentModeDto>("/payment-modes", input);
    return { kind: "created", mode };
  } catch (error) {
    if (error instanceof ApiError && error.status === 409) {
      return { kind: "duplicate", existingId: Number(error.problem?.["existingId"] ?? 0) };
    }
    throw error;
  }
}

export function updatePaymentMode(
  id: number,
  input: {
    name: string;
    requiresAccount: boolean;
    requiresReference: boolean;
    isActive: boolean;
    concurrencyStamp: string;
  },
): Promise<PaymentModeDto> {
  return apiClient.put<PaymentModeDto>(`/payment-modes/${id}`, input);
}
