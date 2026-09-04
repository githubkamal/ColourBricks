import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { DonationOutstandingPanel } from "./donation-outstanding-panel";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("DonationOutstandingPanel", () => {
  it("DonationOutstanding_ShowsPerTempleAllocatedPaidAndOutstanding", async () => {
    server.use(
      http.get(`${apiBaseUrl}/projects/4/donation/outstanding`, () =>
        HttpResponse.json([
          { templeId: 1, templeName: "Temple A", allocated: 120000, paid: 120000, outstanding: 0 },
          {
            templeId: 2,
            templeName: "Temple B",
            allocated: 80000,
            paid: 30000,
            outstanding: 50000,
          },
        ]),
      ),
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/payment-modes`, () => HttpResponse.json([])),
    );

    renderWithClient(<DonationOutstandingPanel projectId={4} />);

    expect(await screen.findByTestId("donation-outstanding-1")).toHaveTextContent("₹0.00");
    expect(screen.getByTestId("donation-outstanding-2")).toHaveTextContent("₹50,000.00");
    expect(screen.getByRole("cell", { name: "Temple B" })).toBeInTheDocument();
  });
});
