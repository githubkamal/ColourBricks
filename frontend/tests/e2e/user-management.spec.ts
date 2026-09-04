import { expect, test } from "@playwright/test";

test("adds a user and deactivates it", async ({ page }) => {
  page.on("dialog", (dialog) => dialog.accept());

  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await page.goto("/admin/users");

  const email = `member-${Date.now()}@colourbricks.local`;
  await page.getByLabel("Name").fill("Site Member");
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Temporary password").fill("Passw0rd!23");
  await page.getByRole("button", { name: "Add user" }).click();

  const row = page.getByRole("row", { name: new RegExp(email.replace(/[.\-]/g, "\\$&")) });
  await expect(row.getByRole("cell", { name: "Active" })).toBeVisible();

  await row.getByRole("button", { name: "Deactivate" }).click();
  await expect(row.getByRole("cell", { name: "Inactive" })).toBeVisible();
});
