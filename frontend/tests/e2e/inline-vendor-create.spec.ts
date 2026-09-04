import { expect, test } from "@playwright/test";

test("adds a vendor inline from the picker without leaving the page", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await page.goto("/vendors");

  const name = `Acme Traders ${Date.now()}`;
  await page.getByLabel("Select or add a vendor").fill(name);

  await page.getByRole("button", { name: /Add .* as a vendor/ }).click();

  // Selected in place — no navigation.
  await expect(page.getByText(`Selected vendor: ${name}`)).toBeVisible();
  await expect(page).toHaveURL(/\/vendors$/);

  // And now listed.
  await expect(page.getByRole("cell", { name })).toBeVisible();
});
