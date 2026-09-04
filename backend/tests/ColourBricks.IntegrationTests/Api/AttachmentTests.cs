using System.Net;
using System.Net.Http.Headers;
using System.Text;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P2-T08 — Document attachments (BRD §67).</summary>
public sealed class AttachmentTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static readonly byte[] PdfBytes =
        [.. "%PDF-1.4"u8.ToArray(), .. new byte[64]];

    private static readonly byte[] ExeBytes =
        [0x4D, 0x5A, 0x90, 0x00, 0x03, 0x00, 0x00, 0x00, .. new byte[64]];

    private static MultipartFormDataContent Form(
        string ownerType, long ownerId, byte[] bytes, string fileName, string contentType)
    {
        var content = new MultipartFormDataContent
        {
            { new StringContent(ownerType), "ownerType" },
            { new StringContent(ownerId.ToString()), "ownerId" },
        };
        var file = new ByteArrayContent(bytes);
        file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        content.Add(file, "file", fileName);
        return content;
    }

    [Fact]
    public async Task Upload_DisallowedExtension_Returns400()
    {
        HttpResponseMessage response = await Admin.PostAsync(
            "/api/v1/attachments", Form("VendorPurchase", 1, PdfBytes, "notes.txt", "text/plain"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_MismatchedMagicBytes_Returns400()
    {
        // An executable renamed to .pdf.
        HttpResponseMessage response = await Admin.PostAsync(
            "/api/v1/attachments", Form("VendorPurchase", 1, ExeBytes, "invoice.pdf", "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Upload_OversizeFile_Returns413()
    {
        byte[] big = [.. "%PDF-1.4"u8.ToArray(), .. new byte[70_000]]; // cap is 65,536 in the test factory

        HttpResponseMessage response = await Admin.PostAsync(
            "/api/v1/attachments", Form("VendorPurchase", 1, big, "big.pdf", "application/pdf"));

        response.StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }

    [Fact]
    public async Task Download_WithoutOwnerPermission_Returns403()
    {
        HttpResponseMessage upload = await Admin.PostAsync(
            "/api/v1/attachments", Form("VendorPurchase", 42, PdfBytes, "invoice.pdf", "application/pdf"));
        upload.StatusCode.Should().Be(HttpStatusCode.Created);
        string location = upload.Headers.Location!.ToString();

        // A user who can see project income but not materials.
        HttpClient other = Factory.CreateClientAs(userId: 2, permissions: "project_income.view");
        HttpResponseMessage download = await other.GetAsync(location);

        download.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // The owner-permitted user can download it.
        HttpClient allowed = Factory.CreateClientAs(userId: 3, permissions: "materials.view");
        (await allowed.GetAsync(location)).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Storage_NotServedStatically()
    {
        HttpResponseMessage response = await Admin.GetAsync("/storage/VendorPurchase/2026/05/anything.pdf");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
