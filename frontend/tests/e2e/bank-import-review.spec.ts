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

test("staged bank import: map rows to projects, remove junk, commit", async ({ page, request }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await request.post("http://localhost:5095/api/v1/auth/login", {
    data: { email: "admin@colourbricks.local", password: "Admin!23456" },
  });

  const stamp = Date.now();
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
  await project("Import A");
  await project("Import B");

  const accounts = await api<{ id: number; name: string }[]>(
    request,
    "get",
    "/accounts?includeInactive=true",
  );
  const hdfc = accounts.find((a) => a.name === "HDFC")!.id;

  const batch = await api<{ id: number; rows: { id: number; sourceLineNo: number }[] }>(
    request,
    "post",
    "/bank-imports",
    {
      accountId: hdfc,
      fileName: `stmt-${stamp}.csv`,
      rows: [
        {
          sourceLineNo: 1,
          valueDate: "2026-05-01",
          narration: "NEFT MULTI VENDOR",
          debit: 100000,
          credit: 0,
        },
        {
          sourceLineNo: 2,
          valueDate: "2026-05-02",
          narration: "RTGS CLIENT RECEIPT",
          debit: 0,
          credit: 500000,
        },
        {
          sourceLineNo: 3,
          valueDate: "2026-05-03",
          narration: "ATM WITHDRAWAL",
          debit: 8000,
          credit: 0,
        },
      ],
    },
  );

  await page.goto(`/reconciliation/imports/${batch.id}`);
  await expect(page.getByTestId("import-counts")).toContainText("Total: 3");

  // Line 1: split ₹1,00,000 across A (25k) and B (75k).
  const line1 = page.getByRole("row", { name: /NEFT MULTI VENDOR/ });
  await page.getByLabel("Line 1 project 1").selectOption({ label: `Import A ${stamp}` });
  await page.getByLabel("Line 1 amount 1").fill("25000");
  await line1.getByRole("button", { name: "+ project" }).click();
  await page.getByLabel("Line 1 project 2").selectOption({ label: `Import B ${stamp}` });
  await page.getByLabel("Line 1 amount 2").fill("75000");
  await page
    .getByRole("row", { name: /NEFT MULTI VENDOR/ })
    .getByRole("button", { name: "Save mapping" })
    .click();
  await expect(page.getByText("Line 1 mapped")).toBeVisible();

  // Line 2: single project (credit).
  await page.getByLabel("Line 2 project 1").selectOption({ label: `Import A ${stamp}` });
  await page
    .getByRole("row", { name: /RTGS CLIENT RECEIPT/ })
    .getByRole("button", { name: "Save mapping" })
    .click();
  await expect(page.getByText("Line 2 mapped")).toBeVisible();

  // Line 3: junk — remove it.
  await page
    .getByRole("row", { name: /ATM WITHDRAWAL/ })
    .getByRole("button", { name: "Remove" })
    .click();
  await expect(page.getByText("Line 3 removed")).toBeVisible();

  await page.getByRole("button", { name: /Commit 2 transaction/ }).click();
  await expect(page.getByText(/Committed 2 transaction\(s\)/)).toBeVisible();

  const after = await api<{ status: string; counts: { committed: number; removed: number } }>(
    request,
    "get",
    `/bank-imports/${batch.id}`,
  );
  expect(after.status).toBe("Committed");
  expect(after.counts.committed).toBe(2);
  expect(after.counts.removed).toBe(1);
});
