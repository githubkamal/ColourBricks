import { expect, test } from "@playwright/test";

test("toggles a permission on a role and saves the matrix", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await page.goto("/admin/roles");

  // A throwaway role so the test never mutates a seeded one.
  const roleName = `Toggle Role ${Date.now()}`;
  await page.getByLabel("New role").fill(roleName);
  await page.getByRole("button", { name: "Add", exact: true }).click();
  await expect(page.getByText(`${roleName} — permission matrix`)).toBeVisible();

  const cell = page.getByRole("checkbox", { name: "bank_reconciliation.reconcile" });
  await expect(cell).not.toBeChecked();
  await cell.check();
  await page.getByRole("button", { name: "Save permissions" }).click();

  await expect(page.getByText(`${roleName} permissions saved`)).toBeVisible();

  // Re-selecting the role shows the grant persisted.
  await page.getByRole("button", { name: /Toggle Role/ }).click();
  await expect(page.getByRole("checkbox", { name: "bank_reconciliation.reconcile" })).toBeChecked();
});
