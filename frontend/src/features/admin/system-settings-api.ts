import { apiClient } from "@/lib/api";

export interface SystemSettings {
  companyName: string;
  companyAddress: string | null;
  companyGstin: string | null;
  companyLogoUrl: string | null;
  vendorOutstandingAlertLimit: number;
  overdueAlertDays: number;
  profitFloorAlertPercent: number;
  loanEmiReminderDaysAhead: number;
  concurrencyStamp: string;
}

export function getSystemSettings(): Promise<SystemSettings> {
  return apiClient.get<SystemSettings>("/admin/settings");
}

export function updateSystemSettings(input: SystemSettings): Promise<SystemSettings> {
  return apiClient.put<SystemSettings>("/admin/settings", input);
}
