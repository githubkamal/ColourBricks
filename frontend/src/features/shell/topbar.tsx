"use client";

import { useQueryClient } from "@tanstack/react-query";
import { ChevronsLeft, ChevronsRight, LogOut, Menu } from "lucide-react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { logout } from "@/lib/auth";
import { NotificationBell } from "./notification-bell";
import { ThemeToggle } from "./theme-toggle";
import { useCurrentUser } from "./user-context";

export function Topbar({
  onToggleSidebar,
  railOpen,
  onToggleRail,
}: {
  onToggleSidebar: () => void;
  railOpen: boolean;
  onToggleRail: () => void;
}) {
  const user = useCurrentUser();
  const router = useRouter();
  const queryClient = useQueryClient();
  const [signingOut, setSigningOut] = useState(false);

  async function handleSignOut() {
    setSigningOut(true);
    try {
      await logout();
      queryClient.clear();
      router.replace("/login");
    } catch (error) {
      const message = error instanceof ApiError ? error.message : "Could not sign out.";
      toast.error(message);
      setSigningOut(false);
    }
  }

  return (
    <header className="border-border bg-card/95 flex h-14 shrink-0 items-center gap-3 border-b px-4 shadow-xs backdrop-blur-sm">
      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        className="lg:hidden"
        aria-label="Toggle navigation"
        onClick={onToggleSidebar}
      >
        <Menu aria-hidden="true" />
      </Button>

      <Button
        type="button"
        variant="ghost"
        size="icon-sm"
        className="hidden lg:inline-flex"
        aria-label={railOpen ? "Expand sidebar" : "Collapse sidebar to icons"}
        aria-pressed={railOpen}
        onClick={onToggleRail}
      >
        {railOpen ? <ChevronsRight aria-hidden="true" /> : <ChevronsLeft aria-hidden="true" />}
      </Button>

      <span className="font-heading hidden font-semibold lg:inline">Colour Bricks</span>

      <div className="ml-auto flex min-w-0 items-center gap-3">
        {user.permissions.includes("dashboard.view") && <NotificationBell />}
        <ThemeToggle />
        <span className="bg-border hidden h-5 w-px sm:block" aria-hidden="true" />
        <span className="text-muted-foreground hidden min-w-0 truncate text-sm sm:inline">
          {user.email}
        </span>
        <span className="min-w-0 truncate text-sm font-medium">{user.name}</span>
        <Button
          type="button"
          variant="ghost"
          size="icon-sm"
          onClick={handleSignOut}
          disabled={signingOut}
          aria-label={signingOut ? "Signing out…" : "Sign out"}
          title="Sign out"
          className="text-primary hover:text-primary hover:bg-primary/10 shrink-0"
        >
          <LogOut aria-hidden="true" />
        </Button>
      </div>
    </header>
  );
}
