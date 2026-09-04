import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { PaymentModeSelect } from "./payment-mode-select";
import type { PaymentModeDto } from "./types";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const modes: PaymentModeDto[] = [
  {
    id: 1,
    name: "Cash",
    requiresAccount: false,
    requiresReference: false,
    sortOrder: 10,
    isActive: true,
    concurrencyStamp: "a",
  },
  {
    id: 4,
    name: "Cheque",
    requiresAccount: true,
    requiresReference: true,
    sortOrder: 40,
    isActive: true,
    concurrencyStamp: "b",
  },
];

describe("PaymentModeSelect", () => {
  it("PaymentModeSelect_ChoosingCheque_ReportsReferenceRequired", async () => {
    server.use(http.get(`${apiBaseUrl}/payment-modes`, () => HttpResponse.json(modes)));

    const onChange = vi.fn();
    renderWithClient(<PaymentModeSelect value={null} onChange={onChange} />);

    await waitFor(() => expect(screen.getByRole("option", { name: "Cheque" })).toBeInTheDocument());

    fireEvent.change(screen.getByLabelText("Payment mode"), { target: { value: "4" } });

    expect(onChange).toHaveBeenCalledWith(
      expect.objectContaining({ name: "Cheque", requiresReference: true, requiresAccount: true }),
    );
  });
});
