using ColourBricks.Domain.Accounts;
using ColourBricks.Domain.Banking;
using ColourBricks.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("ImportBatch");
        builder.Property(x => x.FileName).HasMaxLength(260).IsRequired();

        builder.HasIndex(x => new { x.AccountId, x.Status });

        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Rows).WithOne().HasForeignKey(r => r.ImportBatchId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StagedBankRowConfiguration : IEntityTypeConfiguration<StagedBankRow>
{
    public void Configure(EntityTypeBuilder<StagedBankRow> builder)
    {
        builder.ToTable("StagedBankRow");
        builder.Property(x => x.Narration).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.NormalisedNarration).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.BankReference).HasMaxLength(120);
        builder.Property(x => x.ParseError).HasMaxLength(500);
        // RawLine is unbounded -> LONGTEXT (no explicit HasColumnType — Pomelo 9.0.0 trap).

        builder.HasIndex(x => x.ImportBatchId);

        builder.HasMany(x => x.Allocations).WithOne()
            .HasForeignKey(a => a.StagedBankRowId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class StagedBankRowAllocationConfiguration : IEntityTypeConfiguration<StagedBankRowAllocation>
{
    public void Configure(EntityTypeBuilder<StagedBankRowAllocation> builder)
    {
        builder.ToTable("StagedBankRowAllocation");
        builder.HasIndex(x => x.StagedBankRowId);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BankTransactionConfiguration : IEntityTypeConfiguration<BankTransaction>
{
    public void Configure(EntityTypeBuilder<BankTransaction> builder)
    {
        builder.ToTable("BankTransaction");
        builder.Property(x => x.Narration).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.NormalisedNarration).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.BankReference).HasMaxLength(120);
        builder.Property(x => x.RowHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExclusionReason).HasMaxLength(500);

        builder.HasIndex(x => x.RowHash).IsUnique();
        builder.HasIndex(x => new { x.AccountId, x.Status });
        builder.HasIndex(x => new { x.AccountId, x.ValueDate });
        builder.HasIndex(x => new { x.Status, x.ValueDate }); // P9-T02: pending-reconciliation scans

        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ImportBatch>().WithMany().HasForeignKey(x => x.ImportBatchId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.ProjectHints).WithOne()
            .HasForeignKey(h => h.BankTransactionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class BankTransactionProjectHintConfiguration : IEntityTypeConfiguration<BankTransactionProjectHint>
{
    public void Configure(EntityTypeBuilder<BankTransactionProjectHint> builder)
    {
        builder.ToTable("BankTransactionProjectHint");
        builder.HasIndex(x => x.BankTransactionId);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class BankStatementProfileConfiguration : IEntityTypeConfiguration<BankStatementProfile>
{
    public void Configure(EntityTypeBuilder<BankStatementProfile> builder)
    {
        builder.ToTable("BankStatementProfile");
        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.Delimiter).HasMaxLength(1).IsRequired();
        builder.Property(x => x.DateFormats).HasMaxLength(120).IsRequired();

        builder.HasIndex(x => x.AccountId);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ReconciliationLinkConfiguration : IEntityTypeConfiguration<ReconciliationLink>
{
    public void Configure(EntityTypeBuilder<ReconciliationLink> builder)
    {
        builder.ToTable("ReconciliationLink");
        builder.Property(x => x.UnlinkReason).HasMaxLength(500);

        builder.HasIndex(x => x.BankTransactionId);
        builder.HasIndex(x => x.SettlementId);
        builder.HasIndex(x => x.CommonExpenseId);
        builder.HasIndex(x => x.ObligationId);

        builder.HasOne<BankTransaction>().WithMany().HasForeignKey(x => x.BankTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ColourBricks.Domain.Settlements.Settlement>().WithMany()
            .HasForeignKey(x => x.SettlementId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne<ColourBricks.Domain.CommonExpenses.CommonExpense>().WithMany()
            .HasForeignKey(x => x.CommonExpenseId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
        builder.HasOne<ColourBricks.Domain.Obligations.Obligation>().WithMany()
            .HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict).IsRequired(false);
    }
}

internal sealed class PartyAliasConfiguration : IEntityTypeConfiguration<PartyAlias>
{
    public void Configure(EntityTypeBuilder<PartyAlias> builder)
    {
        builder.ToTable("PartyAlias");
        builder.Property(x => x.Alias).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NormalisedAlias).HasMaxLength(200).IsRequired();

        builder.HasIndex(x => new { x.PartyId, x.NormalisedAlias }).IsUnique();
        builder.HasOne<ColourBricks.Domain.Parties.Party>().WithMany()
            .HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class InternalTransferConfiguration : IEntityTypeConfiguration<InternalTransfer>
{
    public void Configure(EntityTypeBuilder<InternalTransfer> builder)
    {
        builder.ToTable("InternalTransfer");
        builder.HasIndex(x => x.FromTransactionId);
        builder.HasIndex(x => x.ToTransactionId);

        builder.HasOne<BankTransaction>().WithMany().HasForeignKey(x => x.FromTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<BankTransaction>().WithMany().HasForeignKey(x => x.ToTransactionId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.FromAccountId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.ToAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}
