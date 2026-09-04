import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { IntegrityCheckPage } from "./integrity-check-page";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const controls = [
  { control: "Bank", formula: "Opening + Credits − Debits", passed: true, violations: [] },
  { control: "Vendor", formula: "Purchases − Payments", passed: true, violations: [] },
  { control: "Project", formula: "Payables − Payments", passed: true, violations: [] },
  { control: "Client", formula: "Due − Receipts", passed: true, violations: [] },
];

describe("IntegrityCheckPage", () => {
  it("IntegrityCheck_ShowsAllPass", async () => {
    server.use(
      http.get(`${apiBaseUrl}/admin/integrity-check`, () =>
        HttpResponse.json({ passed: true, runAtUtc: "2026-09-03T00:00:00Z", controls }),
      ),
    );
    renderWithClient(<IntegrityCheckPage />);
    expect(await screen.findByTestId("integrity-summary")).toHaveTextContent(
      "All controls passed.",
    );
  });

  it("IntegrityCheck_NamesTheOffendingRecordOnFailure", async () => {
    server.use(
      http.get(`${apiBaseUrl}/admin/integrity-check`, () =>
        HttpResponse.json({
          passed: false,
          runAtUtc: "2026-09-03T00:00:00Z",
          controls: [
            ...controls,
            {
              control: "Multi-Project Payment",
              formula: "Bank Debit = Σ Project Allocations",
              passed: false,
              violations: [
                {
                  entity: "Settlement",
                  id: 42,
                  name: "ABC Hardware",
                  expected: 100000,
                  actual: 109999,
                  detail: "allocations 109999.00, payable legs 100000.00",
                },
              ],
            },
          ],
        }),
      ),
    );
    renderWithClient(<IntegrityCheckPage />);
    expect(await screen.findByTestId("integrity-summary")).toHaveTextContent(
      "One or more controls failed.",
    );
    expect(screen.getByText("Settlement #42 — ABC Hardware")).toBeInTheDocument();
  });
});
