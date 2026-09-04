using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Banking;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P4-T08 — internal bank transfers (BRD §36).</summary>
public sealed class InternalTransferTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
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

    private async Task<decimal> Balance(HttpClient c, long acct) =>
        (await Json(await c.GetAsync($"/api/v1/accounts/{acct}"))).GetProperty("balance").GetDecimal();

    private async Task<long> SeedTx(long accountId, decimal debit, decimal credit, string date = "2026-05-10")
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var batch = new ImportBatch { AccountId = accountId, FileName = "seed.csv", Status = ImportBatchStatus.Committed };
        db.ImportBatches.Add(batch);
        await db.SaveChangesAsync();
        var tx = new BankTransaction
        {
            ImportBatchId = batch.Id, AccountId = accountId, ValueDate = DateOnly.Parse(date),
            Narration = debit > 0 ? "TRANSFER OUT" : "TRANSFER IN",
            NormalisedNarration = debit > 0 ? "TRANSFER OUT" : "TRANSFER IN",
            Debit = debit, Credit = credit, RowHash = Guid.NewGuid().ToString("n"),
            Status = BankTransactionStatus.Pending,
        };
        db.BankTransactions.Add(tx);
        await db.SaveChangesAsync();
        return tx.Id;
    }

    private async Task<BankTransactionStatus> Status(long id)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await db.BankTransactions.Where(t => t.Id == id).Select(t => t.Status).FirstAsync();
    }

    [Fact]
    public async Task InternalTransfer_BrdSection36Example_NetCompanyPositionUnchanged()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long sbi = await Account(c, "SBI");
        decimal hdfcBefore = await Balance(c, hdfc);
        decimal sbiBefore = await Balance(c, sbi);

        long outTx = await SeedTx(hdfc, 200_000m, 0m);
        long inTx = await SeedTx(sbi, 0m, 200_000m);

        JsonElement transfer = await Json(await c.PostAsJsonAsync("/api/v1/internal-transfers",
            new { fromTransactionId = outTx, toTransactionId = inTx }));
        transfer.GetProperty("amount").GetDecimal().Should().Be(200_000m);

        (await Balance(c, hdfc)).Should().Be(hdfcBefore - 200_000m);
        (await Balance(c, sbi)).Should().Be(sbiBefore + 200_000m);
        ((await Balance(c, hdfc)) + (await Balance(c, sbi)))
            .Should().Be(hdfcBefore + sbiBefore); // company net unchanged
        (await Status(outTx)).Should().Be(BankTransactionStatus.InternalTransfer);
        (await Status(inTx)).Should().Be(BankTransactionStatus.InternalTransfer);
    }

    [Fact]
    public async Task InternalTransfer_DoesNotAffectProjectIncomeOrExpense()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long sbi = await Account(c, "SBI");
        long project = (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Transfer Isolation P", code = "CB-2026-1490", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 10_000_000m, estimatedCost = 8_000_000m,
        }))).GetProperty("id").GetInt64();

        long outTx = await SeedTx(hdfc, 150_000m, 0m);
        long inTx = await SeedTx(sbi, 0m, 150_000m);
        await c.PostAsJsonAsync("/api/v1/internal-transfers", new { fromTransactionId = outTx, toTransactionId = inTx });

        (await Json(await c.GetAsync($"/api/v1/projects/{project}/income-total"))).GetProperty("total").GetDecimal()
            .Should().Be(0m);
        JsonElement summary = await Json(await c.GetAsync($"/api/v1/projects/{project}/outstanding-summary"));
        summary.GetProperty("totalPayable").GetDecimal().Should().Be(0m);

        // The P3-T07 integrity controls still pass.
        (await Json(await c.GetAsync("/api/v1/admin/integrity-check"))).GetProperty("passed").GetBoolean()
            .Should().BeTrue();
    }

    [Fact]
    public async Task InternalTransfer_AutoSuggestsMatchingPair()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long sbi = await Account(c, "SBI");
        long outTx = await SeedTx(hdfc, 75_000m, 0m, "2026-05-10");
        long inTx = await SeedTx(sbi, 0m, 75_000m, "2026-05-11");

        JsonElement suggestions = await Json(await c.GetAsync("/api/v1/internal-transfers/suggestions"));
        suggestions.EnumerateArray().Should().Contain(s =>
            s.GetProperty("debitTransactionId").GetInt64() == outTx
            && s.GetProperty("creditTransactionId").GetInt64() == inTx);
    }

    [Fact]
    public async Task InternalTransfer_Unpair_RestoresPending()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long sbi = await Account(c, "SBI");
        decimal hdfcBefore = await Balance(c, hdfc);
        long outTx = await SeedTx(hdfc, 90_000m, 0m);
        long inTx = await SeedTx(sbi, 0m, 90_000m);
        long transferId = (await Json(await c.PostAsJsonAsync("/api/v1/internal-transfers",
            new { fromTransactionId = outTx, toTransactionId = inTx }))).GetProperty("id").GetInt64();

        (await c.PostAsync($"/api/v1/internal-transfers/{transferId}/unpair", null)).EnsureSuccessStatusCode();

        (await Status(outTx)).Should().Be(BankTransactionStatus.Pending);
        (await Status(inTx)).Should().Be(BankTransactionStatus.Pending);
        (await Balance(c, hdfc)).Should().Be(hdfcBefore); // ledger effect reversed
    }

    [Fact]
    public async Task InternalTransfer_SameAccount_Returns400()
    {
        HttpClient c = Admin;
        long hdfc = await Account(c, "HDFC");
        long outTx = await SeedTx(hdfc, 50_000m, 0m);
        long inTx = await SeedTx(hdfc, 0m, 50_000m);

        HttpResponseMessage r = await c.PostAsJsonAsync("/api/v1/internal-transfers",
            new { fromTransactionId = outTx, toTransactionId = inTx });
        r.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
