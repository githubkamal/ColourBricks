using ColourBricks.Domain.Payments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class PaymentModeConfiguration : IEntityTypeConfiguration<PaymentMode>
{
    public void Configure(EntityTypeBuilder<PaymentMode> builder)
    {
        builder.ToTable("PaymentMode");

        builder.Property(x => x.Name).HasMaxLength(60).IsRequired();
        builder.Property(x => x.NormalisedName).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => x.NormalisedName).IsUnique();
    }
}
