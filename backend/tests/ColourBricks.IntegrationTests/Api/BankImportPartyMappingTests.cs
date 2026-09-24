using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>
/// Client request, 2026-09-24 — an imported row can be mapped to a vendor, field officer,
/// labour team, client or a common bucket, not only a project; and a bank account's
/// statement balance is its opening balance plus imported credits minus imported debits.
/// </summary>
public sealed class BankImportPartyMappingTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 3000;

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

    private static async Task<long> CreateParty(HttpClient client, string name, string type) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/parties?confirm=true", new { name, types = new[] { type } })))
        .GetProperty("id").GetInt64();

    private static async Task<long> CreateBank(HttpClient client, string name, decimal openingBalance) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name, type = "Bank", openingBalance, openingBalanceDate = "2026-04-01", bankName = "HDFC Bank",
        }))).GetProperty("id").GetInt64();

    private static object Row(int line, string date, string narration, decimal debit, decimal credit) =>
        new { sourceLineNo = line, valueDate = date, narration, debit, credit };

    private static async Task<JsonElement> CreateImport(HttpClient client, long accountId, params object[] rows) =>
        await Json(await client.PostAsJsonAsync("/api/v1/bank-imports", new
        {
            accountId, fileName = "statement.csv", rows,
        }));

    private static long RowId(JsonElement batch, int sourceLineNo) =>
        batch.GetProperty("rows").EnumerateArray()
            .First(r => r.GetProperty("sourceLineNo").GetInt32() == sourceLineNo)
            .GetProperty("id").GetInt64();

    private static Task<HttpResponseMessage> Map(HttpClient client, long batchId, long rowId, params object[] allocations) =>
        client.PutAsJsonAsync($"/api/v1/bank-imports/{batchId}/rows/{rowId}/allocations", new { allocations });

    [Fact]
    public async Task DebitRow_SplitsAcrossVendorFieldOfficerLabourAndBucket_AndCommitsHints()
    {
        HttpClient client = Client;
        long account = await CreateBank(client, "Party Map Bank", 0m);
        long project = await CreateProject(client, "Party Map Project");
        long vendor = await CreateParty(client, "PM Vendor Co", "Vendor");
        long officer = await CreateParty(client, "PM Field Officer", "FieldOfficer");
        long team = await CreateParty(client, "PM Labour Team", "Subcontractor");

        JsonElement batch = await CreateImport(client, account, Row(1, "2026-05-01", "NEFT SPLIT", 100_000m, 0m));
        long batchId = batch.GetProperty("id").GetInt64();

        JsonElement row = await Json(await Map(client, batchId, RowId(batch, 1),
            new { target = "Vendor", partyId = vendor, projectId = project, amount = 40_000m },
            new { target = "FieldOfficer", partyId = officer, amount = 30_000m },
            new { target = "Labour", partyId = team, amount = 20_000m },
            new { target = "Office", amount = 10_000m }));

        row.GetProperty("readyToCommit").GetBoolean().Should().BeTrue();
        JsonElement[] slices = [.. row.GetProperty("allocations").EnumerateArray()];
        slices.Select(s => s.GetProperty("target").GetString())
            .Should().Equal("Vendor", "FieldOfficer", "Labour", "Office");
        slices[0].GetProperty("partyName").GetString().Should().Be("PM Vendor Co");
        slices[0].GetProperty("projectName").GetString().Should().Be("Party Map Project");
        slices[3].GetProperty("partyId").ValueKind.Should().Be(JsonValueKind.Null);

        (await client.PostAsync($"/api/v1/bank-imports/{batchId}/commit", null)).EnsureSuccessStatusCode();

        JsonElement queue = await Json(await client.GetAsync($"/api/v1/reconciliation?accountId={account}"));
        JsonElement item = queue.GetProperty("items").EnumerateArray().Single();
        item.GetProperty("allocated").GetDecimal().Should().Be(100_000m);
        item.GetProperty("difference").GetDecimal().Should().Be(0m);
        item.GetProperty("counterparty").GetString().Should().Be("3 parties");
    }

    [Fact]
    public async Task CreditRow_MapsToClient()
    {
        HttpClient client = Client;
        long account = await CreateBank(client, "Party Map Credit Bank", 0m);
        long clientParty = await CreateParty(client, "PM Client", "Client");

        JsonElement batch = await CreateImport(client, account, Row(1, "2026-05-02", "RTGS CLIENT", 0m, 250_000m));
        long batchId = batch.GetProperty("id").GetInt64();

        JsonElement row = await Json(await Map(client, batchId, RowId(batch, 1),
            new { target = "Client", partyId = clientParty, amount = 250_000m }));

        row.GetProperty("readyToCommit").GetBoolean().Should().BeTrue();
        row.GetProperty("allocations")[0].GetProperty("partyName").GetString().Should().Be("PM Client");
    }

    [Fact]
    public async Task PartyTarget_RejectsMissingOrWrongRoleParty()
    {
        HttpClient client = Client;
        long account = await CreateBank(client, "Party Map Reject Bank", 0m);
        long vendor = await CreateParty(client, "PM Only A Vendor", "Vendor");

        JsonElement batch = await CreateImport(client, account, Row(1, "2026-05-03", "NEFT X", 5_000m, 0m));
        long batchId = batch.GetProperty("id").GetInt64();
        long rowId = RowId(batch, 1);

        HttpResponseMessage missing = await Map(client, batchId, rowId, new { target = "Vendor", amount = 5_000m });
        missing.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        HttpResponseMessage wrongRole = await Map(client, batchId, rowId,
            new { target = "FieldOfficer", partyId = vendor, amount = 5_000m });
        wrongRole.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await wrongRole.Content.ReadAsStringAsync()).Should().Contain("not a field officer");
    }

    [Fact]
    public async Task StatementBalance_IsOpeningPlusCommittedCreditsMinusDebits_ExcludingExcludedRows()
    {
        HttpClient client = Client;
        long account = await CreateBank(client, "Statement Balance Bank", 100_000m);

        JsonElement fresh = await Json(await client.GetAsync($"/api/v1/accounts/{account}"));
        fresh.GetProperty("statementBalance").GetDecimal().Should().Be(100_000m);

        JsonElement batch = await CreateImport(client, account,
            Row(1, "2026-05-01", "RTGS IN", 0m, 50_000m),
            Row(2, "2026-05-02", "NEFT OUT", 30_000m, 0m),
            Row(3, "2026-05-03", "CHARGES", 500m, 0m));
        long batchId = batch.GetProperty("id").GetInt64();
        foreach ((int line, decimal amount) in new[] { (1, 50_000m), (2, 30_000m), (3, 500m) })
        {
            (await Map(client, batchId, RowId(batch, line), new { target = "Other", amount })).EnsureSuccessStatusCode();
        }

        // Before commit nothing has landed.
        (await Json(await client.GetAsync($"/api/v1/accounts/{account}")))
            .GetProperty("statementBalance").GetDecimal().Should().Be(100_000m);

        (await client.PostAsync($"/api/v1/bank-imports/{batchId}/commit", null)).EnsureSuccessStatusCode();

        JsonElement after = await Json(await client.GetAsync($"/api/v1/accounts/{account}"));
        after.GetProperty("statementCredits").GetDecimal().Should().Be(50_000m);
        after.GetProperty("statementDebits").GetDecimal().Should().Be(30_500m);
        after.GetProperty("statementBalance").GetDecimal().Should().Be(119_500m);

        JsonElement listed = (await Json(await client.GetAsync("/api/v1/accounts?type=Bank")))
            .EnumerateArray().Single(a => a.GetProperty("id").GetInt64() == account);
        listed.GetProperty("statementBalance").GetDecimal().Should().Be(119_500m);
        listed.GetProperty("openingBalance").GetDecimal().Should().Be(100_000m);

        // Excluding the charges row takes it back out of the bank balance.
        JsonElement queue = await Json(await client.GetAsync($"/api/v1/reconciliation?accountId={account}"));
        long charges = queue.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("description").GetString() == "CHARGES").GetProperty("id").GetInt64();
        (await client.PostAsJsonAsync($"/api/v1/bank-transactions/{charges}/exclude", new { reason = "test" }))
            .EnsureSuccessStatusCode();

        (await Json(await client.GetAsync($"/api/v1/accounts/{account}")))
            .GetProperty("statementBalance").GetDecimal().Should().Be(120_000m);
    }
}
