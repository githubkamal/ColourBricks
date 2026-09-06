import { Suspense } from "react";
import { ProjectExpensesPage } from "@/features/direct-expenses/project-expenses-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <ProjectExpensesPage />
    </Suspense>
  );
}
