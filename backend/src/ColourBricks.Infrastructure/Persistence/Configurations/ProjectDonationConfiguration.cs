using ColourBricks.Domain.Donations;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class ProjectDonationConfiguration : IEntityTypeConfiguration<ProjectDonation>
{
    public void Configure(EntityTypeBuilder<ProjectDonation> builder)
    {
        builder.ToTable("ProjectDonation");

        // Percentage stays on the global DECIMAL(18,2) convention (plan.md §5.4) —
        // two decimal places is plenty for a donation rate (e.g. 2.00, 1.50).

        // One donation setup per project (BRD §26).
        builder.HasIndex(x => x.ProjectId).IsUnique();

        builder.HasOne<Project>()
            .WithMany()
            .HasForeignKey(x => x.ProjectId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ProjectDonationTempleConfiguration : IEntityTypeConfiguration<ProjectDonationTemple>
{
    public void Configure(EntityTypeBuilder<ProjectDonationTemple> builder)
    {
        builder.ToTable("ProjectDonationTemple");

        builder.HasIndex(x => x.ProjectDonationId);
        builder.HasIndex(x => new { x.ProjectDonationId, x.TempleId }).IsUnique();

        builder.HasOne<ProjectDonation>()
            .WithMany()
            .HasForeignKey(x => x.ProjectDonationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(x => x.TempleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
