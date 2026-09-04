/**
 * The API base URL. In dev the .NET API runs on http://localhost:5095 with
 * `Auth:CookieSecure=false`; override with `NEXT_PUBLIC_API_BASE_URL` elsewhere.
 */
export const apiBaseUrl =
  process.env.NEXT_PUBLIC_API_BASE_URL?.replace(/\/$/, "") ?? "http://localhost:5095/api/v1";
