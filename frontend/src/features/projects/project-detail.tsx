"use client";

import { useQuery } from "@tanstack/react-query";
import Link from "next/link";
import { buttonVariants } from "@/components/ui/button";
import { formatDate, formatINR } from "@/lib/format";
import { getProject } from "./api";
import { PROJECT_STATUS_LABELS } from "./types";

export function ProjectDetail({ id }: { id: number }) {
  const {
    data: project,
    isPending,
    isError,
  } = useQuery({
    queryKey: ["project", id],
    queryFn: () => getProject(id),
  });

  if (isPending) return <p className="text-muted-foreground text-sm">Loading…</p>;
  if (isError || !project) return <p className="text-negative text-sm">Project not found.</p>;

  return (
    <div className="max-w-3xl space-y-5">
      <div className="flex flex-wrap items-center justify-between gap-2">
        <div>
          <p className="text-muted-foreground font-mono text-xs">{project.code}</p>
          <h1 className="text-lg font-semibold">{project.name}</h1>
        </div>
        <Link href="/projects" className={buttonVariants({ variant: "outline", size: "sm" })}>
          Back to Projects
        </Link>
      </div>

      <dl className="grid grid-cols-1 gap-x-8 gap-y-3 text-sm sm:grid-cols-2">
        <Row label="Status">{PROJECT_STATUS_LABELS[project.status]}</Row>
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

      <div className="flex flex-wrap gap-2 border-t pt-4">
        <Link
          href={`/projects/${id}/dashboard`}
          className={buttonVariants({ variant: "outline", size: "sm" })}
        >
          Dashboard
        </Link>
        <Link
          href={`/projects/${id}/financial-ledger`}
          className={buttonVariants({ variant: "outline", size: "sm" })}
        >
          Financial Ledger
        </Link>
        <Link
          href={`/projects/${id}/budget`}
          className={buttonVariants({ variant: "outline", size: "sm" })}
        >
          Budget
        </Link>
        <Link
          href={`/projects/${id}/budget-vs-actual`}
          className={buttonVariants({ variant: "outline", size: "sm" })}
        >
          Budget vs Actual
        </Link>
        <Link
          href={`/projects/${id}/pnl`}
          className={buttonVariants({ variant: "outline", size: "sm" })}
        >
          Profit/Loss
        </Link>
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
