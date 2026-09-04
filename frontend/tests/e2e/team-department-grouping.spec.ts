import { expect, test } from "@playwright/test";

test("new teams appear grouped under their department in the picker", async ({ page }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await page.goto("/labour/teams");

  const stamp = Date.now();
  const electricalCrew = `Electrical Crew ${stamp}`;
  const plumbingCrew = `Plumbing Crew ${stamp}`;

  // Electrical / Plumbing are seeded departments (BRD §8).
  await page.getByLabel("Team name").fill(electricalCrew);
  await page.getByLabel("Department").selectOption({ label: "Electrical" });
  await page.getByRole("button", { name: "Add team" }).click();
  await expect(page.getByText(`${electricalCrew} added`)).toBeVisible();

  await page.getByLabel("Team name").fill(plumbingCrew);
  await page.getByLabel("Department").selectOption({ label: "Plumbing" });
  await page.getByRole("button", { name: "Add team" }).click();
  await expect(page.getByText(`${plumbingCrew} added`)).toBeVisible();

  // The grouped picker files each team under its department's <optgroup>.
  const picker = page.getByLabel("Pick a team");
  await expect(
    picker.locator('optgroup[label="Electrical"] option', { hasText: electricalCrew }),
  ).toHaveCount(1);
  await expect(
    picker.locator('optgroup[label="Plumbing"] option', { hasText: plumbingCrew }),
  ).toHaveCount(1);
});
