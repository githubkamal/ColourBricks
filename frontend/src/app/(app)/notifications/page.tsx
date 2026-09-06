import { Suspense } from "react";
import { NotificationsPage } from "@/features/notifications/notifications-page";

export default function Page() {
  return (
    <Suspense fallback={null}>
      <NotificationsPage />
    </Suspense>
  );
}
