import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { BankStatementUploadPage } from "./bank-statement-upload-page";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const accounts = [{ id: 3, name: "HDFC", type: "Bank", isActive: true }];

async function pickAccountAndFile() {
  await screen.findByRole("option", { name: "HDFC" });
  fireEvent.change(screen.getByLabelText("Account"), { target: { value: "3" } });

  const input = screen.getByLabelText("Statement file") as HTMLInputElement;
  const file = new File(["Date,Narration,Amt\n01/05/2026,X,100"], "hdfc.csv", { type: "text/csv" });
  Object.defineProperty(input, "files", { value: [file], configurable: true });
  fireEvent.change(input);
}

describe("BankStatementUploadPage", () => {
  it("Upload_WithSavedProfile_UploadsAndNavigatesToReview", async () => {
    server.use(
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json(accounts)),
      http.get(`${apiBaseUrl}/accounts/3/bank-statement-profiles`, () =>
        HttpResponse.json([
          { id: 9, accountId: 3, name: "HDFC current", singleAmountColumn: false },
        ]),
      ),
      http.post(`${apiBaseUrl}/bank-imports/upload`, () =>
        HttpResponse.json({ id: 55, rows: [], counts: {} }),
      ),
    );
    push.mockClear();
    renderWithClient(<BankStatementUploadPage />);

    await pickAccountAndFile();
    fireEvent.change(await screen.findByLabelText("Saved profile"), { target: { value: "9" } });
    fireEvent.click(screen.getByRole("button", { name: /Upload & review/ }));

    await waitFor(() => expect(push).toHaveBeenCalledWith("/reconciliation/imports/55"));
  });

  it("Upload_NoProfile_DetectsColumnsAndShowsWizard", async () => {
    server.use(
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json(accounts)),
      http.get(`${apiBaseUrl}/accounts/3/bank-statement-profiles`, () => HttpResponse.json([])),
      http.post(`${apiBaseUrl}/bank-imports/detect-columns`, () =>
        HttpResponse.json({
          headers: ["Date", "Narration", "Amount"],
          sampleRows: [["01/05/2026", "NEFT ABC", "100.00"]],
        }),
      ),
    );
    renderWithClient(<BankStatementUploadPage />);

    await pickAccountAndFile();
    fireEvent.click(await screen.findByRole("button", { name: "Detect columns" }));

    const wizard = await screen.findByTestId("mapping-wizard");
    expect(wizard).toHaveTextContent("[1] Narration");
    expect(screen.getByLabelText("dateColumn")).toBeInTheDocument();
  });
});
