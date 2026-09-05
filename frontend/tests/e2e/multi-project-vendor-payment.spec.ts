import { expect, test, type APIRequestContext, type Page } from "@playwright/test";

function escapeRe(s: string): string {
  return s.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

async function api<T>(
  request: APIRequestContext,
  method: "get" | "post",
  path: string,
  body?: unknown,
) {
  const res = await request[method](
    `http://localhost:5095/api/v1${path}`,
    body ? { data: body } : undefined,
  );
  expect(
    res.ok(),
    `${method.toUpperCase()} ${path} -> ${res.status()}: ${await res.text()}`,
  ).toBeTruthy();
  return (await res.json()) as T;
}

async function pickVendor(page: Page, name: string) {
  await page.getByLabel("Vendor").fill(name);
  await page.waitForTimeout(400);
  await page
    .getByRole("button", { name: new RegExp(`${escapeRe(name)} · Vendor`) })
    .first()
    .click();
}

test("reproduces the BRD §20 FIFO allocation and applies it", async ({ page, request }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  // Log the API request context in with the same credentials so it can seed data.
  await request.post("http://localhost:5095/api/v1/auth/login", {
    data: { email: "admin@colourbricks.local", password: "Admin!23456" },
  });

  const stamp = Date.now();
  const vendor = `ABC Hardware ${stamp}`;
  const vendorId = (
    await api<{ id: number }>(request, "post", "/parties?confirm=true", {
      name: vendor,
      types: ["Vendor"],
    })
  ).id;

  const setup: [string, number, string][] = [
    ["A", 25000, "2026-05-01"],
    ["B", 10000, "2026-05-02"],
    ["C", 30000, "2026-05-03"],
    ["D", 50000, "2026-05-04"],
  ];
  for (const [name, amount, date] of setup) {
    const project = await api<{ id: number }>(request, "post", "/projects", {
      name: `§20 Project ${name} ${stamp}`,
      status: "Ongoing",
      startDate: "2026-04-01",
      expectedEndDate: "2027-03-31",
      contractValue: 10000000,
      estimatedCost: 8000000,
    });
    await api(request, "post", "/vendor-purchases", {
      projectId: project.id,
      vendorId,
      date,
      total: amount,
      lines: [{ itemName: "Material", quantity: 1, unit: "Nos", rate: amount, taxAmount: 0 }],
    });
  }

  await page.goto("/vendors/allocation");
  await pickVendor(page, vendor);

  await page.getByLabel("Payment amount").fill("100000");
  await page.getByRole("button", { name: "Propose FIFO allocation" }).click();

  // Project D: ₹35,000 allocated, ₹15,000 left; A/B/C fully settled.
  // Admin can override, so the allocated column renders as editable inputs.
  await expect(page.getByLabel(new RegExp(`Allocated §20 Project D ${stamp}`))).toHaveValue(
    "35000",
  );
  await expect(page.getByRole("cell", { name: "₹15,000.00" })).toBeVisible();
  await expect(page.getByTestId("total-allocated")).toHaveText("₹1,00,000.000");
  await expect(page.getByTestId("difference")).toHaveText(/₹0\.00/);

  await page.getByLabel("Date").fill("2026-05-10");
  await page.getByLabel("Payment mode").selectOption({ label: "Cash" });
  await page.getByLabel("Account").selectOption({ label: "Office Cash" });
  await page.getByRole("button", { name: "Apply payment" }).click();

  await expect(page.getByText(/Payment applied across 4 project/)).toBeVisible();

  const outstanding = await api<{ outstanding: number }>(
    request,
    "get",
    `/vendors/${vendorId}/outstanding`,
  );
  expect(outstanding.outstanding).toBe(15000);
});
