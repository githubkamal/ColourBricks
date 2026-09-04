using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P5-T01 — versioned project budgets by category (BRD §40, §41).</summary>
public sealed class ProjectBudgetTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 1400;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c, decimal estimate) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"Budget P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = estimate * 1.25m, estimatedCost = estimate,
        }))).GetProperty("id").GetInt64();

    private async Task<Dictionary<string, long>> Categories(HttpClient c)
    {
        JsonElement list = await Json(await c.GetAsync("/api/v1/expense-categories"));
        return list.EnumerateArray().ToDictionary(
            x => x.GetProperty("slug").GetString()!, x => x.GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task BudgetRevision_PreservesPriorVersion()
    {
        HttpClient c = Admin;
        long project = await Project(c, 8_000_000m);
        var cat = await Categories(c);

        await Json(await c.PostAsJsonAsync($"/api/v1/projects/{project}/budget", new
        {
            lines = new[]
            {
                new { categoryId = cat["labour"], amount = 2_000_000m },
                new { categoryId = cat["materials"], amount = 3_500_000m },
            },
        }));

        JsonElement rev2 = await Json(await c.PostAsJsonAsync($"/api/v1/projects/{project}/budget", new
        {
            lines = new[]
            {
                new { categoryId = cat["labour"], amount = 2_100_000m },
                new { categoryId = cat["materials"], amount = 3_800_000m },
                new { categoryId = cat["electrical"], amount = 600_000m },
            },
            note = "added electrical",
        }));
        rev2.GetProperty("revisionNumber").GetInt32().Should().Be(2);

        JsonElement current = await Json(await c.GetAsync($"/api/v1/projects/{project}/budget"));
        current.GetProperty("revisionNumber").GetInt32().Should().Be(2);
        current.GetProperty("lines").GetArrayLength().Should().Be(3);

        JsonElement revisions = await Json(await c.GetAsync($"/api/v1/projects/{project}/budget/revisions"));
        revisions.GetArrayLength().Should().Be(2);

        // Revision 1 is still intact.
        JsonElement rev1 = await Json(await c.GetAsync($"/api/v1/projects/{project}/budget/revisions/1"));
        rev1.GetProperty("lines").GetArrayLength().Should().Be(2);
        rev1.GetProperty("budgetTotal").GetDecimal().Should().Be(5_500_000m);
    }

    [Fact]
    public async Task BudgetCategorySum_VarianceFromEstimate_IsDisplayed()
    {
        HttpClient c = Admin;
        long project = await Project(c, 8_000_000m);
        var cat = await Categories(c);

        JsonElement budget = await Json(await c.PostAsJsonAsync($"/api/v1/projects/{project}/budget", new
        {
            lines = new[]
            {
                new { categoryId = cat["labour"], amount = 2_000_000m },
                new { categoryId = cat["materials"], amount = 4_500_000m },
                new { categoryId = cat["electrical"], amount = 900_000m },
            },
        }));

        budget.GetProperty("budgetTotal").GetDecimal().Should().Be(7_400_000m);
        budget.GetProperty("estimatedCost").GetDecimal().Should().Be(8_000_000m);
        budget.GetProperty("varianceFromEstimate").GetDecimal().Should().Be(-600_000m); // under, displayed not blocked
    }

    [Fact]
    public async Task BudgetThresholds_AreConfigurable()
    {
        HttpClient c = Admin;
        long project = await Project(c, 5_000_000m);
        var cat = await Categories(c);

        await Json(await c.PostAsJsonAsync($"/api/v1/projects/{project}/budget", new
        {
            lines = new[] { new { categoryId = cat["labour"], amount = 1_000_000m } },
            approachingThresholdPercent = 80m,
        }));

        (await Json(await c.GetAsync($"/api/v1/projects/{project}/budget")))
            .GetProperty("approachingThresholdPercent").GetDecimal().Should().Be(80m);
    }
}
