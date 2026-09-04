import { expect, test } from "@playwright/test";

test("the data-integrity controls page reports all controls passing", async ({ page, request }) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await request.post("http://localhost:5095/api/v1/auth/login", {
    data: { email: "admin@colourbricks.local", password: "Admin!23456" },
  });

  // Some real activity so the controls have something to reconcile.
  const stamp = Date.now();
  const vendor = (
    await request.post("http://localhost:5095/api/v1/parties?confirm=true", {
      data: { name: `Integrity Vendor ${stamp}`, types: ["Vendor"] },
    })
  ).json();
  const vendorId = (await vendor).id as number;
  const project = await (
    await request.post("http://localhost:5095/api/v1/projects", {
      data: {
        name: `Integrity Project ${stamp}`,
        status: "Ongoing",
        startDate: "2026-04-01",
        expectedEndDate: "2027-03-31",
        contractValue: 5000000,
        estimatedCost: 4000000,
      },
    })
  ).json();
  await request.post("http://localhost:5095/api/v1/vendor-purchases", {
    data: {
      projectId: project.id,
      vendorId,
      date: "2026-05-01",
      total: 90000,
      lines: [{ itemName: "X", quantity: 1, unit: "Nos", rate: 90000, taxAmount: 0 }],
    },
  });

  await page.goto("/admin/integrity-check");
  await expect(page.getByTestId("integrity-summary")).toHaveText("All controls passed.");

  const report = await (
    await request.get("http://localhost:5095/api/v1/admin/integrity-check")
  ).json();
  expect(report.passed).toBe(true);
});
