import { expect, test } from "@playwright/test";

test("records a work entry and two partial payments, outstanding falls to ₹25,000", async ({
  page,
}) => {
  page.on("dialog", (d) => d.accept());

  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  const stamp = Date.now();

  // A team under the seeded Electrical department.
  await page.goto("/labour/teams");
  await page.getByLabel("Team name").fill(`Wiring Crew ${stamp}`);
  await page.getByLabel("Department").selectOption({ label: "Electrical" });
  await page.getByRole("button", { name: "Add team" }).click();
  await expect(page.getByText(`Wiring Crew ${stamp} added`)).toBeVisible();

  // A project.
  await page.goto("/projects/new");
  await page.getByLabel("Name").fill(`Labour Project ${stamp}`);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("5000000");
  await page.getByLabel("Estimated cost").fill("4000000");
  await page.getByRole("button", { name: "Create project" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);
  const projectId = page.url().match(/\/projects\/(\d+)$/)![1];

  await page.goto("/labour/work");
  await page.getByLabel("Project").selectOption(projectId);
  await page.getByLabel("Team").selectOption({ label: `Wiring Crew ${stamp}` });
  await page.getByLabel("Work date").fill("2026-05-01");
  await page.getByLabel("Agreed work value").fill("100000");
  await page.getByLabel("Work type").fill("Wiring");
  await page.getByRole("button", { name: "Record work entry" }).click();

  await expect(page.getByText("Outstanding ₹1,00,000.00")).toBeVisible();

  for (const amount of ["30000", "45000"]) {
    await page.getByRole("button", { name: "Add payment" }).click();
    await page.getByLabel(/Pay date/).fill("2026-05-05");
    await page.getByLabel(/Pay amount/).fill(amount);
    await page.getByLabel(/Pay mode|Payment mode/).selectOption({ label: "Cash" });
    await page.getByLabel(/Pay account/).selectOption({ label: "Office Cash" });
    await page.getByRole("button", { name: "Save payment" }).click();
    await expect(page.getByText("Payment recorded")).toBeVisible();
  }

  await expect(page.getByText("Outstanding ₹25,000.00")).toBeVisible();
});
