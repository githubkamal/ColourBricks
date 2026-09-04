import { expect, test } from "@playwright/test";

test("lists the seeded payment modes and can deactivate one", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await page.goto("/accounts/payment-modes");

  // BRD §28 initial modes.
  for (const mode of ["Cash", "Bank Transfer", "UPI", "Cheque", "Credit Card"]) {
    await expect(page.getByRole("cell", { name: mode, exact: true })).toBeVisible();
  }

  // Cheque requires a reference number (BRD §28 / P1-T05).
  const chequeRow = page.getByRole("row", { name: /^Cheque/ });
  await expect(chequeRow.getByRole("cell", { name: "Required" }).first()).toBeVisible();

  // Add a throwaway mode, then deactivate it — it stays in this admin list but flips to Inactive.
  const name = `Wallet ${Date.now()}`;
  await page.getByLabel("New payment mode").fill(name);
  await page.getByRole("button", { name: "Add", exact: true }).click();

  const row = page.getByRole("row", { name: new RegExp(`^${name}`) });
  await expect(row.getByRole("cell", { name: "Active" })).toBeVisible();
  await row.getByRole("button", { name: "Deactivate" }).click();
  await expect(row.getByRole("cell", { name: "Inactive" })).toBeVisible();
});
