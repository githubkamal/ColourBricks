using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P3-T06 — Allocation history and the BRD §51 Vendor Payment Allocation Report.</summary>
public sealed class AllocationReportTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 950;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client, string name) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name, code = $"CB-2026-{++_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 10_000_000m, estimatedCost = 8_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> CreateVendor(HttpClient client, string name) =>
        (await Json(await client.PostAsJsonAsync(
            "/api/v1/parties?confirm=true", new { name, types = new[] { "Vendor" } })))
        .GetProperty("id").GetInt64();

    private async Task Purchase(HttpClient client, long projectId, long vendorId, decimal amount, string date) =>
        (await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId, vendorId, date, total = amount,
            lines = new[] { new { itemName = "X", quantity = 1m, unit = "Nos", rate = amount, taxAmount = 0m } },
        })).EnsureSuccessStatusCode();

    private async Task<long> ModeId(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == "Office Cash").GetProperty("id").GetInt64();

    /// <summary>ABC Hardware owing 25k / 10k / 30k / 50k on four projects (BRD §20).</summary>
    private async Task<(long Vendor, long A, long B, long C, long D)> Section20(HttpClient client, string tag)
    {
        long v = await CreateVendor(client, $"ABC Hardware {tag}");
        long a = await CreateProject(client, $"RA {tag}");
        long b = await CreateProject(client, $"RB {tag}");
        long c = await CreateProject(client, $"RC {tag}");
        long d = await CreateProject(client, $"RD {tag}");
        await Purchase(client, a, v, 25_000m, "2026-05-01");
        await Purchase(client, b, v, 10_000m, "2026-05-02");
        await Purchase(client, c, v, 30_000m, "2026-05-03");
        await Purchase(client, d, v, 50_000m, "2026-05-04");
        return (v, a, b, c, d);
    }

    private async Task<long> Allocate(HttpClient client, long vendor, decimal amount, string date)
    {
        JsonElement result = await Json(await client.PostAsJsonAsync("/api/v1/vendor-payments/allocate", new
        {
            vendorId = vendor, date, amount,
            paymentModeId = await ModeId(client), accountId = await AccountId(client),
        }));
        return result.GetProperty("settlementId").GetInt64();
    }

    [Fact]
    public async Task AllocationReport_RowsSumToPaymentTotal()
    {
        HttpClient client = Client;
        (long vendor, _, _, _, _) = await Section20(client, "sum");
        long settlementId = await Allocate(client, vendor, 100_000m, "2026-05-10");

        JsonElement rows = await Json(await client.GetAsync("/api/v1/reports/vendor-payment-allocations"));

        var group = rows.EnumerateArray()
            .Where(r => r.GetProperty("settlementId").GetInt64() == settlementId)
            .ToList();
        group.Should().HaveCount(4); // one row per project (BRD §51 layout)
        group.Sum(r => r.GetProperty("allocated").GetDecimal()).Should().Be(100_000m);
        group.Should().OnlyContain(r => r.GetProperty("totalPayment").GetDecimal() == 100_000m);
    }

    [Fact]
    public async Task AllocationReport_RowsSumToPaymentTotal_IncludingAdvance()
    {
        HttpClient client = Client;
        (long vendor, _, _, _, _) = await Section20(client, "adv"); // 115k outstanding
        long settlementId = await Allocate(client, vendor, 130_000m, "2026-05-10"); // 15k advance

        JsonElement rows = await Json(await client.GetAsync("/api/v1/reports/vendor-payment-allocations"));

        var group = rows.EnumerateArray()
            .Where(r => r.GetProperty("settlementId").GetInt64() == settlementId)
            .ToList();
        group.Sum(r => r.GetProperty("allocated").GetDecimal()).Should().Be(130_000m);
        group.Should().Contain(r => r.GetProperty("method").GetString() == "Advance"
            && r.GetProperty("allocated").GetDecimal() == 15_000m);
    }

    [Fact]
    public async Task AllocationReport_FiltersByVendorAndDate()
    {
        HttpClient client = Client;
        (long vendorX, _, _, _, _) = await Section20(client, "fx");
        (long vendorY, _, _, _, _) = await Section20(client, "fy");

        long inRange = await Allocate(client, vendorX, 100_000m, "2026-05-15");
        long outOfRange = await Allocate(client, vendorX, 15_000m, "2026-07-01");
        long otherVendor = await Allocate(client, vendorY, 100_000m, "2026-05-15");

        JsonElement rows = await Json(await client.GetAsync(
            $"/api/v1/reports/vendor-payment-allocations?vendorId={vendorX}&dateFrom=2026-05-01&dateTo=2026-05-31"));

        var settlementIds = rows.EnumerateArray()
            .Select(r => r.GetProperty("settlementId").GetInt64())
            .Distinct()
            .ToList();

        settlementIds.Should().Contain(inRange);
        settlementIds.Should().NotContain(outOfRange);
        settlementIds.Should().NotContain(otherVendor);
        rows.EnumerateArray().Should().OnlyContain(r => r.GetProperty("vendorId").GetInt64() == vendorX);
    }

    [Fact]
    public async Task AllocationHistory_SurvivesSettlementReversal_MarkedReversed()
    {
        HttpClient client = Client;
        (long vendor, _, _, _, _) = await Section20(client, "rev");
        long settlementId = await Allocate(client, vendor, 100_000m, "2026-05-10");

        (await client.PostAsJsonAsync(
            $"/api/v1/vendor-payments/{settlementId}/reverse", new { reason = "duplicate" }))
            .EnsureSuccessStatusCode();

        JsonElement history = await Json(await client.GetAsync($"/api/v1/settlements/{settlementId}/allocations"));
        history.GetProperty("status").GetString().Should().Be("Reversed");
        history.GetProperty("lines").GetArrayLength().Should().Be(4); // the distribution is preserved
        history.GetProperty("lines").EnumerateArray()
            .Sum(l => l.GetProperty("amount").GetDecimal()).Should().Be(100_000m);

        JsonElement rows = await Json(await client.GetAsync("/api/v1/reports/vendor-payment-allocations"));
        rows.EnumerateArray()
            .Where(r => r.GetProperty("settlementId").GetInt64() == settlementId)
            .Should().OnlyContain(r => r.GetProperty("status").GetString() == "Reversed");
    }
}
