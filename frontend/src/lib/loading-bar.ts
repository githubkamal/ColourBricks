/**
 * A tiny in-flight-count store, shared by both loading sources the client asked
 * for (2026-09-04): every `apiClient` request (wired in `lib/api.ts`, the single
 * choke point every API call funnels through) and route navigation (wired in
 * `features/shell/route-loading.tsx`, via link-click interception since the App
 * Router has no built-in "navigation started" event). Either source showing
 * "active" is enough to show the bar — plain start/stop counters, no context
 * needed since nothing here is per-subtree.
 */
type Listener = () => void;

let apiCount = 0;
let navActive = false;
const listeners = new Set<Listener>();

function notify() {
  for (const listener of listeners) listener();
}

export function startApiCall(): void {
  apiCount += 1;
  if (apiCount === 1) notify();
}

export function stopApiCall(): void {
  apiCount = Math.max(0, apiCount - 1);
  if (apiCount === 0) notify();
}

export function startNavigation(): void {
  if (navActive) return;
  navActive = true;
  notify();
}

export function stopNavigation(): void {
  if (!navActive) return;
  navActive = false;
  notify();
}

export function isLoadingActive(): boolean {
  return apiCount > 0 || navActive;
}

export function subscribeLoading(listener: Listener): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}
