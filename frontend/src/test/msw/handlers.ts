import { http, HttpResponse } from "msw";
import { apiBaseUrl } from "@/lib/config";
import type { CurrentUser } from "@/lib/auth";

const url = (path: string) => `${apiBaseUrl}${path}`;

export const adminUser: CurrentUser = {
  id: 1,
  name: "Administrator",
  email: "admin@colourbricks.local",
  mobile: null,
  roleId: 1,
  departmentId: null,
  permissions: ["*"],
};

/**
 * Default happy-path handlers shared by every component test. Override per-test with
 * `server.use(...)`.
 */
export const handlers = [
  http.get(url("/auth/me"), () => HttpResponse.json(adminUser)),

  http.post(url("/auth/login"), () => HttpResponse.json(adminUser)),

  http.post(url("/auth/refresh"), () => HttpResponse.json(adminUser)),

  http.post(url("/auth/logout"), () => new HttpResponse(null, { status: 204 })),

  // A representative list endpoint for envelope tests.
  http.get(url("/audit-logs"), () =>
    HttpResponse.json({ items: [], page: 1, pageSize: 50, totalCount: 0, totalPages: 0 }),
  ),
];
