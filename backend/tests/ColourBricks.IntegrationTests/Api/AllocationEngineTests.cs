using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P3-T03 — FIFO multi-project allocation engine (BRD §19–§21, §23).</summary>
public sealed class AllocationEngineTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 800;

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
            contractValue = 10_000_000m,
            estimatedCost = 8_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> CreateVendor(HttpClient client, string name) =>
        (await Json(await client.PostAsJsonAsync(
            "/api/v1/parties?confirm=true", new { name, types = new[] { "Vendor" } })))
        .GetProperty("id").GetInt64();

    private async Task Purchase(
        HttpClient client, long projectId, long vendorId, decimal amount, string date)
    {
        (await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId, vendorId, date, total = amount,
            lines = new[] { new { itemName = "Material", quantity = 1m, unit = "Nos", rate = amount, taxAmount = 0m } },
        })).EnsureSuccessStatusCode();
    }

    private async Task<long> ModeId(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task<long> AccountId(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == "Office Cash").GetProperty("id").GetInt64();

    private async Task<decimal> VendorOutstanding(HttpClient client, long vendorId) =>
        (await Json(await client.GetAsync($"/api/v1/vendors/{vendorId}/outstanding")))
        .GetProperty("outstanding").GetDecimal();

    /// <summary>The BRD §20 fixture: ABC Hardware owing 25k / 10k / 30k / 50k on four projects.</summary>
    private async Task<(long Vendor, long A, long B, long C, long D)> Section20Fixture(HttpClient client, string tag)
    {
        long vendor = await CreateVendor(client, $"ABC Hardware {tag}");
        long a = await CreateProject(client, $"Project A {tag}");
        long b = await CreateProject(client, $"Project B {tag}");
        long c = await CreateProject(client, $"Project C {tag}");
        long d = await CreateProject(client, $"Project D {tag}");

        await Purchase(client, a, vendor, 25_000m, "2026-05-01");
        await Purchase(client, b, vendor, 10_000m, "2026-05-02");
        await Purchase(client, c, vendor, 30_000m, "2026-05-03");
        await Purchase(client, d, vendor, 50_000m, "2026-05-04");
        return (vendor, a, b, c, d);
    }

    [Fact]
    public async Task Fifo_BrdSection20Example_MatchesExactly()
    {
        HttpClient client = Client;
        (long vendor, long a, long b, long c, long d) = await Section20Fixture(client, "s20");

        JsonElement proposal = await Json(await client.PostAsJsonAsync(
            "/api/v1/vendor-payments/propose", new { vendorId = vendor, amount = 100_000m }));

        var lines = proposal.GetProperty("lines").EnumerateArray()
            .ToDictionary(l => l.GetProperty("projectId").GetInt64(), l => l);

        lines[a].GetProperty("allocated").GetDecimal().Should().Be(25_000m);
        lines[a].GetProperty("outstandingAfter").GetDecimal().Should().Be(0m);
        lines[b].GetProperty("allocated").GetDecimal().Should().Be(10_000m);
        lines[c].GetProperty("allocated").GetDecimal().Should().Be(30_000m);
        lines[d].GetProperty("allocated").GetDecimal().Should().Be(35_000m);
        lines[d].GetProperty("outstandingAfter").GetDecimal().Should().Be(15_000m);

        proposal.GetProperty("totalAllocated").GetDecimal().Should().Be(100_000m);
        proposal.GetProperty("advance").GetDecimal().Should().Be(0m);
    }

    [Fact]
    public async Task Fifo_AllocationSum_EqualsPaymentAmount_AndPartialOnLastObligation()
    {
        HttpClient client = Client;
        (long vendor, _, _, _, long d) = await Section20Fixture(client, "sum");
        long mode = await ModeId(client);
        long cash = await AccountId(client);

        JsonElement result = await Json(await client.PostAsJsonAsync("/api/v1/vendor-payments/allocate", new
        {
            vendorId = vendor, date = "2026-05-10", amount = 100_000m, paymentModeId = mode, accountId = cash,
        }));

        long settlementId = result.GetProperty("settlementId").GetInt64();

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        decimal allocated = await db.Allocations.Where(x => x.SettlementId == settlementId).SumAsync(x => x.Amount);
        allocated.Should().Be(100_000m);

        // Project D's obligation is only partially covered.
        var dAllocation = await db.Allocations.SingleAsync(x => x.SettlementId == settlementId && x.ProjectId == d);
        dAllocation.Amount.Should().Be(35_000m);

        (await VendorOutstanding(client, vendor)).Should().Be(15_000m);
    }

    [Fact]
    public async Task Fifo_OldestObligationSettledFirst()
    {
        HttpClient client = Client;
        long vendor = await CreateVendor(client, "Oldest First Vendor");
        long p1 = await CreateProject(client, "Oldest P1");
        long p2 = await CreateProject(client, "Oldest P2");

        await Purchase(client, p1, vendor, 20_000m, "2026-05-01"); // oldest
        await Purchase(client, p2, vendor, 20_000m, "2026-06-01");

        JsonElement proposal = await Json(await client.PostAsJsonAsync(
            "/api/v1/vendor-payments/propose", new { vendorId = vendor, amount = 15_000m }));

        var lines = proposal.GetProperty("lines").EnumerateArray().ToList();
        lines.Should().HaveCount(1);
        lines[0].GetProperty("projectId").GetInt64().Should().Be(p1);
        lines[0].GetProperty("allocated").GetDecimal().Should().Be(15_000m);
    }

    [Fact]
    public async Task Fifo_ZeroOutstanding_AllocatesNothing_ReturnsAdvance()
    {
        HttpClient client = Client;
        long vendor = await CreateVendor(client, "No Outstanding Vendor");

        JsonElement proposal = await Json(await client.PostAsJsonAsync(
            "/api/v1/vendor-payments/propose", new { vendorId = vendor, amount = 50_000m }));

        proposal.GetProperty("lines").GetArrayLength().Should().Be(0);
        proposal.GetProperty("totalAllocated").GetDecimal().Should().Be(0m);
        proposal.GetProperty("advance").GetDecimal().Should().Be(50_000m);
    }

    [Fact]
    public async Task Fifo_IgnoresReversedObligations()
    {
        HttpClient client = Client;
        long vendor = await CreateVendor(client, "Reversed Obligation Vendor");
        long p1 = await CreateProject(client, "Rev P1");
        long p2 = await CreateProject(client, "Rev P2");

        await Purchase(client, p1, vendor, 40_000m, "2026-05-01");
        JsonElement dropPurchase = await Json(await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId = p2, vendorId = vendor, date = "2026-05-02", total = 60_000m,
            lines = new[] { new { itemName = "X", quantity = 1m, unit = "Nos", rate = 60_000m, taxAmount = 0m } },
        }));
        long dropId = dropPurchase.GetProperty("purchase").GetProperty("id").GetInt64();

        (await client.PostAsJsonAsync($"/api/v1/vendor-purchases/{dropId}/reverse", new { reason = "dup" }))
            .EnsureSuccessStatusCode();

        JsonElement proposal = await Json(await client.PostAsJsonAsync(
            "/api/v1/vendor-payments/propose", new { vendorId = vendor, amount = 100_000m }));

        proposal.GetProperty("totalAllocated").GetDecimal().Should().Be(40_000m); // only the active obligation
        proposal.GetProperty("lines").EnumerateArray().Should().OnlyContain(
            l => l.GetProperty("projectId").GetInt64() == p1);
    }

    [Fact]
    public async Task Fifo_ConcurrentAllocations_DoNotOverAllocate()
    {
        HttpClient client = Client;
        (long vendor, _, _, _, _) = await Section20Fixture(client, "conc"); // 1,15,000 outstanding
        long mode = await ModeId(client);
        long cash = await AccountId(client);

        object body = new
        {
            vendorId = vendor, date = "2026-05-10", amount = 100_000m, paymentModeId = mode, accountId = cash,
        };

        Task<HttpResponseMessage> first = client.PostAsJsonAsync("/api/v1/vendor-payments/allocate", body);
        Task<HttpResponseMessage> second = Client.PostAsJsonAsync("/api/v1/vendor-payments/allocate", body);
        HttpResponseMessage[] responses = await Task.WhenAll(first, second);

        responses.Count(r => r.IsSuccessStatusCode).Should().Be(1);
        responses.Count(r => !r.IsSuccessStatusCode).Should().Be(1);
        responses.Single(r => !r.IsSuccessStatusCode).StatusCode
            .Should().BeOneOf(HttpStatusCode.Conflict, HttpStatusCode.BadRequest);

        // Outstanding never goes negative — exactly one payment landed.
        (await VendorOutstanding(client, vendor)).Should().Be(15_000m);
    }
}
