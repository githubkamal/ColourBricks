import { expect, test } from "@playwright/test";

test("records the BRD §16 purchase and vendor outstanding rises to ₹70,000", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  const stamp = Date.now();
  await page.goto("/projects/new");
  await page.getByLabel("Name").fill(`Purchase Project ${stamp}`);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("10000000");
  await page.getByLabel("Estimated cost").fill("8000000");
  await page.getByRole("button", { name: "Create project" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const projectId = page.url().match(/\/projects\/(\d+)$/)![1];

  await page.goto("/materials/purchases");
  await page.getByLabel("Project").selectOption(projectId);

  const vendor = `ABC Cement Agency ${stamp}`;
  await page.getByLabel("Vendor").fill(vendor);
  await page.getByRole("button", { name: /Add .* as a vendor/ }).click();
  // Prior E2E runs may leave a near-duplicate name in the shared dev DB.
  const anyway = page.getByRole("button", { name: /Create .* anyway/ });
  if (await anyway.isVisible().catch(() => false)) await anyway.click();
  await expect(page.getByText(/Current vendor outstanding: ₹0\.00/)).toBeVisible();

  await page.getByLabel("Purchase date").fill("2026-05-01");
  await page.getByLabel("itemName 1").fill("Cement");
  await page.getByLabel("quantity 1").fill("100");
  await page.getByLabel("unit 1").fill("Bag");
  await page.getByLabel("rate 1").fill("400");

  await page.getByRole("button", { name: "+ Add line" }).click();
  await page.getByLabel("itemName 2").fill("Sand");
  await page.getByLabel("quantity 2").fill("2");
  await page.getByLabel("unit 2").fill("Load");
  await page.getByLabel("rate 2").fill("15000");

  await expect(page.getByTestId("purchase-total")).toHaveText("Total: ₹70,000.000");

  await page.getByRole("button", { name: "Record purchase" }).click();

  await expect(page.getByText(vendor, { exact: false }).first()).toBeVisible();
  await expect(page.getByText(/Current vendor outstanding: ₹70,000\.00/)).toBeVisible();
});
