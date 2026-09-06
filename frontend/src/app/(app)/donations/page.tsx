import { Suspense } from "react";
import { DonationsPage } from "@/features/donations/donations-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <DonationsPage />
    </Suspense>
  );
}
