import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { CurrentUserProvider } from "@/features/shell/user-context";
import { server } from "@/test/msw/server";
import { resetNavigationMock } from "@/test/next-navigation-mock";
import { BudgetVsActualPage } from "./budget-vs-actual-page";
import { CompanyDashboardPage } from "./company-dashboard-page";
import { ProjectDashboardPage } from "./project-dashboard-page";
import { ProjectLedgerPage } from "./project-ledger-page";
import { ProjectPnlPage } from "./project-pnl-page";

vi.mock("next/navigation", () => import("@/test/next-navigation-mock"));

const mockUser = {
  id: 1,
  name: "Test Admin",
  email: "admin@colourbricks.local",
  mobile: null,
  roleId: 1,
  departmentId: null,
  permissions: ["*"],
};

function rc(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <CurrentUserProvider value={mockUser}>{ui}</CurrentUserProvider>
    </QueryClientProvider>,
  );
}

describe("reporting pages", () => {
  beforeEach(() => resetNavigationMock());

  it("ProjectDashboard_BreakdownSumsToTotal_And12Tiles", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects/4/dashboard`, () =>
        HttpResponse.json({
          projectId: 4,
          projectName: "Site A",
          summary: Array.from({ length: 12 }, (_, i) => ({
            key: `k${i}`,
            label: `Tile ${i}`,
            value: 1000,
            drillUrl: null,
          })),
          expenseBreakdown: [
            { bucket: "Materials", amount: 1200000 },
            { bucket: "Electrical", amount: 300000 },
          ],
          totalExpenses: 1500000,
          monthlyFlow: [],
        }),
      ),
    );
    rc(<ProjectDashboardPage projectId={4} />);
    expect((await screen.findByTestId("summary-tiles")).children).toHaveLength(12);
    expect(screen.getByTestId("breakdown-total")).toHaveTextContent("₹15,00,000.00");
  });

  it("CompanyDashboard_RendersTilesAndProfitTable", async () => {
    server.use(
      http.get(`${apiBaseUrl}/dashboard`, () =>
        HttpResponse.json({
          tiles: [
            { key: "income", label: "Overall Income", value: 500000, drillUrl: null },
            { key: "ongoingProjects", label: "Ongoing Projects", value: 3, drillUrl: null },
          ],
          projectProfitability: [
            { projectId: 1, projectName: "A", revenue: 300000, actualCost: 100000, profit: 200000 },
          ],
          monthlyFlow: [],
        }),
      ),
    );
    rc(<CompanyDashboardPage />);
    expect(await screen.findByText("Overall Income")).toBeInTheDocument();
    expect(screen.getByText("Ongoing Projects").parentElement?.nextSibling).toHaveTextContent("3");
  });

  it("BudgetVsActual_MarksOverAndOverrun", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects/4/budget-vs-actual`, () =>
        HttpResponse.json({
          projectId: 4,
          budgetRevisionNumber: 1,
          estimatedCost: 8000000,
          actualCost: 8200000,
          approachingThresholdPercent: 90,
          overrunMessage: "Budget Exceeded by 200000.00",
          rows: [
            {
              categoryId: 2,
              categoryName: "Materials",
              bucket: "Materials",
              budget: 3500000,
              actual: 3800000,
              variance: -300000,
              variancePercent: 8.6,
              status: "Exceeded",
            },
          ],
        }),
      ),
    );
    rc(<BudgetVsActualPage projectId={4} />);
    expect(await screen.findByTestId("overrun-message")).toHaveTextContent("Budget Exceeded by");
    expect(screen.getByRole("cell", { name: "Over" })).toBeInTheDocument();
  });

  it("ProjectPnl_BasisToggle_RefetchesAndRelabels", async () => {
    const body = (basis: string) => ({
      projectId: 4,
      projectName: "Site A",
      revenueBasis: basis,
      revenue: basis === "Contract" ? 10000000 : 4000000,
      estimatedCost: 8000000,
      actualCost: 6000000,
      grossProfit: basis === "Contract" ? 4000000 : -2000000,
      profitPercent: basis === "Contract" ? 40 : -50,
      budgetVariance: -2000000,
    });
    server.use(
      http.get(`${apiBaseUrl}/projects/4/pnl`, ({ request }) => {
        const basis = new URL(request.url).searchParams.get("revenueBasis") ?? "Contract";
        return HttpResponse.json(body(basis));
      }),
    );
    rc(<ProjectPnlPage projectId={4} />);
    expect(await screen.findByTestId("basis-label")).toHaveTextContent("contract value");
    fireEvent.change(screen.getByLabelText("Revenue basis"), { target: { value: "Receipts" } });
    expect(await screen.findByTestId("basis-label")).toHaveTextContent("receipts to date");
  });

  it("ProjectLedger_ShowsClosingBalance", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects/4/financial-ledger`, () =>
        HttpResponse.json({
          projectId: 4,
          openingBalance: 0,
          closingBalance: 1310000,
          lines: [
            {
              entryId: 1,
              date: "2026-08-01",
              description: "Client receipt — Project Income",
              credit: 1000000,
              debit: 0,
              runningBalance: 1000000,
              sourceType: "ProjectReceipt",
              sourceId: 1,
              isReversal: false,
              categoryId: 15,
              categoryName: "Project Income",
              partyId: null,
              partyName: null,
            },
          ],
        }),
      ),
    );
    rc(<ProjectLedgerPage projectId={4} />);
    expect(await screen.findByTestId("closing-balance")).toHaveTextContent("₹13,10,000.00");
  });
});
