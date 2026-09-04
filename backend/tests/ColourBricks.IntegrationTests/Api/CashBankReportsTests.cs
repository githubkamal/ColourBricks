using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Banking;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P8-T05 — cash/bank (§55) and reconciliation (§38) reports on the framework.</summary>
public sealed class CashBankReportsTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 4000;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Account(HttpClient c, string name) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
        .First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"CB P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 9_000_000m, estimatedCost = 7_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> Client_(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true",
            new { name = $"CB Client {++_seq}", types = new[] { "Client" } }))).GetProperty("id").GetInt64();

    private async Task Receipt(HttpClient c, long project, decimal amount, long account)
    {
        long mode = (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
            .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();
        (await c.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId = project, type = "ClientAdvance", date = "2026-05-05", amount,
            paymentModeId = mode, accountId = account,
        })).EnsureSuccessStatusCode();
    }

    private async Task<long> SeedBankTx(long accountId, decimal debit, decimal credit, string narration, string date = "2026-05-10")
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = new ImportBatch { AccountId = accountId, FileName = "seed.csv", Status = ImportBatchStatus.Committed };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        var tx = new BankTransaction
        {
            ImportBatchId = batch.Id, AccountId = accountId, ValueDate = DateOnly.Parse(date),
            Narration = narration, NormalisedNarration = narration.ToUpperInvariant(),
            Debit = debit, Credit = credit,
            RowHash = Guid.NewGuid().ToString("n"), Status = BankTransactionStatus.Pending,
        };
        db.BankTransactions.Add(tx);
        await db.SaveChangesAsync();
        return tx.Id;
    }

    private static IEnumerable<JsonElement> Rows(JsonElement r) => r.GetProperty("rows").EnumerateArray();

    [Fact]
    public async Task AccountStatement_ClosingBalance_MatchesAccountBalance()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c);
        await Receipt(c, project, 750_000m, hdfc);

        decimal accountBalance = (await Json(await c.GetAsync($"/api/v1/accounts/{hdfc}")))
            .GetProperty("balance").GetDecimal();

        JsonElement report = await Json(await c.GetAsync(
            $"/api/v1/reports/run/account-statement?accountId={hdfc}&pageSize=50"));

        JsonElement row = Rows(report).Single();
        row.GetProperty("closing").GetDecimal().Should().Be(accountBalance);
        (row.GetProperty("opening").GetDecimal() + row.GetProperty("credits").GetDecimal()
            - row.GetProperty("debits").GetDecimal()).Should().Be(accountBalance);
    }

    [Fact]
    public async Task ReconciliationExceptions_CatchesAmountMismatch()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long project = await Project(c);
        long client = await Client_(c);
        long tx = await SeedBankTx(hdfc, 0m, 500_000m, $"RTGS ADVANCE {_seq}");

        long settlementId = (await Json(await c.PostAsJsonAsync(
            $"/api/v1/bank-transactions/{tx}/reconcile-credit",
            new { clientId = client, projectId = project, incomeType = "ClientAdvance" })))
            .GetProperty("settlementId").GetInt64();

        // Deliberately break the allocation so bank amount (500k) != settlement amount.
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var s = await db.Settlements.FirstAsync(x => x.Id == settlementId);
            s.Amount -= 25_000m;
            await db.SaveChangesAsync();
        }

        JsonElement report = await Json(await c.GetAsync(
            "/api/v1/reports/run/bank-reconciliation-exceptions?pageSize=500"));

        JsonElement row = Rows(report).First(r => r.GetProperty("narration").GetString()!.Contains($"ADVANCE {_seq}"));
        row.GetProperty("label").GetString().Should().Be("Bank amount ≠ allocation");
        row.GetProperty("difference").GetDecimal().Should().Be(25_000m);
    }

    [Fact]
    public async Task ReconciliationExceptions_CatchesUnmatchedRows()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long tx = await SeedBankTx(hdfc, 0m, 987_654.32m, $"UNMATCHED MYSTERY CREDIT {_seq}");

        JsonElement report = await Json(await c.GetAsync(
            "/api/v1/reports/run/bank-reconciliation-exceptions?pageSize=500"));

        Rows(report).Should().Contain(r =>
            r.GetProperty("narration").GetString()!.Contains($"MYSTERY CREDIT {_seq}")
            && r.GetProperty("label").GetString() == "No suggested match");
    }

    [Fact]
    public async Task ExcludedReport_ShowsReasonAndUser()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long tx = await SeedBankTx(hdfc, 12_000m, 0m, $"BANK CHARGES {_seq}");

        (await c.PostAsJsonAsync("/api/v1/bank-transactions/bulk-exclude", new
        {
            ids = new[] { tx }, reason = "Bank charge, not a project expense",
        })).EnsureSuccessStatusCode();

        JsonElement report = await Json(await c.GetAsync(
            "/api/v1/reports/run/excluded-transaction-report?pageSize=500"));

        JsonElement row = Rows(report).First(r => r.GetProperty("narration").GetString()!.Contains($"CHARGES {_seq}"));
        row.GetProperty("reason").GetString().Should().Be("Bank charge, not a project expense");
        row.GetProperty("user").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ControlReport_RendersAllFiveBrdSection38Controls()
    {
        HttpClient c = Admin;
        JsonElement report = await Json(await c.GetAsync(
            "/api/v1/reports/run/reconciliation-control-report?pageSize=50"));

        report.GetProperty("totalCount").GetInt32().Should().Be(5);
        Rows(report).Select(r => r.GetProperty("label").GetString())
            .Should().Contain("Bank");
    }
}
