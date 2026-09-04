import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { UsersPage } from "./users-page";
import type { UserListItem } from "./types";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const users: UserListItem[] = [
  {
    id: 1,
    name: "Administrator",
    email: "admin@colourbricks.local",
    mobile: null,
    roleId: 1,
    roleName: "Administrator",
    departmentId: null,
    isActive: true,
    isAdministrator: true,
    assignedProjectIds: [],
    concurrencyStamp: "a",
  },
  {
    id: 2,
    name: "Scoped Manager",
    email: "pm@colourbricks.local",
    mobile: null,
    roleId: 2,
    roleName: "Project Manager",
    departmentId: null,
    isActive: false,
    isAdministrator: false,
    assignedProjectIds: [10, 11],
    concurrencyStamp: "b",
  },
];

describe("UsersPage", () => {
  it("UsersPage_ListsUsersWithRoleStatusAndProjectScope", async () => {
    server.use(
      http.get(`${apiBaseUrl}/users`, () => HttpResponse.json(users)),
      http.get(`${apiBaseUrl}/users/roles`, () =>
        HttpResponse.json([
          { id: 1, name: "Administrator" },
          { id: 2, name: "Project Manager" },
        ]),
      ),
    );

    renderWithClient(<UsersPage />);

    expect(
      await screen.findByRole("cell", { name: "admin@colourbricks.local" }),
    ).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "pm@colourbricks.local" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "Scoped Manager" })).toBeInTheDocument();
    // "Administrator" appears twice — as the user's name and as their role.
    expect(screen.getAllByRole("cell", { name: "Administrator" })).toHaveLength(2);

    // Unrestricted vs restricted project access.
    expect(screen.getByRole("cell", { name: "All" })).toBeInTheDocument();
    expect(screen.getByRole("cell", { name: "2 assigned" })).toBeInTheDocument();

    // Status column.
    expect(screen.getByRole("cell", { name: "Inactive" })).toBeInTheDocument();
  });
});
