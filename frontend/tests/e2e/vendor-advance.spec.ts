import { expect, test, type APIRequestContext } from "@playwright/test";

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

test("an over-payment becomes a vendor advance and is applied to a later purchase", async ({
  page,
  request,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await request.post("http://localhost:5095/api/v1/auth/login", {
    data: { email: "admin@colourbricks.local", password: "Admin!23456" },
  });

  const stamp = Date.now();
  const vendor = `Advance Vendor ${stamp}`;
  const vendorId = (
    await api<{ id: number }>(request, "post", "/parties?confirm=true", {
      name: vendor,
      types: ["Vendor"],
    })
  ).id;

  const project = async (name: string) =>
    (
      await api<{ id: number }>(request, "post", "/projects", {
        name: `${name} ${stamp}`,
        status: "Ongoing",
        startDate: "2026-04-01",
        expectedEndDate: "2027-03-31",
        contractValue: 10000000,
        estimatedCost: 8000000,
      })
    ).id;

  const purchase = async (projectId: number, total: number, date: string) =>
    (
      await api<{ purchase: { id: number } }>(request, "post", "/vendor-purchases", {
        projectId,
        vendorId,
        date,
        total,
        lines: [{ itemName: "X", quantity: 1, unit: "Nos", rate: total, taxAmount: 0 }],
      })
    ).purchase.id;

  const modes = await api<{ id: number; name: string }[]>(request, "get", "/payment-modes");
  const cash = modes.find((m) => m.name === "Cash")!.id;
  const accounts = await api<{ id: number; name: string }[]>(
    request,
    "get",
    "/accounts?includeInactive=true",
  );
  const officeCash = accounts.find((a) => a.name === "Office Cash")!.id;

  const projectA = await project("Adv A");
  const projectB = await project("Adv B");
  await purchase(projectA, 80000, "2026-05-01");
  const purchaseB = await purchase(projectB, 50000, "2026-05-20");

  // Pay ₹1,00,000 against ₹80,000 outstanding — ₹20,000 becomes an advance.
  await api(request, "post", "/vendor-payments", {
    vendorId,
    projectId: projectA,
    date: "2026-05-10",
    amount: 100000,
    paymentModeId: cash,
    accountId: officeCash,
  });

  await page.goto("/vendors/statements");
  await page.getByLabel("Vendor").fill(vendor);
  await page.waitForTimeout(400);
  await page
    .getByRole("button", {
      name: new RegExp(`${vendor.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")} · Vendor`),
    })
    .first()
    .click();

  await expect(page.getByTestId("advance-balance")).toContainText("₹20,000.00");

  await page.getByLabel("Apply to purchase").selectOption(String(purchaseB));
  await page.getByLabel("Advance amount to apply").fill("20000");
  await page.getByRole("button", { name: "Apply advance" }).click();

  await expect(page.getByText(/Applied ₹20,000\.00 of advance/)).toBeVisible();

  const summary = await api<{ total: number; advance: number }>(
    request,
    "get",
    `/vendors/${vendorId}/outstanding-summary`,
  );
  expect(summary.advance).toBe(0);
  expect(summary.total).toBe(30000);
});
