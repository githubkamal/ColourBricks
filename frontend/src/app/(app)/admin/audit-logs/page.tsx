import { Suspense } from "react";
import { AuditLogsPage } from "@/features/admin/audit-logs-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <AuditLogsPage />
    </Suspense>
  );
}
