import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { resetNavigationMock } from "@/test/next-navigation-mock";
import { FieldOfficerExpensePage } from "./field-officer-expense-page";

vi.mock("next/navigation", () => import("@/test/next-navigation-mock"));

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("FieldOfficerExpensePage", () => {
  beforeEach(() => resetNavigationMock());

  it("FieldOfficerBills_ShowsOutstandingAndAdvance_OnceAnOfficerIsPicked", async () => {
    server.use(
      http.get(`${apiBaseUrl}/parties/search`, () =>
        HttpResponse.json([
          { id: 9, name: "Ravi (Field Officer)", types: ["FieldOfficer"], category: null },
        ]),
      ),
      http.get(`${apiBaseUrl}/vendors/9/outstanding-summary`, () =>
        HttpResponse.json({ vendorId: 9, total: 13000, byProject: [], advance: 0 }),
      ),
      http.get(`${apiBaseUrl}/field-officer-expenses`, () => HttpResponse.json([])),
    );

    renderWithClient(<FieldOfficerExpensePage />);

    fireEvent.change(screen.getByLabelText("Field officer"), { target: { value: "Ravi" } });
    fireEvent.click(await screen.findByText("Ravi (Field Officer)"));

    expect(await screen.findByTestId("field-officer-outstanding")).toHaveTextContent("₹13,000.00");
    expect(screen.getByLabelText("Type")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Record bill" })).toBeDisabled();
  });

  it("FieldOfficerBills_ShowsInlineErrors_AfterLeavingRequiredFieldsBlankOrInvalid", async () => {
    server.use(
      http.get(`${apiBaseUrl}/parties/search`, () =>
        HttpResponse.json([
          { id: 9, name: "Ravi (Field Officer)", types: ["FieldOfficer"], category: null },
        ]),
      ),
      http.get(`${apiBaseUrl}/vendors/9/outstanding-summary`, () =>
        HttpResponse.json({ vendorId: 9, total: 0, byProject: [], advance: 0 }),
      ),
      http.get(`${apiBaseUrl}/field-officer-expenses`, () => HttpResponse.json([])),
    );

    renderWithClient(<FieldOfficerExpensePage />);
    fireEvent.change(screen.getByLabelText("Field officer"), { target: { value: "Ravi" } });
    fireEvent.click(await screen.findByText("Ravi (Field Officer)"));

    // No error shown before the field is touched.
    expect(screen.queryByText("Required")).not.toBeInTheDocument();

    fireEvent.blur(await screen.findByLabelText("Date"));
    expect(screen.getByText("Required")).toBeInTheDocument();

    fireEvent.blur(screen.getByLabelText("Amount"));
    expect(screen.getByText("Must be greater than zero")).toBeInTheDocument();

    // Typing a valid amount clears that specific error, but the still-empty date stays flagged.
    fireEvent.change(screen.getByLabelText("Amount"), { target: { value: "500" } });
    expect(screen.queryByText("Must be greater than zero")).not.toBeInTheDocument();
    expect(screen.getByText("Required")).toBeInTheDocument();
  });
});
