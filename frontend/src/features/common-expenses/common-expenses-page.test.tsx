import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { CommonExpensesPage } from "./common-expenses-page";

function rc(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("CommonExpensesPage", () => {
  it("CommonExpenses_ShowsSubcategoriesAndSavingsSeparate", async () => {
    server.use(
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/payment-modes`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/common-expenses`, () =>
        HttpResponse.json([
          { id: 1, type: "Office", subCategory: "Office rent", date: "2026-08-05", amount: 35000 },
        ]),
      ),
      http.get(`${apiBaseUrl}/common-expenses/summary`, () =>
        HttpResponse.json({ personal: 40000, office: 35000, savings: 25000, total: 75000 }),
      ),
    );
    rc(<CommonExpensesPage type="Office" />);

    expect(await screen.findByTestId("ce-summary")).toHaveTextContent("not counted as an expense");
    // Office sub-categories are offered, not Personal ones.
    const select = screen.getByLabelText("Sub-category");
    expect(select).toHaveTextContent("Office rent");
    expect(select).not.toHaveTextContent("Personal withdrawals");
    expect(screen.getByRole("cell", { name: "₹35,000.000" })).toBeInTheDocument();
  });
});
