using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Banking;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P2-T07 — Customized and ad-hoc work (BRD §27).</summary>
public sealed class CustomWorkTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1950;

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient client, string code) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Custom Work Project {code}",
            code,
            status = "Ongoing",
            startDate = "2026-04-01",
            expectedEndDate = "2027-03-31",
            contractValue = 5_000_000m,
            estimatedCost = 4_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<JsonElement> Record(
        HttpClient client, long projectId, decimal estimated, decimal actual, long? partyId = null) =>
        await Json(await client.PostAsJsonAsync("/api/v1/custom-work", new
        {
            projectId,
            partyId,
            date = "2026-05-01",
            estimatedCost = estimated,
            actualCost = actual,
            workType = "Additional electrical work",
        }));

    private async Task<JsonElement> Breakdown(HttpClient client, long projectId) =>
        await Json(await client.GetAsync($"/api/v1/projects/{projectId}/cost-breakdown"));

    private async Task<long> Party(HttpClient c, string name) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true", new { name, types = new[] { "Vendor" } })))
        .GetProperty("id").GetInt64();

    private async Task<long> Mode(HttpClient c, string name) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes")))
        .EnumerateArray().First(m => m.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<decimal> CustomWorkOutstanding(HttpClient c, long projectId) =>
        (await Json(await c.GetAsync($"/api/v1/projects/{projectId}/outstanding-summary")))
        .GetProperty("customWorkPayable").GetDecimal();

    [Fact]
    public async Task CustomWork_ActualCost_PostsToLedger()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-671");

        await Record(client, projectId, estimated: 70_000m, actual: 85_000m);

        JsonElement ledger = await Json(await client.GetAsync($"/api/v1/projects/{projectId}/ledger"));
        decimal customDebit = ledger.EnumerateArray()
            .Where(r => r.GetProperty("bucket").GetString() == "Customized Work")
            .Sum(r => r.GetProperty("debit").GetDecimal());

        customDebit.Should().Be(85_000m); // actual, not estimated
    }

    [Fact]
    public async Task CustomWork_EstimatedCost_DoesNotPost()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-672");

        await Record(client, projectId, estimated: 70_000m, actual: 85_000m);

        // The 70,000 estimate appears nowhere in the ledger.
        JsonElement ledger = await Json(await client.GetAsync($"/api/v1/projects/{projectId}/ledger"));
        ledger.EnumerateArray().Any(r => r.GetProperty("debit").GetDecimal() == 70_000m).Should().BeFalse();
        ledger.EnumerateArray().Any(r => r.GetProperty("credit").GetDecimal() == 70_000m).Should().BeFalse();
    }

    [Fact]
    public async Task CustomWork_AppearsInExpenseBreakdown()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-673");

        await Record(client, projectId, estimated: 70_000m, actual: 85_000m);

        (await Breakdown(client, projectId)).GetProperty("Customized Work").GetDecimal().Should().Be(85_000m);
    }

    [Fact]
    public async Task CustomWork_Variance_CalculatedCorrectly()
    {
        HttpClient client = Client;
        long projectId = await CreateProject(client, "CB-2026-674");

        JsonElement work = await Record(client, projectId, estimated: 70_000m, actual: 85_000m);

        work.GetProperty("estimatedCost").GetDecimal().Should().Be(70_000m);
        work.GetProperty("actualCost").GetDecimal().Should().Be(85_000m);
        work.GetProperty("variance").GetDecimal().Should().Be(15_000m);
    }

    // ── payment / reversal / bank-match (client request, 2026-09-04) ─────────

    [Fact]
    public async Task CustomWork_CanBePaid_ReducesOutstanding()
    {
        HttpClient c = Client;
        long project = await CreateProject(c, $"CB-2026-{++_seq}");
        long party = await Party(c, "Custom Work Contractor A");
        long cash = await Mode(c, "Cash");
        await Record(c, project, estimated: 50_000m, actual: 60_000m, partyId: party);

        JsonElement paid = await Json(await c.PostAsJsonAsync("/api/v1/custom-work-payments", new
        {
            projectId = project, partyId = party, date = "2026-05-10", amount = 40_000m, paymentModeId = cash,
        }));

        paid.GetProperty("outstandingAfter").GetDecimal().Should().Be(20_000m);
        (await CustomWorkOutstanding(c, project)).Should().Be(20_000m);
    }

    [Fact]
    public async Task CustomWork_PayMoreThanOutstanding_Returns400()
    {
        HttpClient c = Client;
        long project = await CreateProject(c, $"CB-2026-{++_seq}");
        long party = await Party(c, "Custom Work Contractor B");
        long cash = await Mode(c, "Cash");
        await Record(c, project, estimated: 50_000m, actual: 60_000m, partyId: party);

        HttpResponseMessage r = await c.PostAsJsonAsync("/api/v1/custom-work-payments", new
        {
            projectId = project, partyId = party, date = "2026-05-10", amount = 90_000m, paymentModeId = cash,
        });

        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task CustomWorkPayment_Reversed_RestoresOutstanding()
    {
        HttpClient c = Client;
        long project = await CreateProject(c, $"CB-2026-{++_seq}");
        long party = await Party(c, "Custom Work Contractor C");
        long cash = await Mode(c, "Cash");
        await Record(c, project, estimated: 50_000m, actual: 60_000m, partyId: party);
        JsonElement paid = await Json(await c.PostAsJsonAsync("/api/v1/custom-work-payments", new
        {
            projectId = project, partyId = party, date = "2026-05-10", amount = 40_000m, paymentModeId = cash,
        }));
        long paymentId = paid.GetProperty("id").GetInt64();

        (await c.PostAsJsonAsync($"/api/v1/custom-work-payments/{paymentId}/reverse", new { reason = "wrong amount" }))
            .EnsureSuccessStatusCode();

        (await CustomWorkOutstanding(c, project)).Should().Be(60_000m);
    }

    [Fact]
    public async Task CustomWork_Reversed_AlsoReversesActivePaymentAgainstIt()
    {
        HttpClient c = Client;
        long project = await CreateProject(c, $"CB-2026-{++_seq}");
        long party = await Party(c, "Custom Work Contractor D");
        long cash = await Mode(c, "Cash");
        JsonElement work = await Record(c, project, estimated: 50_000m, actual: 60_000m, partyId: party);
        long workId = work.GetProperty("id").GetInt64();

        await c.PostAsJsonAsync("/api/v1/custom-work-payments", new
        {
            projectId = project, partyId = party, date = "2026-05-10", amount = 40_000m, paymentModeId = cash,
            customWorkId = workId,
        });
        (await CustomWorkOutstanding(c, project)).Should().Be(20_000m);

        (await c.PostAsJsonAsync($"/api/v1/custom-work/{workId}/reverse", new { reason = "duplicate entry" }))
            .EnsureSuccessStatusCode();

        // Both the 60k obligation and the 40k payment against it are gone -> net zero,
        // not a lingering -40k (cash paid against a payable that no longer exists).
        (await CustomWorkOutstanding(c, project)).Should().Be(0m);
    }

    [Fact]
    public async Task MapDebit_CustomWorkTarget_PostsPaymentAndLinksToBankTransaction()
    {
        HttpClient c = Client;
        long project = await CreateProject(c, $"CB-2026-{++_seq}");
        long party = await Party(c, "Custom Work Contractor E");
        await Record(c, project, estimated: 50_000m, actual: 60_000m, partyId: party);
        long hdfc = (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true")))
            .EnumerateArray().First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();
        long tx = await SeedBankTx(hdfc, 40_000m, "NEFT DR CUSTOM WORK");

        JsonElement res = await Json(await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[]
            {
                new
                {
                    target = "CustomWork", amount = 40_000m, description = "Contractor D settlement",
                    projectId = (long?)project, vendorId = (long?)party,
                },
            },
        }));

        res.GetProperty("status").GetString().Should().Be("Reconciled");
        (await CustomWorkOutstanding(c, project)).Should().Be(20_000m);
    }

    [Fact]
    public async Task Unreconcile_CustomWorkMap_ReversesPayment()
    {
        HttpClient c = Client;
        long project = await CreateProject(c, $"CB-2026-{++_seq}");
        long party = await Party(c, "Custom Work Contractor F");
        await Record(c, project, estimated: 50_000m, actual: 60_000m, partyId: party);
        long hdfc = (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true")))
            .EnumerateArray().First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();
        long tx = await SeedBankTx(hdfc, 40_000m, "NEFT DR CUSTOM WORK REV");

        await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/map-debit", new
        {
            allocations = new[]
            {
                new
                {
                    target = "CustomWork", amount = 40_000m, description = "Contractor F settlement",
                    projectId = (long?)project, vendorId = (long?)party,
                },
            },
        });
        (await CustomWorkOutstanding(c, project)).Should().Be(20_000m);

        (await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx}/unreconcile", new { reason = "wrong contractor" }))
            .EnsureSuccessStatusCode();

        (await CustomWorkOutstanding(c, project)).Should().Be(60_000m);
    }

    private async Task<long> SeedBankTx(long accountId, decimal debit, string narration)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = new ImportBatch { AccountId = accountId, FileName = "seed.csv", Status = ImportBatchStatus.Committed };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        var tx = new BankTransaction
        {
            ImportBatchId = batch.Id, AccountId = accountId, ValueDate = DateOnly.Parse("2026-08-12"),
            Narration = narration, NormalisedNarration = narration.ToUpperInvariant(),
            Debit = debit, Credit = 0m, BankReference = $"CW-{Guid.NewGuid():n}",
            RowHash = Guid.NewGuid().ToString("n"), Status = BankTransactionStatus.Pending,
        };
        db.BankTransactions.Add(tx);
        await db.SaveChangesAsync();
        return tx.Id;
    }
}
