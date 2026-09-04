import { apiClient } from "@/lib/api";

export interface NotificationItem {
  id: number;
  trigger: string;
  title: string;
  body: string;
  severity: string;
  projectId: number | null;
  entityType: string | null;
  entityId: number | null;
  createdAtUtc: string;
  read: boolean;
}

export interface NotificationRunResult {
  created: number;
  emailsSent: number;
  emailsFailed: number;
}

export interface NotificationChannelConfig {
  roleId: number;
  roleName: string;
  trigger: string;
  channel: "Off" | "Dashboard" | "Email" | "Both";
}

export function listNotifications(unreadOnly = false): Promise<NotificationItem[]> {
  return apiClient.get(`/notifications${unreadOnly ? "?unreadOnly=true" : ""}`);
}

export function markNotificationRead(id: number): Promise<void> {
  return apiClient.post(`/notifications/${id}/read`);
}

export function muteTrigger(trigger: string): Promise<void> {
  return apiClient.post("/notifications/mute", { trigger });
}

export function unmuteTrigger(trigger: string): Promise<void> {
  return apiClient.post("/notifications/unmute", { trigger });
}

export function getChannelConfig(): Promise<NotificationChannelConfig[]> {
  return apiClient.get("/notifications/config");
}

export function setChannelConfig(
  roleId: number,
  trigger: string,
  channel: NotificationChannelConfig["channel"],
): Promise<void> {
  return apiClient.put("/notifications/config", { roleId, trigger, channel });
}

export function evaluateNotifications(): Promise<NotificationRunResult> {
  return apiClient.post("/notifications/evaluate");
}
