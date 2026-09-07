"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";
import { getProject } from "./api";
import { PROJECT_STATUS_LABELS, type ProjectStatus } from "./types";

// Matches the generic StatusBadge palette's ongoing/completed/onhold/cancelled
// colours (client request, 2026-09-06) — fixed, not theme-accent-driven.
const STATUS_BADGE: Record<ProjectStatus, "sky" | "green" | "yellow" | "red"> = {
  Ongoing: "sky",
  Completed: "green",
  OnHold: "yellow",
  Cancelled: "red",
};

const TABS = (id: number) => [
  { label: "Overview", href: `/projects/${id}` },
  { label: "Dashboard", href: `/projects/${id}/dashboard` },
  { label: "Income", href: `/projects/${id}/income` },
  { label: "Expenses", href: `/projects/${id}/expenses` },
  { label: "Budget", href: `/projects/${id}/budget` },
  { label: "Budget vs Actual", href: `/projects/${id}/budget-vs-actual` },
  { label: "Financial Ledger", href: `/projects/${id}/financial-ledger` },
  { label: "Profit/Loss", href: `/projects/${id}/pnl` },
];

/**
 * Shared tab bar for every per-project page (client request, 2026-09-07 —
 * landing on e.g. Financial Ledger from the Overview tabs left no way to
 * reach the other project screens, since each tab is its own top-level
 * route/page component with no shared chrome). Render this once at the top
 * of each per-project page.
 */
export function ProjectTabs({
  projectId,
  title,
}: {
  projectId: number;
  /** Page-specific title (e.g. "Project dashboard") shown next to the project name. */
  title?: string;
}) {
  const pathname = usePathname();
  const { data: project } = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => getProject(projectId),
  });

  return (
    <div className="flex flex-wrap items-center gap-4">
      <nav className="border-border flex scrollbar-none flex-nowrap gap-1 overflow-x-auto border-b">
        {TABS(projectId).map((tab) => {
          const active = pathname === tab.href;
          return (
            <Link
              key={tab.href}
              href={tab.href}
              className={cn(
                "-mb-px shrink-0 border-b-2 px-3 py-2 text-sm whitespace-nowrap transition-colors",
                active
                  ? "border-primary text-foreground font-medium"
                  : "text-muted-foreground hover:text-foreground border-transparent",
              )}
            >
              {tab.label}
            </Link>
          );
        })}
      </nav>
      <div className="flex shrink-0 flex-wrap items-center gap-3">
        {title && <h2 className="font-heading text-lg font-semibold">{title}</h2>}
        {title && project && <span className="text-border">|</span>}
        {project && <span className="text-muted-foreground font-mono text-xs">{project.code}</span>}
        {project && <h1 className="font-heading text-lg font-semibold">{project.name}</h1>}
        {project && (
          <Badge variant={STATUS_BADGE[project.status]}>
            {PROJECT_STATUS_LABELS[project.status]}
          </Badge>
        )}
      </div>
    </div>
  );
}
