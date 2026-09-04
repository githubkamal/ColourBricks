using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P4-T03 — duplicate detection and idempotent import (BRD §31, rule 51).</summary>
public sealed class BankImportDedupeTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1100;

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

    private async Task<long> HdfcAccountId(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();

    private static object Row(int line, string date, string narration, decimal debit, decimal credit,
        string? reference = null) =>
        new { sourceLineNo = line, valueDate = date, narration, debit, credit, bankReference = reference };

    private async Task<JsonElement> Stage(HttpClient client, long accountId, params object[] rows) =>
        await Json(await client.PostAsJsonAsync("/api/v1/bank-imports", new
        {
            accountId, fileName = "hdfc.csv", rows,
        }));

    private static long RowId(JsonElement batch, int line) =>
        batch.GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("sourceLineNo").GetInt32() == line).GetProperty("id").GetInt64();

    private async Task MapAll(HttpClient client, long batchId, JsonElement batch, long project)
    {
        foreach (JsonElement row in batch.GetProperty("rows").EnumerateArray())
        {
            if (row.GetProperty("parseState").GetString() != "Parsed"
                || row.GetProperty("duplicateOfBankTransactionId").ValueKind != JsonValueKind.Null)
            {
                continue;
            }

            decimal amount = row.GetProperty("debit").GetDecimal() > 0
                ? row.GetProperty("debit").GetDecimal()
                : row.GetProperty("credit").GetDecimal();
            (await client.PutAsJsonAsync(
                $"/api/v1/bank-imports/{batchId}/rows/{row.GetProperty("id").GetInt64()}/allocations",
                new { allocations = new[] { new { projectId = project, amount } } })).EnsureSuccessStatusCode();
        }
    }

    private async Task<JsonElement> Commit(HttpClient client, long batchId) =>
        await Json(await client.PostAsync($"/api/v1/bank-imports/{batchId}/commit", null));

    private async Task RemoveDuplicateRows(HttpClient client, long batchId, JsonElement batch)
    {
        foreach (JsonElement row in batch.GetProperty("rows").EnumerateArray())
        {
            if (row.GetProperty("duplicateOfBankTransactionId").ValueKind != JsonValueKind.Null)
            {
                (await client.DeleteAsync($"/api/v1/bank-imports/{batchId}/rows/{row.GetProperty("id").GetInt64()}?reason=TestRemove"))
                    .EnsureSuccessStatusCode();
            }
        }
    }

    [Fact]
    public async Task Import_SameFileTwice_ImportsZeroSecondTime()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);
        long project = await CreateProject(client, "Dedupe Same A");

        object[] rows =
        [
            Row(1, "2026-08-01", "NEFT ABC HARDWARE", 25_000m, 0m, "A1"),
            Row(2, "2026-08-05", "UPI CLIENT ADV", 0m, 5_00_000m, "A2"),
        ];

        JsonElement first = await Stage(client, account, rows);
        await MapAll(client, first.GetProperty("id").GetInt64(), first, project);
        (await Commit(client, first.GetProperty("id").GetInt64())).GetProperty("committed").GetInt32()
            .Should().Be(2);

        JsonElement second = await Stage(client, account, rows);
        long secondId = second.GetProperty("id").GetInt64();
        second.GetProperty("counts").GetProperty("duplicate").GetInt32().Should().Be(2);
        second.GetProperty("rows").EnumerateArray()
            .Should().OnlyContain(r => r.GetProperty("duplicateOfBankTransactionId").ValueKind == JsonValueKind.Number);

        // Commit is blocked while the duplicate rows are still sitting there — the
        // accountant must look at and remove them, not have them silently skipped
        // (client request, 2026-09-04).
        (await client.PostAsync($"/api/v1/bank-imports/{secondId}/commit", null))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);

        await RemoveDuplicateRows(client, secondId, second);
        (await Commit(client, secondId)).GetProperty("committed").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Import_OverlappingRange_ImportsOnlyNewRows()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);
        long project = await CreateProject(client, "Dedupe Overlap A");

        JsonElement aug = await Stage(client, account,
            Row(1, "2026-08-01", "NEFT ABC HARDWARE", 25_000m, 0m, "A1"),
            Row(2, "2026-08-12", "NEFT XYZ TRADERS", 40_000m, 0m, "A3"),
            Row(3, "2026-08-18", "CHQ PAID 000101", 12_500m, 0m, "A4"));
        await MapAll(client, aug.GetProperty("id").GetInt64(), aug, project);
        await Commit(client, aug.GetProperty("id").GetInt64());

        JsonElement augSep = await Stage(client, account,
            Row(1, "2026-08-12", "NEFT XYZ TRADERS", 40_000m, 0m, "A3"),   // overlap
            Row(2, "2026-08-18", "CHQ PAID 000101", 12_500m, 0m, "A4"),    // overlap
            Row(3, "2026-09-02", "NEFT ABC HARDWARE", 30_000m, 0m, "S1"),  // new
            Row(4, "2026-09-09", "UPI CLIENT ADV", 0m, 2_00_000m, "S2"));  // new
        long augSepId = augSep.GetProperty("id").GetInt64();

        augSep.GetProperty("counts").GetProperty("duplicate").GetInt32().Should().Be(2);
        await MapAll(client, augSepId, augSep, project);
        await RemoveDuplicateRows(client, augSepId, augSep);
        (await Commit(client, augSepId)).GetProperty("committed").GetInt32().Should().Be(2);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.BankTransactions.CountAsync(t => t.AccountId == account)).Should().Be(5);
    }

    [Fact]
    public async Task Import_TwoIdenticalSameDayRows_BothImported()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);
        long project = await CreateProject(client, "Dedupe Twins A");

        JsonElement batch = await Stage(client, account,
            Row(1, "2026-08-01", "UPI TEA STALL", 500m, 0m),
            Row(2, "2026-08-01", "UPI TEA STALL", 500m, 0m));
        long batchId = batch.GetProperty("id").GetInt64();

        batch.GetProperty("counts").GetProperty("duplicate").GetInt32().Should().Be(0);
        await MapAll(client, batchId, batch, project);
        (await Commit(client, batchId)).GetProperty("committed").GetInt32().Should().Be(2);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var occ = await db.BankTransactions.Where(t => t.AccountId == account)
            .Select(t => t.OccurrenceIndex).OrderBy(x => x).ToListAsync();
        occ.Should().Equal(0, 1);
    }

    [Fact]
    public async Task Import_ThenExcludeThenReimport_DoesNotResurrect()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);
        long project = await CreateProject(client, "Dedupe Exclude A");

        object[] rows = [Row(1, "2026-08-01", "NEFT ONE-OFF", 9_000m, 0m, "X1")];
        JsonElement first = await Stage(client, account, rows);
        await MapAll(client, first.GetProperty("id").GetInt64(), first, project);
        await Commit(client, first.GetProperty("id").GetInt64());

        long txnId;
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            txnId = await db.BankTransactions.Where(t => t.AccountId == account)
                .Select(t => t.Id).FirstAsync();
        }

        (await client.PostAsJsonAsync($"/api/v1/bank-transactions/{txnId}/exclude", new { reason = "not ours" }))
            .EnsureSuccessStatusCode();

        JsonElement second = await Stage(client, account, rows);
        long secondId = second.GetProperty("id").GetInt64();
        second.GetProperty("rows").EnumerateArray().First()
            .GetProperty("duplicateOfBankTransactionId").GetInt64().Should().Be(txnId);

        await RemoveDuplicateRows(client, secondId, second);
        (await Commit(client, secondId)).GetProperty("committed").GetInt32().Should().Be(0);
    }

    [Fact]
    public async Task Commit_BlockedWhileADuplicateRowRemains_SucceedsOnceItIsRemoved()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);
        long project = await CreateProject(client, "Dedupe Block A");

        object[] rows =
        [
            Row(1, "2026-08-01", "NEFT ABC HARDWARE", 25_000m, 0m, "B1"),
            Row(2, "2026-08-05", "UPI CLIENT ADV", 0m, 5_00_000m, "B2"),
        ];
        JsonElement first = await Stage(client, account, rows);
        await MapAll(client, first.GetProperty("id").GetInt64(), first, project);
        await Commit(client, first.GetProperty("id").GetInt64());

        // Re-stage one duplicate row alongside one genuinely new row.
        JsonElement second = await Stage(client, account,
            Row(1, "2026-08-01", "NEFT ABC HARDWARE", 25_000m, 0m, "B1"),   // duplicate
            Row(2, "2026-09-01", "NEFT NEW VENDOR", 8_000m, 0m, "B3"));    // new
        long secondId = second.GetProperty("id").GetInt64();
        long dupRowId = second.GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("sourceLineNo").GetInt32() == 1).GetProperty("id").GetInt64();

        await MapAll(client, secondId, second, project);

        HttpResponseMessage blocked = await client.PostAsync($"/api/v1/bank-imports/{secondId}/commit", null);
        blocked.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await blocked.Content.ReadAsStringAsync()).Should().Contain("already imported");

        // Removing the duplicate (and only the duplicate) unblocks commit for the rest.
        (await client.DeleteAsync($"/api/v1/bank-imports/{secondId}/rows/{dupRowId}?reason=TestRemove")).EnsureSuccessStatusCode();
        (await Commit(client, secondId)).GetProperty("committed").GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task RowHash_IsStable_AcrossNarrationWhitespaceVariants()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);
        long project = await CreateProject(client, "Dedupe Whitespace A");

        JsonElement first = await Stage(client, account,
            Row(1, "2026-08-01", "  NEFT   ABC    HARDWARE  ", 25_000m, 0m, "W1"));
        await MapAll(client, first.GetProperty("id").GetInt64(), first, project);
        await Commit(client, first.GetProperty("id").GetInt64());

        // Same transaction, narration re-cased and re-spaced by a later export.
        JsonElement second = await Stage(client, account,
            Row(1, "2026-08-01", "neft abc hardware", 25_000m, 0m, "W1"));
        second.GetProperty("rows").EnumerateArray().First()
            .GetProperty("duplicateOfBankTransactionId").ValueKind.Should().Be(JsonValueKind.Number);
    }
}
