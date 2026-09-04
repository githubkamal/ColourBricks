import { expect, test } from "@playwright/test";

test("pays a vendor for one project and outstanding drops by the payment", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  const stamp = Date.now();

  await page.goto("/projects/new");
  await page.getByLabel("Name").fill(`Vendor Pay ${stamp}`);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("10000000");
  await page.getByLabel("Estimated cost").fill("8000000");
  await page.getByRole("button", { name: "Create project" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const projectId = page.url().match(/\/projects\/(\d+)$/)![1];

  // A ₹2,00,000 purchase via the materials screen.
  await page.goto("/materials/purchases");
  await page.getByLabel("Project").selectOption(projectId);
  const vendor = `Pay Flow Vendor ${stamp}`;
  await page.getByLabel("Vendor").fill(vendor);
  await page.getByRole("button", { name: /Add .* as a vendor/ }).click();
  const anyway = page.getByRole("button", { name: /Create .* anyway/ });
  if (await anyway.isVisible().catch(() => false)) await anyway.click();
  await page.getByLabel("Purchase date").fill("2026-05-01");
  await page.getByLabel("itemName 1").fill("Steel");
  await page.getByLabel("quantity 1").fill("1");
  await page.getByLabel("unit 1").fill("Nos");
  await page.getByLabel("rate 1").fill("200000");
  await page.getByRole("button", { name: "Record purchase" }).click();
  await expect(page.getByText(/Current vendor outstanding: ₹2,00,000\.00/)).toBeVisible();

  // Pay ₹50,000.
  await page.goto("/vendors/payments");
  await page.getByLabel("Vendor").fill(vendor);
  await page.getByRole("button", { name: vendor, exact: false }).first().click();
  await expect(page.getByText(/total outstanding ₹2,00,000\.00/)).toBeVisible();

  await page.getByLabel("Date").fill("2026-05-10");
  await page.getByLabel("Amount").fill("50000");
  await page.getByLabel("Payment mode").selectOption({ label: "Cash" });
  await page.getByLabel("Account").selectOption({ label: "Office Cash" });
  await page.getByRole("button", { name: "Record payment" }).click();

  await expect(page.getByText(/total outstanding ₹1,50,000\.00/)).toBeVisible();
});
