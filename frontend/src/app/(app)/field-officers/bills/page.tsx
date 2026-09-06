import { Suspense } from "react";
import { FieldOfficerExpensePage } from "@/features/field-officers/field-officer-expense-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <FieldOfficerExpensePage />
    </Suspense>
  );
}
