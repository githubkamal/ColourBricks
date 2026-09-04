using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P4-T01 — staged bank-statement import: review table, project mapping, commit.</summary>
public sealed class BankImportTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1000;

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

    private async Task<long> AccountId(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();

    private static object Row(int line, string date, string narration, decimal debit, decimal credit,
        string? reference = null, string? parseError = null) =>
        new { sourceLineNo = line, valueDate = date, narration, debit, credit, bankReference = reference, parseError };

    private async Task<JsonElement> CreateImport(HttpClient client, long accountId, params object[] rows) =>
        await Json(await client.PostAsJsonAsync("/api/v1/bank-imports", new
        {
            accountId, fileName = "hdfc-may.csv", rows,
        }));

    private static long RowId(JsonElement batch, int sourceLineNo) =>
        batch.GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("sourceLineNo").GetInt32() == sourceLineNo)
            .GetProperty("id").GetInt64();

    private static Task<HttpResponseMessage> SetAllocations(
        HttpClient client, long batchId, long rowId, params (long ProjectId, decimal Amount)[] allocations) =>
        client.PutAsJsonAsync($"/api/v1/bank-imports/{batchId}/rows/{rowId}/allocations", new
        {
            allocations = allocations.Select(a => new { projectId = a.ProjectId, amount = a.Amount }),
        });

    [Fact]
    public async Task Upload_CreatesDraftBatch_NoBankTransactionsYet()
    {
        HttpClient client = Client;
        long account = await AccountId(client);

        JsonElement batch = await CreateImport(client, account,
            Row(1, "2026-05-01", "NEFT ABC HARDWARE", 25_000m, 0m),
            Row(2, "2026-05-02", "UPI CLIENT ADVANCE", 0m, 500_000m));

        batch.GetProperty("status").GetString().Should().Be("Draft");
        batch.GetProperty("counts").GetProperty("total").GetInt32().Should().Be(2);
        batch.GetProperty("rows").GetArrayLength().Should().Be(2);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.BankTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Commit_Blocked_WhenAnyRowUnmappedOrPartiallyAllocated()
    {
        HttpClient client = Client;
        long account = await AccountId(client);
        long projectA = await CreateProject(client, "BI Block A");

        JsonElement batch = await CreateImport(client, account,
            Row(1, "2026-05-01", "NEFT ABC HARDWARE", 25_000m, 0m),
            Row(2, "2026-05-02", "NEFT XYZ TRADERS", 40_000m, 0m));
        long batchId = batch.GetProperty("id").GetInt64();

        // Only line 1 mapped; line 2 left unmapped.
        (await SetAllocations(client, batchId, RowId(batch, 1), (projectA, 25_000m))).EnsureSuccessStatusCode();

        HttpResponseMessage commit = await client.PostAsync($"/api/v1/bank-imports/{batchId}/commit", null);
        commit.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await commit.Content.ReadAsStringAsync()).Should().Contain("line 2");
    }

    [Fact]
    public async Task Commit_CreditRow_RejectsMultipleProjects()
    {
        HttpClient client = Client;
        long account = await AccountId(client);
        long a = await CreateProject(client, "BI Credit A");
        long b = await CreateProject(client, "BI Credit B");

        JsonElement batch = await CreateImport(client, account,
            Row(1, "2026-05-01", "RTGS CLIENT RECEIPT", 0m, 500_000m));
        long batchId = batch.GetProperty("id").GetInt64();

        HttpResponseMessage split = await SetAllocations(client, batchId, RowId(batch, 1),
            (a, 300_000m), (b, 200_000m));
        split.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await split.Content.ReadAsStringAsync()).Should().Contain("§34");

        // A single project is accepted.
        (await SetAllocations(client, batchId, RowId(batch, 1), (a, 500_000m))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Commit_DebitRow_AllocationsMustSumToDebit()
    {
        HttpClient client = Client;
        long account = await AccountId(client);
        long a = await CreateProject(client, "BI Sum A");
        long b = await CreateProject(client, "BI Sum B");

        JsonElement batch = await CreateImport(client, account,
            Row(1, "2026-05-01", "NEFT MULTI PROJECT VENDOR", 100_000m, 0m));
        long batchId = batch.GetProperty("id").GetInt64();

        HttpResponseMessage bad = await SetAllocations(client, batchId, RowId(batch, 1),
            (a, 25_000m), (b, 60_000m)); // 85k != 100k
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        (await SetAllocations(client, batchId, RowId(batch, 1),
            (a, 25_000m), (b, 75_000m))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Commit_PromotesSurvivorsAsPending_WithProjectHints()
    {
        HttpClient client = Client;
        long account = await AccountId(client);
        long a = await CreateProject(client, "BI Commit A");
        long b = await CreateProject(client, "BI Commit B");

        JsonElement batch = await CreateImport(client, account,
            Row(1, "2026-05-01", "NEFT ABC HARDWARE MULTI", 60_000m, 0m),
            Row(2, "2026-05-02", "RTGS CLIENT RECEIPT", 0m, 800_000m),
            Row(3, "2026-05-03", "ATM CASH WITHDRAWAL", 10_000m, 0m)); // junk — will be removed
        long batchId = batch.GetProperty("id").GetInt64();

        await SetAllocations(client, batchId, RowId(batch, 1), (a, 40_000m), (b, 20_000m));
        await SetAllocations(client, batchId, RowId(batch, 2), (a, 800_000m));
        (await client.DeleteAsync($"/api/v1/bank-imports/{batchId}/rows/{RowId(batch, 3)}?reason=TestRemove"))
            .EnsureSuccessStatusCode();

        JsonElement result = await Json(await client.PostAsync($"/api/v1/bank-imports/{batchId}/commit", null));
        result.GetProperty("committed").GetInt32().Should().Be(2);
        result.GetProperty("removed").GetInt32().Should().Be(1);

        JsonElement after = await Json(await client.GetAsync($"/api/v1/bank-imports/{batchId}"));
        after.GetProperty("status").GetString().Should().Be("Committed");

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var txns = await db.BankTransactions.Include(t => t.ProjectHints)
            .Where(t => t.ImportBatchId == batchId).ToListAsync();

        txns.Should().HaveCount(2);
        txns.Should().OnlyContain(t => t.Status == Domain.Banking.BankTransactionStatus.Pending);
        txns.Single(t => t.Debit == 60_000m).ProjectHints.Sum(h => h.Amount).Should().Be(60_000m);
        txns.Single(t => t.Credit == 800_000m).ProjectHints.Should().ContainSingle();

        // The import creates no settlement — reconciliation does that later (P4-T05+).
        (await db.Settlements.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DeletedStagedRow_NotRemembered_ReappearsOnReimport()
    {
        HttpClient client = Client;
        long account = await AccountId(client);
        long a = await CreateProject(client, "BI Reimport A");

        object keep = Row(1, "2026-05-01", "NEFT ABC HARDWARE", 25_000m, 0m);
        object junk = Row(2, "2026-05-02", "BANK CHARGES", 118m, 0m);

        JsonElement first = await CreateImport(client, account, keep, junk);
        long firstId = first.GetProperty("id").GetInt64();
        await SetAllocations(client, firstId, RowId(first, 1), (a, 25_000m));
        (await client.DeleteAsync($"/api/v1/bank-imports/{firstId}/rows/{RowId(first, 2)}?reason=TestRemove"))
            .EnsureSuccessStatusCode();
        (await Json(await client.PostAsync($"/api/v1/bank-imports/{firstId}/commit", null)))
            .GetProperty("committed").GetInt32().Should().Be(1);

        // Re-import the same file. The kept row is now a duplicate; the deleted junk row is back.
        JsonElement second = await CreateImport(client, account, keep, junk);
        var rows = second.GetProperty("rows").EnumerateArray().ToList();

        rows.Single(r => r.GetProperty("sourceLineNo").GetInt32() == 1)
            .GetProperty("duplicateOfBankTransactionId").ValueKind.Should().Be(JsonValueKind.Number);
        rows.Single(r => r.GetProperty("sourceLineNo").GetInt32() == 2)
            .GetProperty("duplicateOfBankTransactionId").ValueKind.Should().Be(JsonValueKind.Null);
        rows.Single(r => r.GetProperty("sourceLineNo").GetInt32() == 2)
            .GetProperty("isRemoved").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public async Task ImportBatch_CountsReconcileToFileRowCount()
    {
        HttpClient client = Client;
        long account = await AccountId(client);
        long a = await CreateProject(client, "BI Counts A");

        // 6 rows: 3 mapped+committed, 1 removed, 1 parse error, 1 duplicate of a prior commit.
        object dup = Row(10, "2026-05-10", "NEFT SEED PAYMENT", 5_000m, 0m);
        JsonElement seed = await CreateImport(client, account, dup);
        await SetAllocations(client, seed.GetProperty("id").GetInt64(), RowId(seed, 10), (a, 5_000m));
        await client.PostAsync($"/api/v1/bank-imports/{seed.GetProperty("id").GetInt64()}/commit", null);

        JsonElement batch = await CreateImport(client, account,
            Row(1, "2026-05-01", "NEFT ONE", 1_000m, 0m),
            Row(2, "2026-05-02", "NEFT TWO", 2_000m, 0m),
            Row(3, "2026-05-03", "NEFT THREE", 3_000m, 0m),
            Row(4, "2026-05-04", "NEFT JUNK", 9_000m, 0m),
            Row(5, null!, "UNPARSEABLE", 0m, 0m, parseError: "bad date"),
            dup);
        long batchId = batch.GetProperty("id").GetInt64();

        await SetAllocations(client, batchId, RowId(batch, 1), (a, 1_000m));
        await SetAllocations(client, batchId, RowId(batch, 2), (a, 2_000m));
        await SetAllocations(client, batchId, RowId(batch, 3), (a, 3_000m));
        await client.DeleteAsync($"/api/v1/bank-imports/{batchId}/rows/{RowId(batch, 4)}?reason=TestRemove");

        JsonElement counts = (await Json(await client.GetAsync($"/api/v1/bank-imports/{batchId}")))
            .GetProperty("counts");
        counts.GetProperty("total").GetInt32().Should().Be(6);
        counts.GetProperty("mapped").GetInt32().Should().Be(3);
        counts.GetProperty("removed").GetInt32().Should().Be(1);
        counts.GetProperty("parseError").GetInt32().Should().Be(1);
        counts.GetProperty("duplicate").GetInt32().Should().Be(1);

        // A duplicate row now blocks commit until it's explicitly removed too (client
        // request, 2026-09-04) — it doesn't just get silently skipped.
        await client.DeleteAsync($"/api/v1/bank-imports/{batchId}/rows/{RowId(batch, 10)}?reason=TestRemove");

        JsonElement result = await Json(await client.PostAsync($"/api/v1/bank-imports/{batchId}/commit", null));
        result.GetProperty("committed").GetInt32().Should().Be(3);
        (result.GetProperty("committed").GetInt32()
            + result.GetProperty("removed").GetInt32()
            + result.GetProperty("duplicate").GetInt32()
            + result.GetProperty("parseError").GetInt32()).Should().Be(6);
    }

    [Fact]
    public async Task Exclude_OnCommittedRow_SetsStatusAndReason_DoesNotDelete()
    {
        HttpClient client = Client;
        long account = await AccountId(client);
        long a = await CreateProject(client, "BI Exclude A");

        JsonElement batch = await CreateImport(client, account,
            Row(1, "2026-05-01", "NEFT SOMETHING", 7_500m, 0m));
        long batchId = batch.GetProperty("id").GetInt64();
        await SetAllocations(client, batchId, RowId(batch, 1), (a, 7_500m));
        await client.PostAsync($"/api/v1/bank-imports/{batchId}/commit", null);

        long txnId;
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            txnId = await db.BankTransactions.Where(t => t.ImportBatchId == batchId)
                .Select(t => t.Id).FirstAsync();
        }

        (await client.PostAsJsonAsync($"/api/v1/bank-transactions/{txnId}/exclude", new { reason = "not ours" }))
            .EnsureSuccessStatusCode();

        JsonElement tx = await Json(await client.GetAsync($"/api/v1/bank-transactions/{txnId}"));
        tx.GetProperty("status").GetString().Should().Be("Excluded");
        tx.GetProperty("exclusionReason").GetString().Should().Be("not ours");
    }
}
