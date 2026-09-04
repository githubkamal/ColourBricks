using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Projects;
using ColourBricks.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Project");

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SiteAddress).HasMaxLength(500);
        builder.Property(x => x.ContactDetails).HasMaxLength(200);
        // Notes is unbounded -> LONGTEXT (no explicit HasColumnType — Pomelo 9.0.0 trap).

        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.ClientId);
        builder.HasIndex(x => x.ManagerId);
        builder.HasIndex(x => x.StartDate);

        // Manager is a User; client is a Party. No navigations (Project is a Domain
        // type); FK only.
        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(x => x.ManagerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Party>()
            .WithMany()
            .HasForeignKey(x => x.ClientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
