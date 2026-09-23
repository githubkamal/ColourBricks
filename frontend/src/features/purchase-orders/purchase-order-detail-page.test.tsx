import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { PurchaseOrderDetailPage } from "./purchase-order-detail-page";

function rc(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const draftPo = {
  id: 1,
  poNumber: "PO-2026-00001",
  vendorId: 8,
  vendorName: "ABC Hardware",
  orderDate: "2026-08-01",
  status: "Draft",
  invoiceNumber: null,
  submittedDate: null,
  notes: null,
  subtotalTotal: 0,
  taxTotal: 0,
  total: 0,
  lines: [
    {
      id: 11,
      projectId: 5,
      projectName: "Site A",
      itemId: null,
      itemName: "Cement",
      quantity: 10,
      unit: "Bag",
      rate: null,
      subtotal: null,
      taxType: "Amount",
      taxRate: null,
      taxAmount: null,
      lineTotal: null,
    },
  ],
  charges: [],
  chargesSubtotal: 0,
  chargesTax: 0,
  roundOff: 0,
  obligationIds: [],
  concurrencyStamp: "s1",
};

const submittedPo = {
  ...draftPo,
  status: "Submitted",
  invoiceNumber: "INV-100",
  submittedDate: "2026-08-05",
  subtotalTotal: 4000,
  taxTotal: 0,
  lines: [{ ...draftPo.lines[0], rate: 400, subtotal: 4000, taxAmount: 0, lineTotal: 4000 }],
  charges: [
    {
      id: 71,
      chargeType: "Transport",
      amount: 500,
      taxType: "Amount",
      taxRate: null,
      taxAmount: 90,
      total: 590,
    },
  ],
  chargesSubtotal: 500,
  chargesTax: 90,
  roundOff: -0.4,
  total: 4589.6,
  obligationIds: [501],
};

describe("PurchaseOrderDetailPage", () => {
  it("Draft_ShowsEditableLinesAndRevealsSubmitForm", async () => {
    server.use(
      http.get(`${apiBaseUrl}/purchase-orders/1`, () => HttpResponse.json(draftPo)),
      http.get(`${apiBaseUrl}/projects`, () =>
        HttpResponse.json({
          items: [{ id: 5, code: "CB-A", name: "Site A" }],
          page: 1,
          pageSize: 200,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    );

    rc(<PurchaseOrderDetailPage id={1} />);

    expect(await screen.findByTestId("po-status")).toHaveTextContent("Draft");
    expect(screen.getByLabelText("itemName 1")).toHaveValue("Cement");
    expect(screen.getByLabelText("quantity 1")).toHaveValue("10");
    expect(screen.getByRole("button", { name: "Cancel order" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Vendor invoice received — submit" }));

    expect(await screen.findByLabelText("Vendor invoice number")).toBeInTheDocument();
    expect(screen.getByLabelText("rate 1")).toBeInTheDocument();
    expect(screen.getByLabelText("taxValue 1")).toBeInTheDocument();
  });

  it("Submitted_IsReadOnlyAndDrillsIntoEachProject", async () => {
    server.use(http.get(`${apiBaseUrl}/purchase-orders/1`, () => HttpResponse.json(submittedPo)));

    rc(<PurchaseOrderDetailPage id={1} />);

    expect(await screen.findByTestId("po-status")).toHaveTextContent("Submitted");
    expect(screen.getByText("INV-100", { exact: false })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Site A" })).toHaveAttribute("href", "/projects/5");
    expect(screen.queryByRole("button", { name: "Cancel order" })).not.toBeInTheDocument();
    expect(screen.getByText(/Transport/)).toBeInTheDocument();
    expect(screen.getByText(/Round off/)).toBeInTheDocument();
  });

  it("Submit_AddsAnOtherChargeAndRoundOffToTheTotal", async () => {
    server.use(
      http.get(`${apiBaseUrl}/purchase-orders/1`, () => HttpResponse.json(draftPo)),
      http.get(`${apiBaseUrl}/projects`, () =>
        HttpResponse.json({
          items: [{ id: 5, code: "CB-A", name: "Site A" }],
          page: 1,
          pageSize: 200,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    );

    rc(<PurchaseOrderDetailPage id={1} />);

    fireEvent.click(
      await screen.findByRole("button", { name: "Vendor invoice received — submit" }),
    );
    fireEvent.change(await screen.findByLabelText("rate 1"), { target: { value: "400" } });

    // 10 bags x 400 = 4,000, no line GST.
    expect(screen.getByTestId("submit-total")).toHaveTextContent("4,000");

    fireEvent.click(screen.getByRole("button", { name: "+ Add charge" }));
    fireEvent.change(screen.getByLabelText("chargeAmount 1"), { target: { value: "500" } });
    fireEvent.change(screen.getByLabelText("chargeTaxType 1"), { target: { value: "Percentage" } });
    fireEvent.change(screen.getByLabelText("chargeTaxValue 1"), { target: { value: "18" } });

    // + ₹500 transport + 18% GST on it = ₹590.
    expect(screen.getByTestId("submit-charges-total")).toHaveTextContent("590");
    expect(screen.getByTestId("submit-total")).toHaveTextContent("4,590");

    fireEvent.change(screen.getByLabelText("Round off"), { target: { value: "-0.4" } });
    expect(screen.getByTestId("submit-total")).toHaveTextContent("4,589.600");
  });

  it("Submit_RemovingAnOtherChargeTakesItBackOffTheTotal", async () => {
    server.use(
      http.get(`${apiBaseUrl}/purchase-orders/1`, () => HttpResponse.json(draftPo)),
      http.get(`${apiBaseUrl}/projects`, () =>
        HttpResponse.json({
          items: [{ id: 5, code: "CB-A", name: "Site A" }],
          page: 1,
          pageSize: 200,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
    );

    rc(<PurchaseOrderDetailPage id={1} />);

    fireEvent.click(
      await screen.findByRole("button", { name: "Vendor invoice received — submit" }),
    );
    fireEvent.change(await screen.findByLabelText("rate 1"), { target: { value: "400" } });
    fireEvent.click(screen.getByRole("button", { name: "+ Add charge" }));
    fireEvent.change(screen.getByLabelText("chargeAmount 1"), { target: { value: "500" } });
    expect(screen.getByTestId("submit-total")).toHaveTextContent("4,500");

    fireEvent.click(screen.getByRole("button", { name: "Remove charge 1" }));
    expect(screen.getByTestId("submit-total")).toHaveTextContent("4,000");
    expect(screen.queryByTestId("submit-charges-total")).not.toBeInTheDocument();
  });
});
