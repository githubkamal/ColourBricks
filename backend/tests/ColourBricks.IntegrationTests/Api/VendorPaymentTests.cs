using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P3-T02 — Vendor payment, single project (BRD §18, §22).</summary>
public sealed class VendorPaymentTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 730;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client, string name) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name,
            code = $"CB-2026-{++_seq}",
            status = "Ongoing",
            startDate = "2026-04-01",
            expectedEndDate = "2027-03-31",
            contractValue = 5_000_000m,
            estimatedCost = 4_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> CreateVendor(HttpClient client, string name) =>
        (await Json(await client.PostAsJsonAsync(
            "/api/v1/parties?confirm=true", new { name, types = new[] { "Vendor" } })))
        .GetProperty("id").GetInt64();

    private async Task Purchase(HttpClient client, long projectId, long vendorId, decimal amount) =>
        (await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId, vendorId, date = "2026-05-01", total = amount,
            lines = new[] { new { itemName = "Material", quantity = 1m, unit = "Nos", rate = amount, taxAmount = 0m } },
        })).EnsureSuccessStatusCode();

    private async Task<long> ModeId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> Balance(HttpClient client, long accountId) =>
        (await Json(await client.GetAsync($"/api/v1/accounts/{accountId}"))).GetProperty("balance").GetDecimal();

    private async Task<decimal> VendorOutstanding(HttpClient client, long vendorId) =>
        (await Json(await client.GetAsync($"/api/v1/vendors/{vendorId}/outstanding")))
        .GetProperty("outstanding").GetDecimal();

    private async Task<decimal> MaterialsCost(HttpClient client, long projectId)
    {
        JsonElement b = await Json(await client.GetAsync($"/api/v1/projects/{projectId}/cost-breakdown"));
        return b.TryGetProperty("Materials", out JsonElement m) ? m.GetDecimal() : 0m;
    }

    private static Task<HttpResponseMessage> Pay(
        HttpClient client, long vendorId, long projectId, decimal amount, long modeId, long? accountId) =>
        client.PostAsJsonAsync("/api/v1/vendor-payments", new
        {
            vendorId, projectId, date = "2026-05-10", amount, paymentModeId = modeId, accountId,
        });

    [Fact]
    public async Task VendorPayment_BrdSection18Example()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Section 18 Project");
        long vendorId = await CreateVendor(client, "Section 18 Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        await Purchase(client, projectId, vendorId, 200_000m);
        (await VendorOutstanding(client, vendorId)).Should().Be(200_000m);

        (await Pay(client, vendorId, projectId, 50_000m, mode, cash)).EnsureSuccessStatusCode();
        (await VendorOutstanding(client, vendorId)).Should().Be(150_000m);

        (await Pay(client, vendorId, projectId, 75_000m, mode, cash)).EnsureSuccessStatusCode();
        (await VendorOutstanding(client, vendorId)).Should().Be(75_000m);
    }

    [Fact]
    public async Task VendorPayment_DoesNotCreateExpense()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "No Expense Project");
        long vendorId = await CreateVendor(client, "No Expense Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        await Purchase(client, projectId, vendorId, 200_000m);
        (await MaterialsCost(client, projectId)).Should().Be(200_000m);

        (await Pay(client, vendorId, projectId, 50_000m, mode, cash)).EnsureSuccessStatusCode();
        (await Pay(client, vendorId, projectId, 75_000m, mode, cash)).EnsureSuccessStatusCode();

        (await MaterialsCost(client, projectId)).Should().Be(200_000m); // unchanged by payments (BRD §22)
    }

    [Fact]
    public async Task VendorPayment_ReducesOutstandingAndAccount_ByEqualAmount()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Equal Move Project");
        long vendorId = await CreateVendor(client, "Equal Move Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Site Cash");

        await Purchase(client, projectId, vendorId, 100_000m);
        decimal outBefore = await VendorOutstanding(client, vendorId);
        decimal bankBefore = await Balance(client, cash);

        (await Pay(client, vendorId, projectId, 40_000m, mode, cash)).EnsureSuccessStatusCode();

        (await VendorOutstanding(client, vendorId)).Should().Be(outBefore - 40_000m);
        (await Balance(client, cash)).Should().Be(bankBefore - 40_000m);
    }

    [Fact]
    public async Task MultiplePayments_AgainstOnePurchase_SumCorrectly()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Multi Pay Project");
        long vendorId = await CreateVendor(client, "Multi Pay Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        await Purchase(client, projectId, vendorId, 200_000m);
        (await Pay(client, vendorId, projectId, 50_000m, mode, cash)).EnsureSuccessStatusCode();
        (await Pay(client, vendorId, projectId, 75_000m, mode, cash)).EnsureSuccessStatusCode();

        (await VendorOutstanding(client, vendorId)).Should().Be(75_000m);

        JsonElement list = await Json(await client.GetAsync($"/api/v1/vendors/{vendorId}/payments"));
        list.GetArrayLength().Should().Be(2);
        list.EnumerateArray().Sum(p => p.GetProperty("amount").GetDecimal()).Should().Be(125_000m);

        // Paying more than the ₹75,000 outstanding now records the excess as a vendor
        // advance (P3-T05) rather than being rejected.
        JsonElement overpay = await Json(await Pay(client, vendorId, projectId, 100_000m, mode, cash));
        overpay.GetProperty("advanceCreated").GetDecimal().Should().Be(25_000m);
        (await VendorOutstanding(client, vendorId)).Should().Be(0m);
    }

    [Fact]
    public async Task VendorPayment_Reversal_RestoresOutstandingAndAccount()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Reverse Pay Project");
        long vendorId = await CreateVendor(client, "Reverse Pay Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        await Purchase(client, projectId, vendorId, 100_000m);
        decimal outBefore = await VendorOutstanding(client, vendorId);
        decimal bankBefore = await Balance(client, cash);

        long paymentId = (await Json(await Pay(client, vendorId, projectId, 60_000m, mode, cash)))
            .GetProperty("id").GetInt64();

        (await client.PostAsJsonAsync($"/api/v1/vendor-payments/{paymentId}/reverse", new { reason = "wrong vendor" }))
            .EnsureSuccessStatusCode();

        (await VendorOutstanding(client, vendorId)).Should().Be(outBefore);
        (await Balance(client, cash)).Should().Be(bankBefore);
    }
}
