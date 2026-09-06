import { notFound } from "next/navigation";
import { Suspense } from "react";
import { ProjectPnlPage } from "@/features/reporting/project-pnl-page";

export default async function Page({ params }: PageProps<"/projects/[id]/pnl">) {
  const { id } = await params;
  const projectId = Number(id);
  if (!Number.isInteger(projectId) || projectId < 1) notFound();
  return (
    <Suspense fallback={null}>
      <ProjectPnlPage projectId={projectId} />
    </Suspense>
  );
}
