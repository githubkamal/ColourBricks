import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { ProjectBudgetPage } from "./project-budget-page";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const categories = [
  { id: 1, name: "Labour", slug: "labour", bucket: "Labour", isCost: true, isActive: true },
  {
    id: 2,
    name: "Materials",
    slug: "materials",
    bucket: "Materials",
    isCost: true,
    isActive: true,
  },
  {
    id: 9,
    name: "Vendor Payable",
    slug: "vendor_payable",
    bucket: "Liability",
    isCost: false,
    isActive: true,
  },
];

const budget = {
  projectId: 4,
  revisionNumber: 2,
  approachingThresholdPercent: 90,
  note: null,
  revisedAtUtc: "2026-05-01T00:00:00Z",
  revisedByUserId: 1,
  estimatedCost: 8000000,
  budgetTotal: 5500000,
  varianceFromEstimate: -2500000,
  lines: [
    { categoryId: 1, categoryName: "Labour", bucket: "Labour", amount: 2000000 },
    { categoryId: 2, categoryName: "Materials", bucket: "Materials", amount: 3500000 },
  ],
};

describe("ProjectBudgetPage", () => {
  it("BudgetEditor_ShowsRevisionAndVariance_AndOnlyCostCategories", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects/4/budget`, () => HttpResponse.json(budget)),
      http.get(`${apiBaseUrl}/projects/4/budget/revisions`, () =>
        HttpResponse.json([
          {
            revisionNumber: 1,
            budgetTotal: 5000000,
            note: null,
            revisedAtUtc: "x",
            revisedByUserId: 1,
          },
          {
            revisionNumber: 2,
            budgetTotal: 5500000,
            note: null,
            revisedAtUtc: "x",
            revisedByUserId: 1,
          },
        ]),
      ),
      http.get(`${apiBaseUrl}/expense-categories`, () => HttpResponse.json(categories)),
    );
    renderWithClient(<ProjectBudgetPage projectId={4} />);

    expect(await screen.findByTestId("budget-heading")).toHaveTextContent("Current revision 2");
    // Non-cost categories are not offered as budget lines.
    expect(screen.getByLabelText("Budget Labour")).toBeInTheDocument();
    expect(screen.queryByLabelText("Budget Vendor Payable")).not.toBeInTheDocument();

    // Editing a line updates the total and the (displayed, not blocking) variance.
    fireEvent.change(screen.getByLabelText("Budget Materials"), { target: { value: "4500000" } });
    expect(screen.getByTestId("budget-total")).toHaveTextContent("₹65,00,000.00");
    expect(screen.getByTestId("budget-variance")).toHaveTextContent("not blocked");

    expect(screen.getByRole("button", { name: "Save as revision 3" })).toBeInTheDocument();
  });
});
