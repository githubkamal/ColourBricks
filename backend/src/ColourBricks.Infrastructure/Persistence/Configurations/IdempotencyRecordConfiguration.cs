using ColourBricks.Infrastructure.Idempotency;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
    {
        builder.ToTable("IdempotencyRecord");

        builder.Property(x => x.Key).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.Key).IsUnique();

        builder.Property(x => x.Method).HasMaxLength(10).IsRequired();
        builder.Property(x => x.Path).HasMaxLength(2048).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128);

        // Unbounded string — Pomelo maps this to LONGTEXT. Do not set HasColumnType
        // explicitly: an explicit string column type trips an NRE in Pomelo 9.0.0.

        // Sweep expired rows by CreatedAtUtc (plan.md §7 — 24-hour window).
        builder.HasIndex(x => x.CreatedAtUtc);
    }
}
