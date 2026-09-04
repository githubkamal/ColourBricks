import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { PurchaseOrderListPage } from "./purchase-order-list-page";

vi.mock("@/features/parties/party-picker", () => ({
  PartyPicker: () => <div>party picker</div>,
}));

function rc(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("PurchaseOrderListPage", () => {
  it("PurchaseOrderList_ShowsNumberVendorStatusAndProjectCount", async () => {
    server.use(
      http.get(`${apiBaseUrl}/purchase-orders`, () =>
        HttpResponse.json([
          {
            id: 1,
            poNumber: "PO-2026-00001",
            vendorId: 8,
            vendorName: "ABC Hardware",
            orderDate: "2026-08-01",
            status: "Draft",
            invoiceNumber: null,
            submittedDate: null,
            notes: null,
            total: 0,
            lines: [
              {
                id: 1,
                projectId: 5,
                projectName: "Site A",
                itemId: null,
                itemName: "Cement",
                quantity: 10,
                unit: "Bag",
                rate: null,
                taxAmount: null,
                lineTotal: null,
              },
              {
                id: 2,
                projectId: 6,
                projectName: "Site B",
                itemId: null,
                itemName: "Sand",
                quantity: 5,
                unit: "Ton",
                rate: null,
                taxAmount: null,
                lineTotal: null,
              },
            ],
            obligationIds: [],
            concurrencyStamp: "s1",
          },
        ]),
      ),
    );

    rc(<PurchaseOrderListPage />);

    expect(await screen.findByRole("cell", { name: "PO-2026-00001" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "ABC Hardware" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Draft" })).toBeInTheDocument();
    // Two distinct projects across the order's lines.
    expect(screen.getByRole("cell", { name: "2" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "New purchase order" })).toBeInTheDocument();
  });
});
