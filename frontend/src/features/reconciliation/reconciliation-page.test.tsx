import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { ReconciliationPage } from "./reconciliation-page";

vi.mock("@/features/parties/party-picker", () => ({
  PartyPicker: ({ onSelect, label }: { onSelect: (p: unknown) => void; label: string }) => (
    <button type="button" onClick={() => onSelect({ id: 5, name: "Vendor Ltd" })}>
      pick {label}
    </button>
  ),
}));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const debitRow = {
  id: 1,
  date: "2026-05-10",
  bank: "HDFC",
  description: "NEFT DR-ABC HARDWARE",
  type: "Debit" as const,
  credit: 0,
  debit: 100000,
  counterparty: null,
  projects: "1 Project",
  allocated: 40000,
  difference: 60000,
  status: "Pending",
  projectDetail: [{ projectId: 7, projectName: "Project A", amount: 40000 }],
};

function queue(items: unknown[]) {
  return { items, page: 1, pageSize: 100, totalCount: items.length, totalPages: 1 };
}

function stub(items: unknown[]) {
  server.use(
    http.get(`${apiBaseUrl}/accounts`, () =>
      HttpResponse.json([{ id: 3, name: "HDFC", isActive: true }]),
    ),
    http.get(`${apiBaseUrl}/reconciliation`, () => HttpResponse.json(queue(items))),
    http.get(`${apiBaseUrl}/internal-transfers/suggestions`, () => HttpResponse.json([])),
    http.get(`${apiBaseUrl}/projects`, () =>
      HttpResponse.json({ items: [], page: 1, pageSize: 200, totalCount: 0, totalPages: 1 }),
    ),
  );
}

describe("ReconciliationPage", () => {
  it("ReconciliationGrid_RendersAllBrdColumns", async () => {
    stub([debitRow]);
    renderWithClient(<ReconciliationPage />);
    for (const col of [
      "Date",
      "Bank",
      "Description",
      "Type",
      "Credit",
      "Debit",
      "Client/Vendor",
      "Project(s)",
      "Allocated",
      "Difference",
      "Status",
      "Action",
    ]) {
      expect(await screen.findByRole("columnheader", { name: col })).toBeInTheDocument();
    }
  });

  it("Row_ShowsMapDeleteHoldActions", async () => {
    stub([debitRow]);
    renderWithClient(<ReconciliationPage />);

    expect(await screen.findByRole("button", { name: "Map" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Delete" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Hold" })).toBeInTheDocument();
  });

  it("MapDebit_SubmitDisabled_WhileSplitDoesNotMatchAmount", async () => {
    stub([debitRow]);
    renderWithClient(<ReconciliationPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Map" }));
    fireEvent.click(await screen.findByRole("button", { name: "pick Vendor 1" }));

    expect(screen.getByTestId("split-difference")).toHaveTextContent("difference ₹60,000.00");
    expect(screen.getByRole("button", { name: "Submit" })).toBeDisabled();
  });

  it("MapDebit_SubmitEnabled_OnceSplitMatchesAndEachLineHasADescription", async () => {
    stub([debitRow]);
    renderWithClient(<ReconciliationPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Map" }));
    fireEvent.click(await screen.findByRole("button", { name: "pick Vendor 1" }));
    fireEvent.change(screen.getByLabelText("Amount 1"), { target: { value: "100000" } });
    fireEvent.change(screen.getByLabelText("Description 1"), {
      target: { value: "Full settlement" },
    });

    expect(screen.getByTestId("split-difference")).toHaveTextContent("difference ₹0.00");
    expect(screen.getByRole("button", { name: "Submit" })).toBeEnabled();
  });

  it("MapDebit_TargetDropdown_OffersFieldOfficerAndCustom", async () => {
    stub([debitRow]);
    renderWithClient(<ReconciliationPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Map" }));

    const target = await screen.findByLabelText("Target 1");
    expect(screen.getByRole("option", { name: "Field officer" })).toBeInTheDocument();
    expect(screen.getByRole("option", { name: "Custom" })).toBeInTheDocument();

    fireEvent.change(target, { target: { value: "FieldOfficer" } });
    expect(screen.getByRole("button", { name: "pick Field officer 1" })).toBeInTheDocument();

    fireEvent.change(target, { target: { value: "Custom" } });
    expect(screen.queryByRole("button", { name: /pick/ })).not.toBeInTheDocument();
  });

  it("MapDebit_CustomWorkTarget_RequiresBothPartyAndProject", async () => {
    stub([debitRow]);
    renderWithClient(<ReconciliationPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Map" }));
    const target = await screen.findByLabelText("Target 1");
    expect(screen.getByRole("option", { name: "Custom work" })).toBeInTheDocument();

    fireEvent.change(target, { target: { value: "CustomWork" } });
    expect(screen.getByRole("button", { name: "pick Party 1" })).toBeInTheDocument();
    expect(screen.getByLabelText("Project 1")).toBeInTheDocument();
    // Unlike Vendor, there's no "advance" affordance for custom work.
    expect(screen.queryByText(/none = advance/)).not.toBeInTheDocument();
  });

  it("MapDebit_BankChargesTarget_RequiresProjectButNoParty", async () => {
    stub([debitRow]);
    renderWithClient(<ReconciliationPage />);

    fireEvent.click(await screen.findByRole("button", { name: "Map" }));
    const target = await screen.findByLabelText("Target 1");
    expect(screen.getByRole("option", { name: "Bank charges" })).toBeInTheDocument();

    fireEvent.change(target, { target: { value: "BankCharges" } });
    expect(screen.getByLabelText("Project 1")).toBeInTheDocument();
    // No party picker for a bank charge — it has no counterparty.
    expect(screen.queryByRole("button", { name: /pick/ })).not.toBeInTheDocument();
    expect(screen.getByText("Project")).toBeInTheDocument(); // mandatory label, not "(optional...)"
  });

  it("BulkExclude_RequiresReason", async () => {
    stub([debitRow]);
    renderWithClient(<ReconciliationPage />);

    fireEvent.click(await screen.findByLabelText("Select row 1"));
    const excludeBtn = screen.getByRole("button", { name: /Exclude 1/ });
    expect(excludeBtn).toBeDisabled();

    fireEvent.change(screen.getByLabelText("Exclude reason"), { target: { value: "not ours" } });
    expect(excludeBtn).toBeEnabled();
  });
});
