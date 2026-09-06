import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { resetNavigationMock } from "@/test/next-navigation-mock";
import { CommonExpenseReportPage } from "./common-expense-report-page";

vi.mock("next/navigation", () => import("@/test/next-navigation-mock"));

function rc(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("CommonExpenseReportPage", () => {
  beforeEach(() => resetNavigationMock());

  it("CommonExpenseReport_ShowsPerProjectAndTotalAndSubCategorySummary", async () => {
    server.use(
      http.get(`${apiBaseUrl}/reports/common-expense-allocations`, () =>
        HttpResponse.json([
          {
            runId: 1,
            periodFrom: "2026-08-01",
            periodTo: "2026-08-31",
            types: "Personal,Office",
            method: "Equal",
            poolAmount: 100000,
            projectId: 1,
            projectName: "Alpha",
            allocated: 50000,
            status: "Active",
          },
          {
            runId: 1,
            periodFrom: "2026-08-01",
            periodTo: "2026-08-31",
            types: "Personal,Office",
            method: "Equal",
            poolAmount: 100000,
            projectId: 2,
            projectName: "Beta",
            allocated: 50000,
            status: "Active",
          },
        ]),
      ),
      http.get(`${apiBaseUrl}/common-expenses`, () =>
        HttpResponse.json([
          {
            id: 1,
            type: "Personal",
            subCategory: "Personal purchases",
            date: "2026-08-05",
            amount: 30000,
          },
          { id: 2, type: "Office", subCategory: "Office rent", date: "2026-08-06", amount: 70000 },
        ]),
      ),
    );
    rc(<CommonExpenseReportPage />);

    expect(await screen.findByRole("cell", { name: "Alpha" })).toBeInTheDocument();
    expect(screen.getByTestId("report-total")).toHaveTextContent("₹1,00,000.00");
    expect(screen.getByRole("cell", { name: "Personal purchases" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Office rent" })).toBeInTheDocument();
  });
});
