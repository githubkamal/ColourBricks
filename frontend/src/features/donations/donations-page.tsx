"use client";

import { useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { getProject } from "@/features/projects/api";
import { ProjectPicker } from "@/features/projects/project-picker";
import type { ProjectListItem } from "@/features/projects/types";
import { PageHeader } from "@/components/ui/page-header";
import { useQueryParamNumber } from "@/lib/use-query-param";
import { DonationOutstandingPanel } from "./donation-outstanding-panel";
import { ProjectDonationForm } from "./project-donation-form";

export function DonationsPage() {
  const [projectId, setProjectId] = useQueryParamNumber("projectId", 0);
  const [manualProject, setManualProject] = useState<ProjectListItem | null | undefined>(
    undefined,
  );

  const { data: restoredProject } = useQuery({
    queryKey: ["project", projectId],
    queryFn: () => getProject(projectId),
    enabled: projectId !== 0,
  });
  const project = manualProject !== undefined ? manualProject : (restoredProject ?? null);

  function handleSelect(next: ProjectListItem | null) {
    setManualProject(next);
    setProjectId(next?.id ?? 0);
  }

  return (
    <div className="max-w-xl space-y-6">
      <PageHeader title="Project Donations" />

      <div className="bg-card max-w-xs rounded border p-4">
        <ProjectPicker selected={project} onSelect={handleSelect} label="Project" />
      </div>

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
