import { expect, test } from "@playwright/test";

test("records a work entry with a reference number, pays it down, edits and deletes it", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  const stamp = Date.now();

  // A team under the seeded Electrical department.
  await page.goto("/labour/teams");
  await page.getByRole("button", { name: "+ New team" }).click();
  await page.getByLabel("New team name").fill(`Wiring Crew ${stamp}`);
  await page.getByLabel("Department", { exact: true }).selectOption({ label: "Electrical" });
  await page.getByRole("button", { name: "Add team" }).click();
  await expect(page.getByText(`Wiring Crew ${stamp} added`)).toBeVisible();

  // The new team opens in the master form; fill in contact and bank details.
  await page.getByLabel("Mobile number").fill("9876543210");
  await page.getByLabel("IFSC code").fill("SBIN0001234");
  await page.getByRole("button", { name: "Save team details" }).click();
  await expect(page.getByText(`Wiring Crew ${stamp} saved`)).toBeVisible();

  // A project.
  await page.goto("/projects/new");
  await page.getByLabel("Name").fill(`Labour Project ${stamp}`);
  await page.getByLabel("Start date").fill("2026-04-01");
  await page.getByLabel("Expected completion").fill("2027-03-31");
  await page.getByLabel("Contract value").fill("5000000");
  await page.getByLabel("Estimated cost").fill("4000000");
  await page.getByRole("button", { name: "Create project" }).click();
  await expect(page).toHaveURL(/\/projects\/\d+$/);

  // Team first, then work date → project → agreed value.
  await page.goto("/labour/work");
  await page.getByLabel("Team").selectOption({ label: `Wiring Crew ${stamp}` });
  await page.getByLabel("Work date").fill("2026-05-01");
  await page.getByRole("combobox", { name: "Project" }).fill(`Labour Project ${stamp}`);
  await page.getByRole("button", { name: new RegExp(`Labour Project ${stamp}`) }).click();
  await page.getByLabel("Agreed work value").fill("100000");
  await page.getByLabel("Work type").fill("Wiring");
  await page.getByRole("button", { name: "Record work entry" }).click();
  await expect(page.getByText(/Work entry LW-\d{4}-\d{5} recorded/)).toBeVisible();

  const row = page.locator("div", { hasText: `Wiring Crew ${stamp}` }).filter({
    has: page.getByRole("button", { name: "Edit" }),
  });
  await page.getByLabel("Search work entries").fill(`Wiring Crew ${stamp}`);
  await expect(row.getByText(/Outstanding ₹1,00,000/)).toBeVisible();

  await row.getByRole("button", { name: "Add payment" }).click();
  await page.getByLabel(/Pay date/).fill("2026-05-05");
  await page.getByLabel(/Pay amount/).fill("30000");
  await page.getByLabel(/Pay mode|Payment mode/).selectOption({ label: "Cash" });
  await page.getByLabel(/Pay account/).selectOption({ label: "Office Cash" });
  await page.getByRole("button", { name: "Save payment" }).click();
  await expect(page.getByText("Payment recorded")).toBeVisible();
  await expect(row.getByText(/Outstanding ₹70,000/)).toBeVisible();

  // Edit keeps the reference number and the payment.
  await row.getByRole("button", { name: "Edit" }).click();
  await page.getByLabel("Agreed work value").fill("120000");
  await page.getByRole("button", { name: "Save changes" }).click();
  await expect(page.getByText(/LW-\d{4}-\d{5} updated/)).toBeVisible();
  await expect(row.getByText(/Outstanding ₹90,000/)).toBeVisible();

  // Delete removes it (and its payment) from the list.
  await row.getByRole("button", { name: "Delete" }).click();
  await page.getByRole("button", { name: "Delete", exact: true }).last().click();
  await expect(page.getByText("No work entries match.")).toBeVisible();
});
