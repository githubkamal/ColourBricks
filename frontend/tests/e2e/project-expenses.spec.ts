import { expect, test } from "@playwright/test";

test("records a payable expense that does not touch an account", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  const stamp = Date.now();
  await page.goto("/projects/new");
  await page.getByLabel("Name").fill(`Expense Project ${stamp}`);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("5000000");
  await page.getByLabel("Estimated cost").fill("4000000");
  await page.getByRole("button", { name: "Create project" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const projectId = page.url().match(/\/projects\/(\d+)$/)![1];

  await page.goto("/project-expenses");
  await page.getByLabel("Project").selectOption(projectId);

  await page.getByLabel("Category").selectOption({ label: "Electrical" });
  await page.getByLabel("Date").fill("2026-05-01");
  await page.getByLabel("Amount").fill("12500");
  await page.getByLabel("Description").fill("Site wiring rework");
  await page.getByRole("button", { name: "Record expense" }).click();

  await expect(page.getByRole("cell", { name: "Electrical" }).first()).toBeVisible();
  await expect(page.getByRole("cell", { name: "₹12,500.00" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "Payable" })).toBeVisible();
});
