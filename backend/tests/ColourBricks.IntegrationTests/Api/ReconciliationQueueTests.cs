using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Banking;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P4-T05 — reconciliation queue filters and bulk exclude (BRD §39).</summary>
public sealed class ReconciliationQueueTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Account(HttpClient c, string name) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == name).GetProperty("id").GetInt64();

    private async Task<long> SeedTx(long accountId, decimal debit, decimal credit, string date)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = new ImportBatch { AccountId = accountId, FileName = "seed.csv", Status = ImportBatchStatus.Committed };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        var tx = new BankTransaction
        {
            ImportBatchId = batch.Id, AccountId = accountId, ValueDate = DateOnly.Parse(date),
            Narration = "SEED", NormalisedNarration = "SEED", Debit = debit, Credit = credit,
            RowHash = Guid.NewGuid().ToString("n"), Status = BankTransactionStatus.Pending,
        };
        db.BankTransactions.Add(tx);
        await db.SaveChangesAsync();
        return tx.Id;
    }

    [Fact]
    public async Task Grid_FiltersByStatusAndDateRange()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long inRange = await SeedTx(hdfc, 1_000m, 0m, "2026-05-15");
        await SeedTx(hdfc, 2_000m, 0m, "2026-07-01"); // out of range

        JsonElement page = await Json(await c.GetAsync(
            $"/api/v1/reconciliation?accountId={hdfc}&status=Pending&dateFrom=2026-05-01&dateTo=2026-05-31"));

        var ids = page.GetProperty("items").EnumerateArray().Select(x => x.GetProperty("id").GetInt64()).ToList();
        ids.Should().Contain(inRange);
        ids.Should().HaveCount(1);
        page.GetProperty("items").EnumerateArray().First().GetProperty("type").GetString().Should().Be("Debit");
    }

    [Fact]
    public async Task BulkExclude_RequiresReason()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long t1 = await SeedTx(hdfc, 1_000m, 0m, "2026-05-15");
        long t2 = await SeedTx(hdfc, 2_000m, 0m, "2026-05-16");

        (await c.PostAsJsonAsync("/api/v1/bank-transactions/bulk-exclude", new { ids = new[] { t1, t2 }, reason = "" }))
            .StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);

        (await c.PostAsJsonAsync("/api/v1/bank-transactions/bulk-exclude",
            new { ids = new[] { t1, t2 }, reason = "not our account" })).EnsureSuccessStatusCode();

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.BankTransactions.Where(t => t.Id == t1 || t.Id == t2)
            .Select(t => t.Status).ToListAsync())
            .Should().OnlyContain(s => s == BankTransactionStatus.Excluded);
    }
}
