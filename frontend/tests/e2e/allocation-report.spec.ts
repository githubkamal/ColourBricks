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

test("the §51 report shows how a multi-project payment was distributed", async ({
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
  const vendor = `Report Vendor ${stamp}`;
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
    api(request, "post", "/vendor-purchases", {
      projectId,
      vendorId,
      date,
      total,
      lines: [{ itemName: "X", quantity: 1, unit: "Nos", rate: total, taxAmount: 0 }],
    });

  const setup: [number, string][] = [
    [25000, "2026-05-01"],
    [10000, "2026-05-02"],
    [30000, "2026-05-03"],
    [50000, "2026-05-04"],
  ];
  for (const [amount, date] of setup) {
    await purchase(await project("Rep P"), amount, date);
  }

  const modes = await api<{ id: number; name: string }[]>(request, "get", "/payment-modes");
  const cash = modes.find((m) => m.name === "Cash")!.id;
  const accounts = await api<{ id: number; name: string }[]>(
    request,
    "get",
    "/accounts?includeInactive=true",
  );
  const officeCash = accounts.find((a) => a.name === "Office Cash")!.id;

  // A distinctive amount so this payment's total is unambiguous in a shared dev DB.
  const { settlementId } = await api<{ settlementId: number }>(
    request,
    "post",
    "/vendor-payments/allocate",
    {
      vendorId,
      date: "2026-05-10",
      amount: 97531,
      paymentModeId: cash,
      accountId: officeCash,
    },
  );

  await page.goto("/vendors/allocation-report");
  await page.getByLabel("Vendor").fill(vendor);
  await page.waitForTimeout(400);
  await page
    .getByRole("button", {
      name: new RegExp(`${vendor.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")} · Vendor`),
    })
    .first()
    .click();
  await page.getByLabel("From", { exact: true }).fill("2026-05-01");
  await page.getByLabel("To", { exact: true }).fill("2026-05-31");

  // FIFO over 25k/10k/30k/50k of ₹97,531 leaves ₹32,531 on the fourth project.
  await expect(page.getByRole("cell", { name: "₹32,531.00" })).toBeVisible();
  await expect(page.getByRole("button", { name: "₹97,531.00" })).toBeVisible();

  await page.getByRole("button", { name: "₹97,531.00" }).click();
  await expect(page.getByTestId("allocation-detail")).toContainText(`Settlement #${settlementId}`);
});
