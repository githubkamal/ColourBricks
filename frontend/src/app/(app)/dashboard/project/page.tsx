import { Suspense } from "react";
import { ProjectDashboardPicker } from "@/features/reporting/project-dashboard-picker";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <ProjectDashboardPicker />
    </Suspense>
  );
}
