import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { SystemSettingsPage } from "./system-settings-page";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

const baseSettings = {
  id: 1,
  companyName: "Colour Bricks",
  companyAddress: null,
  companyGstin: null,
  companyLogoUrl: null,
  companyLogoAttachmentId: null,
  vendorOutstandingAlertLimit: 500_000,
  overdueAlertDays: 30,
  profitFloorAlertPercent: 10,
  loanEmiReminderDaysAhead: 7,
  concurrencyStamp: "stamp-1",
};

describe("SystemSettingsPage", () => {
  it("SystemSettingsPage_UploadingALogo_StagesTheAttachmentIdUntilSaved", async () => {
    let savedBody: Record<string, unknown> | null = null;
    server.use(
      http.get(`${apiBaseUrl}/admin/settings`, () => HttpResponse.json(baseSettings)),
      http.post(`${apiBaseUrl}/attachments`, () =>
        HttpResponse.json({
          id: 42,
          ownerType: "SystemSettings",
          ownerId: 1,
          originalFileName: "logo.png",
          contentType: "image/png",
          sizeBytes: 100,
          uploadedAtUtc: "2026-09-05T00:00:00Z",
          uploadedByUserId: 1,
        }),
      ),
      http.put(`${apiBaseUrl}/admin/settings`, async ({ request }) => {
        savedBody = (await request.json()) as Record<string, unknown>;
        return HttpResponse.json({ ...baseSettings, ...savedBody, concurrencyStamp: "stamp-2" });
      }),
    );

    renderWithClient(<SystemSettingsPage />);

    const fileInput = await screen.findByLabelText("Upload a logo");
    const file = new File(["fake"], "logo.png", { type: "image/png" });
    fireEvent.change(fileInput, { target: { files: [file] } });

    // Uploading swaps the file picker for a preview + Remove button.
    expect(await screen.findByAltText("Company logo")).toBeInTheDocument();
    expect(screen.getByAltText("Company logo")).toHaveAttribute("src", "/api/attachments/42");

    fireEvent.click(screen.getByRole("button", { name: "Save settings" }));

    await waitFor(() => expect(savedBody).not.toBeNull());
    expect(savedBody).toMatchObject({ companyLogoAttachmentId: 42, companyLogoUrl: null });
  });
});
