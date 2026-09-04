import { expect, test } from "@playwright/test";

test("pays a temple donation and its outstanding falls", async ({ page }) => {
  page.on("dialog", (d) => d.accept());

  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  const stamp = Date.now();

  await page.goto("/projects/new");
  await page.getByLabel("Name").fill(`Donation Pay ${stamp}`);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("10000000");
  await page.getByLabel("Estimated cost").fill("8000000");
  await page.getByRole("button", { name: "Create project" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const projectId = page.url().match(/\/projects\/(\d+)$/)![1];

  await page.goto("/donations/temples");
  const temple = `Temple Solo ${stamp}`;
  await page.getByLabel("Temple name").fill(temple);
  await page.getByRole("button", { name: "Add", exact: true }).click();
  await expect(page.getByRole("cell", { name: temple })).toBeVisible();

  await page.goto("/donations");
  await page.getByLabel("Project").selectOption(projectId);
  await page.getByLabel("Percentage of contract value").fill("2");
  await page.getByLabel("Temple 1").selectOption({ label: temple });
  await page.getByLabel("Amount 1").fill("200000");
  await page.getByRole("button", { name: "Save donation" }).click();
  await expect(page.getByText("Donation set: ₹2,00,000.00")).toBeVisible();

  await page.getByRole("button", { name: "Pay", exact: true }).click();
  await page.getByLabel(/Donation pay date/).fill("2026-05-10");
  await page.getByLabel(/Donation pay amount/).fill("120000");
  await page.getByLabel(/Payment mode/).selectOption({ label: "Cash" });
  await page.getByLabel(/Donation pay account/).selectOption({ label: "Office Cash" });
  await page.getByRole("button", { name: "Save payment" }).click();

  await expect(page.getByText("Donation payment recorded")).toBeVisible();
  await expect(page.getByText("₹80,000.00")).toBeVisible();
});
