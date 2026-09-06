"use client";

import { usePathname } from "next/navigation";
import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";
import { Footer } from "./footer";
import { Sidebar } from "./sidebar";
import { Topbar } from "./topbar";
import { useCurrentUser } from "./user-context";

const RAIL_KEY = "cb.sidebarRail";

function readStoredRail(): boolean {
  try {
    return window.localStorage.getItem(RAIL_KEY) === "1";
  } catch {
    return false;
  }
}

export function AppShell({ children }: { children: React.ReactNode }) {
  const user = useCurrentUser();
  const pathname = usePathname();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  // Full sidebar vs icon-only rail (client request, 2026-09-06). Read lazily
  // in an initializer, not an effect, so there's no flash of the wrong width.
  const [rail, setRail] = useState(() =>
    typeof window === "undefined" ? false : readStoredRail(),
  );

  useEffect(() => {
    if (!sidebarOpen) return;
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setSidebarOpen(false);
    }
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [sidebarOpen]);

  function toggleRail() {
    setRail((v) => {
      const next = !v;
      try {
        window.localStorage.setItem(RAIL_KEY, next ? "1" : "0");
      } catch {
        /* private mode / disabled storage — the toggle still works this visit */
      }
      return next;
    });
  }

  return (
    <div className="flex h-full min-h-0 flex-1">
      {/* Backdrop for the mobile drawer */}
      {sidebarOpen && (
        <button
          type="button"
          aria-label="Close navigation"
          className="fixed inset-0 z-20 bg-black/20 lg:hidden"
          onClick={() => setSidebarOpen(false)}
        />
      )}

      <aside
        className={cn(
          "bg-sidebar border-sidebar-border z-30 shrink-0 overscroll-contain border-r transition-[width]",
          rail ? "lg:w-16" : "lg:w-68",
          "fixed inset-y-0 left-0 w-68 transition-transform lg:static lg:translate-x-0",
          sidebarOpen ? "translate-x-0" : "-translate-x-full",
        )}
      >
        <Sidebar permissions={user.permissions} rail={rail} />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar
          onToggleSidebar={() => setSidebarOpen((open) => !open)}
          railOpen={rail}
          onToggleRail={toggleRail}
        />
        <main className="min-h-0 flex-1 overflow-auto p-4">
          <div
            key={pathname}
            className="bg-card border-border animate-in fade-in-0 slide-in-from-bottom-1 min-h-full rounded-xl border p-6 shadow-sm duration-300 ease-out"
          >
            {children}
          </div>
        </main>
        <Footer />
      </div>
    </div>
  );
}
