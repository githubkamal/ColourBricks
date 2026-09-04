import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { ProjectIncomePage } from "./project-income-page";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("ProjectIncomePage", () => {
  it("ProjectIncome_ShowsReceiptsAndTotalForTheSelectedProject", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects`, () =>
        HttpResponse.json({
          items: [{ id: 5, code: "CB-2026-001", name: "Riverside" }],
          page: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
      http.get(`${apiBaseUrl}/payment-modes`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/projects/5/receipts`, () =>
        HttpResponse.json([
          {
            id: 1,
            projectId: 5,
            type: "ClientAdvance",
            date: "2026-05-01",
            amount: 1000000,
            paymentModeId: 1,
            paymentModeName: "Cash",
            accountId: null,
            accountName: null,
            referenceNo: null,
            description: null,
            status: "Active",
            concurrencyStamp: "a",
          },
        ]),
      ),
      http.get(`${apiBaseUrl}/projects/5/income-total`, () =>
        HttpResponse.json({ projectId: 5, total: 1000000 }),
      ),
    );

    renderWithClient(<ProjectIncomePage />);

    await screen.findByRole("option", { name: "CB-2026-001 — Riverside" });
    fireEvent.change(screen.getByLabelText("Project"), { target: { value: "5" } });

    await waitFor(() =>
      expect(screen.getByTestId("income-total")).toHaveTextContent("₹10,00,000.00"),
    );
    expect(screen.getByRole("cell", { name: "Client Advance" })).toBeInTheDocument();
  });
});
