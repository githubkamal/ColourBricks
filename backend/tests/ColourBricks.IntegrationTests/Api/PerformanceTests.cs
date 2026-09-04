using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Ledger;
using ColourBricks.Infrastructure.Diagnostics;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P9-T02 — volume performance and N+1 guards.</summary>
[Trait("Category", "Performance")]
public sealed class PerformanceTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 4400;
    private QueryCounter Counter => Factory.Services.GetRequiredService<QueryCounter>();

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"PERF P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 50_000_000m, estimatedCost = 40_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task SeedLedger(long projectId, int count)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        long cost = await db.ExpenseCategories.Where(x => x.Slug == "electrical").Select(x => x.Id).FirstAsync();
        long income = await db.ExpenseCategories.Where(x => x.Slug == "project_income").Select(x => x.Id).FirstAsync();
        long account = await db.Accounts.OrderBy(a => a.Id).Select(a => a.Id).FirstAsync();
        var now = DateTimeOffset.UtcNow;

        for (int batch = 0; batch < count; batch += 1_000)
        {
            int take = Math.Min(1_000, count - batch);
            for (int i = 0; i < take; i++)
            {
                int n = batch + i;
                db.LedgerEntries.Add(new LedgerEntry
                {
                    EntryDate = new DateOnly(2026, 4, 1).AddDays(n % 300),
                    ProjectId = projectId,
                    CategoryId = n % 6 == 0 ? income : cost,
                    AccountId = n % 3 == 0 ? account : null,
                    Debit = 1_000m,
                    Credit = 0m,
                    SourceType = "PerfSeed",
                    SourceId = n + 1,
                    CreatedAtUtc = now,
                });
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    private async Task SeedBankTransactions(int count)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        long account = await db.Accounts.OrderBy(a => a.Id).Select(a => a.Id).FirstAsync();
        var b = new ImportBatch { AccountId = account, FileName = "perf.csv", Status = ImportBatchStatus.Committed };
        db.ImportBatches.Add(b);
        await db.SaveChangesAsync();
        var now = DateTimeOffset.UtcNow;

        for (int batch = 0; batch < count; batch += 1_000)
        {
            int take = Math.Min(1_000, count - batch);
            for (int i = 0; i < take; i++)
            {
                int n = batch + i;
                db.BankTransactions.Add(new BankTransaction
                {
                    ImportBatchId = b.Id, AccountId = account,
                    ValueDate = new DateOnly(2026, 4, 1).AddDays(n % 300),
                    Narration = $"PERF TXN {n}", NormalisedNarration = $"PERF TXN {n}",
                    Credit = n % 2 == 0 ? 5_000m : 0m, Debit = n % 2 == 0 ? 0m : 5_000m,
                    RowHash = Guid.NewGuid().ToString("n"),
                    Status = BankTransactionStatus.Pending, CreatedAtUtc = now,
                });
            }

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
        }
    }

    [Fact]
    public async Task Dashboard_UnderOneSecond_AtVolume()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        await SeedLedger(project, 5_000);

        var sw = Stopwatch.StartNew();
        (await c.GetAsync($"/api/v1/projects/{project}/dashboard")).EnsureSuccessStatusCode();
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task ReconciliationQueue_UnderOnePointFiveSeconds_AtVolume()
    {
        HttpClient c = Admin;
        await SeedBankTransactions(4_000);

        var sw = Stopwatch.StartNew();
        (await c.GetAsync("/api/v1/reconciliation?pageSize=100")).EnsureSuccessStatusCode();
        sw.Stop();

        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public async Task NoNPlusOne_OnListEndpoints()
    {
        HttpClient c = Admin;
        long project = await Project(c);

        foreach (string path in new[]
                 {
                     "/api/v1/projects?pageSize=25",
                     $"/api/v1/reports/run/project-ledger?projectId={project}&datePreset=ThisFinancialYear&pageSize=25",
                 })
        {
            await SeedLedger(project, 30);
            Counter.Reset();
            (await c.GetAsync(path)).EnsureSuccessStatusCode();
            long small = Counter.Count;

            await SeedLedger(project, 300);
            Counter.Reset();
            (await c.GetAsync(path)).EnsureSuccessStatusCode();
            long large = Counter.Count;

            small.Should().BeLessThan(25, $"{path} should not fan out per row");
            large.Should().BeLessThanOrEqualTo(small + 2, $"{path} query count must not scale with row count");
        }
    }
}
