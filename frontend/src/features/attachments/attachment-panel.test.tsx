import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { render, screen } from "@testing-library/react";
import { http, HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { apiBaseUrl } from "@/lib/config";
import { server } from "@/test/msw/server";
import { AttachmentPanel } from "./attachment-panel";

function renderWithClient(ui: React.ReactElement) {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={client}>{ui}</QueryClientProvider>);
}

describe("AttachmentPanel", () => {
  it("AttachmentPanel_ListsFilesAndOffersAnUploadInput", async () => {
    server.use(
      http.get(`${apiBaseUrl}/attachments`, ({ request }) => {
        const url = new URL(request.url);
        expect(url.searchParams.get("ownerType")).toBe("VendorPurchase");
        expect(url.searchParams.get("ownerId")).toBe("7");
        return HttpResponse.json([
          {
            id: 1,
            ownerType: "VendorPurchase",
            ownerId: 7,
            originalFileName: "invoice.pdf",
            contentType: "application/pdf",
            sizeBytes: 2048,
            uploadedAtUtc: "2026-05-01T00:00:00Z",
            uploadedByUserId: 1,
          },
        ]);
      }),
    );

    renderWithClient(<AttachmentPanel ownerType="VendorPurchase" ownerId={7} />);

    expect(await screen.findByRole("link", { name: "invoice.pdf" })).toHaveAttribute(
      "href",
      `${apiBaseUrl}/attachments/1`,
    );
    expect(screen.getByLabelText("Attach a file")).toBeInTheDocument();
  });
});
