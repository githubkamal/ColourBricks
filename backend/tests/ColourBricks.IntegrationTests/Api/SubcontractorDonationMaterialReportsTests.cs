using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P8-T04 — subcontractor (§52), donation (§53) and material (§49) reports.</summary>
public sealed class SubcontractorDonationMaterialReportsTests(IntegrationFixture fixture)
    : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 3900;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c, decimal contract = 10_000_000m) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"SDM P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = contract, estimatedCost = contract * 0.8m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> Cash(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
        .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task<long> OfficeCash(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
        .First(a => a.GetProperty("name").GetString() == "Office Cash").GetProperty("id").GetInt64();

    private static decimal Total(JsonElement r, string key) => r.GetProperty("totals").GetProperty(key).GetDecimal();
    private static IEnumerable<JsonElement> Rows(JsonElement r) => r.GetProperty("rows").EnumerateArray();

    [Fact]
    public async Task SubcontractorReport_WorkValueMinusPaid_EqualsOutstanding()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        long cash = await Cash(c);
        long acct = await OfficeCash(c);

        long deptId = (await Json(await c.GetAsync("/api/v1/departments"))).EnumerateArray()
            .First().GetProperty("id").GetInt64();
        long team = (await Json(await c.PostAsJsonAsync("/api/v1/teams?confirm=true",
            new { name = $"SDM Team {++_seq}", departmentId = deptId }))).GetProperty("id").GetInt64();

        long work = (await Json(await c.PostAsJsonAsync("/api/v1/labour/work", new
        {
            projectId = project, teamId = team, date = "2026-05-01", agreedValue = 400_000m, workType = "Wiring",
        }))).GetProperty("id").GetInt64();
        (await c.PostAsJsonAsync($"/api/v1/labour/work/{work}/payments", new
        {
            date = "2026-05-10", amount = 250_000m, frequency = "Weekly", paymentModeId = cash, accountId = acct,
        })).EnsureSuccessStatusCode();

        JsonElement report = await Json(await c.GetAsync(
            $"/api/v1/reports/run/subcontractor-work-value-vs-payment?subcontractorId={team}&pageSize=200"));

        JsonElement row = Rows(report).First(x => x.GetProperty("team").GetString()!.StartsWith("SDM Team"));
        (row.GetProperty("workValue").GetDecimal() - row.GetProperty("paid").GetDecimal())
            .Should().Be(row.GetProperty("outstanding").GetDecimal())
            .And.Be(150_000m);
    }

    [Fact]
    public async Task DonationReport_AllocatedEqualsPaidPlusOutstanding()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        long mode = await Cash(c);
        long acct = await OfficeCash(c);

        long templeA = (await Json(await c.PostAsJsonAsync("/api/v1/temples?confirm=true",
            new { name = $"SDM Temple A {++_seq}" }))).GetProperty("id").GetInt64();
        long templeB = (await Json(await c.PostAsJsonAsync("/api/v1/temples?confirm=true",
            new { name = $"SDM Temple B {++_seq}" }))).GetProperty("id").GetInt64();

        (await c.PutAsJsonAsync($"/api/v1/projects/{project}/donation", new
        {
            basis = "Percentage", percentage = 2m,
            temples = new[]
            {
                new { templeId = templeA, amount = 120_000m },
                new { templeId = templeB, amount = 80_000m },
            },
        })).EnsureSuccessStatusCode();

        await c.PostAsJsonAsync($"/api/v1/projects/{project}/donation/temples/{templeA}/payments",
            new { date = "2026-05-10", amount = 120_000m, paymentModeId = mode, accountId = acct });
        await c.PostAsJsonAsync($"/api/v1/projects/{project}/donation/temples/{templeB}/payments",
            new { date = "2026-05-11", amount = 30_000m, paymentModeId = mode, accountId = acct });

        // per project
        JsonElement byProject = await Json(await c.GetAsync(
            $"/api/v1/reports/run/donation-project-wise?projectId={project}&pageSize=200"));
        JsonElement pRow = Rows(byProject).Single();
        pRow.GetProperty("allocated").GetDecimal()
            .Should().Be(pRow.GetProperty("paid").GetDecimal() + pRow.GetProperty("outstanding").GetDecimal())
            .And.Be(200_000m);

        // per temple
        JsonElement byTemple = await Json(await c.GetAsync(
            "/api/v1/reports/run/donation-outstanding?pageSize=500"));
        foreach (JsonElement row in Rows(byTemple).Where(r => r.GetProperty("project").GetString() == pRow.GetProperty("project").GetString()))
        {
            row.GetProperty("allocated").GetDecimal()
                .Should().Be(row.GetProperty("paid").GetDecimal() + row.GetProperty("outstanding").GetDecimal());
        }
    }

    [Fact]
    public async Task MaterialReport_QuantityAndAmount_MatchPurchaseLines()
    {
        HttpClient c = Admin;
        long project = await Project(c);
        long vendor = (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true",
            new { name = $"SDM Vendor {++_seq}", types = new[] { "Vendor" } }))).GetProperty("id").GetInt64();

        await c.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId = project, vendorId = vendor, date = "2026-08-04", total = 60_000m,
            lines = new[]
            {
                new { itemName = "Cement", quantity = 100m, unit = "Bag", rate = 400m, taxAmount = 0m },
                new { itemName = "Sand", quantity = 20m, unit = "Ton", rate = 1_000m, taxAmount = 0m },
            },
        });

        JsonElement report = await Json(await c.GetAsync(
            $"/api/v1/reports/run/project-material?projectId={project}&datePreset=ThisFinancialYear&pageSize=200"));

        Total(report, "amount").Should().Be(60_000m);      // 100*400 + 20*1000
        Total(report, "quantity").Should().Be(120m);        // 100 + 20
        report.GetProperty("totalCount").GetInt32().Should().Be(2);
    }
}
