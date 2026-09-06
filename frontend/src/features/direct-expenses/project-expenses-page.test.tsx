import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { resetNavigationMock } from "@/test/next-navigation-mock";
import { ProjectExpensesPage } from "./project-expenses-page";

vi.mock("next/navigation", () => import("@/test/next-navigation-mock"));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("ProjectExpensesPage", () => {
  beforeEach(() => resetNavigationMock());

  it("ProjectExpenses_ListsExpensesWithBucketAndPaidState", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects`, () =>
        HttpResponse.json({
          items: [{ id: 7, code: "CB-2026-007", name: "Proj" }],
          page: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
      http.get(`${apiBaseUrl}/expense-categories`, () =>
        HttpResponse.json([
          {
            id: 5,
            name: "Electrical",
            slug: "electrical",
            bucket: "Electrical",
            isCost: true,
            isActive: true,
          },
        ]),
      ),
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/payment-modes`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/projects/7/expenses`, () =>
        HttpResponse.json([
          {
            id: 1,
            projectId: 7,
            categoryId: 5,
            categoryName: "Electrical",
            bucket: "Electrical",
            partyId: null,
            date: "2026-05-01",
            amount: 12500,
            paidImmediately: false,
            description: "wiring",
            status: "Active",
          },
        ]),
      ),
    );

    renderWithClient(<ProjectExpensesPage />);
    await screen.findByRole("option", { name: "CB-2026-007 — Proj" });
    fireEvent.change(screen.getByLabelText("Project"), { target: { value: "7" } });

    // Category cell + Bucket cell both read "Electrical".
    expect(await screen.findAllByRole("cell", { name: "Electrical" })).toHaveLength(2);
    expect(screen.getByRole("cell", { name: "₹12,500.000" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Payable" })).toBeInTheDocument();
  });
});
