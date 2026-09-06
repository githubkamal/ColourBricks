import { Suspense } from "react";
import { LoanSchedulePage } from "@/features/loans/loan-schedule-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <LoanSchedulePage />
    </Suspense>
  );
}
