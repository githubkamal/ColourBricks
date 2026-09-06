import { Suspense } from "react";
import { LoanReportsPage } from "@/features/loans/loan-reports-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <LoanReportsPage />
    </Suspense>
  );
}
