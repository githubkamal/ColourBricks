import { Suspense } from "react";
import { FieldOfficerProjectPaymentPage } from "@/features/field-officers/field-officer-project-payment-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <FieldOfficerProjectPaymentPage />
    </Suspense>
  );
}
