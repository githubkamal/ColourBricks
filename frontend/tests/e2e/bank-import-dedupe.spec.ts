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

test("re-importing an already-committed statement shows every row as a duplicate", async ({
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
  const projectId = (
    await api<{ id: number }>(request, "post", "/projects", {
      name: `Dedupe UI ${stamp}`,
      status: "Ongoing",
      startDate: "2026-04-01",
      expectedEndDate: "2027-03-31",
      contractValue: 10000000,
      estimatedCost: 8000000,
    })
  ).id;
  const accounts = await api<{ id: number; name: string }[]>(
    request,
    "get",
    "/accounts?includeInactive=true",
  );
  const hdfc = accounts.find((a) => a.name === "HDFC")!.id;

  const rows = [
    {
      sourceLineNo: 1,
      valueDate: "2026-08-01",
      narration: `NEFT DEDUPE ${stamp}`,
      debit: 25000,
      credit: 0,
    },
  ];

  const first = await api<{ id: number; rows: { id: number }[] }>(
    request,
    "post",
    "/bank-imports",
    {
      accountId: hdfc,
      fileName: `d-${stamp}.csv`,
      rows,
    },
  );
  const mapped = await request.put(
    `http://localhost:5095/api/v1/bank-imports/${first.id}/rows/${first.rows[0].id}/allocations`,
    { data: { allocations: [{ projectId, amount: 25000 }] } },
  );
  expect(mapped.ok(), await mapped.text()).toBeTruthy();
  await api(request, "post", `/bank-imports/${first.id}/commit`);

  const second = await api<{ id: number }>(request, "post", "/bank-imports", {
    accountId: hdfc,
    fileName: `d-${stamp}-again.csv`,
    rows,
  });

  await page.goto(`/reconciliation/imports/${second.id}`);
  await expect(page.getByTestId("import-counts")).toContainText("Duplicate: 1");
  await expect(page.getByRole("row", { name: new RegExp(`NEFT DEDUPE ${stamp}`) })).toContainText(
    "Duplicate",
  );
  await expect(page.getByRole("button", { name: /Commit 0 transaction/ })).toBeDisabled();
});
