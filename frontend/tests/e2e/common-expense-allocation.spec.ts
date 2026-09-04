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

test("common expense allocation previews as a pure read, then commit posts and records history", async ({
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
  const project = async (name: string) =>
    (
      await api<{ id: number }>(request, "post", "/projects", {
        name: `${name} ${stamp}`,
        code: `CB-CEA-${stamp}-${name}`,
        status: "Ongoing",
        startDate: "2026-04-01",
        expectedEndDate: "2027-03-31",
        contractValue: 10000000,
        estimatedCost: 8000000,
      })
    ).id;

  const p1 = await project("A");
  await project("B"); // a second ongoing project so an equal split is ₹1,00,000 each

  const modes = await api<{ id: number; name: string }[]>(request, "get", "/payment-modes");
  const cash = modes.find((m) => m.name === "Cash")!.id;
  const accounts = await api<{ id: number; name: string }[]>(
    request,
    "get",
    "/accounts?includeInactive=true",
  );
  const officeCash = accounts.find((a) => a.name === "Office Cash")!.id;

  // A distinctive pool so it is unambiguous in a shared dev DB: 123456 + 76544 = 200000.
  const day = "2026-10-12";
  await api(request, "post", "/common-expenses", {
    type: "Personal",
    subCategory: "Personal purchases",
    date: day,
    amount: 123456,
    paymentModeId: cash,
    accountId: officeCash,
  });
  await api(request, "post", "/common-expenses", {
    type: "Office",
    subCategory: "Office rent",
    date: day,
    amount: 76544,
    paymentModeId: cash,
    accountId: officeCash,
  });

  const actualCost = async (id: number) =>
    (await api<{ actualCost: number }>(request, "get", `/projects/${id}/budget-vs-actual`))
      .actualCost;

  const p1Before = await actualCost(p1);

  await page.goto("/expenses/common");
  await page.getByLabel("From", { exact: true }).fill("2026-10-01");
  await page.getByLabel("To", { exact: true }).fill("2026-10-31");
  await page.getByRole("button", { name: "Preview" }).click();

  // Preview is a pure read: it shows the split and the balance check, posts nothing.
  await expect(page.getByTestId("preview-balance")).toHaveText("balances");
  await expect(page.getByText(/Pool ₹2,00,000\.00/)).toBeVisible();
  expect(await actualCost(p1)).toBe(p1Before);

  // Navigate away and back — still nothing posted.
  await page.goto("/");
  await page.goto("/expenses/common");
  expect(await actualCost(p1)).toBe(p1Before);

  await page.getByLabel("From", { exact: true }).fill("2026-10-01");
  await page.getByLabel("To", { exact: true }).fill("2026-10-31");
  await page.getByRole("button", { name: "Preview" }).click();
  await expect(page.getByTestId("preview-balance")).toHaveText("balances");
  await page.getByRole("button", { name: "Commit allocation" }).click();

  // Commit posts: each project takes an equal ₹1,00,000 share.
  await expect(page.getByRole("cell", { name: "₹2,00,000.00" }).first()).toBeVisible();
  await expect.poll(() => actualCost(p1)).toBe(p1Before + 100000);

  // History row is recorded with its method and can be reversed.
  const row = page.getByRole("row", { name: /Equal/ }).first();
  await expect(row).toBeVisible();
  await row.getByRole("button", { name: "Reverse" }).click();
  await expect.poll(() => actualCost(p1)).toBe(p1Before);
});
