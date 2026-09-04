using ColourBricks.Domain.Accounts;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Payments;
using ColourBricks.Domain.Projects;
using ColourBricks.Domain.Settlements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class SettlementConfiguration : IEntityTypeConfiguration<Settlement>
{
    public void Configure(EntityTypeBuilder<Settlement> builder)
    {
        builder.ToTable("Settlement");

        builder.Property(x => x.ReferenceNo).HasMaxLength(80);
        // Description is unbounded -> LONGTEXT (no explicit HasColumnType — Pomelo 9.0.0 trap).

        builder.HasIndex(x => new { x.ProjectId, x.Date });
        builder.HasIndex(x => new { x.PartyId, x.Date });
        builder.HasIndex(x => x.Direction);

        // Rule 46 — an income settlement belongs to exactly one project. Non-null
        // ProjectId + a single FK makes multi-project structurally impossible.
        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(x => x.PartyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<PaymentMode>()
            .WithMany()
            .HasForeignKey(x => x.PaymentModeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(x => x.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Obligation>()
            .WithMany()
            .HasForeignKey(x => x.ObligationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
