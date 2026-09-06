import { Suspense } from "react";
import { TeamStatementPage } from "@/features/labour/team-statement-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <TeamStatementPage />
    </Suspense>
  );
}
