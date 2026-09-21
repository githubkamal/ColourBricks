import { NextResponse, type NextRequest } from "next/server";

// Mirrors AuthCookies in the API — kept in sync manually since the two live in
// separate apps/repos-in-a-monorepo with no shared package.
const ACCESS_TOKEN_COOKIE = "cb_access";
const REFRESH_TOKEN_COOKIE = "cb_refresh";

/**
 * A cheap, cookie-presence gate that runs before any (app) route's bundle
 * ships. Without this, a logged-out visit to "/" downloaded the whole
 * authenticated shell (sidebar, dashboards, charts), mounted it, called
 * `/auth/me`, waited for the 401, and only then redirected client-side —
 * the exact chain GTmetrix flagged as a 5.6s LCP with "unused JavaScript"
 * and a chained critical request. This only works because the API's auth
 * cookies now carry a shared `Auth:CookieDomain` (see docs/deployment.md)
 * so the app subdomain can see them at all.
 *
 * This is a fast path, not the source of truth: an expired-but-present
 * cookie still falls through to `AuthGate`'s `/auth/me` call, which
 * transparently refreshes or, failing that, redirects — same as today.
 */
export function proxy(request: NextRequest) {
  const hasSession =
    request.cookies.has(ACCESS_TOKEN_COOKIE) || request.cookies.has(REFRESH_TOKEN_COOKIE);

  if (!hasSession) {
    return NextResponse.redirect(new URL("/login", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/((?!login|_next/static|_next/image|favicon.ico).*)"],
};
