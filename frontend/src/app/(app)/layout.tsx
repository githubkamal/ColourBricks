import { AuthGate } from "@/features/shell/auth-gate";

export default function AppLayout({ children }: { children: React.ReactNode }) {
  // A definite height (not just body's `min-h-full`, which is `auto` and grows
  // with content) so AppShell's `h-full` actually resolves to one viewport's
  // worth of space instead of stretching with the page. That's what lets the
  // sidebar's nav and the main content area scroll independently while the
  // topbar and footer stay put, instead of the whole page scrolling as one
  // (client request, 2026-09-07).
  return (
    <div className="h-dvh overflow-hidden">
      <AuthGate>{children}</AuthGate>
    </div>
  );
}
