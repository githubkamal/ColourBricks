using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P3-T01 — Outstanding calculation engine (BRD §17, §21, §38).</summary>
public sealed class OutstandingTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private int _codeSeq = 700;

    private async Task<long> CreateProject(HttpClient client, string name) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name,
            code = $"CB-2026-{++_codeSeq}",
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

    private async Task<long> Purchase(
        HttpClient client, long projectId, long vendorId, decimal amount, string date = "2026-05-01")
    {
        JsonElement created = await Json(await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId,
            vendorId,
            date,
            total = amount,
            lines = new[]
            {
                new { itemName = "Material", quantity = 1m, unit = "Nos", rate = amount, taxAmount = 0m },
            },
        }));
        return created.GetProperty("purchase").GetProperty("id").GetInt64();
    }

    private async Task<JsonElement> VendorSummary(HttpClient client, long vendorId) =>
        await Json(await client.GetAsync($"/api/v1/vendors/{vendorId}/outstanding-summary"));

    [Fact]
    public async Task Outstanding_BrdSection21Scenario_MatchesExpected()
    {
        HttpClient client = Client;
        long vendorId = await CreateVendor(client, "ABC Hardware");
        long a = await CreateProject(client, "Project A");
        long b = await CreateProject(client, "Project B");
        long c = await CreateProject(client, "Project C");
        long d = await CreateProject(client, "Project D");

        await Purchase(client, a, vendorId, 25_000m);
        await Purchase(client, b, vendorId, 10_000m);
        await Purchase(client, c, vendorId, 30_000m);
        await Purchase(client, d, vendorId, 50_000m);

        JsonElement summary = await VendorSummary(client, vendorId);

        summary.GetProperty("total").GetDecimal().Should().Be(115_000m);

        var byProject = summary.GetProperty("byProject").EnumerateArray()
            .ToDictionary(x => x.GetProperty("projectId").GetInt64(),
                x => x.GetProperty("outstanding").GetDecimal());

        byProject[a].Should().Be(25_000m);
        byProject[b].Should().Be(10_000m);
        byProject[c].Should().Be(30_000m);
        byProject[d].Should().Be(50_000m);
    }

    [Fact]
    public async Task VendorOutstanding_EqualsSumOfProjectOutstanding()
    {
        HttpClient client = Client;
        long vendorId = await CreateVendor(client, "Sync Vendor");
        long p1 = await CreateProject(client, "Sync P1");
        long p2 = await CreateProject(client, "Sync P2");

        await Purchase(client, p1, vendorId, 40_000m);
        await Purchase(client, p1, vendorId, 15_000m);
        await Purchase(client, p2, vendorId, 22_000m);

        JsonElement summary = await VendorSummary(client, vendorId);
        decimal total = summary.GetProperty("total").GetDecimal();
        decimal sumOfProjects = summary.GetProperty("byProject").EnumerateArray()
            .Sum(x => x.GetProperty("outstanding").GetDecimal());

        total.Should().Be(77_000m);
        total.Should().Be(sumOfProjects);
    }

    [Fact]
    public async Task Outstanding_ExcludesReversedObligations()
    {
        HttpClient client = Client;
        long vendorId = await CreateVendor(client, "Reversal Vendor");
        long projectId = await CreateProject(client, "Reversal Project");

        long keep = await Purchase(client, projectId, vendorId, 30_000m);
        long drop = await Purchase(client, projectId, vendorId, 12_000m);

        (await Json(await client.GetAsync($"/api/v1/vendors/{vendorId}/outstanding-summary")))
            .GetProperty("total").GetDecimal().Should().Be(42_000m);

        (await client.PostAsJsonAsync($"/api/v1/vendor-purchases/{drop}/reverse", new { reason = "duplicate" }))
            .EnsureSuccessStatusCode();

        (await VendorSummary(client, vendorId)).GetProperty("total").GetDecimal().Should().Be(30_000m);
        _ = keep;
    }

    [Fact]
    public async Task Outstanding_ExcludesReversedSettlements()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "Client Receipt Project");
        long modeId = (await Json(await client.GetAsync("/api/v1/payment-modes")))
            .EnumerateArray().First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

        // contractValue is 5,000,000; a 1,000,000 receipt leaves 4,000,000 client-outstanding.
        JsonElement receipt = await Json(await client.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId, type = "Stage", date = "2026-05-01", amount = 1_000_000m, paymentModeId = modeId,
        }));
        long receiptId = receipt.GetProperty("id").GetInt64();

        (await Json(await client.GetAsync($"/api/v1/projects/{projectId}/outstanding-summary")))
            .GetProperty("clientReceivable").GetDecimal().Should().Be(4_000_000m);

        (await client.PostAsJsonAsync($"/api/v1/receipts/{receiptId}/reverse", new { reason = "wrong project" }))
            .EnsureSuccessStatusCode();

        (await Json(await client.GetAsync($"/api/v1/projects/{projectId}/outstanding-summary")))
            .GetProperty("clientReceivable").GetDecimal().Should().Be(5_000_000m);
    }

    [Fact]
    public async Task Outstanding_AgeingBuckets_AssignCorrectly()
    {
        HttpClient client = Client;
        long vendorId = await CreateVendor(client, "Ageing Vendor");
        long projectId = await CreateProject(client, "Ageing Project");

        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);
        await Purchase(client, projectId, vendorId, 10_000m, today.AddDays(-10).ToString("yyyy-MM-dd"));
        await Purchase(client, projectId, vendorId, 20_000m, today.AddDays(-45).ToString("yyyy-MM-dd"));
        await Purchase(client, projectId, vendorId, 30_000m, today.AddDays(-75).ToString("yyyy-MM-dd"));
        await Purchase(client, projectId, vendorId, 40_000m, today.AddDays(-200).ToString("yyyy-MM-dd"));

        JsonElement ageing = await Json(await client.GetAsync($"/api/v1/vendors/{vendorId}/ageing"));

        ageing.GetProperty("current").GetDecimal().Should().Be(10_000m);
        ageing.GetProperty("days31To60").GetDecimal().Should().Be(20_000m);
        ageing.GetProperty("days61To90").GetDecimal().Should().Be(30_000m);
        ageing.GetProperty("over90").GetDecimal().Should().Be(40_000m);
        ageing.GetProperty("total").GetDecimal().Should().Be(100_000m);
    }
}
