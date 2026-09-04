import { apiClient } from "./api";

export interface CurrentUser {
  id: number;
  name: string;
  email: string;
  mobile: string | null;
  roleId: number | null;
  departmentId: number | null;
  /** Flattened `module.action` keys, or `["*"]` for an Administrator. */
  permissions: string[];
}

export function login(email: string, password: string): Promise<CurrentUser> {
  return apiClient.post<CurrentUser>("/auth/login", { email, password });
}

export function logout(): Promise<void> {
  return apiClient.post<void>("/auth/logout");
}

export function fetchCurrentUser(): Promise<CurrentUser> {
  return apiClient.get<CurrentUser>("/auth/me");
}
