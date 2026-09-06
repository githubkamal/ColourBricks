import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import type { ItemSearchItem } from "@/features/items/types";
import { VendorPurchasePage } from "./vendor-purchase-page";

const cement: ItemSearchItem = {
  id: 42,
  name: "Cement",
  categoryName: null,
  unit: "Bag",
  defaultRate: 400,
  taxRate: 18,
};
const sand: ItemSearchItem = {
  id: 43,
  name: "Sand",
  categoryName: null,
  unit: "Load",
  defaultRate: 15000,
  taxRate: 0,
};

async function pickItem(label: string, item: ItemSearchItem) {
  fireEvent.change(screen.getByLabelText(label), { target: { value: item.name } });
  const option = await screen.findByRole("button", { name: new RegExp(`^${item.name} ·`) });
  fireEvent.click(option);
}

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
      http.get(`${apiBaseUrl}/items/search`, ({ request }) => {
        const q = new URL(request.url).searchParams.get("q")?.toLowerCase() ?? "";
        return HttpResponse.json([cement, sand].filter((i) => i.name.toLowerCase().includes(q)));
      }),
    );

    renderWithClient(<VendorPurchasePage />);

    await screen.findByRole("option", { name: "CB-2026-016 — Section 16" });
    fireEvent.change(screen.getByLabelText("Project"), { target: { value: "3" } });

    // BRD §16: 100 bags cement @ ₹400 + 2 loads sand @ ₹15,000 = ₹70,000.
    await pickItem("itemName 1", cement);
    fireEvent.change(screen.getByLabelText("quantity 1"), { target: { value: "100" } });
    fireEvent.change(screen.getByLabelText("rate 1"), { target: { value: "400" } });

    fireEvent.click(screen.getByRole("button", { name: "+ Add line" }));
    await pickItem("itemName 2", sand);
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
