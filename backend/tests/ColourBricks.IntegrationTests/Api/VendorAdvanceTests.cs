using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P3-T05 — Vendor advance and excess payment (BRD §25, rule 30).</summary>
public sealed class VendorAdvanceTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 900;

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

    private async Task<long> Purchase(HttpClient client, long projectId, long vendorId, decimal amount, string date = "2026-05-01") =>
        (await Json(await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId, vendorId, date, total = amount,
            lines = new[] { new { itemName = "Material", quantity = 1m, unit = "Nos", rate = amount, taxAmount = 0m } },
        }))).GetProperty("purchase").GetProperty("id").GetInt64();

    private async Task<long> ModeId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> Balance(HttpClient client, long accountId) =>
        (await Json(await client.GetAsync($"/api/v1/accounts/{accountId}"))).GetProperty("balance").GetDecimal();

    private static Task<HttpResponseMessage> Pay(
        HttpClient client, long vendorId, long projectId, decimal amount, long modeId, long? accountId) =>
        client.PostAsJsonAsync("/api/v1/vendor-payments", new
        {
            vendorId, projectId, date = "2026-05-10", amount, paymentModeId = modeId, accountId,
        });

    private static Task<HttpResponseMessage> ApplyAdvance(
        HttpClient client, long vendorId, long obligationId, decimal amount) =>
        client.PostAsJsonAsync("/api/v1/vendor-payments/apply-advance", new
        {
            vendorId, obligationId, amount, date = "2026-06-01",
        });

    private async Task<(decimal Total, decimal Advance)> Summary(HttpClient client, long vendorId)
    {
        JsonElement s = await Json(await client.GetAsync($"/api/v1/vendors/{vendorId}/outstanding-summary"));
        return (s.GetProperty("total").GetDecimal(), s.GetProperty("advance").GetDecimal());
    }

    [Fact]
    public async Task Payment_ExceedingOutstanding_CreatesAdvance()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Advance Create Project");
        long vendorId = await CreateVendor(client, "Advance Create Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        await Purchase(client, projectId, vendorId, 60_000m);

        (await Pay(client, vendorId, projectId, 100_000m, mode, cash)).EnsureSuccessStatusCode();

        (decimal total, decimal advance) = await Summary(client, vendorId);
        total.Should().Be(0m);
        advance.Should().Be(40_000m);
    }

    [Fact]
    public async Task Advance_BrdSection25Example()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Section 25 Project");
        long vendorId = await CreateVendor(client, "Section 25 Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        await Purchase(client, projectId, vendorId, 80_000m);

        JsonElement payment = await Json(await Pay(client, vendorId, projectId, 100_000m, mode, cash));
        payment.GetProperty("advanceCreated").GetDecimal().Should().Be(20_000m);

        (decimal total, decimal advance) = await Summary(client, vendorId);
        total.Should().Be(0m);        // zero outstanding (BRD §25)
        advance.Should().Be(20_000m); // ₹20,000 credit balance
    }

    [Fact]
    public async Task Advance_AppliedToPurchase_NoCashMovement()
    {
        HttpClient client = Client;
        long projectA = await CreateProject(client, "Advance Apply A");
        long projectB = await CreateProject(client, "Advance Apply B");
        long vendorId = await CreateVendor(client, "Advance Apply Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        await Purchase(client, projectA, vendorId, 80_000m);
        (await Pay(client, vendorId, projectA, 100_000m, mode, cash)).EnsureSuccessStatusCode();

        decimal cashAfterPayment = await Balance(client, cash);

        long purchaseB = await Purchase(client, projectB, vendorId, 50_000m, "2026-05-20");

        JsonElement applied = await Json(await ApplyAdvance(client, vendorId, purchaseB, 20_000m));
        applied.GetProperty("applied").GetDecimal().Should().Be(20_000m);
        applied.GetProperty("advanceRemaining").GetDecimal().Should().Be(0m);
        applied.GetProperty("obligationOutstandingAfter").GetDecimal().Should().Be(30_000m);

        // No cash moved when the advance was applied.
        (await Balance(client, cash)).Should().Be(cashAfterPayment);

        (decimal total, decimal advance) = await Summary(client, vendorId);
        total.Should().Be(30_000m);
        advance.Should().Be(0m);
    }

    [Fact]
    public async Task Advance_CannotExceedAvailableBalance()
    {
        HttpClient client = Client;
        long projectA = await CreateProject(client, "Advance Limit A");
        long projectB = await CreateProject(client, "Advance Limit B");
        long vendorId = await CreateVendor(client, "Advance Limit Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        await Purchase(client, projectA, vendorId, 80_000m);
        (await Pay(client, vendorId, projectA, 100_000m, mode, cash)).EnsureSuccessStatusCode(); // advance 20,000

        long purchaseB = await Purchase(client, projectB, vendorId, 50_000m, "2026-05-20");

        HttpResponseMessage tooMuch = await ApplyAdvance(client, vendorId, purchaseB, 25_000m);
        tooMuch.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // The failed attempt changed nothing.
        (decimal _, decimal advance) = await Summary(client, vendorId);
        advance.Should().Be(20_000m);
    }

    [Fact]
    public async Task VendorStatement_ShowsAdvanceBalance()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Statement Advance Project");
        long vendorId = await CreateVendor(client, "Statement Advance Vendor");
        long mode = await ModeId(client, "Cash");
        long cash = await AccountId(client, "Office Cash");

        await Purchase(client, projectId, vendorId, 80_000m);
        (await Pay(client, vendorId, projectId, 100_000m, mode, cash)).EnsureSuccessStatusCode();

        JsonElement rows = await Json(await client.GetAsync($"/api/v1/vendors/{vendorId}/statement"));
        JsonElement last = rows.EnumerateArray().Last();
        last.GetProperty("kind").GetString().Should().Be("Advance");
        last.GetProperty("paid").GetDecimal().Should().Be(20_000m);
    }
}
