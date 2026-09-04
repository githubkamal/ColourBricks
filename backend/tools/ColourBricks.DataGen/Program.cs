using ColourBricks.Domain.Ledger;
using ColourBricks.Domain.Projects;
using ColourBricks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

// P9-T02 — realistic volume dataset generator.
//   dotnet run --project tools/ColourBricks.DataGen -- --years 3 --projects 20
// Defaults: 3 years, 20 projects, ~50,000 ledger entries, ~10,000 bank transactions.

int years = ArgInt("--years", 3);
int projectCount = ArgInt("--projects", 20);
int ledgerTarget = ArgInt("--ledger", 50_000);
int bankTarget = ArgInt("--bank", 10_000);
string connection = ArgStr("--connection",
    "Server=localhost;Port=3306;Database=colourbricks;User ID=root;Password=;TreatTinyAsBoolean=false;AllowUserVariables=true;UseAffectedRows=false");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseMySql(connection, ServerVersion.AutoDetect(connection))
    .Options;

await using var db = new AppDbContext(options);
await db.Database.MigrateAsync();

var rng = new Random(20260903);
DateOnly start = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-years);
var now = DateTimeOffset.UtcNow;

long incomeCategoryId = await db.Set<ExpenseCategory>().Where(c => c.Slug == "project_income").Select(c => c.Id).FirstAsync();
List<long> costCategoryIds = await db.Set<ExpenseCategory>().Where(c => c.IsCost).Select(c => c.Id).Take(6).ToListAsync();
long accountId = await db.Accounts.OrderBy(a => a.Id).Select(a => a.Id).FirstAsync();

Console.WriteLine($"Generating {projectCount} projects, ~{ledgerTarget} ledger entries, ~{bankTarget} bank transactions over {years} years…");

var projects = new List<Project>();
for (int i = 0; i < projectCount; i++)
{
    projects.Add(new Project
    {
        Name = $"Volume Project {i + 1:D3}",
        Code = $"VOL-{DateTime.UtcNow:yyyyMMddHHmmss}-{i:D3}",
        Status = i % 5 == 0 ? ProjectStatus.Completed : ProjectStatus.Ongoing,
        StartDate = start,
        ExpectedEndDate = start.AddYears(years),
        ContractValue = rng.Next(50, 500) * 100_000m,
        EstimatedCost = rng.Next(40, 400) * 100_000m,
        CreatedAtUtc = now,
    });
}

db.Projects.AddRange(projects);
await db.SaveChangesAsync();

int totalDays = years * 365;
var ledger = new List<LedgerEntry>(2_000);
int written = 0;
for (int i = 0; i < ledgerTarget; i++)
{
    Project project = projects[rng.Next(projects.Count)];
    DateOnly date = start.AddDays(rng.Next(totalDays));
    bool income = i % 7 == 0;
    decimal amount = rng.Next(5, 500) * 1_000m;

    ledger.Add(new LedgerEntry
    {
        EntryDate = date,
        ProjectId = project.Id,
        CategoryId = income ? incomeCategoryId : costCategoryIds[rng.Next(costCategoryIds.Count)],
        AccountId = i % 3 == 0 ? accountId : null,
        Debit = amount,
        Credit = 0m,
        SourceType = income ? "VolumeReceipt" : "VolumeExpense",
        SourceId = i + 1,
        CreatedAtUtc = now,
    });

    if (ledger.Count == 2_000)
    {
        db.Set<LedgerEntry>().AddRange(ledger);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        written += ledger.Count;
        ledger.Clear();
        Console.Write($"\r  ledger {written}/{ledgerTarget}");
    }
}

if (ledger.Count > 0)
{
    db.Set<LedgerEntry>().AddRange(ledger);
    await db.SaveChangesAsync();
    db.ChangeTracker.Clear();
}

Console.WriteLine($"\r  ledger {ledgerTarget}/{ledgerTarget} done.");

var batch = new ColourBricks.Domain.Banking.ImportBatch
{
    AccountId = accountId,
    FileName = "volume.csv",
    Status = ColourBricks.Domain.Banking.ImportBatchStatus.Committed,
    CreatedAtUtc = now,
};
db.ImportBatches.Add(batch);
await db.SaveChangesAsync();

var txns = new List<ColourBricks.Domain.Banking.BankTransaction>(2_000);
written = 0;
for (int i = 0; i < bankTarget; i++)
{
    DateOnly date = start.AddDays(rng.Next(totalDays));
    bool credit = i % 2 == 0;
    decimal amount = rng.Next(5, 500) * 1_000m;
    txns.Add(new ColourBricks.Domain.Banking.BankTransaction
    {
        ImportBatchId = batch.Id,
        AccountId = accountId,
        ValueDate = date,
        Narration = $"VOLUME TXN {i}",
        NormalisedNarration = $"VOLUME TXN {i}",
        Debit = credit ? 0m : amount,
        Credit = credit ? amount : 0m,
        RowHash = Guid.NewGuid().ToString("n"),
        Status = i % 4 == 0
            ? ColourBricks.Domain.Banking.BankTransactionStatus.Pending
            : ColourBricks.Domain.Banking.BankTransactionStatus.Reconciled,
        CreatedAtUtc = now,
    });

    if (txns.Count == 2_000)
    {
        db.BankTransactions.AddRange(txns);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        written += txns.Count;
        txns.Clear();
        Console.Write($"\r  bank {written}/{bankTarget}");
    }
}

if (txns.Count > 0)
{
    db.BankTransactions.AddRange(txns);
    await db.SaveChangesAsync();
}

Console.WriteLine($"\r  bank {bankTarget}/{bankTarget} done.");
Console.WriteLine("Volume dataset generated.");

int ArgInt(string name, int fallback)
{
    int idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length && int.TryParse(args[idx + 1], out int v) ? v : fallback;
}

string ArgStr(string name, string fallback)
{
    int idx = Array.IndexOf(args, name);
    return idx >= 0 && idx + 1 < args.Length ? args[idx + 1] : fallback;
}
