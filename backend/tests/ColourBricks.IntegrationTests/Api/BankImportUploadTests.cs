using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P4-T02 — statement upload: parse against a saved profile into a Draft batch.</summary>
public sealed class BankImportUploadTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private const string HdfcCsv =
        "Statement for account XXXXXX1234\n"
        + "Period 01/05/2026 to 31/05/2026\n"
        + "Date,Narration,Ref,Withdrawal,Deposit,Balance\n"
        + "01/05/2026,NEFT DR-ABC HARDWARE,N1,\"25,000.00\",,\"1,75,000.00\"\n"
        + "03/05/2026,UPI CLIENT RECEIPT,N2,,\"5,00,000.00\",\"6,75,000.00\"\n"
        + "NOT-A-DATE,GARBLED LINE,N3,\"1,000.00\",,\n";

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> HdfcAccountId(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();

    private async Task<long> CreateProfile(HttpClient client, long accountId) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/bank-statement-profiles", new
        {
            accountId,
            name = "HDFC current",
            headerRowIndex = 2,
            dateColumn = 0,
            narrationColumn = 1,
            referenceColumn = 2,
            balanceColumn = 5,
            singleAmountColumn = false,
            debitColumn = 3,
            creditColumn = 4,
            dateFormats = "dd/MM/yyyy",
        }))).GetProperty("id").GetInt64();

    private static MultipartFormDataContent Upload(long accountId, long profileId, string csv, string fileName)
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(accountId.ToString()), "accountId" },
            { new StringContent(profileId.ToString()), "profileId" },
        };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", fileName);
        return form;
    }

    [Fact]
    public async Task DetectColumns_ReturnsHeadersAndSamples()
    {
        HttpClient client = Client;
        var form = new MultipartFormDataContent { { new StringContent("2"), "headerRowIndex" } };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(HdfcCsv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", "hdfc.csv");

        JsonElement detected = await Json(await client.PostAsync("/api/v1/bank-imports/detect-columns", form));

        detected.GetProperty("headers").EnumerateArray().Select(h => h.GetString())
            .Should().ContainInOrder("Date", "Narration", "Ref");
        detected.GetProperty("sampleRows").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Upload_CsvWithProfile_StagesDraftBatch_BadRowsNotFatal()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);
        long profileId = await CreateProfile(client, account);

        JsonElement batch = await Json(await client.PostAsync(
            "/api/v1/bank-imports/upload", Upload(account, profileId, HdfcCsv, "hdfc.csv")));

        batch.GetProperty("status").GetString().Should().Be("Draft");
        batch.GetProperty("counts").GetProperty("total").GetInt32().Should().Be(3);
        batch.GetProperty("counts").GetProperty("parseError").GetInt32().Should().Be(1);

        var rows = batch.GetProperty("rows").EnumerateArray().ToList();
        JsonElement debitRow = rows.First(r => r.GetProperty("debit").GetDecimal() == 25_000m);
        debitRow.GetProperty("bankReference").GetString().Should().Be("N1");
        debitRow.GetProperty("valueDate").GetString().Should().Be("2026-05-01");

        rows.First(r => r.GetProperty("credit").GetDecimal() == 5_00_000m).GetProperty("parseState")
            .GetString().Should().Be("Parsed");

        JsonElement bad = rows.First(r => r.GetProperty("parseState").GetString() == "Error");
        bad.GetProperty("valueDate").ValueKind.Should().Be(JsonValueKind.Null);
        bad.GetProperty("parseError").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Upload_ProfileForAnotherAccount_Returns400()
    {
        HttpClient client = Client;
        long hdfc = await HdfcAccountId(client);
        long sbi = (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
            .EnumerateArray().First(a => a.GetProperty("name").GetString() == "SBI").GetProperty("id").GetInt64();
        long profileId = await CreateProfile(client, hdfc);

        HttpResponseMessage response = await client.PostAsync(
            "/api/v1/bank-imports/upload", Upload(sbi, profileId, HdfcCsv, "hdfc.csv"));

        response.StatusCode.Should().Be(System.Net.HttpStatusCode.BadRequest);
    }
}
