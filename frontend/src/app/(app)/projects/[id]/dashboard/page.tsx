import { notFound } from "next/navigation";
import { ProjectDashboardPage } from "@/features/reporting/project-dashboard-page";

export default async function Page({ params }: PageProps<"/projects/[id]/dashboard">) {
  const { id } = await params;
  const projectId = Number(id);
  if (!Number.isInteger(projectId) || projectId < 1) notFound();
  return <ProjectDashboardPage projectId={projectId} />;
}
