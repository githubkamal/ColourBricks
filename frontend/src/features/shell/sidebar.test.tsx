import { fireEvent, render, screen } from "@testing-library/react";
import { describe, expect, it, vi } from "vitest";
import { Sidebar } from "./sidebar";

vi.mock("next/navigation", () => ({ usePathname: () => "/" }));
vi.mock("next/link", () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) => (
    <a href={href}>{children}</a>
  ),
}));

describe("Sidebar", () => {
  it("Sidebar_HidesItems_WithoutPermission", () => {
    render(<Sidebar permissions={["projects.view", "vendors.view"]} />);

    // Every section starts collapsed — expand the ones under test.
    fireEvent.click(screen.getByRole("button", { name: "Projects" }));
    fireEvent.click(screen.getByRole("button", { name: "Vendors" }));

    // Permitted items are shown.
    expect(screen.getByRole("link", { name: "Project Master" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Vendor Master" })).toBeInTheDocument();

    // BRD §61: an item the user lacks permission for is hidden, not disabled.
    expect(screen.queryByRole("link", { name: "Users" })).not.toBeInTheDocument();
    expect(screen.queryByText("Administration")).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Reconciliation Queue" })).not.toBeInTheDocument();
  });

  it("shows the full menu for an Administrator", () => {
    render(<Sidebar permissions={["*"]} />);

    expect(screen.getByText("Administration")).toBeInTheDocument();
    fireEvent.click(screen.getByRole("button", { name: "Administration" }));
    expect(screen.getByRole("link", { name: "Users" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Audit Logs" })).toBeInTheDocument();
  });
});
