import { ApiError, apiClient } from "@/lib/api";
import type { ProjectDonation, UpsertDonationInput } from "./types";

export async function getProjectDonation(projectId: number): Promise<ProjectDonation | null> {
  try {
    return await apiClient.get<ProjectDonation>(`/projects/${projectId}/donation`);
  } catch (error) {
    if (error instanceof ApiError && error.status === 404) return null;
    throw error;
  }
}

export function upsertProjectDonation(
  projectId: number,
  input: UpsertDonationInput,
): Promise<ProjectDonation> {
  return apiClient.put<ProjectDonation>(`/projects/${projectId}/donation`, input);
}

export interface DonationTempleOutstanding {
  templeId: number;
  templeName: string;
  allocated: number;
  paid: number;
  outstanding: number;
}

export function donationOutstanding(projectId: number): Promise<DonationTempleOutstanding[]> {
  return apiClient.get<DonationTempleOutstanding[]>(`/projects/${projectId}/donation/outstanding`);
}

export function payDonationTemple(
  projectId: number,
  templeId: number,
  input: { date: string; amount: number; paymentModeId: number; accountId?: number | null },
): Promise<unknown> {
  return apiClient.post(`/projects/${projectId}/donation/temples/${templeId}/payments`, input);
}
