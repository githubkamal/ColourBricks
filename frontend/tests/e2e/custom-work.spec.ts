import { expect, test } from "@playwright/test";

test("records client-requested extra electrical work with an over-estimate variance", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  const stamp = Date.now();
  await page.goto("/projects/new");
  await page.getByLabel("Name").fill(`Custom Work ${stamp}`);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("5000000");
  await page.getByLabel("Estimated cost").fill("4000000");
  await page.getByRole("button", { name: "Create project" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const projectId = page.url().match(/\/projects\/(\d+)$/)![1];

  await page.goto("/expenses/customized");
  await page.getByLabel("Project").selectOption(projectId);

  await page.getByLabel("Work date").fill("2026-05-01");
  await page.getByLabel("Work type").fill("Additional electrical work");
  await page.getByLabel("Estimated cost").fill("70000");
  await page.getByLabel("Actual cost").fill("85000");
  await expect(page.getByTestId("variance-preview")).toHaveText(/₹15,000\.00/);
  await page.getByRole("button", { name: "Record custom work" }).click();

  await expect(page.getByRole("cell", { name: "₹85,000.00" })).toBeVisible();
  await expect(page.getByRole("cell", { name: "₹15,000.00" })).toBeVisible();
});
