"use client";

import { useRouter } from "next/navigation";
import { useEffect } from "react";
import { Spinner } from "@/components/ui/spinner";
import { useCurrentUser } from "@/features/shell/user-context";
import { hasPermission, visibleNavigation } from "@/lib/navigation";

/** Redirects to the best landing page for the signed-in user's role — the company
 * dashboard when they can see it, otherwise the first nav item their permissions grant. */
export default function RootPage() {
  const router = useRouter();
  const user = useCurrentUser();

  useEffect(() => {
    const target = hasPermission(user.permissions, "dashboard.view")
      ? "/dashboard/company"
      : (visibleNavigation(user.permissions)[0]?.items[0]?.href ?? null);
    if (target) router.replace(target);
  }, [user.permissions, router]);

  return (
    <div className="text-muted-foreground flex flex-1 flex-col items-center justify-center gap-2 text-sm">
      <Spinner className="size-6" />
      Loading…
    </div>
  );
}
