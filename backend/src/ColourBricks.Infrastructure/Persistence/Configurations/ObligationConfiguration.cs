using ColourBricks.Domain.Departments;
using ColourBricks.Domain.Obligations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class ObligationConfiguration : IEntityTypeConfiguration<Obligation>
{
    public void Configure(EntityTypeBuilder<Obligation> builder)
    {
        builder.ToTable("Obligation");

        builder.Property(x => x.Reference).HasMaxLength(80);
        // Description is unbounded -> LONGTEXT.

        builder.HasIndex(x => new { x.ProjectId, x.Date });
        builder.HasIndex(x => new { x.PartyId, x.Date });
        builder.HasIndex(x => x.Type);
        builder.HasIndex(x => x.PurchaseOrderId);

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Party>().WithMany().HasForeignKey(x => x.PartyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Department>().WithMany().HasForeignKey(x => x.DepartmentId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines)
            .WithOne()
            .HasForeignKey(l => l.ObligationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ObligationLineConfiguration : IEntityTypeConfiguration<ObligationLine>
{
    public void Configure(EntityTypeBuilder<ObligationLine> builder)
    {
        builder.ToTable("ObligationLine");

        builder.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(30).IsRequired();

        builder.HasIndex(x => x.ObligationId);
    }
}
