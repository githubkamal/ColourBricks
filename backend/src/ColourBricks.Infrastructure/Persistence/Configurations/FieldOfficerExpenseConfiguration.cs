using ColourBricks.Domain.FieldOfficers;
using ColourBricks.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class FieldOfficerExpenseConfiguration : IEntityTypeConfiguration<FieldOfficerExpense>
{
    public void Configure(EntityTypeBuilder<FieldOfficerExpense> builder)
    {
        builder.ToTable("FieldOfficerExpense");
        builder.Property(x => x.ReferenceNo).HasMaxLength(80);
        // Description is unbounded -> LONGTEXT (Pomelo 9.0.0 trap: no explicit HasColumnType on strings).

        builder.HasIndex(x => new { x.FieldOfficerId, x.Date });

        builder.HasOne<Party>().WithMany().HasForeignKey(x => x.FieldOfficerId).OnDelete(DeleteBehavior.Restrict);
    }
}
