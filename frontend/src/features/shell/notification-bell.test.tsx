import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { NotificationBell } from "./notification-bell";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const notifications = [
  {
    id: 1,
    trigger: "vendor_payment_overdue",
    title: "Vendor payment overdue",
    body: "Party #7 has an overdue payment.",
    severity: "Warning",
    projectId: null,
    entityType: "Party",
    entityId: 7,
    createdAtUtc: "2026-09-05T08:00:00Z",
    read: false,
  },
];

describe("NotificationBell", () => {
  beforeEach(() => localStorage.clear());

  it("NotificationBell_ShowsUnreadBadge_AndOpensFeedOnClick", async () => {
    server.use(http.get(`${apiBaseUrl}/notifications`, () => HttpResponse.json(notifications)));

    renderWithClient(<NotificationBell />);

    const button = await screen.findByRole("button", { name: "Notifications (1 unread)" });
    expect(button).toBeInTheDocument();

    fireEvent.click(button);
    expect(await screen.findByText("Vendor payment overdue")).toBeInTheDocument();
  });

  it("NotificationBell_NoUnread_ShowsNoBadge", async () => {
    server.use(
      http.get(`${apiBaseUrl}/notifications`, () =>
        HttpResponse.json([{ ...notifications[0], read: true }]),
      ),
    );

    renderWithClient(<NotificationBell />);

    expect(await screen.findByRole("button", { name: "Notifications" })).toBeInTheDocument();
  });
});
