import { expect, test } from "@playwright/test";

test("reproduces the BRD §26 donation example split across two temples", async ({ page }) => {
  // "Temple North" / "Temple South" are near-duplicates by edit distance — accept the confirm.
  page.on("dialog", (dialog) => dialog.accept());

  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  const stamp = Date.now();

  // A ₹1 crore project.
  await page.goto("/projects/new");
  await page.getByLabel("Name").fill(`Crore Project ${stamp}`);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("10000000");
  await page.getByLabel("Estimated cost").fill("8000000");
  await page.getByRole("button", { name: "Create project" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const projectId = page.url().match(/\/projects\/(\d+)$/)![1];

  // Two temples.
  await page.goto("/donations/temples");
  for (const temple of [`Temple North ${stamp}`, `Temple South ${stamp}`]) {
    await page.getByLabel("Temple name").fill(temple);
    await page.getByRole("button", { name: "Add", exact: true }).click();
    await expect(page.getByRole("cell", { name: temple })).toBeVisible();
  }

  // Configure the donation: 2% split ₹1,00,000 to each temple.
  await page.goto("/donations");
  await page.getByLabel("Project").selectOption(projectId);
  await page.getByLabel("Percentage of contract value").fill("2");
  await expect(page.getByTestId("donation-amount")).toHaveText("₹2,00,000.00");

  await page.getByLabel("Temple 1").selectOption({ label: `Temple North ${stamp}` });
  await page.getByLabel("Amount 1").fill("100000");
  await page.getByRole("button", { name: "+ Add temple" }).click();
  await page.getByLabel("Temple 2").selectOption({ label: `Temple South ${stamp}` });
  await page.getByLabel("Amount 2").fill("100000");

  await page.getByRole("button", { name: "Save donation" }).click();
  await expect(page.getByText("Donation set: ₹2,00,000.00")).toBeVisible();
});
