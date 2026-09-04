using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ColourBricks.Domain.Ledger;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P1-T06 — Cash and bank account master (BRD §29, §70 rule 19, plan.md §5.3).</summary>
public sealed class AccountTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private HttpClient Admin => Factory.CreateClientAs(userId: 1, permissions: "*");

    private HttpClient NonAdmin =>
        Factory.CreateClientAs(userId: 2, permissions: "accounts.view accounts.add accounts.edit");

    private static async Task<(long Id, string Stamp, JsonElement Body)> Create(
        HttpClient client,
        string name,
        string type = "Bank",
        string? accountNumber = null,
        decimal openingBalance = 0m,
        string openingBalanceDate = "2026-04-01")
    {
        HttpResponseMessage response = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name,
            type,
            bankName = type == "Bank" ? "Placeholder Bank" : (string?)null,
            accountNumber,
            ifsc = (string?)null,
            openingBalance,
            openingBalanceDate,
        });
        response.EnsureSuccessStatusCode();
        using JsonDocument doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        JsonElement root = doc.RootElement.Clone();
        return (root.GetProperty("id").GetInt64(), root.GetProperty("concurrencyStamp").GetString()!, root);
    }

    private void AddLedger(long accountId, decimal debit, decimal credit)
    {
        using IServiceScope scope = Factory.Services.CreateScope();
        AppDbContext db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.LedgerEntries.Add(new LedgerEntry
        {
            EntryDate = new DateOnly(2026, 5, 1),
            AccountId = accountId,
            CategoryId = 1,
            Debit = debit,
            Credit = credit,
            SourceType = "Manual",
            SourceId = 0,
        });
        db.SaveChanges();
    }

    [Fact]
    public async Task Account_Balance_IsDerivedFromLedger()
    {
        HttpClient client = Admin;
        (long id, _, _) = await Create(client, "Site Cash Box", "Cash", openingBalance: 100_000m);

        AddLedger(id, debit: 0m, credit: 50_000m);
        AddLedger(id, debit: 20_000m, credit: 0m);

        HttpResponseMessage response = await client.GetAsync($"/api/v1/accounts/{id}");
        response.EnsureSuccessStatusCode();
        using JsonDocument body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

        // opening + credit - debit = 100000 + 50000 - 20000
        body.RootElement.GetProperty("balance").GetDecimal().Should().Be(130_000m);
    }

    [Fact]
    public async Task Account_OpeningBalance_ImmutableAfterFirstTransaction()
    {
        HttpClient client = Admin;
        (long id, string stamp, _) = await Create(client, "Ops Account", "Bank", openingBalance: 10_000m);

        AddLedger(id, debit: 500m, credit: 0m);

        // Changing the opening balance once a ledger entry exists is rejected.
        HttpResponseMessage rejected = await client.PutAsJsonAsync($"/api/v1/accounts/{id}", new
        {
            name = "Ops Account",
            type = "Bank",
            bankName = "Placeholder Bank",
            accountNumber = (string?)null,
            ifsc = (string?)null,
            openingBalance = 25_000m,
            openingBalanceDate = "2026-04-01",
            isActive = true,
            concurrencyStamp = stamp,
        });
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Editing other fields, opening balance unchanged, still works.
        HttpResponseMessage ok = await client.PutAsJsonAsync($"/api/v1/accounts/{id}", new
        {
            name = "Operations Account",
            type = "Bank",
            bankName = "Placeholder Bank",
            accountNumber = (string?)null,
            ifsc = (string?)null,
            openingBalance = 10_000m,
            openingBalanceDate = "2026-04-01",
            isActive = true,
            concurrencyStamp = stamp,
        });
        ok.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Account_NumberMasked_ForNonAdmin()
    {
        (long id, _, _) = await Create(Admin, "Payroll HDFC", "Bank", accountNumber: "123456789012");

        HttpResponseMessage masked = await NonAdmin.GetAsync($"/api/v1/accounts/{id}");
        masked.EnsureSuccessStatusCode();
        JsonDocument.Parse(await masked.Content.ReadAsStringAsync())
            .RootElement.GetProperty("accountNumber").GetString().Should().Be("********9012");

        HttpResponseMessage full = await Admin.GetAsync($"/api/v1/accounts/{id}");
        full.EnsureSuccessStatusCode();
        JsonDocument.Parse(await full.Content.ReadAsStringAsync())
            .RootElement.GetProperty("accountNumber").GetString().Should().Be("123456789012");
    }

    [Fact]
    public async Task CreateAccount_DuplicateName_Returns409()
    {
        HttpClient client = Admin;
        await Create(client, "Duplicate Cash", "Cash");

        HttpResponseMessage duplicate = await client.PostAsJsonAsync("/api/v1/accounts", new
        {
            name = "  duplicate   cash ",
            type = "Cash",
            openingBalance = 0m,
            openingBalanceDate = "2026-04-01",
        });

        duplicate.StatusCode.Should().Be(HttpStatusCode.Conflict);
        duplicate.Content.Headers.ContentType!.MediaType.Should().Be("application/problem+json");
    }
}
