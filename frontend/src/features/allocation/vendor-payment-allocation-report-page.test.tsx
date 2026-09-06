import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { resetNavigationMock } from "@/test/next-navigation-mock";
import { VendorPaymentAllocationReportPage } from "./vendor-payment-allocation-report-page";

vi.mock("@/features/parties/party-picker", () => ({
  PartyPicker: () => <div>party picker</div>,
}));

vi.mock("next/navigation", () => import("@/test/next-navigation-mock"));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const reportRows = [
  {
    date: "2026-08-26",
    vendorId: 8,
    vendorName: "ABC Hardware",
    settlementId: 5,
    totalPayment: 100000,
    projectId: 1,
    projectName: "Project A",
    allocated: 25000,
    method: "Fifo",
    status: "Active",
  },
  {
    date: "2026-08-26",
    vendorId: 8,
    vendorName: "ABC Hardware",
    settlementId: 5,
    totalPayment: 100000,
    projectId: 4,
    projectName: "Project D",
    allocated: 75000,
    method: "Fifo",
    status: "Active",
  },
];

describe("VendorPaymentAllocationReportPage", () => {
  beforeEach(() => resetNavigationMock());

  it("AllocationReport_ShowsRowsAndDrillsIntoOneSettlement", async () => {
    server.use(
      http.get(`${apiBaseUrl}/reports/vendor-payment-allocations`, () =>
        HttpResponse.json(reportRows),
      ),
      http.get(`${apiBaseUrl}/settlements/5/allocations`, () =>
        HttpResponse.json({
          settlementId: 5,
          date: "2026-08-26",
          vendorId: 8,
          vendorName: "ABC Hardware",
          totalPayment: 100000,
          status: "Active",
          lines: [
            {
              projectId: 1,
              projectName: "Project A",
              obligationId: 1,
              obligationReference: "INV-1",
              amount: 25000,
              method: "Fifo",
            },
            {
              projectId: 4,
              projectName: "Project D",
              obligationId: 4,
              obligationReference: null,
              amount: 75000,
              method: "Fifo",
            },
          ],
        }),
      ),
    );

    renderWithClient(<VendorPaymentAllocationReportPage />);

    // Both project slices of the one payment are listed (BRD §51 layout).
    expect(await screen.findByRole("cell", { name: "Project A" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Project D" })).toBeInTheDocument();
    // The header cells repeat only on the first row of the group.
    expect(screen.getAllByRole("cell", { name: "ABC Hardware" })).toHaveLength(1);

    // Clicking the payment total drills into the full allocation.
    fireEvent.click(screen.getByRole("button", { name: "₹1,00,000.000" }));
    const detail = await screen.findByTestId("allocation-detail");
    expect(detail).toHaveTextContent("Settlement #5");
    expect(detail).toHaveTextContent("INV-1");
    expect(detail).toHaveTextContent("₹75,000.000");
  });
});
