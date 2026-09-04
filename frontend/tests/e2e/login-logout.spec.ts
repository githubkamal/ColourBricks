import { expect, test } from "@playwright/test";

/**
 * P0-T08 / P0-T09 — the login → dashboard → logout journey.
 * Playwright is configured and run from P0-T09 (browsers, seeded stack). This spec
 * assumes the seeded Administrator (`admin@colourbricks.local`).
 */
test("signs in, lands on the dashboard, signs out", async ({ page }) => {
  await page.goto("/login");

  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();

  await expect(page).toHaveURL("/");
  await expect(page.getByRole("heading", { name: "Dashboard" })).toBeVisible();
  // Administrator sees the Administration section.
  await expect(page.getByText("Administration")).toBeVisible();

  await page.getByRole("button", { name: "Sign out" }).click();
  await expect(page).toHaveURL(/\/login$/);
});
