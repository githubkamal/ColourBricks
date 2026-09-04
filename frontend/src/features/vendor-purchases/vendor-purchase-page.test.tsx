import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { VendorPurchasePage } from "./vendor-purchase-page";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("VendorPurchasePage", () => {
  it("VendorPurchase_ComputesHeaderTotalFromLines", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects`, () =>
        HttpResponse.json({
          items: [{ id: 3, code: "CB-2026-016", name: "Section 16" }],
          page: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
      http.get(`${apiBaseUrl}/vendor-purchases`, () => HttpResponse.json([])),
    );

    renderWithClient(<VendorPurchasePage />);

    await screen.findByRole("option", { name: "CB-2026-016 — Section 16" });
    fireEvent.change(screen.getByLabelText("Project"), { target: { value: "3" } });

    // BRD §16: 100 bags cement @ ₹400 + 2 loads sand @ ₹15,000 = ₹70,000.
    fireEvent.change(await screen.findByLabelText("itemName 1"), { target: { value: "Cement" } });
    fireEvent.change(screen.getByLabelText("quantity 1"), { target: { value: "100" } });
    fireEvent.change(screen.getByLabelText("rate 1"), { target: { value: "400" } });

    fireEvent.click(screen.getByRole("button", { name: "+ Add line" }));
    fireEvent.change(screen.getByLabelText("itemName 2"), { target: { value: "Sand" } });
    fireEvent.change(screen.getByLabelText("quantity 2"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("rate 2"), { target: { value: "15000" } });

    expect(screen.getByTestId("purchase-total")).toHaveTextContent("₹70,000.00");
  });

  it("VendorPurchase_BoughtByFieldOfficer_SwapsThePartyPicker", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects`, () =>
        HttpResponse.json({
          items: [{ id: 3, code: "CB-2026-016", name: "Section 16" }],
          page: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
      http.get(`${apiBaseUrl}/vendor-purchases`, () => HttpResponse.json([])),
    );

    renderWithClient(<VendorPurchasePage />);
    await screen.findByRole("option", { name: "CB-2026-016 — Section 16" });
    fireEvent.change(screen.getByLabelText("Project"), { target: { value: "3" } });
    expect(await screen.findByLabelText("Vendor")).toBeInTheDocument();

    fireEvent.change(screen.getByLabelText("Bought by"), { target: { value: "FieldOfficer" } });

    expect(screen.getByLabelText("Field officer")).toBeInTheDocument();
    expect(screen.queryByLabelText("Vendor")).not.toBeInTheDocument();
  });
});
