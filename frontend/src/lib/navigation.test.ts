import { describe, expect, it } from "vitest";
import { navigation, visibleNavigation } from "./navigation";

describe("visibleNavigation", () => {
  it("shows every section for an Administrator (wildcard)", () => {
    expect(visibleNavigation(["*"])).toHaveLength(navigation.length);
  });

  it("drops a section when every one of its items is gated out", () => {
    const labels = visibleNavigation(["projects.view", "project_income.view"]).map((s) => s.label);

    expect(labels).toContain("Projects");
    expect(labels).not.toContain("Vendors");
    expect(labels).not.toContain("Administration");
  });

  it("keeps only the permitted items within a visible section", () => {
    const projects = visibleNavigation(["project_income.view"]).find((s) => s.label === "Projects");

    expect(projects).toBeDefined();
    expect(projects?.items.map((i) => i.label)).toEqual(["Project Income"]);
  });

  it("hides Administration for a role without any admin permission (BRD §61)", () => {
    const accountsTeamLike = [
      "dashboard.view",
      "projects.view",
      "vendors.view",
      "payments.view",
      "bank_reconciliation.view",
      "reports.view",
    ];
    const labels = visibleNavigation(accountsTeamLike).map((s) => s.label);

    expect(labels).toContain("Accounts");
    expect(labels).toContain("Reports");
    expect(labels).not.toContain("Administration");
  });
});
