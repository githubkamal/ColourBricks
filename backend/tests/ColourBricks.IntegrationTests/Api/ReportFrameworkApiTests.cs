using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>
/// P8-T01 — the "throwaway report on the framework" validation: the ledger report is
/// pure definition + projection, yet gets filters, pagination and project scope.
/// </summary>
public sealed class ReportFrameworkApiTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 3600;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"RF P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 9_000_000m, estimatedCost = 7_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task Receipt(HttpClient c, long project, decimal amount)
    {
        long mode = (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
            .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();
        long acct = (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
            .First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();
        (await c.PostAsJsonAsync("/api/v1/receipts", new
        {
            projectId = project, type = "ClientAdvance", date = "2026-05-05", amount,
            paymentModeId = mode, accountId = acct,
        })).EnsureSuccessStatusCode();
    }

    private static decimal Total(JsonElement result, string key) =>
        result.GetProperty("totals").GetProperty(key).GetDecimal();

    [Fact]
    public async Task ReportCatalog_ListsTheLedgerReport_WithColumnsAndFilters()
    {
        JsonElement catalog = await Json(await Admin.GetAsync("/api/v1/reports/catalog"));
        JsonElement ledger = catalog.EnumerateArray().First(e => e.GetProperty("key").GetString() == "ledger");

        ledger.GetProperty("columns").EnumerateArray().Select(c => c.GetProperty("key").GetString())
            .Should().Contain(["date", "debit", "credit"]);
        ledger.GetProperty("supportedFilters").EnumerateArray().Select(f => f.GetString())
            .Should().Contain(["date", "project", "category"]);
        // "category" and "source" have no sort applier — the frontend must not offer
        // a sort-by-click on a column that would silently do nothing.
        ledger.GetProperty("sortableColumnKeys").EnumerateArray().Select(k => k.GetString())
            .Should().BeEquivalentTo(["date", "debit", "credit"]);
    }

    [Fact]
    public async Task LedgerReport_FiltersByProject_AndTotalsCoverWholeSet_NotPage()
    {
        HttpClient c = Admin;
        long a = await Project(c);
        long b = await Project(c);
        await Receipt(c, a, 100_000m);
        await Receipt(c, a, 11_111m);
        await Receipt(c, b, 222_222m);

        // Filter to project A: only A's two income debit legs survive.
        JsonElement forA = await Json(await c.GetAsync(
            $"/api/v1/reports/run/ledger?projectId={a}&datePreset=ThisFinancialYear&pageSize=1"));

        forA.GetProperty("rows").GetArrayLength().Should().Be(1);           // one page
        forA.GetProperty("totalCount").GetInt32().Should().Be(2);          // ...but two rows match
        Total(forA, "debit").Should().Be(111_111m);                        // total is the whole filtered set
        Total(forA, "credit").Should().Be(0m);
    }

    [Fact]
    public async Task LedgerReport_AppliesProjectScopeAutomatically()
    {
        HttpClient admin = Admin;
        long allowed = await Project(admin);
        long forbidden = await Project(admin);
        await Receipt(admin, allowed, 55_000m);
        await Receipt(admin, forbidden, 66_000m);

        long userId;
        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            userId = await db.Users.OrderBy(u => u.Id).Select(u => u.Id).FirstAsync();
            db.UserProjectAccess.Add(new UserProjectAccess { UserId = userId, ProjectId = allowed });
            await db.SaveChangesAsync();
        }

        HttpClient scoped = Factory.CreateClientAs(userId: userId, permissions: "reports.view");
        JsonElement result = await Json(await scoped.GetAsync(
            "/api/v1/reports/run/ledger?datePreset=ThisFinancialYear&pageSize=200"));

        // No projectId filter given — scope alone keeps the forbidden project's ₹66,000 out.
        Total(result, "debit").Should().Be(55_000m);
    }
}
