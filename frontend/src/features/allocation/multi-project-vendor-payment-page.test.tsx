import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { MultiProjectVendorPaymentPage } from "./multi-project-vendor-payment-page";

vi.mock("@/features/parties/party-picker", () => ({
  PartyPicker: ({ onSelect }: { onSelect: (p: unknown) => void }) => (
    <button type="button" onClick={() => onSelect({ id: 8, name: "ABC Hardware" })}>
      pick vendor
    </button>
  ),
}));

const permissions = { current: ["*"] as string[] };
vi.mock("@/features/shell/user-context", () => ({
  useCurrentUser: () => ({ id: 1, name: "T", permissions: permissions.current }),
}));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const section20Proposal = {
  partyId: 8,
  amount: 100000,
  method: "Fifo",
  totalAllocated: 100000,
  advance: 0,
  lines: [
    {
      projectId: 1,
      projectName: "Project A",
      obligationId: 1,
      obligationReference: null,
      outstandingBefore: 25000,
      allocated: 25000,
      outstandingAfter: 0,
    },
    {
      projectId: 2,
      projectName: "Project B",
      obligationId: 2,
      obligationReference: null,
      outstandingBefore: 10000,
      allocated: 10000,
      outstandingAfter: 0,
    },
    {
      projectId: 3,
      projectName: "Project C",
      obligationId: 3,
      obligationReference: null,
      outstandingBefore: 30000,
      allocated: 30000,
      outstandingAfter: 0,
    },
    {
      projectId: 4,
      projectName: "Project D",
      obligationId: 4,
      obligationReference: null,
      outstandingBefore: 50000,
      allocated: 35000,
      outstandingAfter: 15000,
    },
  ],
};

async function propose() {
  server.use(
    http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json([])),
    http.get(`${apiBaseUrl}/payment-modes`, () => HttpResponse.json([])),
    http.post(`${apiBaseUrl}/vendor-payments/propose`, () => HttpResponse.json(section20Proposal)),
  );
  renderWithClient(<MultiProjectVendorPaymentPage />);
  fireEvent.click(screen.getByText("pick vendor"));
  fireEvent.change(await screen.findByLabelText("Payment amount"), { target: { value: "100000" } });
  fireEvent.click(screen.getByRole("button", { name: "Propose FIFO allocation" }));
  await screen.findByTestId("total-allocated");
}

describe("MultiProjectVendorPaymentPage", () => {
  it("FifoProposal_ShowsBrdSection20Split", async () => {
    permissions.current = ["vendor_payment_allocation.add"]; // no override
    await propose();

    expect(screen.getByRole("cell", { name: "₹35,000.000" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "₹15,000.000" })).toBeInTheDocument();
    expect(screen.getByTestId("total-allocated")).toHaveTextContent("₹1,00,000.000");
    expect(screen.getByTestId("difference")).toHaveTextContent("₹0.000");
    // Not authorised to override -> the allocated cells are plain text, no reason field.
    expect(screen.queryByLabelText("Allocated Project C")).not.toBeInTheDocument();
  });

  it("AllocationOverride_EditableForAuthorised_RequiresReasonAndBalances", async () => {
    permissions.current = ["*"]; // has approve
    await propose();

    // Move ₹5,000 from C to D.
    fireEvent.change(screen.getByLabelText("Allocated Project C"), { target: { value: "25000" } });
    expect(screen.getByTestId("difference")).toHaveTextContent("₹5,000.000"); // now unbalanced

    fireEvent.change(screen.getByLabelText("Allocated Project D"), { target: { value: "40000" } });
    expect(screen.getByTestId("difference")).toHaveTextContent("₹0.000");

    // Reason is now required.
    const reason = screen.getByLabelText("Override reason");
    fireEvent.change(reason, { target: { value: "prioritise D" } });
    expect(screen.getByRole("button", { name: "Apply override" })).toBeInTheDocument();
  });

  it("FifoRemainder_ShownAsAdvance_AndApplyStaysEnabled", async () => {
    permissions.current = ["vendor_payment_allocation.add"]; // no override
    const advanceProposal = {
      partyId: 8,
      amount: 100000,
      method: "Fifo",
      totalAllocated: 80000,
      advance: 20000,
      lines: [
        {
          projectId: 1,
          projectName: "Project A",
          obligationId: 1,
          obligationReference: null,
          outstandingBefore: 80000,
          allocated: 80000,
          outstandingAfter: 0,
        },
      ],
    };
    server.use(
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json([])),
      http.get(`${apiBaseUrl}/payment-modes`, () => HttpResponse.json([])),
      http.post(`${apiBaseUrl}/vendor-payments/propose`, () => HttpResponse.json(advanceProposal)),
    );
    renderWithClient(<MultiProjectVendorPaymentPage />);
    fireEvent.click(screen.getByText("pick vendor"));
    fireEvent.change(await screen.findByLabelText("Payment amount"), {
      target: { value: "100000" },
    });
    fireEvent.click(screen.getByRole("button", { name: "Propose FIFO allocation" }));
    await screen.findByTestId("total-allocated");

    expect(screen.getByTestId("total-allocated")).toHaveTextContent("₹80,000.000");
    expect(screen.getByTestId("difference")).toHaveTextContent("recorded as a vendor advance");
    // The ₹20,000 remainder does not block submission.
    expect(screen.getByRole("button", { name: "Apply payment + advance" })).toBeInTheDocument();
  });
});
