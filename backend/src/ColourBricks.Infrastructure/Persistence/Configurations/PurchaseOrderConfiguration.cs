using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Projects;
using ColourBricks.Domain.PurchaseOrders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
{
    public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
    {
        builder.ToTable("PurchaseOrder");

        builder.Property(x => x.PoNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.InvoiceNumber).HasMaxLength(80);
        // Notes is unbounded -> LONGTEXT (Pomelo 9.0.0 trap: no explicit HasColumnType on strings).

        builder.HasIndex(x => x.PoNumber).IsUnique();
        builder.HasIndex(x => new { x.VendorId, x.Status });

        builder.HasOne<Party>().WithMany().HasForeignKey(x => x.VendorId).OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.Lines).WithOne()
            .HasForeignKey(l => l.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class PurchaseOrderLineConfiguration : IEntityTypeConfiguration<PurchaseOrderLine>
{
    public void Configure(EntityTypeBuilder<PurchaseOrderLine> builder)
    {
        builder.ToTable("PurchaseOrderLine");

        builder.Property(x => x.ItemName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Unit).HasMaxLength(30).IsRequired();

        builder.HasIndex(x => x.PurchaseOrderId);
        builder.HasIndex(x => x.ProjectId);

        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
    }
}
