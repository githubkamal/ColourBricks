using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P3-T07 — BRD §38 data-integrity control suite (plan.md §11 safety net).</summary>
[Trait("Category", "IntegrityControls")]
public sealed class IntegrityControlsTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 990;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client, string name, decimal contract = 10_000_000m) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name, code = $"CB-2026-{++_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = contract, estimatedCost = contract * 0.8m,
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

    private static Task<HttpResponseMessage> Pay(
        HttpClient c, long vendor, long project, decimal amount, long mode, long account) =>
        c.PostAsJsonAsync("/api/v1/vendor-payments", new
        {
            vendorId = vendor, projectId = project, date = "2026-05-10",
            amount, paymentModeId = mode, accountId = account,
        });

    private static Task<HttpResponseMessage> Allocate(
        HttpClient c, long vendor, decimal amount, long mode, long account, string date = "2026-06-01") =>
        c.PostAsJsonAsync("/api/v1/vendor-payments/allocate", new
        {
            vendorId = vendor, date, amount, paymentModeId = mode, accountId = account,
        });

    private static Task<HttpResponseMessage> Receipt(
        HttpClient c, long project, decimal amount, long mode, long account) =>
        c.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId = project, type = "ClientAdvance", date = "2026-05-05",
            amount, paymentModeId = mode, accountId = account,
        });

    private async Task<JsonElement> RunCheck(HttpClient client) =>
        await Json(await client.GetAsync("/api/v1/admin/integrity-check"));

    private static JsonElement Control(JsonElement report, string name) =>
        report.GetProperty("controls").EnumerateArray()
            .First(c => c.GetProperty("control").GetString() == name);

    private static void AllControlsPass(JsonElement report, string because)
    {
        report.GetProperty("passed").GetBoolean().Should().BeTrue(
            $"{because}: {report.GetProperty("controls").EnumerateArray()
                .Where(c => !c.GetProperty("passed").GetBoolean())
                .Select(c => $"{c.GetProperty("control").GetString()} {c.GetProperty("violations")}")
                .Aggregate("", (a, b) => a + " | " + b)}");
    }

    [Fact]
    public async Task Control_BankBalance_OpeningPlusCreditsMinusDebits()
    {
        HttpClient client = Client;
        long mode = await ModeId(client);
        long cash = await AccountId(client);
        long project = await CreateProject(client, "Bank Ctrl Project");
        long vendor = await CreateVendor(client, "Bank Ctrl Vendor");

        await Purchase(client, project, vendor, 100_000m, "2026-05-01");
        (await Pay(client, vendor, project, 40_000m, mode, cash)).EnsureSuccessStatusCode();
        (await Receipt(client, project, 250_000m, mode, cash)).EnsureSuccessStatusCode();

        JsonElement report = await RunCheck(client);
        Control(report, "Bank").GetProperty("passed").GetBoolean().Should().BeTrue();
        AllControlsPass(report, "after a purchase, a payment and a receipt");
    }

    [Fact]
    public async Task Control_VendorOutstanding_PurchasesMinusPayments()
    {
        HttpClient client = Client;
        long mode = await ModeId(client);
        long cash = await AccountId(client);
        long project = await CreateProject(client, "Vendor Ctrl Project");
        long vendor = await CreateVendor(client, "Vendor Ctrl Vendor");

        await Purchase(client, project, vendor, 100_000m, "2026-05-01");
        await Purchase(client, project, vendor, 50_000m, "2026-05-03");
        (await Pay(client, vendor, project, 30_000m, mode, cash)).EnsureSuccessStatusCode();

        JsonElement report = await RunCheck(client);
        Control(report, "Vendor").GetProperty("passed").GetBoolean().Should().BeTrue();

        decimal served = (await Json(await client.GetAsync($"/api/v1/vendors/{vendor}/outstanding")))
            .GetProperty("outstanding").GetDecimal();
        served.Should().Be(120_000m); // 150k purchases − 30k paid
    }

    [Fact]
    public async Task Control_ProjectOutstanding_PayablesMinusPayments()
    {
        HttpClient client = Client;
        long mode = await ModeId(client);
        long cash = await AccountId(client);
        long projectA = await CreateProject(client, "Proj Ctrl A");
        long projectB = await CreateProject(client, "Proj Ctrl B");
        long vendor = await CreateVendor(client, "Proj Ctrl Vendor");

        await Purchase(client, projectA, vendor, 60_000m, "2026-05-01");
        await Purchase(client, projectB, vendor, 40_000m, "2026-05-02");
        (await Pay(client, vendor, projectA, 25_000m, mode, cash)).EnsureSuccessStatusCode();
        (await Allocate(client, vendor, 50_000m, mode, cash)).EnsureSuccessStatusCode();

        JsonElement report = await RunCheck(client);
        Control(report, "Project").GetProperty("passed").GetBoolean().Should().BeTrue();
        AllControlsPass(report, "single- and multi-project payments against two projects");
    }

    [Fact]
    public async Task Control_ClientOutstanding_DueMinusReceipts()
    {
        HttpClient client = Client;
        long mode = await ModeId(client);
        long cash = await AccountId(client);
        long project = await CreateProject(client, "Client Ctrl Project", contract: 1_000_000m);

        (await Receipt(client, project, 300_000m, mode, cash)).EnsureSuccessStatusCode();
        (await Receipt(client, project, 200_000m, mode, cash)).EnsureSuccessStatusCode();

        JsonElement report = await RunCheck(client);
        Control(report, "Client").GetProperty("passed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Control_MultiProjectPayment_DebitEqualsAllocations()
    {
        HttpClient client = Client;
        long mode = await ModeId(client);
        long cash = await AccountId(client);
        long vendor = await CreateVendor(client, "MP Ctrl Vendor");
        long a = await CreateProject(client, "MP A");
        long b = await CreateProject(client, "MP B");
        long c = await CreateProject(client, "MP C");
        long d = await CreateProject(client, "MP D");
        await Purchase(client, a, vendor, 25_000m, "2026-05-01");
        await Purchase(client, b, vendor, 10_000m, "2026-05-02");
        await Purchase(client, c, vendor, 30_000m, "2026-05-03");
        await Purchase(client, d, vendor, 50_000m, "2026-05-04");

        (await Allocate(client, vendor, 100_000m, mode, cash)).EnsureSuccessStatusCode();  // exact
        (await Allocate(client, vendor, 40_000m, mode, cash, "2026-06-02")).EnsureSuccessStatusCode(); // 25k advance

        JsonElement report = await RunCheck(client);
        Control(report, "Multi-Project Payment").GetProperty("passed").GetBoolean().Should().BeTrue();
        AllControlsPass(report, "an exact and an advance-leaving multi-project payment");
    }

    [Fact]
    public async Task Control_DetectsInjectedCorruption()
    {
        HttpClient client = Client;
        long mode = await ModeId(client);
        long cash = await AccountId(client);
        long vendor = await CreateVendor(client, "Corrupt Vendor");
        long a = await CreateProject(client, "Corrupt A");
        long b = await CreateProject(client, "Corrupt B");
        await Purchase(client, a, vendor, 60_000m, "2026-05-01");
        await Purchase(client, b, vendor, 40_000m, "2026-05-02");

        long settlementId = (await Json(await Allocate(client, vendor, 100_000m, mode, cash)))
            .GetProperty("settlementId").GetInt64();

        AllControlsPass(await RunCheck(client), "before corruption");

        // Tamper one allocation slice by direct SQL — nothing else touched.
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            long allocId = await db.Allocations
                .Where(x => x.SettlementId == settlementId && x.ObligationId != null)
                .Select(x => x.Id).FirstAsync();
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE `Allocation` SET `Amount` = `Amount` + 9999 WHERE `Id` = {0}", allocId);
        }

        JsonElement report = await RunCheck(client);
        report.GetProperty("passed").GetBoolean().Should().BeFalse();

        JsonElement mp = Control(report, "Multi-Project Payment");
        mp.GetProperty("passed").GetBoolean().Should().BeFalse();
        mp.GetProperty("violations").EnumerateArray()
            .Select(v => v.GetProperty("id").GetInt64())
            .Should().Contain(settlementId);
    }

    [Fact]
    public async Task Fuzz_RandomTransactionSequences_AllControlsHold()
    {
        HttpClient client = Client;
        long mode = await ModeId(client);
        long cash = await AccountId(client);
        var rng = new Random(20260903);

        var vendors = new List<long>();
        var projects = new List<long>();
        for (int i = 0; i < 4; i++)
        {
            vendors.Add(await CreateVendor(client, $"Fuzz V{i}"));
            projects.Add(await CreateProject(client, $"Fuzz P{i}"));
        }

        var liveMultiPayments = new List<long>();

        for (int step = 1; step <= 500; step++)
        {
            long vendor = vendors[rng.Next(vendors.Count)];
            long project = projects[rng.Next(projects.Count)];
            decimal amount = rng.Next(1, 30) * 1_000m;
            int roll = rng.Next(100);

            try
            {
                if (roll < 35)
                {
                    await Purchase(client, project, vendor, amount, $"2026-05-{1 + rng.Next(27):D2}");
                }
                else if (roll < 55)
                {
                    (await Pay(client, vendor, project, amount, mode, cash)).EnsureSuccessStatusCode();
                }
                else if (roll < 75)
                {
                    HttpResponseMessage r = await Allocate(client, vendor, amount, mode, cash, "2026-06-01");
                    if (r.IsSuccessStatusCode)
                    {
                        liveMultiPayments.Add((await Json(r)).GetProperty("settlementId").GetInt64());
                    }
                }
                else if (roll < 92)
                {
                    (await Receipt(client, project, amount, mode, cash)).EnsureSuccessStatusCode();
                }
                else if (liveMultiPayments.Count > 0)
                {
                    int idx = rng.Next(liveMultiPayments.Count);
                    (await client.PostAsJsonAsync(
                        $"/api/v1/vendor-payments/{liveMultiPayments[idx]}/reverse", new { reason = "fuzz" }))
                        .EnsureSuccessStatusCode();
                    liveMultiPayments.RemoveAt(idx);
                }
            }
            catch
            {
                // A randomly-generated operation may be rejected (e.g. a validation rule).
                // That is fine — the point is that whatever *did* commit keeps the books straight.
            }

            if (step % 25 == 0)
            {
                AllControlsPass(await RunCheck(client), $"after {step} random operations");
            }
        }

        AllControlsPass(await RunCheck(client), "after 500 random operations");
    }
}
