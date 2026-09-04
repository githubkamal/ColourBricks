using ColourBricks.Domain.CommonExpenses;
using ColourBricks.Domain.Payments;
using ColourBricks.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class CommonExpenseConfiguration : IEntityTypeConfiguration<CommonExpense>
{
    public void Configure(EntityTypeBuilder<CommonExpense> builder)
    {
        builder.ToTable("CommonExpense");
        builder.Property(x => x.SubCategory).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ReferenceNo).HasMaxLength(80);
        // Description is unbounded -> LONGTEXT (Pomelo 9.0.0 trap: no explicit HasColumnType on strings).

        builder.HasIndex(x => new { x.Type, x.Date });

        builder.HasIndex(x => x.AllocationRunId);
        builder.HasOne<PaymentMode>().WithMany().HasForeignKey(x => x.PaymentModeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CommonExpenseAllocationRunConfiguration
    : IEntityTypeConfiguration<Domain.CommonExpenses.CommonExpenseAllocationRun>
{
    public void Configure(EntityTypeBuilder<Domain.CommonExpenses.CommonExpenseAllocationRun> builder)
    {
        builder.ToTable("CommonExpenseAllocationRun");
        builder.Property(x => x.Types).HasMaxLength(60).IsRequired();
        builder.Property(x => x.Method).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasIndex(x => new { x.PeriodFrom, x.PeriodTo });
        builder.HasMany(x => x.Lines).WithOne()
            .HasForeignKey(l => l.RunId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class CommonExpenseAllocationLineConfiguration
    : IEntityTypeConfiguration<Domain.CommonExpenses.CommonExpenseAllocationLine>
{
    public void Configure(EntityTypeBuilder<Domain.CommonExpenses.CommonExpenseAllocationLine> builder)
    {
        builder.ToTable("CommonExpenseAllocationLine");
        builder.HasIndex(x => x.RunId);
        builder.HasOne<Domain.Projects.Project>().WithMany()
            .HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
