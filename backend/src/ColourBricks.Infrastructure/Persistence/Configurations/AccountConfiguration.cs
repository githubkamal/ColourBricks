using ColourBricks.Domain.Accounts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("Account");

        builder.Property(x => x.Name).HasMaxLength(120).IsRequired();
        builder.Property(x => x.NormalisedName).HasMaxLength(120).IsRequired();
        builder.Property(x => x.BankName).HasMaxLength(120);
        builder.Property(x => x.AccountNumber).HasMaxLength(40);
        builder.Property(x => x.Ifsc).HasMaxLength(20);

        builder.HasIndex(x => x.NormalisedName).IsUnique();
        builder.HasIndex(x => x.Type);
    }
}
