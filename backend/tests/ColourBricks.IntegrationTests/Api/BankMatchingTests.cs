using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Banking;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P4-T04 — match suggestion engine (BRD §32).</summary>
public sealed class BankMatchingTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1200;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> CreateProject(HttpClient c, string name) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name, code = $"CB-2026-{++_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 10_000_000m, estimatedCost = 8_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> CreateVendor(HttpClient c, string name) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true", new { name, types = new[] { "Vendor" } })))
        .GetProperty("id").GetInt64();

    private async Task<(long ModeId, long AccountId)> CashAndBank(HttpClient c)
    {
        long mode = (await Json(await c.GetAsync("/api/v1/payment-modes")))
            .EnumerateArray().First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();
        long acct = (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true")))
            .EnumerateArray().First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();
        return (mode, acct);
    }

    private async Task Purchase(HttpClient c, long projectId, long vendorId, decimal amount) =>
        (await c.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId, vendorId, date = "2026-05-01", total = amount,
            lines = new[] { new { itemName = "X", quantity = 1m, unit = "Nos", rate = amount, taxAmount = 0m } },
        })).EnsureSuccessStatusCode();

    private async Task<long> PayVendor(HttpClient c, long vendorId, long projectId, decimal amount, long mode, long acct, string? reference)
    {
        JsonElement dto = await Json(await c.PostAsJsonAsync("/api/v1/vendor-payments", new
        {
            vendorId, projectId, date = "2026-05-10", amount, paymentModeId = mode, accountId = acct,
            referenceNo = reference,
        }));
        return dto.GetProperty("id").GetInt64();
    }

    private async Task<long> SeedBankDebit(long accountId, decimal amount, string narration, string? reference, string date = "2026-05-10")
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
            Debit = amount, Credit = 0m, BankReference = reference,
            RowHash = Guid.NewGuid().ToString("n"), Status = BankTransactionStatus.Pending,
        };
        db.BankTransactions.Add(tx);
        await db.SaveChangesAsync();
        return tx.Id;
    }

    private async Task<JsonElement> Suggestions(HttpClient c, long txId) =>
        await Json(await c.GetAsync($"/api/v1/bank-transactions/{txId}/match-suggestions"));

    [Fact]
    public async Task Match_ExactAmountAndUtr_ScoresHighest()
    {
        HttpClient c = Client;
        (long mode, long bank) = await CashAndBank(c);
        long project = await CreateProject(c, "Match Utr P");
        long v1 = await CreateVendor(c, "Match Utr Vendor One");
        long v2 = await CreateVendor(c, "Match Utr Vendor Two");
        await Purchase(c, project, v1, 100_000m);
        await Purchase(c, project, v2, 100_000m);

        long withUtr = await PayVendor(c, v1, project, 100_000m, mode, bank, "UTR12345");
        await PayVendor(c, v2, project, 100_000m, mode, bank, null);

        long tx = await SeedBankDebit(bank, 100_000m, "SOME OPAQUE NARRATION", "UTR12345");
        JsonElement s = await Suggestions(c, tx);

        var top = s.GetProperty("suggestions").EnumerateArray().First();
        top.GetProperty("settlementId").GetInt64().Should().Be(withUtr);
        top.GetProperty("reasons").EnumerateArray().Select(x => x.GetString())
            .Should().Contain(r => r!.Contains("UTR"));
    }

    [Fact]
    public async Task Match_BrdSection32Scenario_SuggestsCorrectSettlement()
    {
        HttpClient c = Client;
        (long mode, long bank) = await CashAndBank(c);
        long project = await CreateProject(c, "S32 P");
        long abc = await CreateVendor(c, "ABC Hardware");
        await Purchase(c, project, abc, 100_000m);
        long payment = await PayVendor(c, abc, project, 100_000m, mode, bank, null);

        long tx = await SeedBankDebit(bank, 100_000m, "NEFT DR-ABC HARDWARE-N123", "N123");
        JsonElement s = await Suggestions(c, tx);

        s.GetProperty("suggestions").EnumerateArray().First()
            .GetProperty("settlementId").GetInt64().Should().Be(payment);
    }

    [Fact]
    public async Task Match_LearnsAliasFromManualMatch()
    {
        HttpClient c = Client;
        (long mode, long bank) = await CashAndBank(c);
        long project = await CreateProject(c, "Alias P");
        long abc = await CreateVendor(c, "ABC Hardware");
        await Purchase(c, project, abc, 50_000m);
        long payment = await PayVendor(c, abc, project, 50_000m, mode, bank, null);

        long tx = await SeedBankDebit(bank, 50_000m, "NEFT/ABCHRDW/998877", null);
        int before = (await Suggestions(c, tx)).GetProperty("suggestions").EnumerateArray()
            .First(x => x.GetProperty("settlementId").GetInt64() == payment).GetProperty("score").GetInt32();

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            var matcher = scope.ServiceProvider.GetRequiredService<Application.Banking.IMatchSuggestionService>();
            await matcher.RememberAliasesAsync(abc, "NEFT/ABCHRDW/998877", CancellationToken.None);
        }

        long tx2 = await SeedBankDebit(bank, 50_000m, "NEFT/ABCHRDW/112233", null);
        int after = (await Suggestions(c, tx2)).GetProperty("suggestions").EnumerateArray()
            .First(x => x.GetProperty("settlementId").GetInt64() == payment).GetProperty("score").GetInt32();

        after.Should().BeGreaterThan(before);
    }

    [Fact]
    public async Task Match_NoCandidate_ReturnsEmpty()
    {
        HttpClient c = Client;
        (_, long bank) = await CashAndBank(c);
        long tx = await SeedBankDebit(bank, 9_999m, "NOTHING MATCHES THIS", null);

        (await Suggestions(c, tx)).GetProperty("suggestions").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Match_NeverAutoReconciles()
    {
        HttpClient c = Client;
        (long mode, long bank) = await CashAndBank(c);
        long project = await CreateProject(c, "NoAuto P");
        long abc = await CreateVendor(c, "ABC Hardware");
        await Purchase(c, project, abc, 100_000m);
        await PayVendor(c, abc, project, 100_000m, mode, bank, "N123");
        long tx = await SeedBankDebit(bank, 100_000m, "NEFT DR-ABC HARDWARE-N123", "N123");

        await Suggestions(c, tx);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ReconciliationLinks.CountAsync()).Should().Be(0);
        (await db.BankTransactions.Where(t => t.Id == tx).Select(t => t.Status).FirstAsync())
            .Should().Be(BankTransactionStatus.Pending);
    }

    [Fact]
    public async Task Match_ExcludesAlreadyReconciledSettlements()
    {
        HttpClient c = Client;
        (long mode, long bank) = await CashAndBank(c);
        long project = await CreateProject(c, "Reconciled P");
        long abc = await CreateVendor(c, "ABC Hardware");
        await Purchase(c, project, abc, 200_000m);
        long payment = await PayVendor(c, abc, project, 100_000m, mode, bank, "N1");

        long tx1 = await SeedBankDebit(bank, 100_000m, "NEFT DR-ABC HARDWARE-N1", "N1");
        await Json(await c.PostAsJsonAsync($"/api/v1/bank-transactions/{tx1}/reconcile-debit",
            new { existingPaymentId = payment }));

        long tx2 = await SeedBankDebit(bank, 100_000m, "NEFT DR-ABC HARDWARE-N1", "N1");
        JsonElement s = await Suggestions(c, tx2);
        s.GetProperty("suggestions").EnumerateArray()
            .Should().NotContain(x => x.GetProperty("settlementId").GetInt64() == payment);
    }
}
