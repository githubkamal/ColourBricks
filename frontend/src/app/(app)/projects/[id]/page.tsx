import { notFound } from "next/navigation";
import { ProjectDetail } from "@/features/projects/project-detail";

export default async function ProjectDetailPage({ params }: PageProps<"/projects/[id]">) {
  const { id } = await params;
  const projectId = Number(id);
  if (!Number.isInteger(projectId) || projectId < 1) notFound();

  return <ProjectDetail id={projectId} />;
}
