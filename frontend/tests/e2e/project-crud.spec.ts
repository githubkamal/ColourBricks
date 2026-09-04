import { expect, test } from "@playwright/test";

async function signInAsAdmin(page: import("@playwright/test").Page) {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");
}

test("creates a project and sees it in the list", async ({ page }) => {
  await signInAsAdmin(page);

  const name = `Riverside Towers ${Date.now()}`;

  await page.goto("/projects");
  await page.getByRole("link", { name: "New project" }).click();
  await expect(page).toHaveURL(/\/projects\/new$/);

  await page.getByLabel("Name").fill(name);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("10000000");
  await page.getByLabel("Estimated cost").fill("8000000");
  await page.getByRole("button", { name: "Create project" }).click();

  // Lands on the detail page.
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  await expect(page.getByRole("heading", { name })).toBeVisible();
  await expect(page.getByText(/^CB-\d{4}-\d{3}$/)).toBeVisible();

  // And appears in the list.
  await page.getByRole("link", { name: "Back to projects" }).click();
  await expect(page).toHaveURL(/\/projects$/);
  await expect(page.getByRole("link", { name })).toBeVisible();
});

test("rejects an end date before the start date", async ({ page }) => {
  await signInAsAdmin(page);
  await page.goto("/projects/new");

  await page.getByLabel("Name").fill("Bad Dates");
  await page.getByLabel("Start date").fill("2026-06-01");
  await page.getByLabel("Expected completion").fill("2026-05-01");
  await page.getByLabel("Contract value").fill("1000000");
  await page.getByLabel("Estimated cost").fill("900000");
  await page.getByRole("button", { name: "Create project" }).click();

  await expect(page.getByText("Cannot be before the start date")).toBeVisible();
  await expect(page).toHaveURL(/\/projects\/new$/);
});
