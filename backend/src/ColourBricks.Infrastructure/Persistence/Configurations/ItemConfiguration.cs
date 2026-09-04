using ColourBricks.Domain.Items;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class ItemConfiguration : IEntityTypeConfiguration<Item>
{
    public void Configure(EntityTypeBuilder<Item> builder)
    {
        builder.ToTable("Item");

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NormalisedName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(30).IsRequired();

        // plan.md §6 — the name is the identity of a master row.
        builder.HasIndex(x => x.NormalisedName).IsUnique();
        builder.HasIndex(x => x.CategoryId);

        // No navigation (Item is a Domain type); FK only. Restrict — a category in
        // use cannot be hard-deleted (plan.md §5.6).
        builder.HasOne<ItemCategory>()
            .WithMany()
            .HasForeignKey(x => x.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class ItemCategoryConfiguration : IEntityTypeConfiguration<ItemCategory>
{
    public void Configure(EntityTypeBuilder<ItemCategory> builder)
    {
        builder.ToTable("ItemCategory");

        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NormalisedName).HasMaxLength(200).IsRequired();
        builder.HasIndex(x => x.NormalisedName).IsUnique();
    }
}

internal sealed class UnitOfMeasureConfiguration : IEntityTypeConfiguration<UnitOfMeasure>
{
    public void Configure(EntityTypeBuilder<UnitOfMeasure> builder)
    {
        builder.ToTable("Unit");

        builder.Property(x => x.Code).HasMaxLength(30).IsRequired();
        builder.Property(x => x.NormalisedCode).HasMaxLength(30).IsRequired();
        builder.HasIndex(x => x.NormalisedCode).IsUnique();
    }
}
