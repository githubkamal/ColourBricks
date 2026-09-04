import { notFound } from "next/navigation";
import { ProjectLedgerPage } from "@/features/reporting/project-ledger-page";

export default async function Page({ params }: PageProps<"/projects/[id]/financial-ledger">) {
  const { id } = await params;
  const projectId = Number(id);
  if (!Number.isInteger(projectId) || projectId < 1) notFound();
  return <ProjectLedgerPage projectId={projectId} />;
}
