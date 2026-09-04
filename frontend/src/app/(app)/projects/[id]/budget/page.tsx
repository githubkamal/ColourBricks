import { notFound } from "next/navigation";
import { ProjectBudgetPage } from "@/features/budgets/project-budget-page";

export default async function Page({ params }: PageProps<"/projects/[id]/budget">) {
  const { id } = await params;
  const projectId = Number(id);
  if (!Number.isInteger(projectId) || projectId < 1) notFound();
  return <ProjectBudgetPage projectId={projectId} />;
}
