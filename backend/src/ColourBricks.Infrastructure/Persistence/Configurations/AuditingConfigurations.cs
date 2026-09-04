using ColourBricks.Domain.Ledger;
using ColourBricks.Infrastructure.Auditing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLog");

        builder.Property(x => x.Module).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(40).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(120);
        builder.Property(x => x.RecordId).HasMaxLength(120).IsRequired();
        builder.Property(x => x.ReconciliationStatus).HasMaxLength(40);
        // OldValues / NewValues / Details / ReversalHistory are unbounded -> LONGTEXT.

        builder.HasIndex(x => x.TimestampUtc);
        builder.HasIndex(x => new { x.Module, x.Action });
        builder.HasIndex(x => x.UserId);
        builder.HasIndex(x => x.RecordId);
    }
}

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntry");

        builder.Property(x => x.SourceType).HasMaxLength(60).IsRequired();

        builder.HasIndex(x => new { x.ProjectId, x.EntryDate });
        builder.HasIndex(x => new { x.PartyId, x.EntryDate });
        builder.HasIndex(x => new { x.SourceType, x.SourceId });
        // P9-T02 covering indexes: account-balance and report-by-category scans.
        builder.HasIndex(x => new { x.AccountId, x.EntryDate });
        builder.HasIndex(x => x.CategoryId);
    }
}
