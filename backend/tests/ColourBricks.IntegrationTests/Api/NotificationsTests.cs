using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Banking;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P9-T01 — notifications and alerts (BRD §66).</summary>
public sealed class NotificationsTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");
    private int _seq = 4300;

    private static async Task<JsonElement> Json(HttpResponseMessage r)
    {
        string body = await r.Content.ReadAsStringAsync();
        r.IsSuccessStatusCode.Should().BeTrue($"HTTP {(int)r.StatusCode}: {body}");
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    private async Task<long> Project(HttpClient c, decimal estimate) =>
        (await Json(await c.PostAsJsonAsync("/api/v1/projects", new
        {
            name = $"NF P {++_seq}", code = $"CB-2026-{_seq}", status = "Ongoing",
            startDate = "2026-04-01", expectedEndDate = "2027-03-31",
            contractValue = estimate * 1.5m, estimatedCost = estimate,
        }))).GetProperty("id").GetInt64();

    private async Task Expense(HttpClient c, long project, decimal amount)
    {
        long cat = (await Json(await c.GetAsync("/api/v1/expense-categories"))).EnumerateArray()
            .First(x => x.GetProperty("slug").GetString() == "electrical").GetProperty("id").GetInt64();
        (await c.PostAsJsonAsync("/api/v1/project-expenses", new
        {
            projectId = project, categoryId = cat, date = "2026-08-03", amount, paidImmediately = false,
        })).EnsureSuccessStatusCode();
    }

    private static async Task<int> Created(HttpResponseMessage r) =>
        (await Json(r)).GetProperty("created").GetInt32();

    private Task<HttpResponseMessage> Evaluate(HttpClient c) =>
        c.PostAsync("/api/v1/notifications/evaluate", null);

    private async Task<(long UserId, long RoleId)> SeededAdmin()
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var admin = await db.Users.AsNoTracking().OrderBy(u => u.Id)
            .Select(u => new { u.Id, u.RoleId }).FirstAsync();
        return (admin.Id, admin.RoleId!.Value);
    }

    private static IEnumerable<string?> Triggers(JsonElement list) =>
        list.EnumerateArray().Select(n => n.GetProperty("trigger").GetString());

    [Fact]
    public async Task Notifications_Idempotent_AcrossRuns()
    {
        HttpClient c = Admin;
        long project = await Project(c, 100_000m);
        await Expense(c, project, 150_000m); // over budget

        (await Created(await Evaluate(c))).Should().BeGreaterThan(0);
        (await Created(await Evaluate(c))).Should().Be(0);
    }

    [Fact]
    public async Task Notifications_BudgetExceeded_FiresOncePerBreach()
    {
        HttpClient c = Admin;
        long project = await Project(c, 200_000m);
        await Expense(c, project, 260_000m);

        await Evaluate(c);
        await Evaluate(c);

        JsonElement list = await Json(await c.GetAsync("/api/v1/notifications"));
        list.EnumerateArray()
            .Count(n => n.GetProperty("trigger").GetString() == "budget_exceeded"
                && n.GetProperty("projectId").GetInt64() == project)
            .Should().Be(1);
    }

    [Fact]
    public async Task Notifications_PendingReconciliation_FiresOnImport()
    {
        HttpClient c = Admin;
        long hdfc = (await Json(await c.GetAsync("/api/v1/accounts?includeInactive=true"))).EnumerateArray()
            .First(a => a.GetProperty("name").GetString() == "HDFC").GetProperty("id").GetInt64();

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var batch = new ImportBatch { AccountId = hdfc, FileName = "seed.csv", Status = ImportBatchStatus.Committed };
            db.ImportBatches.Add(batch);
            await db.SaveChangesAsync();
            db.BankTransactions.Add(new BankTransaction
            {
                ImportBatchId = batch.Id, AccountId = hdfc, ValueDate = new DateOnly(2026, 5, 10),
                Narration = "PENDING CREDIT", NormalisedNarration = "PENDING CREDIT",
                Credit = 40_000m, RowHash = Guid.NewGuid().ToString("n"), Status = BankTransactionStatus.Pending,
            });
            await db.SaveChangesAsync();
        }

        await Evaluate(c);

        JsonElement list = await Json(await c.GetAsync("/api/v1/notifications"));
        Triggers(list).Should().Contain("pending_reconciliation");
    }

    [Fact]
    public async Task Notifications_RespectPerRoleChannelConfig()
    {
        (long userId, long roleId) = await SeededAdmin();
        HttpClient c = Factory.CreateClientAs(userId: userId, permissions: "*");
        long project = await Project(c, 100_000m);
        await Expense(c, project, 140_000m);
        await Evaluate(c);

        // Off for this role -> not in the dashboard feed.
        (await c.PutAsJsonAsync("/api/v1/notifications/config",
            new { roleId, trigger = "budget_exceeded", channel = "Off" })).EnsureSuccessStatusCode();
        Triggers(await Json(await c.GetAsync("/api/v1/notifications"))).Should().NotContain("budget_exceeded");

        // Dashboard -> back in the feed.
        (await c.PutAsJsonAsync("/api/v1/notifications/config",
            new { roleId, trigger = "budget_exceeded", channel = "Dashboard" })).EnsureSuccessStatusCode();
        Triggers(await Json(await c.GetAsync("/api/v1/notifications"))).Should().Contain("budget_exceeded");
    }

    [Fact]
    public async Task Notifications_EmailFailure_DoesNotBlockJob()
    {
        (long userId, long roleId) = await SeededAdmin();
        HttpClient c = Factory.CreateClientAs(userId: userId, permissions: "*");
        (await c.PutAsJsonAsync("/api/v1/notifications/config",
            new { roleId, trigger = "budget_exceeded", channel = "Email" })).EnsureSuccessStatusCode();

        long project = await Project(c, 100_000m);
        await Expense(c, project, 175_000m);

        JsonElement run = await Json(await Evaluate(c));

        run.GetProperty("created").GetInt32().Should().BeGreaterThan(0);   // the job still created notifications
        run.GetProperty("emailsFailed").GetInt32().Should().BeGreaterThan(0); // ...and reported the delivery failure

        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await db.Notifications.AnyAsync(n => n.Trigger == "budget_exceeded" && n.EmailError != null))
            .Should().BeTrue();
    }
}
