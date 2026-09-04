using ColourBricks.Domain.Ledger;
using ColourBricks.Infrastructure.Identity;
using ColourBricks.Infrastructure.Persistence;
using ColourBricks.IntegrationTests.Support;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.IntegrationTests.Persistence;

/// <summary>P0-T06 — Audit trail infrastructure (plan.md §5.6, §10, BRD §65).</summary>
public sealed class AuditTests(IntegrationFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Audit_OnUpdate_RecordsOnlyChangedProperties()
    {
        await using AppDbContext db = TestDatabase.CreateContextWithInterceptors(currentUserId: 7);
        User user = await NewUserAsync(db);

        user.Name = "Renamed Only";
        await db.SaveChangesAsync();

        Infrastructure.Auditing.AuditLog log = await LatestAsync(db, user.Id, "update");
        log.Module.Should().Be("users");
        log.OldValues.Should().Contain("\"Name\"");
        log.NewValues.Should().Contain("Renamed Only");
        log.NewValues.Should().NotContainAny("Email", "Mobile", "ConcurrencyStamp", "UpdatedAtUtc", "PasswordHash");
    }

    [Fact]
    public async Task Audit_CapturesCurrentUserId()
    {
        await using AppDbContext db = TestDatabase.CreateContextWithInterceptors(currentUserId: 4242);
        User user = await NewUserAsync(db);

        user.Mobile = "+91 99999 00000";
        await db.SaveChangesAsync();

        Infrastructure.Auditing.AuditLog log = await LatestAsync(db, user.Id, "update");
        log.UserId.Should().Be(4242);
    }

    [Fact]
    public async Task Audit_WrittenInSameTransaction_RollsBackTogether()
    {
        await using AppDbContext db = TestDatabase.CreateContextWithInterceptors();
        User user = await NewUserAsync(db);

        await using (var transaction = await db.Database.BeginTransactionAsync())
        {
            user.Name = "Doomed Change";
            await db.SaveChangesAsync();
            await transaction.RollbackAsync();
        }

        db.ChangeTracker.Clear();
        int updateRows = await db.AuditLogs.CountAsync(
            a => a.RecordId == user.Id.ToString() && a.Action == "update");
        updateRows.Should().Be(0, "the audit row must roll back with the change");
    }

    [Fact]
    public async Task LedgerEntry_Update_Throws()
    {
        await using AppDbContext db = TestDatabase.CreateContextWithInterceptors();
        LedgerEntry entry = await NewLedgerEntryAsync(db);

        db.Entry(entry).State = EntityState.Modified;

        await FluentActions.Awaiting(() => db.SaveChangesAsync())
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task LedgerEntry_Delete_Throws()
    {
        await using AppDbContext db = TestDatabase.CreateContextWithInterceptors();
        LedgerEntry entry = await NewLedgerEntryAsync(db);

        db.LedgerEntries.Remove(entry);

        await FluentActions.Awaiting(() => db.SaveChangesAsync())
            .Should().ThrowAsync<InvalidOperationException>();
    }

    private static async Task<User> NewUserAsync(AppDbContext db)
    {
        var user = new User
        {
            Name = "Audit Subject",
            Email = $"audit+{Guid.NewGuid():N}@colourbricks.test",
            PasswordHash = "x",
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<LedgerEntry> NewLedgerEntryAsync(AppDbContext db)
    {
        var entry = new LedgerEntry
        {
            EntryDate = new DateOnly(2026, 4, 1),
            CategoryId = 1,
            Debit = 1000m,
            Credit = 0m,
            SourceType = "AuditTest",
            SourceId = 1,
        };
        db.LedgerEntries.Add(entry);
        await db.SaveChangesAsync();
        return entry;
    }

    private static async Task<Infrastructure.Auditing.AuditLog> LatestAsync(AppDbContext db, long recordId, string action)
    {
        return await db.AuditLogs
            .Where(a => a.RecordId == recordId.ToString() && a.Action == action)
            .OrderByDescending(a => a.Id)
            .FirstAsync();
    }
}
