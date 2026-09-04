using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P3-T04 — Manual allocation override (BRD §24, rules 24, 54).</summary>
public sealed class AllocationOverrideTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 850;

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

    /// <summary>ABC owing 25k / 10k / 30k / 50k on four projects (BRD §20).</summary>
    private async Task<(long Vendor, long A, long B, long C, long D)> Section20(HttpClient client, string tag)
    {
        long v = await CreateVendor(client, $"Override ABC {tag}");
        long a = await CreateProject(client, $"OA {tag}");
        long b = await CreateProject(client, $"OB {tag}");
        long c = await CreateProject(client, $"OC {tag}");
        long d = await CreateProject(client, $"OD {tag}");
        await Purchase(client, a, v, 25_000m, "2026-05-01");
        await Purchase(client, b, v, 10_000m, "2026-05-02");
        await Purchase(client, c, v, 30_000m, "2026-05-03");
        await Purchase(client, d, v, 50_000m, "2026-05-04");
        return (v, a, b, c, d);
    }

    private async Task<HttpResponseMessage> Allocate(
        HttpClient client, long vendor, object[] allocations, string? reason)
    {
        return await client.PostAsJsonAsync("/api/v1/vendor-payments/allocate", new
        {
            vendorId = vendor,
            date = "2026-05-10",
            amount = 100_000m,
            paymentModeId = await ModeId(Admin),
            accountId = await AccountId(Admin),
            overrideReason = reason,
            allocations,
        });
    }

    private static object Line(long projectId, decimal amount) =>
        new { projectId, obligationId = (long?)null, amount };

    [Fact]
    public async Task Override_WithoutPermission_Returns403()
    {
        (long vendor, long a, long b, long c, long d) = await Section20(Admin, "noperm");
        HttpClient user = Factory.CreateClientAs(userId: 2, permissions: "vendor_payment_allocation.add");

        HttpResponseMessage response = await Allocate(user, vendor,
            [Line(a, 25_000m), Line(b, 10_000m), Line(c, 25_000m), Line(d, 40_000m)], "prioritise D");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Override_WithoutReason_Returns400()
    {
        (long vendor, long a, long b, long c, long d) = await Section20(Admin, "noreason");

        HttpResponseMessage response = await Allocate(Admin, vendor,
            [Line(a, 25_000m), Line(b, 10_000m), Line(c, 30_000m), Line(d, 35_000m)], reason: null);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Override_SumMismatch_Returns400()
    {
        (long vendor, long a, long b, long c, long d) = await Section20(Admin, "mismatch");

        HttpResponseMessage response = await Allocate(Admin, vendor,
            [Line(a, 25_000m), Line(b, 10_000m), Line(c, 30_000m), Line(d, 60_000m)], "typo"); // 125k

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Override_WritesAuditWithProposedAndApplied()
    {
        (long vendor, long a, long b, long c, long d) = await Section20(Admin, "audit");

        // FIFO would give C 30,000 / D 35,000; move ₹5,000 from C to D.
        JsonElement result = await Json(await Allocate(Admin, vendor,
            [Line(a, 25_000m), Line(b, 10_000m), Line(c, 25_000m), Line(d, 40_000m)],
            "client asked to prioritise Project D"));

        long settlementId = result.GetProperty("settlementId").GetInt64();

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var audit = await db.AuditLogs.SingleAsync(x =>
            x.Module == "vendor_payment_allocation"
            && x.Action == "allocation_override"
            && x.RecordId == settlementId.ToString());

        audit.Details.Should().NotBeNull();
        audit.Details!.Should().Contain("proposed").And.Contain("applied");
        audit.Details.Should().Contain("client asked to prioritise Project D");

        // The override is what actually landed: C settled to 5,000, D to 10,000 outstanding.
        JsonElement byProject = (await Json(await Admin.GetAsync($"/api/v1/vendors/{vendor}/outstanding-summary")))
            .GetProperty("byProject");
        var outstanding = byProject.EnumerateArray()
            .ToDictionary(x => x.GetProperty("projectId").GetInt64(), x => x.GetProperty("outstanding").GetDecimal());
        outstanding.GetValueOrDefault(c).Should().Be(5_000m);
        outstanding.GetValueOrDefault(d).Should().Be(10_000m);
    }
}
