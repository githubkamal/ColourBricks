"use client";

import { useQueryClient } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";
import { Button } from "@/components/ui/button";
import { ApiError } from "@/lib/api";
import { logout } from "@/lib/auth";
import { NotificationBell } from "./notification-bell";
import { ThemeToggle } from "./theme-toggle";
import { useCurrentUser } from "./user-context";

export function Topbar({ onToggleSidebar }: { onToggleSidebar: () => void }) {
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
    <header className="border-border bg-background/95 flex h-14 shrink-0 items-center gap-3 border-b px-4 shadow-xs backdrop-blur-sm">
      <Button
        type="button"
        variant="ghost"
        size="sm"
        className="lg:hidden"
        aria-label="Toggle navigation"
        onClick={onToggleSidebar}
      >
        Menu
      </Button>

      <span className="flex items-center gap-2 font-semibold">
        <span className="bg-primary size-2 rounded-full" aria-hidden="true" />
        Colour Bricks
      </span>

      <div className="ml-auto flex items-center gap-3">
        {user.permissions.includes("dashboard.view") && <NotificationBell />}
        <ThemeToggle />
        <span className="bg-border hidden h-5 w-px sm:block" aria-hidden="true" />
        <span className="text-muted-foreground hidden text-sm sm:inline">{user.email}</span>
        <span className="text-sm font-medium">{user.name}</span>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={handleSignOut}
          disabled={signingOut}
        >
          {signingOut ? "Signing out…" : "Sign out"}
        </Button>
      </div>
    </header>
  );
}
