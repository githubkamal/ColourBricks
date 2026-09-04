import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { TeamStatementPage } from "./team-statement-page";

vi.mock("@/features/teams/team-picker", () => ({
  TeamPicker: ({ onSelect }: { onSelect: (t: unknown) => void }) => (
    <button type="button" onClick={() => onSelect({ id: 9, name: "Electrical Team C" })}>
      pick team
    </button>
  ),
}));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("TeamStatementPage", () => {
  it("TeamStatement_ShowsRunningOutstandingPerRow", async () => {
    server.use(
      http.get(`${apiBaseUrl}/labour/teams/9/statement`, () =>
        HttpResponse.json([
          {
            date: "2026-05-01",
            kind: "Work",
            reference: "Wiring",
            workValue: 100000,
            paid: 0,
            runningOutstanding: 100000,
          },
          {
            date: "2026-05-05",
            kind: "Payment",
            reference: "Payment",
            workValue: 0,
            paid: 30000,
            runningOutstanding: 70000,
          },
          {
            date: "2026-05-12",
            kind: "Payment",
            reference: "Payment",
            workValue: 0,
            paid: 45000,
            runningOutstanding: 25000,
          },
        ]),
      ),
    );

    renderWithClient(<TeamStatementPage />);
    screen.getByText("pick team").click();

    // Row 1: work value ₹1,00,000 and running outstanding ₹1,00,000.
    expect(await screen.findAllByRole("cell", { name: "₹1,00,000.000" })).toHaveLength(2);
    expect(screen.getByRole("cell", { name: "₹70,000.000" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "₹25,000.000" })).toBeInTheDocument();
  });
});
