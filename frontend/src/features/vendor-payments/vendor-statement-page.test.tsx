import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { VendorStatementPage } from "./vendor-statement-page";

vi.mock("@/features/parties/party-picker", () => ({
  PartyPicker: ({ onSelect }: { onSelect: (p: unknown) => void }) => (
    <button type="button" onClick={() => onSelect({ id: 8, name: "ABC Hardware" })}>
      pick vendor
    </button>
  ),
}));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const noAdvanceSummary = {
  vendorId: 8,
  total: 75000,
  byProject: [{ projectId: 5, projectName: "Site A", outstanding: 75000 }],
  advance: 0,
};

describe("VendorStatementPage", () => {
  it("VendorStatement_ShowsRunningOutstandingPerRow", async () => {
    server.use(
      http.get(`${apiBaseUrl}/vendors/8/outstanding-summary`, () =>
        HttpResponse.json(noAdvanceSummary),
      ),
      http.get(`${apiBaseUrl}/vendors/8/statement`, () =>
        HttpResponse.json([
          {
            date: "2026-05-01",
            kind: "Purchase",
            reference: "INV-1",
            purchaseAmount: 200000,
            paid: 0,
            runningOutstanding: 200000,
          },
          {
            date: "2026-05-10",
            kind: "Payment",
            reference: "Payment",
            purchaseAmount: 0,
            paid: 50000,
            runningOutstanding: 150000,
          },
          {
            date: "2026-05-20",
            kind: "Payment",
            reference: "Payment",
            purchaseAmount: 0,
            paid: 75000,
            runningOutstanding: 75000,
          },
        ]),
      ),
    );

    renderWithClient(<VendorStatementPage />);
    screen.getByText("pick vendor").click();

    // The total outstanding and its per-project breakdown are shown up top.
    expect(await screen.findByTestId("vendor-total-outstanding")).toHaveTextContent("₹75,000.000");
    expect(screen.getByRole("cell", { name: "Site A" })).toBeInTheDocument();

    // ₹1,50,000 is the running outstanding after payment 1 (unique in the table).
    expect(await screen.findByRole("cell", { name: "₹1,50,000.000" })).toBeInTheDocument();
    // Row 3: a ₹75,000 payment leaves ₹75,000 running outstanding, plus the
    // by-project breakdown showing the same figure for Site A — three cells.
    expect(screen.getAllByRole("cell", { name: "₹75,000.000" })).toHaveLength(3);
    // Row 1: ₹2,00,000 purchase, ₹2,00,000 running outstanding.
    expect(screen.getAllByRole("cell", { name: "₹2,00,000.000" })).toHaveLength(2);
  });

  it("VendorStatement_ShowsAdvanceBalanceAndCanApplyIt", async () => {
    server.use(
      http.get(`${apiBaseUrl}/vendors/8/outstanding-summary`, () =>
        HttpResponse.json({ vendorId: 8, total: 0, byProject: [], advance: 20000 }),
      ),
      http.get(`${apiBaseUrl}/vendors/8/statement`, () =>
        HttpResponse.json([
          {
            date: "2026-05-01",
            kind: "Purchase",
            reference: "INV-1",
            purchaseAmount: 80000,
            paid: 0,
            runningOutstanding: 80000,
          },
          {
            date: "2026-05-10",
            kind: "Payment",
            reference: "Payment",
            purchaseAmount: 0,
            paid: 100000,
            runningOutstanding: -20000,
          },
          {
            date: "2026-05-10",
            kind: "Advance",
            reference: "Credit balance",
            purchaseAmount: 0,
            paid: 20000,
            runningOutstanding: -20000,
          },
        ]),
      ),
      http.get(`${apiBaseUrl}/vendor-purchases`, () =>
        HttpResponse.json([
          {
            id: 42,
            projectId: 5,
            vendorId: 8,
            vendorName: "ABC Hardware",
            date: "2026-06-01",
            invoiceNumber: null,
            total: 50000,
            partPaid: 0,
            vendorOutstandingAfter: 50000,
            status: "Active",
            lines: [],
          },
        ]),
      ),
    );

    renderWithClient(<VendorStatementPage />);
    screen.getByText("pick vendor").click();

    // The credit balance is surfaced above the table…
    const banner = await screen.findByTestId("advance-balance");
    expect(banner).toHaveTextContent("₹20,000.000");
    // …and the statement closes with an Advance row.
    expect(screen.getByRole("cell", { name: /Advance .* Credit balance/ })).toBeInTheDocument();
    // …with an affordance to apply it against an open purchase.
    expect(screen.getByLabelText("Apply to purchase")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Apply advance" })).toBeInTheDocument();
  });
});
