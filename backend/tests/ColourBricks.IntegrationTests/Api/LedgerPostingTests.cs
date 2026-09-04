using ColourBricks.Application.Ledger;
using ColourBricks.Domain.Accounts;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ColourBricks.IntegrationTests.Api;

/// <summary>P2-T01 — Ledger core and posting service (plan.md §5.2, §5.3, §5.6).</summary>
public sealed class LedgerPostingTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    private async Task<T> InScope<T>(Func<IServiceProvider, Task<T>> body)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        return await body(scope.ServiceProvider);
    }

    private async Task InScope(Func<IServiceProvider, Task> body)
    {
        await using AsyncServiceScope scope = Factory.Services.CreateAsyncScope();
        await body(scope.ServiceProvider);
    }

    private async Task<long> SeedAccountAsync(decimal opening)
    {
        return await InScope(async sp =>
        {
            AppDbContext db = sp.GetRequiredService<AppDbContext>();
            var account = new Account
            {
                Name = $"Ledger Test {Guid.NewGuid():N}",
                NormalisedName = Guid.NewGuid().ToString("N"),
                Type = AccountType.Bank,
                OpeningBalance = opening,
                OpeningBalanceDate = new DateOnly(2026, 4, 1),
            };
            db.Accounts.Add(account);
            await db.SaveChangesAsync();
            return account.Id;
        });
    }

    [Fact]
    public async Task Post_CreatesEntriesWithSourceTrace()
    {
        long accountId = await SeedAccountAsync(0m);

        await InScope(async sp =>
        {
            var categories = sp.GetRequiredService<IExpenseCategoryService>();
            long income = await categories.RequireIdAsync("project_income", default);
            var posting = sp.GetRequiredService<ILedgerPostingService>();

            await posting.PostAsync(new LedgerPosting("ManualTest", 4242, new DateOnly(2026, 5, 1),
            [
                new LedgerLeg(income, Debit: 0m, Credit: 5_000m, AccountId: accountId),
                new LedgerLeg(income, Debit: 5_000m, Credit: 0m, ProjectId: 1),
            ]), default);
        });

        await InScope(async sp =>
        {
            AppDbContext db = sp.GetRequiredService<AppDbContext>();
            var rows = await db.LedgerEntries.Where(e => e.SourceType == "ManualTest").ToListAsync();
            rows.Should().HaveCount(2);
            rows.Should().OnlyContain(e => e.SourceId == 4242 && e.SourceType.Length > 0);

            // No orphan entries anywhere.
            (await db.LedgerEntries.CountAsync(e => e.SourceId <= 0 || e.SourceType == "")).Should().Be(0);
        });
    }

    [Fact]
    public async Task Reverse_ProducesNetZero()
    {
        long accountId = await SeedAccountAsync(0m);

        await InScope(async sp =>
        {
            var categories = sp.GetRequiredService<IExpenseCategoryService>();
            long cat = await categories.RequireIdAsync("other_expenses", default);
            var posting = sp.GetRequiredService<ILedgerPostingService>();

            await posting.PostAsync(new LedgerPosting("ReverseTest", 77, new DateOnly(2026, 5, 1),
            [
                new LedgerLeg(cat, Debit: 12_345m, Credit: 0m, ProjectId: 1),
                new LedgerLeg(cat, Debit: 0m, Credit: 12_345m, AccountId: accountId),
            ]), default);

            await posting.ReverseAsync("ReverseTest", 77, "test reversal", default);
        });

        await InScope(async sp =>
        {
            AppDbContext db = sp.GetRequiredService<AppDbContext>();
            var rows = await db.LedgerEntries
                .Where(e => e.SourceType == "ReverseTest" && e.SourceId == 77)
                .ToListAsync();

            rows.Should().HaveCount(4);
            (rows.Sum(e => e.Debit) - rows.Sum(e => e.Credit)).Should().Be(0m);
            rows.Count(e => e.IsReversal).Should().Be(2);
        });
    }

    [Fact]
    public async Task Reverse_Twice_Throws()
    {
        await InScope(async sp =>
        {
            var categories = sp.GetRequiredService<IExpenseCategoryService>();
            long cat = await categories.RequireIdAsync("other_expenses", default);
            var posting = sp.GetRequiredService<ILedgerPostingService>();

            await posting.PostAsync(new LedgerPosting("DoubleReverse", 9, new DateOnly(2026, 5, 1),
            [
                new LedgerLeg(cat, Debit: 100m, Credit: 0m, ProjectId: 1),
            ]), default);

            await posting.ReverseAsync("DoubleReverse", 9, "first", default);

            Func<Task> second = () => posting.ReverseAsync("DoubleReverse", 9, "second", default);
            await second.Should().ThrowAsync<LedgerAlreadyReversedException>();
        });
    }

    [Fact]
    public async Task AccountBalance_MatchesOpeningPlusCreditsMinusDebits()
    {
        long accountId = await SeedAccountAsync(100_000m);

        decimal balance = await InScope(async sp =>
        {
            var categories = sp.GetRequiredService<IExpenseCategoryService>();
            long cat = await categories.RequireIdAsync("other_expenses", default);
            var posting = sp.GetRequiredService<ILedgerPostingService>();

            await posting.PostAsync(new LedgerPosting("BalanceTest", 1, new DateOnly(2026, 5, 2),
            [
                new LedgerLeg(cat, Debit: 0m, Credit: 30_000m, AccountId: accountId),
                new LedgerLeg(cat, Debit: 30_000m, Credit: 0m, ProjectId: 1),
            ]), default);

            await posting.PostAsync(new LedgerPosting("BalanceTest", 2, new DateOnly(2026, 5, 3),
            [
                new LedgerLeg(cat, Debit: 10_000m, Credit: 0m, AccountId: accountId),
                new LedgerLeg(cat, Debit: 0m, Credit: 10_000m, ProjectId: 1),
            ]), default);

            return await sp.GetRequiredService<ILedgerQueryService>()
                .GetAccountBalanceAsync(accountId, default);
        });

        // 100,000 opening + 30,000 credit - 10,000 debit
        balance.Should().Be(120_000m);
    }
}
