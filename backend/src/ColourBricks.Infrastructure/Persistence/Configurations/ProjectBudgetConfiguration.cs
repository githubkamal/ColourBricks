using ColourBricks.Domain.Budgets;
using ColourBricks.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class ProjectBudgetRevisionConfiguration : IEntityTypeConfiguration<ProjectBudgetRevision>
{
    public void Configure(EntityTypeBuilder<ProjectBudgetRevision> builder)
    {
        builder.ToTable("ProjectBudgetRevision");
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasIndex(x => new { x.ProjectId, x.RevisionNumber }).IsUnique();
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(x => x.Lines).WithOne()
            .HasForeignKey(l => l.RevisionId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ProjectBudgetLineConfiguration : IEntityTypeConfiguration<ProjectBudgetLine>
{
    public void Configure(EntityTypeBuilder<ProjectBudgetLine> builder)
    {
        builder.ToTable("ProjectBudgetLine");
        builder.HasIndex(x => x.RevisionId);
    }
}
