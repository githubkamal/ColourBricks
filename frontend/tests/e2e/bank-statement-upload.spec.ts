import { expect, test } from "@playwright/test";

const CSV = [
  "Statement for account XXXXXX1234",
  "Period 01/05/2026 to 31/05/2026",
  "Date,Narration,Ref,Withdrawal,Deposit,Balance",
  '01/05/2026,NEFT DR-ABC HARDWARE,N1,"25,000.00",,"1,75,000.00"',
  '03/05/2026,UPI CLIENT RECEIPT,N2,,"5,00,000.00","6,75,000.00"',
].join("\n");

test("upload a statement, map its columns in the wizard, land on the review screen", async ({
  page,
}) => {
  await page.goto("/login");
  await page.getByLabel("Email").fill("admin@colourbricks.local");
  await page.getByLabel("Password").fill("Admin!23456");
  await page.getByRole("button", { name: "Sign in" }).click();
  await expect(page).toHaveURL("/");

  await page.goto("/reconciliation/upload");

  await page.getByLabel("Account").selectOption({ label: "HDFC" });
  await page.getByLabel("Statement file").setInputFiles({
    name: `hdfc-${Date.now()}.csv`,
    mimeType: "text/csv",
    buffer: Buffer.from(CSV, "utf-8"),
  });

  await page.getByLabel("Header row index").fill("2");
  await page.getByRole("button", { name: "Detect columns" }).click();

  const wizard = page.getByTestId("mapping-wizard");
  await expect(wizard).toContainText("[1] Narration");

  await wizard.getByLabel("dateColumn").selectOption("0");
  await wizard.getByLabel("narrationColumn").selectOption("1");
  await wizard.getByLabel("referenceColumn").selectOption("2");
  await wizard.getByLabel("balanceColumn").selectOption("5");
  await wizard.getByLabel("debitColumn").selectOption("3");
  await wizard.getByLabel("creditColumn").selectOption("4");
  await wizard.getByLabel("Date formats").fill("dd/MM/yyyy");
  await wizard.getByLabel("Mapping name").fill(`HDFC ${Date.now()}`);

  await wizard.getByRole("button", { name: /Save mapping & review/ }).click();

  await expect(page).toHaveURL(/\/reconciliation\/imports\/\d+$/);
  await expect(page.getByTestId("import-counts")).toContainText("Total: 2");
  await expect(page.getByRole("row", { name: /NEFT DR-ABC HARDWARE/ })).toBeVisible();
  await expect(page.getByRole("row", { name: /UPI CLIENT RECEIPT/ })).toBeVisible();
});
