"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { usePathname } from "next/navigation";
import { Badge } from "@/components/ui/badge";
import { buttonVariants } from "@/components/ui/button";
import { dataState } from "@/components/ui/data-state";
import { cn } from "@/lib/utils";
import { formatDate, formatINR } from "@/lib/format";
import { getProject } from "./api";
import { PROJECT_STATUS_LABELS, type ProjectStatus } from "./types";

const STATUS_BADGE: Record<ProjectStatus, "primary" | "positive" | "attention" | "negative"> = {
  Ongoing: "primary",
  Completed: "positive",
  OnHold: "attention",
  Cancelled: "negative",
};

const TABS = (id: number) => [
  { label: "Overview", href: `/projects/${id}` },
  { label: "Dashboard", href: `/projects/${id}/dashboard` },
  { label: "Budget", href: `/projects/${id}/budget` },
  { label: "Budget vs Actual", href: `/projects/${id}/budget-vs-actual` },
  { label: "Financial Ledger", href: `/projects/${id}/financial-ledger` },
  { label: "Profit/Loss", href: `/projects/${id}/pnl` },
];

export function ProjectDetail({ id }: { id: number }) {
  const pathname = usePathname();
  const {
    data: project,
    isPending,
    isError,
  } = useQuery({
    queryKey: ["project", id],
    queryFn: () => getProject(id),
  });

  const nav = (
    <nav className="border-border flex flex-wrap gap-1 border-b">
      {TABS(id).map((tab) => {
        const active = pathname === tab.href;
        return (
          <Link
            key={tab.href}
            href={tab.href}
            className={cn(
              "-mb-px border-b-2 px-3 py-2 text-sm transition-colors",
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
  );

  if (isPending || isError || !project) {
    return (
      <div className="max-w-3xl space-y-5">
        <div className="flex flex-wrap items-center justify-between gap-2">
          <h1 className="font-heading text-2xl font-semibold">Project</h1>
          <Link href="/projects" className={buttonVariants({ variant: "outline", size: "sm" })}>
            Back to Projects
          </Link>
        </div>
        {nav}
        {dataState({ isPending, isError: isError || !project, errorLabel: "Project not found." })}
      </div>
    );
  }

  return (
    <div className="max-w-3xl space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="text-muted-foreground font-mono text-xs">{project.code}</p>
          <div className="flex items-center gap-2">
            <h1 className="font-heading text-2xl font-semibold">{project.name}</h1>
            <Badge variant={STATUS_BADGE[project.status]}>
              {PROJECT_STATUS_LABELS[project.status]}
            </Badge>
          </div>
        </div>
        <Link href="/projects" className={buttonVariants({ variant: "outline", size: "sm" })}>
          Back to Projects
        </Link>
      </div>

      {nav}

      <div className="bg-card border-border rounded-xl border p-5 shadow-xs">
        <dl className="grid grid-cols-1 gap-x-8 gap-y-4 text-sm sm:grid-cols-2">
          <Row label="Manager">{project.managerId ? `#${project.managerId}` : "—"}</Row>
          <Row label="Start date">{formatDate(project.startDate)}</Row>
          <Row label="Expected completion">
            {project.expectedEndDate ? formatDate(project.expectedEndDate) : "—"}
          </Row>
          <Row label="Contract value" numeric>
            {formatINR(project.contractValue)}
          </Row>
          <Row label="Estimated cost" numeric>
            {formatINR(project.estimatedCost)}
          </Row>
          <Row label="Site address" className="sm:col-span-2">
            {project.siteAddress ?? "—"}
          </Row>
          <Row label="Notes" className="sm:col-span-2">
            {project.notes ?? "—"}
          </Row>
        </dl>
      </div>
    </div>
  );
}

function Row({
  label,
  children,
  numeric,
  className,
}: {
  label: string;
  children: React.ReactNode;
  numeric?: boolean;
  className?: string;
}) {
  return (
    <div className={className}>
      <dt className="text-muted-foreground text-xs">{label}</dt>
      <dd className={numeric ? "num tabular-nums" : undefined}>{children}</dd>
    </div>
  );
}
