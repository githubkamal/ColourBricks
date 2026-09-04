import { apiBaseUrl } from "@/lib/config";

/**
 * Same-origin proxy for one attachment (client-added, 2026-09-05 — built for the
 * System Settings company logo, but works for any attachment id).
 *
 * The API's auth cookies are `SameSite=Lax`, so a browser will not attach them to
 * a cross-origin subresource request like `<img src="http://localhost:5095/...">`
 * — only to a top-level navigation (which is why the existing `AttachmentPanel`
 * opens attachments via `<a target="_blank">`, not `<img>`). An inline preview
 * (the logo in a print header, say) needs the request to be same-origin instead,
 * so this route runs server-side, forwards the browser's own cookie header along
 * (a server-to-server fetch has no SameSite restriction), and streams the API's
 * response straight back — from the browser's point of view it only ever talked
 * to its own origin.
 */
export async function GET(request: Request, { params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const cookie = request.headers.get("cookie");

  const response = await fetch(`${apiBaseUrl}/attachments/${id}`, {
    headers: cookie ? { cookie } : {},
    cache: "no-store",
  });

  return new Response(response.body, {
    status: response.status,
    headers: {
      "content-type": response.headers.get("content-type") ?? "application/octet-stream",
      "cache-control": "private, max-age=300",
    },
  });
}
