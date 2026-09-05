import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { ReportShell } from "./report-shell";

function rc(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const catalog = [
  {
    key: "ledger",
    title: "Ledger",
    columns: [
      {
        key: "date",
        header: "Date",
        numeric: false,
        defaultVisible: true,
        total: null,
        drillThrough: null,
      },
      {
        key: "category",
        header: "Category",
        numeric: false,
        defaultVisible: true,
        total: null,
        drillThrough: null,
      },
      {
        key: "debit",
        header: "Debit",
        numeric: true,
        defaultVisible: true,
        total: "debit",
        drillThrough: null,
      },
    ],
    supportedFilters: ["date", "project", "category", "search"],
    sortableColumnKeys: ["date", "debit"],
  },
];

const result = {
  key: "ledger",
  title: "Ledger",
  columns: catalog[0].columns,
  rows: [
    { date: "2026-05-05", category: "Project Income", debit: 100 },
    { date: "2026-05-06", category: "Materials", debit: 50 },
  ],
  totals: { debit: 5000 },
  rangeFrom: null,
  rangeTo: null,
  page: 1,
  pageSize: 50,
  totalCount: 2,
  totalPages: 1,
};

describe("ReportShell", () => {
  beforeEach(() => {
    // The Columns chooser persists hidden columns per report key across tests.
    localStorage.clear();
    // Every render fetches the System Settings company profile for the print
    // header/CSV export (P10-T17) — not under test here, so just quiet it.
    server.use(
      http.get(`${apiBaseUrl}/admin/settings`, () => HttpResponse.json(null, { status: 404 })),
    );
  });

  it("ReportShell_RendersDeclaredColumnsFiltersAndFullSetTotal", async () => {
    let lastUrl = "";
    server.use(
      http.get(`${apiBaseUrl}/reports/catalog`, () => HttpResponse.json(catalog)),
      http.get(`${apiBaseUrl}/reports/run/ledger`, ({ request }) => {
        lastUrl = new URL(request.url).search;
        return HttpResponse.json(result);
      }),
    );

    rc(<ReportShell reportKey="ledger" />);

    // Columns from the catalog definition.
    expect(await screen.findByRole("columnheader", { name: /Date/ })).toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /Debit/ })).toBeInTheDocument();

    // Only declared filters render.
    expect(screen.getByLabelText("Date preset")).toBeInTheDocument();
    expect(screen.getByLabelText("Project")).toBeInTheDocument();
    expect(screen.getByLabelText("Search")).toBeInTheDocument();
    expect(screen.queryByLabelText("Account")).not.toBeInTheDocument();

    // The footer total comes from result.totals (full set: 5000), not the visible rows (150).
    expect(await screen.findByText("₹5,000.000")).toBeInTheDocument();
    expect(screen.getByTestId("report-rowcount")).toHaveTextContent("2 rows");

    // Filter changes are pushed to the query string.
    fireEvent.change(screen.getByLabelText("Project"), { target: { value: "7" } });
    await waitFor(() => expect(lastUrl).toContain("projectId=7"));
  });

  it("ReportShell_ColumnChooserHidesAColumn", async () => {
    server.use(
      http.get(`${apiBaseUrl}/reports/catalog`, () => HttpResponse.json(catalog)),
      http.get(`${apiBaseUrl}/reports/run/ledger`, () => HttpResponse.json(result)),
    );

    rc(<ReportShell reportKey="ledger" />);
    await screen.findByRole("columnheader", { name: /Category/ });

    const chooser = screen.getByText("Columns");
    fireEvent.click(chooser);
    fireEvent.click(within(chooser.closest("details")!).getByLabelText("Category"));

    expect(screen.queryByRole("columnheader", { name: /Category/ })).not.toBeInTheDocument();
    expect(screen.getByRole("columnheader", { name: /Date/ })).toBeInTheDocument();
  });

  it("ReportShell_ClickingAColumnWithNoSortApplier_DoesNotChangeTheQuery", async () => {
    let lastUrl = "";
    server.use(
      http.get(`${apiBaseUrl}/reports/catalog`, () => HttpResponse.json(catalog)),
      http.get(`${apiBaseUrl}/reports/run/ledger`, ({ request }) => {
        lastUrl = new URL(request.url).search;
        return HttpResponse.json(result);
      }),
    );

    rc(<ReportShell reportKey="ledger" />);
    const categoryHeader = await screen.findByRole("columnheader", { name: /Category/ });
    await waitFor(() => expect(lastUrl).not.toBe(""));
    const beforeClick = lastUrl;

    // "category" has no sort applier on the backend — clicking it must not send a
    // sortBy that would silently do nothing.
    fireEvent.click(categoryHeader);
    await new Promise((resolve) => setTimeout(resolve, 0));
    expect(lastUrl).toBe(beforeClick);

    // A sortable column still works.
    fireEvent.click(screen.getByRole("columnheader", { name: /Date/ }));
    await waitFor(() => expect(lastUrl).toContain("sortBy=date"));
  });
});
