import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { ProjectDonationForm } from "./project-donation-form";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("ProjectDonationForm", () => {
  it("DonationForm_PercentageBasis_ComputesAmount", async () => {
    server.use(
      http.get(`${apiBaseUrl}/temples`, () => HttpResponse.json([{ id: 1, name: "Temple A" }])),
      http.get(`${apiBaseUrl}/projects/9/donation`, () => new HttpResponse(null, { status: 404 })),
    );

    renderWithClient(<ProjectDonationForm projectId={9} projectContractValue={10_000_000} />);

    await waitFor(() =>
      expect(screen.getByLabelText("Percentage of contract value")).toBeInTheDocument(),
    );

    fireEvent.change(screen.getByLabelText("Percentage of contract value"), {
      target: { value: "2" },
    });

    // BRD §26: 2% of ₹1,00,00,000 = ₹2,00,000.
    expect(screen.getByTestId("donation-amount")).toHaveTextContent("₹2,00,000.00");
  });
});
