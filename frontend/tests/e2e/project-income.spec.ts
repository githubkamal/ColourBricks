import { expect, test } from "@playwright/test";

test("records a client advance and it shows in the project income total", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  const stamp = Date.now();
  await page.goto("/projects/new");
  await page.getByLabel("Name").fill(`Income Project ${stamp}`);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("20000000");
  await page.getByLabel("Estimated cost").fill("16000000");
  await page.getByRole("button", { name: "Create project" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const projectId = page.url().match(/\/projects\/(\d+)$/)![1];

  await page.goto("/project-income");
  await page.getByLabel("Project").selectOption(projectId);

  await page.getByLabel("Date").fill("2026-05-01");
  await page.getByLabel("Amount").fill("1000000");
  await page.getByLabel("Payment mode").selectOption({ label: "Cash" });
  await page.getByLabel("Account").selectOption({ label: "Office Cash" });
  await page.getByRole("button", { name: "Record receipt" }).click();

  await expect(page.getByText(/Total income:/)).toHaveText(/₹10,00,000\.00/);
  await expect(page.getByRole("cell", { name: "Client Advance" })).toBeVisible();
});
