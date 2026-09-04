using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P8-T03 — the six BRD §50/§51 vendor reports, on the P8-T01 framework.</summary>
public sealed class VendorReportsTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 3800;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"VR P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = 9_000_000m, estimatedCost = 7_000_000m,
        }))).GetProperty("id").GetInt64();

    private async Task<long> Vendor(HttpClient c) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/parties?confirm=true",
            new { name = $"VR Vendor {++_seq}", types = new[] { "Vendor" } }))).GetProperty("id").GetInt64();

    private async Task<long> Cash(HttpClient c) =>
        (await Json(await c.GetAsync("/api/v1/payment-modes"))).EnumerateArray()
        .First(m => m.GetProperty("name").GetString() == "Cash").GetProperty("id").GetInt64();

    private async Task Purchase(HttpClient c, long project, long vendor, decimal amount) =>
        (await c.PostAsJsonAsync("/api/v1/vendor-purchases", new
        {
            projectId = project, vendorId = vendor, date = "2026-08-04", total = amount,
            lines = new[] { new { itemName = "Steel", quantity = 5m, unit = "Ton", rate = amount / 5m, taxAmount = 0m } },
        })).EnsureSuccessStatusCode();

    private async Task Pay(HttpClient c, long vendor, long project, decimal amount, long mode) =>
        (await c.PostAsJsonAsync("/api/v1/vendor-payments", new
        {
            vendorId = vendor, projectId = project, date = "2026-08-20", amount, paymentModeId = mode,
        })).EnsureSuccessStatusCode();

    private static decimal Total(JsonElement result, string key) =>
        result.GetProperty("totals").GetProperty(key).GetDecimal();

    private static IEnumerable<JsonElement> Rows(JsonElement result) =>
        result.GetProperty("rows").EnumerateArray();

    [Fact]
    public async Task VendorStatement_ClosingBalance_EqualsOutstandingService()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long project = await Project(c);
        long cash = await Cash(c);

        await Purchase(c, project, vendor, 200_000m);
        await Pay(c, vendor, project, 120_000m, cash);

        decimal serviceOutstanding = (await Json(await c.GetAsync($"/api/v1/vendors/{vendor}/outstanding-summary")))
            .GetProperty("total").GetDecimal();

        JsonElement statement = await Json(await c.GetAsync(
            $"/api/v1/reports/run/vendor-statement?vendorId={vendor}&sortBy=date&sortDir=asc&pageSize=200"));

        Rows(statement).Last().GetProperty("runningOutstanding").GetDecimal().Should().Be(serviceOutstanding);
        Total(statement, "purchase").Should().Be(200_000m);
        Total(statement, "paid").Should().Be(120_000m);
    }

    [Fact]
    public async Task VendorProjectWiseStatement_MatchesBrdSection50Layout()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long a = await Project(c);
        long b = await Project(c);
        long cash = await Cash(c);

        await Purchase(c, a, vendor, 200_000m);
        await Pay(c, vendor, a, 150_000m, cash);
        await Purchase(c, b, vendor, 100_000m);
        await Pay(c, vendor, b, 50_000m, cash);

        JsonElement report = await Json(await c.GetAsync(
            $"/api/v1/reports/run/vendor-project-wise-statement?vendorId={vendor}&pageSize=200"));

        report.GetProperty("columns").EnumerateArray().Select(x => x.GetProperty("key").GetString())
            .Should().Equal("vendor", "project", "purchase", "paid", "outstanding");

        var rows = Rows(report).Where(r => r.GetProperty("vendor").GetString()!.StartsWith("VR Vendor")).ToList();
        rows.Should().HaveCount(2);
        foreach (JsonElement row in rows)
        {
            (row.GetProperty("purchase").GetDecimal() - row.GetProperty("paid").GetDecimal())
                .Should().Be(row.GetProperty("outstanding").GetDecimal());
        }
        rows.Sum(r => r.GetProperty("outstanding").GetDecimal()).Should().Be(100_000m); // 50k + 50k
    }

    [Fact]
    public async Task VendorReports_IncludeAdvances()
    {
        HttpClient c = Admin;
        long vendor = await Vendor(c);
        long project = await Project(c);
        long cash = await Cash(c);

        await Purchase(c, project, vendor, 100_000m);
        await Pay(c, vendor, project, 130_000m, cash); // ₹30,000 over -> advance

        JsonElement outstanding = await Json(await c.GetAsync(
            $"/api/v1/reports/run/vendor-outstanding?vendorId={vendor}&pageSize=200"));
        Rows(outstanding).First(r => r.GetProperty("vendor").GetString()!.StartsWith("VR Vendor"))
            .GetProperty("advance").GetDecimal().Should().Be(30_000m);

        JsonElement statement = await Json(await c.GetAsync(
            $"/api/v1/reports/run/vendor-statement?vendorId={vendor}&pageSize=200"));
        Rows(statement).Select(r => r.GetProperty("kind").GetString()).Should().Contain("Advance");
    }
}
