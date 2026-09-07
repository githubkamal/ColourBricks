"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { getProject } from "@/features/projects/api";
import { ProjectPicker } from "@/features/projects/project-picker";
import type { ProjectListItem } from "@/features/projects/types";
import { useQueryParam } from "@/lib/use-query-param";
import { ProjectDashboardPage } from "./project-dashboard-page";

export function ProjectDashboardPicker() {
  const [projectIdParam, setProjectIdParam] = useQueryParam("projectId", "");
  const projectId = projectIdParam ? Number(projectIdParam) : 0;
  const [manualProject, setManualProject] = useState<ProjectListItem | null | undefined>(undefined);
  const { data: restoredProject } = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => getProject(projectId),
    enabled: projectId > 0 && manualProject === undefined,
  });
  const project = manualProject !== undefined ? manualProject : (restoredProject ?? null);

  function handleSelect(next: ProjectListItem | null) {
    setManualProject(next);
    setProjectIdParam(next ? String(next.id) : "");
  }

  return (
    <div className="space-y-4">
      <div className="max-w-xs">
        <ProjectPicker selected={project} onSelect={handleSelect} label="Project" />
      </div>
      {project && <ProjectDashboardPage projectId={project.id} />}
    </div>
  );
}
