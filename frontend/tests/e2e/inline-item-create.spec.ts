import { expect, test } from "@playwright/test";

test("adds an item inline from the picker without leaving the page", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await page.goto("/materials");

  const name = `Ready Mix ${Date.now()}`;
  await page.getByLabel("Select or add an item").fill(name);

  await page.getByRole("button", { name: /Add .* as an item/ }).click();

  // Selected in place — no navigation.
  await expect(page.getByText(`Selected item: ${name}`)).toBeVisible();
  await expect(page).toHaveURL(/\/materials$/);

  // And now listed in the master.
  await expect(page.getByRole("cell", { name })).toBeVisible();
});
