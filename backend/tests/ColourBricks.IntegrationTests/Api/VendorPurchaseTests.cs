using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P2-T03 — Vendor purchase with line items (BRD §16, §17, §22).</summary>
public sealed class VendorPurchaseTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client, string code) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Purchase Project {code}",
            code,
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

    private async Task<long> AccountId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> PaymentModeId(HttpClient client, string name) =>
        (await Json(await client.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> AccountBalance(HttpClient client, long accountId) =>
        (await Json(await client.GetAsync($"/api/v1/accounts/{accountId}"))).GetProperty("balance").GetDecimal();

    private async Task<decimal> VendorOutstanding(HttpClient client, long vendorId) =>
        (await Json(await client.GetAsync($"/api/v1/vendors/{vendorId}/outstanding")))
        .GetProperty("outstanding").GetDecimal();

    private static object BrdSection16Lines() => new[]
    {
        new { itemId = (long?)null, itemName = "Cement", quantity = 100m, unit = "Bag", rate = 400m, taxAmount = 0m },
        new { itemId = (long?)null, itemName = "Sand", quantity = 2m, unit = "Load", rate = 15_000m, taxAmount = 0m },
    };

    [Fact]
    public async Task Purchase_LineTotalsMustSumToHeader()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-621");
        long vendorId = await CreateVendor(client, "Mismatch Traders");

        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId,
            vendorId,
            date = "2026-05-01",
            total = 65_000m, // lines sum to 70,000
            lines = BrdSection16Lines(),
        });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Purchase_DoesNotChangeAccountBalance()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-622");
        long vendorId = await CreateVendor(client, "No Cash Traders");
        long bank = await AccountId(client, "HDFC");

        decimal before = await AccountBalance(client, bank);

        (await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId, vendorId, date = "2026-05-01", total = 70_000m, lines = BrdSection16Lines(),
        })).EnsureSuccessStatusCode();

        (await AccountBalance(client, bank)).Should().Be(before);
    }

    [Fact]
    public async Task Purchase_IncreasesVendorOutstanding()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-623");
        long vendorId = await CreateVendor(client, "Outstanding Traders");

        (await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId, vendorId, date = "2026-05-01", total = 70_000m, lines = BrdSection16Lines(),
        })).EnsureSuccessStatusCode();

        (await VendorOutstanding(client, vendorId)).Should().Be(70_000m);
    }

    [Fact]
    public async Task Purchase_WithInlinePartPayment_CreatesLinkedSettlement()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-624");
        long vendorId = await CreateVendor(client, "Part Pay Traders");
        long cash = await AccountId(client, "Office Cash");
        long mode = await PaymentModeId(client, "Cash");

        decimal before = await AccountBalance(client, cash);

        JsonElement created = await Json(await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId,
            vendorId,
            date = "2026-05-01",
            total = 70_000m,
            lines = BrdSection16Lines(),
            partPayment = 20_000m,
            partPaymentModeId = mode,
            partPaymentAccountId = cash,
        }));
        long purchaseId = created.GetProperty("purchase").GetProperty("id").GetInt64();

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var settlement = await db.Settlements.SingleAsync(s => s.ObligationId == purchaseId);
            settlement.Amount.Should().Be(20_000m);
            settlement.Direction.Should().Be(ColourBricks.Domain.Settlements.SettlementDirection.Out);
        }

        (await VendorOutstanding(client, vendorId)).Should().Be(50_000m);
        (await AccountBalance(client, cash)).Should().Be(before - 20_000m);
    }

    [Fact]
    public async Task Purchase_BrdSection16Example_ProducesExpectedTotals()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-625");
        long vendorId = await CreateVendor(client, "ABC Cement Agency");
        long bank = await AccountId(client, "SBI");

        decimal bankBefore = await AccountBalance(client, bank);

        (await client.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId, vendorId, date = "2026-05-01", total = 70_000m, lines = BrdSection16Lines(),
        })).EnsureSuccessStatusCode();

        JsonElement breakdown = await Json(await client.GetAsync($"/api/v1/projects/{projectId}/cost-breakdown"));
        breakdown.GetProperty("Materials").GetDecimal().Should().Be(70_000m);
        (await VendorOutstanding(client, vendorId)).Should().Be(70_000m);
        (await AccountBalance(client, bank)).Should().Be(bankBefore);
    }

    [Fact]
    public async Task Purchase_DuplicateInvoiceNumber_ReturnsWarningNotError()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-626");
        long vendorId = await CreateVendor(client, "Repeat Invoice Traders");

        object body = new
        {
            projectId, vendorId, date = "2026-05-01", total = 70_000m,
            invoiceNumber = "INV-777", lines = BrdSection16Lines(),
        };

        (await client.PostAsJsonAsync("/api/v1/vendor-purchases", body)).StatusCode
            .Should().Be(HttpStatusCode.Created);

        HttpResponseMessage second = await client.PostAsJsonAsync("/api/v1/vendor-purchases", body);
        second.StatusCode.Should().Be(HttpStatusCode.Created);
        (await Json(second)).GetProperty("duplicateInvoiceWarning").GetBoolean().Should().BeTrue();
    }
}
