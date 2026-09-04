using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T05 — Payment mode master (BRD §28, §70 rule 18).</summary>
public sealed class PaymentModeTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private async Task<(long Id, string Stamp)> ModeByName(HttpClient client, string name)
    {
        using JsonDocument list = JsonDocument.Parse(
            await (await client.GetAsync("/api/v1/payment-modes?includeInactive=true")).Content.ReadAsStringAsync());
        JsonElement mode = list.RootElement.EnumerateArray()
            .First(m => m.GetProperty("name").GetString() == name);
        return (mode.GetProperty("id").GetInt64(), mode.GetProperty("concurrencyStamp").GetString()!);
    }

    [Fact]
    public async Task Payment_WithModeRequiringReference_AndNoReference_Returns400()
    {
        HttpClient client = Client;
        (long chequeId, _) = await ModeByName(client, "Cheque");

        HttpResponseMessage missing = await client.PostAsJsonAsync(
            "/api/v1/payment-modes/validate",
            new { paymentModeId = chequeId, accountId = 1L, referenceNo = (string?)null });

        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        missing.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
        using JsonDocument problem = JsonDocument.Parse(await missing.Content.ReadAsStringAsync());
        problem.RootElement.GetProperty("errors").TryGetProperty("referenceNo", out _).Should().BeTrue();

        HttpResponseMessage ok = await client.PostAsJsonAsync(
            "/api/v1/payment-modes/validate",
            new { paymentModeId = chequeId, accountId = 1L, referenceNo = "CHQ-88123" });
        ok.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Validate_CashMode_NeedsNeitherReferenceNorAccount()
    {
        HttpClient client = Client;
        (long cashId, _) = await ModeByName(client, "Cash");

        HttpResponseMessage response = await client.PostAsJsonAsync(
            "/api/v1/payment-modes/validate",
            new { paymentModeId = cashId, accountId = (long?)null, referenceNo = (string?)null });

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task PaymentMode_Deactivated_NotOfferedButStillResolvable()
    {
        HttpClient client = Client;
        (long chequeId, string stamp) = await ModeByName(client, "Cheque");

        HttpResponseMessage deactivated = await client.PutAsJsonAsync(
            $"/api/v1/payment-modes/{chequeId}",
            new
            {
                name = "Cheque",
                requiresReference = true,
                requiresAccount = true,
                isActive = false,
                concurrencyStamp = stamp,
            });
        deactivated.EnsureSuccessStatusCode();

        List<string> offered = await Names(client, "/api/v1/payment-modes");
        offered.Should().NotContain("Cheque");

        List<string> all = await Names(client, "/api/v1/payment-modes?includeInactive=true");
        all.Should().Contain("Cheque");

        // A historical transaction can still resolve the mode's name.
        HttpResponseMessage fetched = await client.GetAsync($"/api/v1/payment-modes/{chequeId}");
        fetched.EnsureSuccessStatusCode();
        JsonDocument.Parse(await fetched.Content.ReadAsStringAsync())
            .RootElement.GetProperty("name").GetString().Should().Be("Cheque");
    }

    [Fact]
    public async Task CreatePaymentMode_DuplicateName_Returns409()
    {
        HttpClient client = Client;

        (await client.PostAsJsonAsync("/api/v1/payment-modes", new { name = "Wallet" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);

        HttpResponseMessage duplicate =
            await client.PostAsJsonAsync("/api/v1/payment-modes", new { name = "  wallet  " });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }

    private static async Task<List<string>> Names(HttpClient client, string url)
    {
        HttpResponseMessage response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.EnumerateArray().Select(m => m.GetProperty("name").GetString()!).ToList();
    }
}
