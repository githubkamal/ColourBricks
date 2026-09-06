import { Suspense } from "react";
import { LoanPaymentsPage } from "@/features/loans/loan-payments-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <LoanPaymentsPage />
    </Suspense>
  );
}
