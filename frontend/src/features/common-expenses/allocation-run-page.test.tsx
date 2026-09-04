import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { AllocationRunPage } from "./allocation-run-page";

function rc(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const preview = {
  periodFrom: "2026-08-01",
  periodTo: "2026-08-31",
  method: "Equal",
  poolAmount: 100000,
  totalAllocated: 100000,
  balances: true,
  lines: [
    {
      projectId: 1,
      projectName: "Alpha",
      before: 0,
      allocated: 50000,
      after: 50000,
      percent: null,
    },
    {
      projectId: 2,
      projectName: "Beta",
      before: 1000,
      allocated: 50000,
      after: 51000,
      percent: null,
    },
  ],
};

describe("AllocationRunPage", () => {
  it("AllocationRun_PreviewShowsBeforeAfterAndTotalCheck", async () => {
    let committed = false;
    server.use(
      http.get(`${apiBaseUrl}/common-expense-allocations`, () => HttpResponse.json([])),
      http.post(`${apiBaseUrl}/common-expense-allocations/preview`, () =>
        HttpResponse.json(preview),
      ),
      http.post(`${apiBaseUrl}/common-expense-allocations`, () => {
        committed = true;
        return HttpResponse.json({
          ...preview,
          id: 7,
          types: "Personal",
          status: "Active",
          note: null,
          createdAtUtc: "2026-09-03T00:00:00Z",
        });
      }),
    );
    rc(<AllocationRunPage />);

    fireEvent.change(screen.getByLabelText("From"), { target: { value: "2026-08-01" } });
    fireEvent.change(screen.getByLabelText("To"), { target: { value: "2026-08-31" } });
    fireEvent.click(screen.getByRole("button", { name: "Preview" }));

    expect(await screen.findByTestId("preview-balance")).toHaveTextContent("balances");
    expect(screen.getByRole("cell", { name: "Alpha" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "₹1,000.000" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "₹51,000.000" })).toBeInTheDocument();

    fireEvent.click(screen.getByRole("button", { name: "Commit allocation" }));
    await waitFor(() => expect(committed).toBe(true));
  });

  it("AllocationRun_ListsHistoryWithReverseAction", async () => {
    server.use(
      http.get(`${apiBaseUrl}/common-expense-allocations`, () =>
        HttpResponse.json([
          {
            id: 3,
            periodFrom: "2026-07-01",
            periodTo: "2026-07-31",
            types: "Personal,Office",
            method: "Equal",
            poolAmount: 80000,
            status: "Active",
            note: null,
            createdAtUtc: "2026-08-01T00:00:00Z",
            lines: [],
          },
        ]),
      ),
    );
    rc(<AllocationRunPage />);

    expect(await screen.findByText("Personal,Office")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Reverse" })).toBeInTheDocument();
  });
});
