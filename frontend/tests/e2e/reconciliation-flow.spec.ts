import { expect, test, type APIRequestContext } from "@playwright/test";

const BASE = "http://localhost:5095/api/v1";

async function jpost<T>(request: APIRequestContext, path: string, body?: unknown) {
  const res = await request.post(`${BASE}${path}`, body ? { data: body } : undefined);
  expect(res.ok(), `POST ${path} -> ${res.status()}: ${await res.text()}`).toBeTruthy();
  return (await res.json()) as T;
}
async function jput(request: APIRequestContext, path: string, body: unknown) {
  const res = await request.put(`${BASE}${path}`, { data: body });
  expect(res.ok(), `PUT ${path} -> ${res.status()}: ${await res.text()}`).toBeTruthy();
}
async function jget<T>(request: APIRequestContext, path: string) {
  const res = await request.get(`${BASE}${path}`);
  expect(res.ok(), `GET ${path} -> ${res.status()}`).toBeTruthy();
  return (await res.json()) as T;
}

test("reconcile a multi-project bank debit from the queue", async ({ page, request }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");
  await request.post(`${BASE}/auth/login`, {
    data: { email: "admin@colourbricks.local", password: "Admin!23456" },
  });

  const stamp = Date.now();
  const vendor = `Recon Vendor ${stamp}`;
  const vendorId = (
    await jpost<{ id: number }>(request, "/parties?confirm=true", {
      name: vendor,
      types: ["Vendor"],
    })
  ).id;

  const project = async (n: string) =>
    (
      await jpost<{ id: number }>(request, "/projects", {
        name: `Recon ${n} ${stamp}`,
        status: "Ongoing",
        startDate: "2026-04-01",
        expectedEndDate: "2027-03-31",
        contractValue: 10000000,
        estimatedCost: 8000000,
      })
    ).id;
  const [a, b, c, d] = [
    await project("A"),
    await project("B"),
    await project("C"),
    await project("D"),
  ];
  const amounts: Record<number, number> = { [a]: 25000, [b]: 10000, [c]: 30000, [d]: 50000 };
  for (const p of [a, b, c, d]) {
    await jpost(request, "/vendor-purchases", {
      projectId: p,
      vendorId,
      date: "2026-05-01",
      total: amounts[p],
      lines: [{ itemName: "X", quantity: 1, unit: "Nos", rate: amounts[p], taxAmount: 0 }],
    });
  }

  const accounts = await jget<{ id: number; name: string }[]>(
    request,
    "/accounts?includeInactive=true",
  );
  const hdfc = accounts.find((x) => x.name === "HDFC")!.id;

  // Import + map + commit a ₹1,00,000 debit split A25/B10/C30/D35 (BRD §20 FIFO).
  const batch = await jpost<{ id: number; rows: { id: number }[] }>(request, "/bank-imports", {
    accountId: hdfc,
    fileName: `r-${stamp}.csv`,
    rows: [
      {
        sourceLineNo: 1,
        valueDate: "2026-05-10",
        narration: "NEFT DR-RECON VENDOR",
        debit: 100000,
        credit: 0,
      },
    ],
  });
  await jput(request, `/bank-imports/${batch.id}/rows/${batch.rows[0].id}/allocations`, {
    allocations: [
      { projectId: a, amount: 25000 },
      { projectId: b, amount: 10000 },
      { projectId: c, amount: 30000 },
      { projectId: d, amount: 35000 },
    ],
  });
  await jpost(request, `/bank-imports/${batch.id}/commit`);

  const queue = await jget<{ items: { id: number; description: string }[] }>(
    request,
    `/reconciliation?accountId=${hdfc}&status=Pending`,
  );
  const txId = queue.items.find((x) => x.description === "NEFT DR-RECON VENDOR")!.id;

  await page.goto("/reconciliation");
  await page.getByLabel("Account").selectOption({ label: "HDFC" });

  // BRD §39 queue: the row shows the import-review split and a zero difference.
  const row = page.getByRole("row", { name: /NEFT DR-RECON VENDOR/ });
  await expect(row).toContainText("4 Projects");
  await expect(row).toContainText("Pending");
  for (const col of [
    "Date",
    "Description",
    "Type",
    "Debit",
    "Project(s)",
    "Difference",
    "Action",
  ]) {
    await expect(page.getByRole("columnheader", { name: col, exact: true })).toBeVisible();
  }
  await row.getByRole("button", { name: "Reconcile" }).click();
  await expect(page.getByTestId(`difference-${txId}`)).toBeVisible();

  // The debit reconcile itself (P4-T07) — driven via the API the panel calls.
  await jpost(request, `/bank-transactions/${txId}/reconcile-debit`, {
    vendorId,
    allocations: [
      { projectId: a, amount: 25000 },
      { projectId: b, amount: 10000 },
      { projectId: c, amount: 30000 },
      { projectId: d, amount: 35000 },
    ],
  });

  await page.reload();
  await page.getByLabel("Account").selectOption({ label: "HDFC" });
  await page.getByLabel("Status").selectOption({ label: "Reconciled" });
  await expect(page.getByRole("row", { name: /NEFT DR-RECON VENDOR/ })).toContainText("Reconciled");

  const outstanding = await jget<{ outstanding: number }>(
    request,
    `/vendors/${vendorId}/outstanding`,
  );
  expect(outstanding.outstanding).toBe(15000); // 115k purchases − 100k reconciled
});
