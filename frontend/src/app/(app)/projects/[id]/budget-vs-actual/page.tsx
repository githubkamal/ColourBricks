import { notFound } from "next/navigation";
import { BudgetVsActualPage } from "@/features/reporting/budget-vs-actual-page";

export default async function Page({ params }: PageProps<"/projects/[id]/budget-vs-actual">) {
  const { id } = await params;
  const projectId = Number(id);
  if (!Number.isInteger(projectId) || projectId < 1) notFound();
  return <BudgetVsActualPage projectId={projectId} />;
}
