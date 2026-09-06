"use client";

import { useQuery } from "@tanstack/react-query";
import { listProjects } from "@/features/projects/api";
import { useQueryParam } from "@/lib/use-query-param";
import { ProjectDashboardPage } from "./project-dashboard-page";

export function ProjectDashboardPicker() {
  const [projectIdParam, setProjectIdParam] = useQueryParam("projectId", "");
  const projectId = projectIdParam ? Number(projectIdParam) : "";
  const { data } = useQuery({
    queryKey: ["projects", "dashboard-picker"],
    queryFn: () => listProjects({ pageSize: 200 }),
  });

  return (
    <div className="space-y-4">
      <label className="flex items-center gap-2 text-sm">
        <span className="font-medium">Project</span>
        <select
          className="bg-card rounded border px-3 py-1.5 text-sm"
          aria-label="Project"
          value={projectId}
          onChange={(e) => setProjectIdParam(e.target.value)}
        >
          <option value="">Select a project…</option>
          {(data?.items ?? []).map((p) => (
            <option key={p.id} value={p.id}>
              {p.name}
            </option>
          ))}
        </select>
      </label>
      {projectId !== "" && <ProjectDashboardPage projectId={projectId} />}
    </div>
  );
}
