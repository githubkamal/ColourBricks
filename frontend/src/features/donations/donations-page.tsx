"use client";

import { useQuery } from "@tanstack/react-query";
import { getProject, listProjects } from "@/features/projects/api";
import { PageHeader } from "@/components/ui/page-header";
import { useQueryParamNumber } from "@/lib/use-query-param";
import { DonationOutstandingPanel } from "./donation-outstanding-panel";
import { ProjectDonationForm } from "./project-donation-form";

export function DonationsPage() {
  const [projectId, setProjectId] = useQueryParamNumber("projectId", 0);

  const { data: projects } = useQuery({
    queryKey: ["projects", { forDonations: true }],
    queryFn: () => listProjects({ pageSize: 100 }),
  });

  const { data: project } = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => getProject(projectId),
    enabled: projectId !== 0,
  });

  return (
    <div className="max-w-xl space-y-6">
      <PageHeader title="Project Donations" />

      <label className="block space-y-1">
        <span className="text-sm font-medium">Project</span>
        <select
          className="w-full rounded border bg-transparent px-3 py-1.5 text-sm"
          value={projectId || ""}
          aria-label="Project"
          onChange={(e) => setProjectId(e.target.value ? Number(e.target.value) : 0)}
        >
          <option value="">Select a project…</option>
          {projects?.items.map((p) => (
            <option key={p.id} value={p.id}>
              {p.code} — {p.name}
            </option>
          ))}
        </select>
      </label>

      {project && (
        <>
          <ProjectDonationForm
            projectId={project.id}
            projectContractValue={project.contractValue}
          />
          <DonationOutstandingPanel projectId={project.id} />
        </>
      )}
    </div>
  );
}
