using ColourBricks.Domain.Departments;
using ColourBricks.Domain.Parties;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class PartyConfiguration : IEntityTypeConfiguration<Party>
{
    public void Configure(EntityTypeBuilder<Party> builder)
    {
        builder.ToTable("Party");

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NormalisedName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(100);
        builder.Property(x => x.ContactPerson).HasMaxLength(120);
        builder.Property(x => x.Phone).HasMaxLength(40);
        builder.Property(x => x.Email).HasMaxLength(200);
        builder.Property(x => x.Address).HasMaxLength(500);
        builder.Property(x => x.GstNumber).HasMaxLength(20);
        builder.Property(x => x.PaymentTerms).HasMaxLength(200);
        // BankDetails is unbounded -> LONGTEXT.

        builder.Property(x => x.Types).HasColumnType("int");

        // plan.md §6 — one row per counterparty; the name is the identity.
        builder.HasIndex(x => x.NormalisedName).IsUnique();
        builder.HasIndex(x => x.DepartmentId);

        // Department for subcontractor teams (BRD §9). No navigation (Party is a
        // Domain type); FK only. Restrict — a department in use is deactivated,
        // never hard-deleted (plan.md §5.6).
        builder.HasOne<Department>()
            .WithMany()
            .HasForeignKey(x => x.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
