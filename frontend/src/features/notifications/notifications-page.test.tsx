import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { NotificationsPage } from "./notifications-page";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const notifications = [
  {
    id: 1,
    trigger: "budget_exceeded",
    title: "Budget exceeded",
    body: "Project #4 has exceeded its budget.",
    severity: "Critical",
    projectId: 4,
    entityType: "Project",
    entityId: 4,
    createdAtUtc: "2026-09-05T08:00:00Z",
    read: false,
  },
  {
    id: 2,
    trigger: "emi_due",
    title: "EMI due soon",
    body: "Loan #2 instalment 3 is due 2026-09-10.",
    severity: "Warning",
    projectId: null,
    entityType: "LoanEmiInstalment",
    entityId: 9,
    createdAtUtc: "2026-09-04T08:00:00Z",
    read: true,
  },
];

describe("NotificationsPage", () => {
  beforeEach(() => localStorage.clear());

  it("NotificationsPage_ListsNotificationsAndMarksOneRead", async () => {
    let readCalled = false;
    server.use(
      http.get(`${apiBaseUrl}/notifications`, () => HttpResponse.json(notifications)),
      http.post(`${apiBaseUrl}/notifications/1/read`, () => {
        readCalled = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderWithClient(<NotificationsPage />);

    expect(await screen.findByText("Budget exceeded")).toBeInTheDocument();
    expect(screen.getByText("EMI due soon")).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Mark read" }));
    await waitFor(() => expect(readCalled).toBe(true));
  });

  it("NotificationsPage_MutingAType_RemembersItLocallyAndOffersUnmute", async () => {
    let mutedTrigger = "";
    server.use(
      http.get(`${apiBaseUrl}/notifications`, () => HttpResponse.json(notifications)),
      http.post(`${apiBaseUrl}/notifications/mute`, async ({ request }) => {
        const body = (await request.json()) as { trigger: string };
        mutedTrigger = body.trigger;
        return new HttpResponse(null, { status: 204 });
      }),
    );

    renderWithClient(<NotificationsPage />);
    await screen.findByText("Budget exceeded");

    fireEvent.click(screen.getAllByRole("button", { name: "Mute this type" })[0]);
    await waitFor(() => expect(mutedTrigger).toBe("budget_exceeded"));
    expect(await screen.findByText("budget_exceeded")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Unmute" })).toBeInTheDocument();
  });

  it("NotificationsPage_UnreadOnlyToggle_RefetchesWithTheFlag", async () => {
    let lastUrl = "";
    server.use(
      http.get(`${apiBaseUrl}/notifications`, ({ request }) => {
        lastUrl = new URL(request.url).search;
        return HttpResponse.json(notifications);
      }),
    );

    renderWithClient(<NotificationsPage />);
    await screen.findByText("Budget exceeded");

    fireEvent.click(screen.getByLabelText("Unread only"));
    await waitFor(() => expect(lastUrl).toContain("unreadOnly=true"));
  });
});
