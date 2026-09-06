import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { CustomWorkPage } from "./custom-work-page";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("CustomWorkPage", () => {
  it("CustomWork_ShowsVarianceInListAndPreview", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects`, () =>
        HttpResponse.json({
          items: [{ id: 2, code: "CB-2026-027", name: "Extras" }],
          page: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
      http.get(`${apiBaseUrl}/parties/search`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/projects/2/custom-work`, () =>
        HttpResponse.json([
          {
            id: 1,
            projectId: 2,
            partyId: null,
            partyName: null,
            departmentId: null,
            date: "2026-05-01",
            workType: "Additional electrical work",
            description: null,
            estimatedCost: 70000,
            actualCost: 85000,
            variance: 15000,
            status: "Active",
          },
        ]),
      ),
    );

    renderWithClient(<CustomWorkPage />);
    fireEvent.change(screen.getByLabelText("Project"), { target: { value: "Extras" } });
    fireEvent.click(await screen.findByRole("button", { name: "CB-2026-027 — Extras" }));

    // Recorded variance in the list.
    expect(await screen.findByRole("cell", { name: "₹15,000.000" })).toBeInTheDocument();

    // Live preview from the form inputs.
    fireEvent.change(screen.getByLabelText("Estimated cost"), { target: { value: "70000" } });
    fireEvent.change(screen.getByLabelText("Actual cost"), { target: { value: "85000" } });
    expect(screen.getByTestId("variance-preview")).toHaveTextContent("₹15,000.000");
  });

  it("CustomWork_RowWithParty_OffersPayAndReverse_OpensInlinePaymentForm", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects`, () =>
        HttpResponse.json({
          items: [{ id: 2, code: "CB-2026-027", name: "Extras" }],
          page: 1,
          pageSize: 100,
          totalCount: 1,
          totalPages: 1,
        }),
      ),
      http.get(`${apiBaseUrl}/parties/search`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/projects/2/custom-work`, () =>
        HttpResponse.json([
          {
            id: 9,
            projectId: 2,
            partyId: 5,
            partyName: "Contractor Co",
            departmentId: null,
            date: "2026-05-01",
            workType: "Extra tiling",
            description: null,
            estimatedCost: 40000,
            actualCost: 40000,
            variance: 0,
            status: "Active",
          },
        ]),
      ),
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/payment-modes`, () => HttpResponse.json([])),
    );

    renderWithClient(<CustomWorkPage />);
    fireEvent.change(screen.getByLabelText("Project"), { target: { value: "Extras" } });
    fireEvent.click(await screen.findByRole("button", { name: "CB-2026-027 — Extras" }));

    expect(await screen.findByRole("cell", { name: "Contractor Co" })).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Pay" }));

    expect(screen.getByLabelText("Payment amount")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Record payment" })).toBeDisabled();
  });
});
