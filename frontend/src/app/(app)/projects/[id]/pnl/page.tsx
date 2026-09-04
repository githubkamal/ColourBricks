import { notFound } from "next/navigation";
import { ProjectPnlPage } from "@/features/reporting/project-pnl-page";

export default async function Page({ params }: PageProps<"/projects/[id]/pnl">) {
  const { id } = await params;
  const projectId = Number(id);
  if (!Number.isInteger(projectId) || projectId < 1) notFound();
  return <ProjectPnlPage projectId={projectId} />;
}
