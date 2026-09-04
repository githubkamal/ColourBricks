using System.Reflection;
using ColourBricks.Domain.Common;
using ColourBricks.Domain.Accounts;
using ColourBricks.Domain.Allocations;
using ColourBricks.Domain.Attachments;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Departments;
using ColourBricks.Domain.Donations;
using ColourBricks.Domain.Items;
using ColourBricks.Domain.Ledger;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Payments;
using ColourBricks.Domain.Projects;
using ColourBricks.Domain.Settlements;
using ColourBricks.Infrastructure.Auditing;
using ColourBricks.Infrastructure.Idempotency;
using ColourBricks.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Pomelo.EntityFrameworkCore.MySql.Infrastructure;

namespace ColourBricks.Infrastructure.Persistence;

/// <summary>
/// The single EF Core context for the application. Global model conventions here
/// implement plan.md §6: <c>utf8mb4_unicode_ci</c> on every table, <c>DECIMAL(18,2)</c>
/// for every <see cref="decimal"/>, <c>DATE</c> for every <see cref="DateOnly"/>,
/// enums as <c>TINYINT</c>, and a rotating GUID concurrency token on every table.
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<IdempotencyRecord> IdempotencyRecords => Set<IdempotencyRecord>();

    public DbSet<User> Users => Set<User>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<Permission> Permissions => Set<Permission>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<UserProjectAccess> UserProjectAccess => Set<UserProjectAccess>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    /// <summary>
    /// Append-only financial ledger. Internal on purpose (plan.md P2-T01): the only
    /// writer is <c>ILedgerPostingService</c>. Read models go through
    /// <c>ILedgerQueryService</c>.
    /// </summary>
    internal DbSet<LedgerEntry> LedgerEntries => Set<LedgerEntry>();

    internal DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<Settlement> Settlements => Set<Settlement>();

    public DbSet<Obligation> Obligations => Set<Obligation>();

    public DbSet<Attachment> Attachments => Set<Attachment>();

    public DbSet<ObligationLine> ObligationLines => Set<ObligationLine>();

    public DbSet<Allocation> Allocations => Set<Allocation>();

    public DbSet<Party> Parties => Set<Party>();

    public DbSet<Item> Items => Set<Item>();

    public DbSet<ItemCategory> ItemCategories => Set<ItemCategory>();

    public DbSet<UnitOfMeasure> Units => Set<UnitOfMeasure>();

    public DbSet<Department> Departments => Set<Department>();

    public DbSet<PaymentMode> PaymentModes => Set<PaymentMode>();

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<ProjectDonation> ProjectDonations => Set<ProjectDonation>();

    public DbSet<ProjectDonationTemple> ProjectDonationTemples => Set<ProjectDonationTemple>();

    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();

    public DbSet<StagedBankRow> StagedBankRows => Set<StagedBankRow>();

    public DbSet<StagedBankRowAllocation> StagedBankRowAllocations => Set<StagedBankRowAllocation>();

    public DbSet<BankTransaction> BankTransactions => Set<BankTransaction>();

    public DbSet<BankTransactionProjectHint> BankTransactionProjectHints => Set<BankTransactionProjectHint>();

    public DbSet<BankStatementProfile> BankStatementProfiles => Set<BankStatementProfile>();

    public DbSet<ReconciliationLink> ReconciliationLinks => Set<ReconciliationLink>();

    public DbSet<PartyAlias> PartyAliases => Set<PartyAlias>();

    public DbSet<InternalTransfer> InternalTransfers => Set<InternalTransfer>();

    public DbSet<ColourBricks.Domain.Budgets.ProjectBudgetRevision> ProjectBudgetRevisions =>
        Set<ColourBricks.Domain.Budgets.ProjectBudgetRevision>();

    public DbSet<ColourBricks.Domain.Budgets.ProjectBudgetLine> ProjectBudgetLines =>
        Set<ColourBricks.Domain.Budgets.ProjectBudgetLine>();

    public DbSet<ColourBricks.Domain.CommonExpenses.CommonExpense> CommonExpenses =>
        Set<ColourBricks.Domain.CommonExpenses.CommonExpense>();

    public DbSet<ColourBricks.Domain.CommonExpenses.CommonExpenseAllocationRun> CommonExpenseAllocationRuns =>
        Set<ColourBricks.Domain.CommonExpenses.CommonExpenseAllocationRun>();

    public DbSet<ColourBricks.Domain.CommonExpenses.CommonExpenseAllocationLine> CommonExpenseAllocationLines =>
        Set<ColourBricks.Domain.CommonExpenses.CommonExpenseAllocationLine>();

    public DbSet<ColourBricks.Domain.FieldOfficers.FieldOfficerExpense> FieldOfficerExpenses =>
        Set<ColourBricks.Domain.FieldOfficers.FieldOfficerExpense>();

    public DbSet<ColourBricks.Domain.Loans.Loan> Loans => Set<ColourBricks.Domain.Loans.Loan>();

    public DbSet<ColourBricks.Domain.Loans.LoanEmiInstalment> LoanEmiInstalments =>
        Set<ColourBricks.Domain.Loans.LoanEmiInstalment>();

    public DbSet<ColourBricks.Domain.Loans.LoanEmiPayment> LoanEmiPayments =>
        Set<ColourBricks.Domain.Loans.LoanEmiPayment>();

    public DbSet<ColourBricks.Domain.Loans.LoanAlert> LoanAlerts =>
        Set<ColourBricks.Domain.Loans.LoanAlert>();

    public DbSet<ColourBricks.Domain.Notifications.Notification> Notifications =>
        Set<ColourBricks.Domain.Notifications.Notification>();

    public DbSet<ColourBricks.Domain.Notifications.NotificationRead> NotificationReads =>
        Set<ColourBricks.Domain.Notifications.NotificationRead>();

    public DbSet<ColourBricks.Domain.Notifications.NotificationMute> NotificationMutes =>
        Set<ColourBricks.Domain.Notifications.NotificationMute>();

    public DbSet<ColourBricks.Domain.Notifications.NotificationChannelConfig> NotificationChannelConfigs =>
        Set<ColourBricks.Domain.Notifications.NotificationChannelConfig>();

    public DbSet<ColourBricks.Domain.PurchaseOrders.PurchaseOrder> PurchaseOrders =>
        Set<ColourBricks.Domain.PurchaseOrders.PurchaseOrder>();

    public DbSet<ColourBricks.Domain.PurchaseOrders.PurchaseOrderLine> PurchaseOrderLines =>
        Set<ColourBricks.Domain.PurchaseOrders.PurchaseOrderLine>();

    public DbSet<ColourBricks.Domain.Settings.SystemSettings> SystemSettings =>
        Set<ColourBricks.Domain.Settings.SystemSettings>();

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);

        // plan.md §5.4 — money is DECIMAL(18, Money.Scale) everywhere. Never float/double.
        // Client request, 2026-09-04: raised from 2 to 3 decimal places — kept in sync
        // with Money.Scale, the single source of truth for this precision.
        configurationBuilder.Properties<decimal>().HavePrecision(18, ColourBricks.Domain.Services.Money.Scale);

        // plan.md §5.5 — business dates are DATE (no time, no timezone).
        configurationBuilder.Properties<DateOnly>().HaveColumnType("date");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // plan.md §3.1 / §6 — pin charset and collation explicitly on every table
        // and column so schemas stay portable and never emit utf8mb4_0900_ai_ci.
        modelBuilder.HasCharSet("utf8mb4", DelegationModes.ApplyToAll);
        modelBuilder.UseCollation("utf8mb4_unicode_ci", DelegationModes.ApplyToAll);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Bulk conventions via metadata setters. The fluent builder is deliberately
        // not re-entered while enumerating the model.
        foreach (IMutableEntityType entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (IMutableProperty property in entityType.GetProperties())
            {
                // plan.md §6 — enums stored as TINYINT, mapped in C#. Never as strings.
                // Explicit per-property config wins (e.g. a wide [Flags] enum as int).
                Type clrType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
                if (clrType.IsEnum && property.GetColumnType() is null)
                {
                    property.SetColumnType("tinyint");
                }
            }

            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            // plan.md §6 — CHAR(36) GUID optimistic-concurrency token on every table.
            // NOTE: set fixed-length + max-length only. An explicit HasColumnType("char(36)")
            // on a string trips an NRE in Pomelo 9.0.0's type-mapping source; IsFixedLength
            // + MaxLength(36) already produces char(36). See ADR 0001.
            IMutableProperty? stamp = entityType.FindProperty(nameof(BaseEntity.ConcurrencyStamp));
            if (stamp is not null)
            {
                stamp.IsConcurrencyToken = true;
                stamp.IsNullable = false;
                stamp.SetMaxLength(36);
            }

            IMutableProperty? createdAt = entityType.FindProperty(nameof(BaseEntity.CreatedAtUtc));
            if (createdAt is not null)
            {
                createdAt.IsNullable = false;
            }
        }
    }
}
