"use client";

import { useQuery } from "@tanstack/react-query";
import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { ApiError } from "@/lib/api";
import { fetchCurrentUser } from "@/lib/auth";
import { AppShell } from "./app-shell";
import { CurrentUserProvider } from "./user-context";

/**
 * Gates the authenticated shell. `fetchCurrentUser` goes through `apiClient`, so an
 * expired access token is refreshed transparently; a hard 401 sends the user to
 * sign in (plan.md P0-T08 acceptance).
 */
export function AuthGate({ children }: { children: React.ReactNode }) {
  const router = useRouter();

  const {
    data: user,
    isLoading,
    isError,
    error,
  } = useQuery({
    queryKey: ["current-user"],
    queryFn: fetchCurrentUser,
    retry: false,
  });

  useEffect(() => {
    if (isError && error instanceof ApiError && error.status === 401) {
      router.replace("/login");
    }
  }, [isError, error, router]);

  if (isLoading) {
    return (
      <div className="text-muted-foreground flex flex-1 items-center justify-center text-sm">
        Loading…
      </div>
    );
  }

  if (!user) {
    return null;
  }

  return (
    <CurrentUserProvider value={user}>
      <AppShell>{children}</AppShell>
    </CurrentUserProvider>
  );
}
