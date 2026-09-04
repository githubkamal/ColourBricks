using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ClosedXML.Excel;
using ColourBricks.Domain.Ledger;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P8-T07 — Excel / PDF export of the full result set, with permissions and scope.</summary>
public sealed class ReportExportTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 4200;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"EX P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 9_000_000m, estimatedCost = 7_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task SeedLedger(long projectId, int count, decimal each)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        long categoryId = await db.ExpenseCategories.Where(x => x.Slug == "electrical").Select(x => x.Id).FirstAsync();
        for (int i = 0; i < count; i++)
        {
            db.LedgerEntries.Add(new LedgerEntry
            {
                EntryDate = new DateOnly(2026, 8, 1).AddDays(i % 27),
                ProjectId = projectId,
                CategoryId = categoryId,
                Debit = each,
                Credit = 0m,
                SourceType = "SeedExport",
                SourceId = i + 1,
            });
        }

        await db.SaveChangesAsync();
    }

    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    [Fact]
    public async Task Export_TotalsMatchFullResultSet_NotCurrentPage()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        await SeedLedger(project, 12, 1_000m); // 12 rows across 2+ pages of 5

        JsonElement run = await Json(await c.GetAsync(
            $"/api/v1/reports/run/project-ledger?projectId={project}&datePreset=ThisFinancialYear&page=1&pageSize=5"));
        run.GetProperty("rows").GetArrayLength().Should().Be(5);
        decimal fullSetDebit = run.GetProperty("totals").GetProperty("debit").GetDecimal();
        fullSetDebit.Should().Be(12_000m);

        byte[] xlsx = await (await c.GetAsync(
            $"/api/v1/reports/export/project-ledger?projectId={project}&datePreset=ThisFinancialYear&page=1&pageSize=5&format=xlsx"))
            .Content.ReadAsByteArrayAsync();

        using XLWorkbook wb = Open(xlsx);
        IXLWorksheet sheet = wb.Worksheet(1);
        int totalRow = sheet.CellsUsed(cell => cell.GetString() == "Total").Single().Address.RowNumber;
        int debitCol = sheet.CellsUsed(cell => cell.GetString() == "Debit").Single().Address.ColumnNumber;

        // The total row's Debit column equals the whole filtered set, not one page.
        sheet.Cell(totalRow, debitCol).GetValue<decimal>().Should().Be(12_000m);
    }

    [Fact]
    public async Task Export_AmountsAreNumericInExcel()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        await SeedLedger(project, 3, 2_500m);

        byte[] xlsx = await (await c.GetAsync(
            $"/api/v1/reports/export/project-ledger?projectId={project}&datePreset=ThisFinancialYear"))
            .Content.ReadAsByteArrayAsync();

        using XLWorkbook wb = Open(xlsx);
        IXLWorksheet sheet = wb.Worksheet(1);
        IXLCell headerDebit = sheet.CellsUsed(cell => cell.GetString() == "Debit").First();
        IXLCell firstDataCell = sheet.Cell(headerDebit.Address.RowNumber + 1, headerDebit.Address.ColumnNumber);

        firstDataCell.DataType.Should().Be(XLDataType.Number);
        firstDataCell.GetValue<decimal>().Should().Be(2_500m);
        firstDataCell.Style.NumberFormat.Format.Should().Be("#,##,##0.00");
    }

    [Fact]
    public async Task Export_WithoutPermission_Returns403()
    {
        HttpClient viewer = Factory.CreateClientAs(userId: 1, permissions: "reports.view");
        (await viewer.GetAsync("/api/v1/reports/export/ledger?datePreset=ThisFinancialYear"))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Export_RespectsProjectScope()
    {
        HttpClient admin = Admin;
        long allowed = await Project(admin);
        long forbidden = await Project(admin);
        await SeedLedger(allowed, 4, 5_000m);   // 20,000
        await SeedLedger(forbidden, 4, 9_000m); // 36,000

        long userId;
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
            db.UserProjectAccess.Add(new UserProjectAccess { UserId = userId, ProjectId = allowed });
            await db.SaveChangesAsync();
        }

        HttpClient scoped = Factory.CreateClientAs(userId: userId, permissions: "*");
        HttpResponseMessage response = await scoped.GetAsync(
            "/api/v1/reports/export/project-ledger?datePreset=ThisFinancialYear&pageSize=5");
        response.EnsureSuccessStatusCode();
        byte[] xlsx = await response.Content.ReadAsByteArrayAsync();

        using XLWorkbook wb = Open(xlsx);
        IXLWorksheet sheet = wb.Worksheet(1);
        int totalRow = sheet.CellsUsed(cell => cell.GetString() == "Total").Single().Address.RowNumber;
        int debitCol = sheet.CellsUsed(cell => cell.GetString() == "Debit").Single().Address.ColumnNumber;
        sheet.Cell(totalRow, debitCol).GetValue<decimal>().Should().Be(20_000m); // not 56,000
    }

    [Fact]
    public async Task Export_LargeReport_CompletesWithinTimeout()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        await SeedLedger(project, 3_000, 100m);

        var sw = Stopwatch.StartNew();
        HttpResponseMessage xlsx = await c.GetAsync(
            $"/api/v1/reports/export/project-ledger?projectId={project}&datePreset=ThisFinancialYear&format=xlsx");
        HttpResponseMessage pdf = await c.GetAsync(
            $"/api/v1/reports/export/project-ledger?projectId={project}&datePreset=ThisFinancialYear&format=pdf");
        sw.Stop();

        xlsx.EnsureSuccessStatusCode();
        pdf.EnsureSuccessStatusCode();
        (await xlsx.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(1000);
        (await pdf.Content.ReadAsByteArrayAsync()).Length.Should().BeGreaterThan(1000);
        sw.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(30));
    }
}
