"use client";

import { useQuery } from "@tanstack/react-query";
import { dataState } from "@/components/ui/data-state";
import { formatDate, formatINR } from "@/lib/format";
import { getProject } from "./api";
import { ProjectTabs } from "./project-tabs";

export function ProjectDetail({ id }: { id: number }) {
  const {
    data: project,
    isPending,
    isError,
  } = useQuery({
    queryKey: ["project", id],
    queryFn: () => getProject(id),
  });

  if (isPending || isError || !project) {
    return (
      <div className="max-w-5xl space-y-5">
        <ProjectTabs projectId={id} title="Overview" />
        {dataState({ isPending, isError: isError || !project, errorLabel: "Project not found." })}
      </div>
    );
  }

  return (
    <div className="max-w-5xl space-y-5">
      <ProjectTabs projectId={id} title="Overview" />

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
      <dd className={numeric ? "tabular-nums" : undefined}>{children}</dd>
    </div>
  );
}
