import { Suspense } from "react";
import { LabourWorkPage } from "@/features/labour/labour-work-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <LabourWorkPage />
    </Suspense>
  );
}
