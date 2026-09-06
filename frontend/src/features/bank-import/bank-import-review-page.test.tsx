import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { BankImportReviewPage } from "./bank-import-review-page";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const projects = {
  items: [
    { id: 1, code: "A", name: "Project A", status: "Ongoing" },
    { id: 2, code: "B", name: "Project B", status: "Ongoing" },
  ],
  page: 1,
  pageSize: 200,
  totalCount: 2,
  totalPages: 1,
};

function batch(rows: unknown[], overrides: Record<string, unknown> = {}) {
  return {
    id: 7,
    accountId: 3,
    fileName: "hdfc-may.csv",
    status: "Draft",
    uploadedAtUtc: "2026-05-01T00:00:00Z",
    uploadedByUserId: 1,
    counts: {
      total: rows.length,
      mapped: 0,
      unmapped: 0,
      removed: 0,
      duplicate: 0,
      parseError: 0,
      committed: 0,
    },
    rows,
    ...overrides,
  };
}

const debitRow = {
  id: 11,
  sourceLineNo: 1,
  valueDate: "2026-05-01",
  narration: "NEFT MULTI PROJECT VENDOR",
  debit: 100000,
  credit: 0,
  balance: null,
  bankReference: null,
  parseState: "Parsed",
  parseError: null,
  duplicateOfBankTransactionId: null,
  isRemoved: false,
  allocations: [],
  allocatedTotal: 0,
  readyToCommit: false,
  blockedReason: "not mapped to a project",
};

const creditRowReady = {
  id: 12,
  sourceLineNo: 2,
  valueDate: "2026-05-02",
  narration: "RTGS CLIENT RECEIPT",
  debit: 0,
  credit: 500000,
  balance: null,
  bankReference: null,
  parseState: "Parsed",
  parseError: null,
  duplicateOfBankTransactionId: null,
  isRemoved: false,
  allocations: [{ projectId: 1, projectName: "Project A", amount: 500000 }],
  allocatedTotal: 500000,
  readyToCommit: true,
  blockedReason: null,
};

describe("BankImportReviewPage", () => {
  it("BankImportReview_BlocksCommitUntilEveryKeptRowIsMapped", async () => {
    server.use(
      http.get(`${apiBaseUrl}/bank-imports/7`, () =>
        HttpResponse.json(batch([debitRow, creditRowReady])),
      ),
      http.get(`${apiBaseUrl}/projects`, () => HttpResponse.json(projects)),
    );
    renderWithClient(<BankImportReviewPage batchId={7} />);

    expect(await screen.findByTestId("import-counts")).toBeInTheDocument();
    expect(screen.getByText(/1 row\(s\) still need a project mapping/)).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /Commit 1 transaction/ })).toBeDisabled();

    // The credit row offers a single project slot — no "+ project" — and it's
    // already mapped to Project A, shown as one picked chip (not a labelled
    // search input, since a value is already selected).
    expect(await screen.findAllByText("Project A")).toHaveLength(1);
  });

  it("BankImportReview_EnablesCommit_WhenAllRowsReady", async () => {
    server.use(
      http.get(`${apiBaseUrl}/bank-imports/7`, () =>
        HttpResponse.json(
          batch([
            creditRowReady,
            {
              ...debitRow,
              allocatedTotal: 100000,
              readyToCommit: true,
              blockedReason: null,
              allocations: [{ projectId: 1, projectName: "Project A", amount: 100000 }],
            },
          ]),
        ),
      ),
      http.get(`${apiBaseUrl}/projects`, () => HttpResponse.json(projects)),
    );
    renderWithClient(<BankImportReviewPage batchId={7} />);

    const commit = await screen.findByRole("button", { name: /Commit 2 transaction/ });
    expect(commit).toBeEnabled();
  });

  it("BankImportReview_BlocksCommitWhileADuplicateRowRemains", async () => {
    const duplicateRow = {
      ...debitRow,
      id: 13,
      sourceLineNo: 3,
      duplicateOfBankTransactionId: 42,
      blockedReason: "already imported",
    };
    server.use(
      http.get(`${apiBaseUrl}/bank-imports/7`, () =>
        HttpResponse.json(batch([creditRowReady, duplicateRow])),
      ),
      http.get(`${apiBaseUrl}/projects`, () => HttpResponse.json(projects)),
    );
    renderWithClient(<BankImportReviewPage batchId={7} />);

    expect(await screen.findByTestId("duplicate-warning")).toHaveTextContent(
      "1 duplicate row(s) must be removed before you can commit",
    );
    expect(screen.getByRole("button", { name: /Commit 1 transaction/ })).toBeDisabled();
    // The duplicate row shows no mapping controls — nothing to map, it needs removing.
    expect(screen.getByText("not applicable")).toBeInTheDocument();
  });
});
