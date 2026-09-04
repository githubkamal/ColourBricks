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

test("an authorised user overrides the FIFO allocation with a reason", async ({
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
  const vendor = `Override Vendor ${stamp}`;
  const vendorId = (
    await api<{ id: number }>(request, "post", "/parties?confirm=true", {
      name: vendor,
      types: ["Vendor"],
    })
  ).id;

  const projectIds: number[] = [];
  const setup: [number, string][] = [
    [25000, "2026-05-01"],
    [10000, "2026-05-02"],
    [30000, "2026-05-03"],
    [50000, "2026-05-04"],
  ];
  for (const [amount, date] of setup) {
    const p = await api<{ id: number }>(request, "post", "/projects", {
      name: `Ovr P${projectIds.length} ${stamp}`,
      status: "Ongoing",
      startDate: "2026-04-01",
      expectedEndDate: "2027-03-31",
      contractValue: 10000000,
      estimatedCost: 8000000,
    });
    projectIds.push(p.id);
    await api(request, "post", "/vendor-purchases", {
      projectId: p.id,
      vendorId,
      date,
      total: amount,
      lines: [{ itemName: "X", quantity: 1, unit: "Nos", rate: amount, taxAmount: 0 }],
    });
  }
  const [, , c, d] = projectIds;

  await page.goto("/vendors/allocation");
  await page.getByLabel("Vendor").fill(vendor);
  await page.waitForTimeout(400);
  await page
    .getByRole("button", {
      name: new RegExp(`${vendor.replace(/[.*+?^${}()|[\]\\]/g, "\\$&")} · Vendor`),
    })
    .first()
    .click();

  await page.getByLabel("Payment amount").fill("100000");
  await page.getByRole("button", { name: "Propose FIFO allocation" }).click();
  await expect(page.getByTestId("total-allocated")).toHaveText("₹1,00,000.00");

  // FIFO gave C ₹30,000 / D ₹35,000; move ₹5,000 from C to D.
  await page.getByLabel(/Allocated Ovr P2/).fill("25000");
  await page.getByLabel(/Allocated Ovr P3/).fill("40000");
  await expect(page.getByTestId("difference")).toHaveText(/₹0\.00/);

  await page.getByLabel("Override reason").fill("client asked to prioritise the last project");
  await page.getByLabel("Date").fill("2026-05-10");
  await page.getByLabel("Payment mode").selectOption({ label: "Cash" });
  await page.getByLabel("Account").selectOption({ label: "Office Cash" });
  await page.getByRole("button", { name: "Apply override" }).click();

  await expect(page.getByText(/Payment applied across/)).toBeVisible();

  const summary = await api<{ byProject: { projectId: number; outstanding: number }[] }>(
    request,
    "get",
    `/vendors/${vendorId}/outstanding-summary`,
  );
  const byProject = Object.fromEntries(summary.byProject.map((x) => [x.projectId, x.outstanding]));
  expect(byProject[c]).toBe(5000);
  expect(byProject[d]).toBe(10000);
});
