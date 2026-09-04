import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { AccountDetail } from "./account-detail";
import type { AccountDetail as AccountDetailDto } from "./types";

vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const account: AccountDetailDto = {
  id: 7,
  name: "Payroll HDFC",
  type: "Bank",
  bankName: "HDFC Bank",
  accountNumber: "********9012",
  ifsc: "HDFC0001234",
  openingBalance: 100000,
  openingBalanceDate: "2026-04-01",
  balance: 130000,
  openingBalanceLocked: true,
  isActive: true,
  concurrencyStamp: "abc",
};

describe("AccountDetail", () => {
  it("AccountDetail_ShowsDerivedBalance_AndLockedOpeningBalance", async () => {
    server.use(http.get(`${apiBaseUrl}/accounts/7`, () => HttpResponse.json(account)));

    renderWithClient(<AccountDetail id={7} />);

    // Derived balance: opening 100000 + credit 50000 − debit 20000.
    expect(await screen.findByText("₹1,30,000.000")).toBeInTheDocument();
    // Opening balance cannot be edited once transactions exist.
    expect(screen.getByText(/locked \(has transactions\)/)).toBeInTheDocument();
    // Number arrives already masked from the API for non-admins.
    expect(screen.getByText("********9012")).toBeInTheDocument();
  });
});
