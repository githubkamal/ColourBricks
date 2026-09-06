import { Suspense } from "react";
import { CommonExpenseReportPage } from "@/features/common-expenses/common-expense-report-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <CommonExpenseReportPage />
    </Suspense>
  );
}
