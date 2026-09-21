using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>Upload wizard preview: a dry-run parse that stages nothing and, on request, flags
/// rows that already exist in the ledger so the wizard can highlight them before the real
/// import happens.</summary>
public sealed class BankImportPreviewTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Client => Factory.CreateClientAs(userId: 1, permissions: "*");

    private const string HdfcCsv =
        "Date,Narration,Ref,Withdrawal,Deposit,Balance\n"
        + "01/05/2026,NEFT DR-ABC HARDWARE,N1,\"25,000.00\",,\"1,75,000.00\"\n"
        + "03/05/2026,UPI CLIENT RECEIPT,N2,,\"5,00,000.00\",\"6,75,000.00\"\n";

    private static async Task<JsonElement> Json(HttpResponseMessage response)
    {
        string body = await response.Content.ReadAsStringAsync();
        response.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)response.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> HdfcAccountId(HttpClient client) =>
        (await Json(await client.GetAsync("/api/v1/accounts?includeInactive=true")))
        .EnumerateArray().First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();

    private async Task<long> CreateProject(HttpClient client) =>
        (await Json(await client.PostAsJsonAsync("/api/v1/projects", new
        {
            name = "Preview Test Project", code = "CB-2026-9901", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 10_000_000m, estimatedCost = 8_000_000m,
        }))).GetProperty("id").GetInt64();

    private static MultipartFormDataContent PreviewForm(
        long accountId, string csv, bool checkExisting, string fileName = "hdfc.csv")
    {
        var form = new MultipartFormDataContent
        {
            { new StringContent(accountId.ToString()), "accountId" },
            { new StringContent("0"), "headerRowIndex" },
            { new StringContent("0"), "dateColumn" },
            { new StringContent("1"), "narrationColumn" },
            { new StringContent("2"), "referenceColumn" },
            { new StringContent("5"), "balanceColumn" },
            { new StringContent("false"), "singleAmountColumn" },
            { new StringContent("3"), "debitColumn" },
            { new StringContent("4"), "creditColumn" },
            { new StringContent("dd/MM/yyyy"), "dateFormats" },
            { new StringContent(checkExisting.ToString()), "checkExisting" },
        };
        var file = new ByteArrayContent(Encoding.UTF8.GetBytes(csv));
        file.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(file, "file", fileName);
        return form;
    }

    [Fact]
    public async Task Preview_ParsesRows_WithoutPersistingAnything()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);

        MultipartFormDataContent form = PreviewForm(account, HdfcCsv, checkExisting: false);
        JsonElement rows = await Json(await client.PostAsync("/api/v1/bank-imports/preview", form));

        rows.GetArrayLength().Should().Be(2);
        rows[0].GetProperty("debit").GetDecimal().Should().Be(25_000m);
        rows[0].GetProperty("existsInDb").GetBoolean().Should().BeFalse();
        rows[1].GetProperty("credit").GetDecimal().Should().Be(5_00_000m);

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.ImportBatches.CountAsync()).Should().Be(0);
        (await db.BankTransactions.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Preview_CheckExistingFalse_NeverFlagsRows_EvenIfTheyExist()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);
        long project = await CreateProject(client);

        // Stage + commit the same statement first, via the ordinary staged-row path.
        JsonElement staged = await Json(await client.PostAsJsonAsync("/api/v1/bank-imports", new
        {
            accountId = account,
            fileName = "hdfc.csv",
            rows = new object[]
            {
                new { sourceLineNo = 1, valueDate = "2026-05-01", narration = "NEFT DR-ABC HARDWARE", debit = 25_000m, credit = 0m, bankReference = "N1" },
            },
        }));
        long batchId = staged.GetProperty("id").GetInt64();
        long rowId = staged.GetProperty("rows")[0].GetProperty("id").GetInt64();
        (await client.PutAsJsonAsync($"/api/v1/bank-imports/{batchId}/rows/{rowId}/allocations",
            new { allocations = new[] { new { projectId = project, amount = 25_000m } } }))
            .EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/v1/bank-imports/{batchId}/commit", null)).EnsureSuccessStatusCode();

        JsonElement rows = await Json(await client.PostAsync(
            "/api/v1/bank-imports/preview", PreviewForm(account, HdfcCsv, checkExisting: false)));

        rows.EnumerateArray().Should().OnlyContain(r => r.GetProperty("existsInDb").GetBoolean() == false);
    }

    [Fact]
    public async Task Preview_CheckExistingTrue_FlagsRowsAlreadyInTheLedger()
    {
        HttpClient client = Client;
        long account = await HdfcAccountId(client);
        long project = await CreateProject(client);

        JsonElement staged = await Json(await client.PostAsJsonAsync("/api/v1/bank-imports", new
        {
            accountId = account,
            fileName = "hdfc.csv",
            rows = new object[]
            {
                new { sourceLineNo = 1, valueDate = "2026-05-01", narration = "NEFT DR-ABC HARDWARE", debit = 25_000m, credit = 0m, bankReference = "N1" },
            },
        }));
        long batchId = staged.GetProperty("id").GetInt64();
        long rowId = staged.GetProperty("rows")[0].GetProperty("id").GetInt64();
        (await client.PutAsJsonAsync($"/api/v1/bank-imports/{batchId}/rows/{rowId}/allocations",
            new { allocations = new[] { new { projectId = project, amount = 25_000m } } }))
            .EnsureSuccessStatusCode();
        (await client.PostAsync($"/api/v1/bank-imports/{batchId}/commit", null)).EnsureSuccessStatusCode();

        JsonElement rows = await Json(await client.PostAsync(
            "/api/v1/bank-imports/preview", PreviewForm(account, HdfcCsv, checkExisting: true)));

        rows[0].GetProperty("existsInDb").GetBoolean().Should().BeTrue(); // the committed row, same statement
        rows[1].GetProperty("existsInDb").GetBoolean().Should().BeFalse(); // never imported

        // Still nothing new was written by the preview itself.
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.BankTransactions.CountAsync(t => t.AccountId == account)).Should().Be(1);
    }
}
