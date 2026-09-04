using ColourBricks.Domain.Allocations;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Projects;
using ColourBricks.Domain.Settlements;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class AllocationConfiguration : IEntityTypeConfiguration<Allocation>
{
    public void Configure(EntityTypeBuilder<Allocation> builder)
    {
        builder.ToTable("Allocation");

        builder.Property(x => x.Method).HasMaxLength(20).IsRequired();

        builder.HasIndex(x => x.SettlementId);
        builder.HasIndex(x => x.ObligationId);
        builder.HasIndex(x => new { x.PartyId, x.ProjectId });

        builder.HasOne<Settlement>().WithMany().HasForeignKey(x => x.SettlementId)
            .IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Obligation>().WithMany().HasForeignKey(x => x.ObligationId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId)
            .IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Party>().WithMany().HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
    }
}
