"use client";

import { useEffect, useState } from "react";
import { cn } from "@/lib/utils";
import { Footer } from "./footer";
import { Sidebar } from "./sidebar";
import { Topbar } from "./topbar";
import { useCurrentUser } from "./user-context";

export function AppShell({ children }: { children: React.ReactNode }) {
  const user = useCurrentUser();
  const [sidebarOpen, setSidebarOpen] = useState(false);

  useEffect(() => {
    if (!sidebarOpen) return;
    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") setSidebarOpen(false);
    }
    document.addEventListener("keydown", onKeyDown);
    return () => document.removeEventListener("keydown", onKeyDown);
  }, [sidebarOpen]);

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
          "bg-sidebar border-sidebar-border z-30 w-64 shrink-0 overscroll-contain border-r",
          "fixed inset-y-0 left-0 transition-transform lg:static lg:translate-x-0",
          sidebarOpen ? "translate-x-0" : "-translate-x-full",
        )}
      >
        <Sidebar permissions={user.permissions} />
      </aside>

      <div className="flex min-w-0 flex-1 flex-col">
        <Topbar onToggleSidebar={() => setSidebarOpen((open) => !open)} />
        <main className="min-h-0 flex-1 overflow-auto p-6">{children}</main>
        <Footer />
      </div>
    </div>
  );
}
