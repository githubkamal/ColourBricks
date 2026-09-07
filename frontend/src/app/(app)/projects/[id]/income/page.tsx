import { notFound } from "next/navigation";
import { ProjectTabs } from "@/features/projects/project-tabs";
import { ProjectIncome } from "@/features/receipts/project-income-page";

export default async function Page({ params }: PageProps<"/projects/[id]/income">) {
  const { id } = await params;
  const projectId = Number(id);
  if (!Number.isInteger(projectId) || projectId < 1) notFound();
  return (
    <div className="max-w-5xl space-y-6">
      <ProjectTabs projectId={projectId} title="Project Income" />
      <ProjectIncome projectId={projectId} />
    </div>
  );
}
