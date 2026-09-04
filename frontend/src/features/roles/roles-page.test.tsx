import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { RolesPage } from "./roles-page";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("RolesPage", () => {
  it("RolePermissionMatrix_RendersGridAndReflectsGrants", async () => {
    server.use(
      http.get(`${apiBaseUrl}/roles`, () =>
        HttpResponse.json([
          {
            id: 3,
            name: "Data Entry User",
            description: null,
            isSystem: false,
            isActive: true,
            permissionCount: 1,
          },
        ]),
      ),
      http.get(`${apiBaseUrl}/roles/catalogue`, () =>
        HttpResponse.json({
          modules: [
            { key: "projects", name: "Project Management" },
            { key: "vendors", name: "Vendor Management" },
          ],
          actions: ["view", "add"],
        }),
      ),
      http.get(`${apiBaseUrl}/roles/3`, () =>
        HttpResponse.json({
          id: 3,
          name: "Data Entry User",
          description: null,
          isSystem: false,
          isActive: true,
          permissionCount: 1,
          permissionKeys: ["projects.view"],
          concurrencyStamp: "abc",
        }),
      ),
    );

    renderWithClient(<RolesPage />);

    fireEvent.click(await screen.findByRole("button", { name: /Data Entry User/ }));

    // The one granted cell is checked; the others are not.
    expect((await screen.findByLabelText("projects.view")) as HTMLInputElement).toBeChecked();
    expect(screen.getByLabelText("projects.add")).not.toBeChecked();
    expect(screen.getByLabelText("vendors.view")).not.toBeChecked();

    // Row toggle grants every action for the module.
    fireEvent.click(screen.getByLabelText("All Vendor Management"));
    expect(screen.getByLabelText("vendors.view")).toBeChecked();
    expect(screen.getByLabelText("vendors.add")).toBeChecked();
  });
});
