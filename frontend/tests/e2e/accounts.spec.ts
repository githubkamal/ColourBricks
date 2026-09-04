import { expect, test } from "@playwright/test";

test("lists seeded accounts and shows a derived balance on the detail page", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await page.goto("/accounts/cash");
  await expect(page.getByRole("cell", { name: "Office Cash", exact: true })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Site Cash", exact: true })).toBeVisible();

  await page.goto("/accounts/bank");
  for (const bank of ["HDFC", "SBI", "ICICI"]) {
    await expect(page.getByRole("cell", { name: bank, exact: true })).toBeVisible();
  }

  await page.getByRole("row", { name: /HDFC/ }).getByRole("link", { name: "View" }).click();

  await expect(page.getByText("Balance (derived from ledger)")).toBeVisible();
  await expect(page.getByText(/computed, never stored/)).toBeVisible();
});
