using ColourBricks.Domain.Accounts;
using ColourBricks.Domain.Departments;
using ColourBricks.Domain.Items;
using ColourBricks.Domain.Ledger;
using ColourBricks.Domain.Payments;
using ColourBricks.Domain.Services;
using Microsoft.EntityFrameworkCore;

namespace ColourBricks.Infrastructure.Persistence.Seeding;

/// <summary>
/// Idempotently seeds the small lookup lists a fresh database needs before any
/// transaction can be entered: the unit master and example item categories
/// (BRD §14), the initial departments (BRD §8) and the initial payment modes
/// (BRD §28). Safe to run on every startup.
/// </summary>
public sealed class ReferenceDataSeeder(AppDbContext db)
{
    // BRD §28. RequiresReference for Cheque/UPI/Bank Transfer (P1-T05); RequiresAccount
    // for everything except cash-in-hand. All editable by an Administrator afterwards.
    private static readonly (string Name, bool Account, bool Reference, int Sort)[] PaymentModes =
    [
        ("Cash", false, false, 10),
        ("Bank Transfer", true, true, 20),
        ("UPI", true, true, 30),
        ("Cheque", true, true, 40),
        ("Credit Card", true, false, 50),
        ("Debit Card", true, false, 60),
        ("Online Transfer", true, false, 70),
        ("Other", true, false, 80),
    ];

    // BRD §29. Placeholders with a zero opening balance and the FY-start date; an
    // Administrator sets the real figures before the first transaction is recorded.
    private static readonly (string Name, AccountType Type, string? Bank)[] Accounts =
    [
        ("Office Cash", AccountType.Cash, null),
        ("Site Cash", AccountType.Cash, null),
        ("HDFC", AccountType.Bank, "HDFC Bank"),
        ("SBI", AccountType.Bank, "State Bank of India"),
        ("ICICI", AccountType.Bank, "ICICI Bank"),
    ];

    private static readonly DateOnly OpeningBalanceDate = new(2026, 4, 1);

    private static readonly string[] Departments =
    [
        "Mesthri / Building Construction",
        "Interior",
        "Plumbing",
        "Electrical",
    ];

    private static readonly (string Code, int SortOrder)[] Units =
    [
        ("Bag", 10),
        ("Load", 20),
        ("Nos", 30),
        ("Kg", 40),
        ("Ton", 50),
        ("Sqft", 60),
        ("Rft", 70),
        ("Litre", 80),
    ];

    // BRD §7 expense categories + §5 dashboard breakdown buckets, plus the
    // income/liability categories the posting services (P2-T02..T07) reference by slug.
    private static readonly (string Slug, string Name, string Bucket, bool IsCost)[] ExpenseCategories =
    [
        ("labour", "Labour", "Labour", true),
        ("building_construction", "Building Construction / Mesthri", "Building Construction / Mesthri", true),
        ("interior", "Interior", "Interior", true),
        ("plumbing", "Plumbing", "Plumbing", true),
        ("electrical", "Electrical", "Electrical", true),
        ("materials", "Materials", "Materials", true),
        ("vendor_purchases", "Vendor Purchases", "Vendors", true),
        ("customized_work", "Customized Work", "Customized Work", true),
        ("temple_donation", "Temple Donation", "Temple Donations", true),
        ("personal_common", "Personal / Common Expenses", "Personal/Common Expenses", true),
        ("office_common", "Office / Common Expenses", "Office/Common Expenses", true),
        ("savings_allocation", "Savings Allocation", "Savings Allocation", true),
        ("custom_common", "Custom Expenses", "Custom Expenses", true),
        ("loan_emi", "Loan / EMI", "Loan/EMI", true),
        ("other_expenses", "Other Expenses", "Other Expenses", true),
        ("project_income", "Project Income", "Income", false),
        ("vendor_payable", "Vendor Payable", "Liability", false),
        ("subcontractor_payable", "Subcontractor Payable", "Liability", false),
        ("temple_donation_payable", "Temple Donation Payable", "Liability", false),
        ("custom_work_payable", "Custom Work Payable", "Liability", false),
        ("loan_payable", "Loan Payable", "Liability", false),
        ("internal_transfer", "Internal Transfer", "Internal Transfer", false),
    ];

    private static readonly string[] Categories =
    [
        "Cement",
        "Bricks",
        "Sand",
        "Steel",
        "Pipes",
        "Electrical Cables",
        "Switches",
        "Tiles",
        "Paint",
        "Doors",
        "Windows",
        "Hardware Items",
        "Plumbing Items",
        "Interior Materials",
    ];

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        HashSet<string> existingUnits = (await db.Units
                .Select(u => u.NormalisedCode)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach ((string code, int sortOrder) in Units)
        {
            string norm = NameNormalizer.Normalize(code);
            if (existingUnits.Add(norm))
            {
                db.Units.Add(new UnitOfMeasure { Code = code, NormalisedCode = norm, SortOrder = sortOrder });
            }
        }

        HashSet<string> existingCategories = (await db.ItemCategories
                .Select(c => c.NormalisedName)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (string name in Categories)
        {
            string norm = NameNormalizer.Normalize(name);
            if (existingCategories.Add(norm))
            {
                db.ItemCategories.Add(new ItemCategory { Name = name, NormalisedName = norm });
            }
        }

        HashSet<string> existingDepartments = (await db.Departments
                .Select(d => d.NormalisedName)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach (string name in Departments)
        {
            string norm = NameNormalizer.Normalize(name);
            if (existingDepartments.Add(norm))
            {
                db.Departments.Add(new Department { Name = name, NormalisedName = norm });
            }
        }

        HashSet<string> existingModes = (await db.PaymentModes
                .Select(m => m.NormalisedName)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach ((string name, bool account, bool reference, int sort) in PaymentModes)
        {
            string norm = NameNormalizer.Normalize(name);
            if (existingModes.Add(norm))
            {
                db.PaymentModes.Add(new PaymentMode
                {
                    Name = name,
                    NormalisedName = norm,
                    RequiresAccount = account,
                    RequiresReference = reference,
                    SortOrder = sort,
                });
            }
        }

        HashSet<string> existingAccounts = (await db.Accounts
                .Select(a => a.NormalisedName)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach ((string name, AccountType type, string? bank) in Accounts)
        {
            string norm = NameNormalizer.Normalize(name);
            if (existingAccounts.Add(norm))
            {
                db.Accounts.Add(new Account
                {
                    Name = name,
                    NormalisedName = norm,
                    Type = type,
                    BankName = bank,
                    OpeningBalance = 0m,
                    OpeningBalanceDate = OpeningBalanceDate,
                });
            }
        }

        HashSet<string> existingExpenseCategories = (await db.ExpenseCategories
                .Select(c => c.Slug)
                .ToListAsync(cancellationToken))
            .ToHashSet(StringComparer.Ordinal);

        foreach ((string slug, string name, string bucket, bool isCost) in ExpenseCategories)
        {
            if (existingExpenseCategories.Add(slug))
            {
                db.ExpenseCategories.Add(new ExpenseCategory
                {
                    Slug = slug,
                    Name = name,
                    Bucket = bucket,
                    IsCost = isCost,
                    IsSystem = true,
                });
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
