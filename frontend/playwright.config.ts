import { defineConfig, devices } from "@playwright/test";

/**
 * Runs the smoke journey against a live stack: the .NET API on :5095 (self-seeds the
 * Administrator) and the Next dev server on :3000 (plan.md P0-T09).
 */
export default defineConfig({
  testDir: "./tests/e2e",
  // Serial: the whole suite shares one dev API + database.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  reporter: process.env.CI ? "github" : "list",
  use: {
    baseURL: process.env.E2E_BASE_URL ?? "http://localhost:3000",
    trace: "on-first-retry",
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      command: "dotnet run --no-launch-profile --urls http://localhost:5095",
      // cwd = the API project so appsettings.Development.json (→ colourbricks) resolves
      // regardless of where `playwright test` is invoked from.
      cwd: "../backend/src/ColourBricks.Api",
      url: "http://localhost:5095/health",
      timeout: 180_000,
      reuseExistingServer: false,
      env: { ASPNETCORE_ENVIRONMENT: "Development" },
    },
    {
      command: "npm run dev",
      url: "http://localhost:3000",
      timeout: 120_000,
      reuseExistingServer: false,
    },
  ],
});
