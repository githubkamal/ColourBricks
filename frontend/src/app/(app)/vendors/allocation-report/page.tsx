import { Suspense } from "react";
import { VendorPaymentAllocationReportPage } from "@/features/allocation/vendor-payment-allocation-report-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <VendorPaymentAllocationReportPage />
    </Suspense>
  );
}
