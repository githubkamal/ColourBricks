import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { beforeAll, describe, expect, it, vi } from "vitest";
import { ThemeProvider } from "@/features/shell/theme-context";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { BankStatementUploadPage } from "./bank-statement-upload-page";

const push = vi.fn();
vi.mock("next/navigation", () => ({ useRouter: () => ({ push }) }));

// AG Grid's first mount registers its community modules and builds the theme's
// stylesheet — comfortably under the default 5s alone, but that first-mount cost can
// push a test over it when the whole suite runs in parallel under load.
vi.setConfig({ testTimeout: 15000 });

// jsdom has no ResizeObserver; AG Grid uses it to size the grid viewport.
beforeAll(() => {
  if (!("ResizeObserver" in globalThis)) {
    (globalThis as unknown as { ResizeObserver: unknown }).ResizeObserver = class {
      observe() {}
      unobserve() {}
      disconnect() {}
    };
  }
});

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <ThemeProvider>{ui}</ThemeProvider>
    </QueryClientProvider>,
  );
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
  it("Upload_WithOneSavedProfile_ValidatesThenUploadsOnlyTheCheckedRows", async () => {
    server.use(
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json(accounts)),
      http.get(`${apiBaseUrl}/accounts/3/bank-statement-profiles`, () =>
        HttpResponse.json([
          {
            id: 9,
            accountId: 3,
            name: "HDFC current",
            headerRowIndex: 0,
            delimiter: ",",
            dateColumn: 0,
            narrationColumn: 1,
            referenceColumn: null,
            balanceColumn: null,
            singleAmountColumn: false,
            amountColumn: null,
            debitColumn: 2,
            creditColumn: 3,
            debitSign: "Negative",
            dateFormats: "dd/MM/yyyy",
          },
        ]),
      ),
      http.post(`${apiBaseUrl}/bank-imports/preview`, () =>
        HttpResponse.json([
          {
            sourceLineNo: 1,
            valueDate: "2026-05-01",
            narration: "NEFT NEW VENDOR",
            debit: 100,
            credit: 0,
            balance: null,
            bankReference: null,
            parseError: null,
            existsInDb: false,
          },
          {
            sourceLineNo: 2,
            valueDate: "2026-05-02",
            narration: "ALREADY IMPORTED",
            debit: 0,
            credit: 50,
            balance: null,
            bankReference: null,
            parseError: null,
            existsInDb: true,
          },
        ]),
      ),
      http.post(`${apiBaseUrl}/bank-imports`, async ({ request }) => {
        const body = (await request.json()) as { rows: unknown[] };
        expect(body.rows).toHaveLength(1);
        return HttpResponse.json({ id: 55, rows: [], counts: {} });
      }),
    );
    push.mockClear();
    renderWithClient(<BankStatementUploadPage />);

    await pickAccountAndFile();
    await screen.findByText("HDFC current");
    // Exactly one saved mapping — no picker, just its name and a button to use it.
    expect(screen.queryByLabelText("Saved profile")).not.toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Use this mapping" }));

    // The preview grid populates automatically once a mapping is in play.
    await screen.findByLabelText("Select row 1");
    expect(screen.getByLabelText("Select row 1")).toBeChecked();
    expect(screen.getByLabelText("Select row 2")).toBeChecked();

    fireEvent.click(screen.getByRole("button", { name: "Validate" }));

    // Validating flags row 2 as already in the ledger and unchecks it by default.
    await waitFor(() => expect(screen.getByLabelText("Select row 2")).not.toBeChecked());
    expect(screen.getByLabelText("Select row 1")).toBeChecked();

    fireEvent.click(await screen.findByRole("button", { name: "Upload 1 transaction(s)" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith("/reconciliation/imports/55"));
  });

  it("Upload_WithSeveralSavedProfiles_StillOffersAPicker", async () => {
    server.use(
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json(accounts)),
      http.get(`${apiBaseUrl}/accounts/3/bank-statement-profiles`, () =>
        HttpResponse.json([
          {
            id: 9,
            accountId: 3,
            name: "HDFC current",
            headerRowIndex: 0,
            delimiter: ",",
            dateColumn: 0,
            narrationColumn: 1,
            referenceColumn: null,
            balanceColumn: null,
            singleAmountColumn: false,
            amountColumn: null,
            debitColumn: 2,
            creditColumn: 3,
            debitSign: "Negative",
            dateFormats: "dd/MM/yyyy",
          },
          {
            id: 10,
            accountId: 3,
            name: "HDFC old layout",
            headerRowIndex: 0,
            delimiter: ",",
            dateColumn: 0,
            narrationColumn: 1,
            referenceColumn: null,
            balanceColumn: null,
            singleAmountColumn: false,
            amountColumn: null,
            debitColumn: 2,
            creditColumn: 3,
            debitSign: "Negative",
            dateFormats: "dd/MM/yyyy",
          },
        ]),
      ),
      http.post(`${apiBaseUrl}/bank-imports/preview`, () =>
        HttpResponse.json([
          {
            sourceLineNo: 1,
            valueDate: "2026-05-01",
            narration: "NEFT NEW VENDOR",
            debit: 100,
            credit: 0,
            balance: null,
            bankReference: null,
            parseError: null,
            existsInDb: false,
          },
        ]),
      ),
      http.post(`${apiBaseUrl}/bank-imports`, () => HttpResponse.json({ id: 55, rows: [], counts: {} })),
    );
    push.mockClear();
    renderWithClient(<BankStatementUploadPage />);

    await pickAccountAndFile();
    fireEvent.change(await screen.findByLabelText("Saved profile"), { target: { value: "9" } });
    fireEvent.click(screen.getByRole("button", { name: "Use this mapping" }));

    await screen.findByLabelText("Select row 1");
    fireEvent.click(screen.getByRole("button", { name: "Validate" }));
    fireEvent.click(await screen.findByRole("button", { name: "Upload 1 transaction(s)" }));

    await waitFor(() => expect(push).toHaveBeenCalledWith("/reconciliation/imports/55"));
  });

  it("Upload_NoProfile_MapsColumnsAndBlanksUnmappedCellsUntilAssigned", async () => {
    server.use(
      http.get(`${apiBaseUrl}/accounts`, () => HttpResponse.json(accounts)),
      http.get(`${apiBaseUrl}/accounts/3/bank-statement-profiles`, () => HttpResponse.json([])),
      http.post(`${apiBaseUrl}/bank-imports/detect-columns`, () =>
        HttpResponse.json({
          headers: ["Date", "Narration", "Amount"],
          sampleRows: [["01/05/2026", "NEFT ABC", "100.00"]],
        }),
      ),
      http.post(`${apiBaseUrl}/bank-imports/preview`, () =>
        HttpResponse.json([
          {
            sourceLineNo: 1,
            valueDate: "2026-05-01",
            narration: "NEFT ABC",
            debit: 100,
            credit: 0,
            balance: null,
            bankReference: null,
            parseError: null,
            existsInDb: false,
          },
        ]),
      ),
    );
    renderWithClient(<BankStatementUploadPage />);

    await pickAccountAndFile();
    fireEvent.click(await screen.findByRole("button", { name: "Detect columns" }));

    const wizard = await screen.findByTestId("mapping-wizard");
    expect(wizard).toHaveTextContent("[1] Narration");
    // Nothing mapped yet — the sample table shows no values.
    expect(wizard).not.toHaveTextContent("NEFT ABC");

    fireEvent.change(screen.getByLabelText("Field for column 0"), {
      target: { value: "dateColumn" },
    });
    fireEvent.change(screen.getByLabelText("Field for column 1"), {
      target: { value: "narrationColumn" },
    });

    // Narration is now mapped — its sample cell fills in; the preview grid appears too.
    await waitFor(() => expect(wizard).toHaveTextContent("NEFT ABC"));
    await screen.findByLabelText("Select row 1");
  });
});
