import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { ItemPicker } from "./item-picker";
import type { ItemSearchItem } from "./types";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const cement: ItemSearchItem = {
  id: 42,
  name: "Cement",
  categoryName: "Cement",
  unit: "Bag",
  defaultRate: 400,
  taxRate: 18,
};

describe("ItemPicker", () => {
  it("ItemPicker_PrefillsUnitAndRate", async () => {
    server.use(http.get(`${apiBaseUrl}/items/search`, () => HttpResponse.json([cement])));

    const onSelect = vi.fn();
    renderWithClient(<ItemPicker selected={null} onSelect={onSelect} label="Item" />);

    fireEvent.change(screen.getByLabelText("Item"), { target: { value: "cement" } });

    const option = await screen.findByRole("button", { name: /Cement · Bag · ₹400/ });
    fireEvent.click(option);

    await waitFor(() => expect(onSelect).toHaveBeenCalledTimes(1));
    expect(onSelect).toHaveBeenCalledWith(
      expect.objectContaining({ unit: "Bag", defaultRate: 400, taxRate: 18 }),
    );
  });
});
