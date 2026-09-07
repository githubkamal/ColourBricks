import { notFound } from "next/navigation";
import { ExpenseForm } from "@/features/direct-expenses/project-expenses-page";
import { ProjectTabs } from "@/features/projects/project-tabs";

export default async function Page({ params }: PageProps<"/projects/[id]/expenses">) {
  const { id } = await params;
  const projectId = Number(id);
  if (!Number.isInteger(projectId) || projectId < 1) notFound();
  return (
    <div className="max-w-5xl space-y-6">
      <ProjectTabs projectId={projectId} title="Project Expenses" />
      <ExpenseForm projectId={projectId} />
    </div>
  );
}
