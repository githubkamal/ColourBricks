import { Suspense } from "react";
import { ReportExplorerPage } from "@/features/reports/report-explorer-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <ReportExplorerPage />
    </Suspense>
  );
}
