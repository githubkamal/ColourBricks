using ColourBricks.Domain.Accounts;
using ColourBricks.Domain.Loans;
using ColourBricks.Domain.Parties;
using ColourBricks.Domain.Projects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class LoanConfiguration : IEntityTypeConfiguration<Loan>
{
    public void Configure(EntityTypeBuilder<Loan> builder)
    {
        builder.ToTable("Loan");
        builder.Property(x => x.Reference).HasMaxLength(80);
        builder.Property(x => x.Notes).HasMaxLength(1000);

        builder.HasIndex(x => x.LenderId);
        builder.HasIndex(x => x.ProjectId);
        builder.HasIndex(x => x.Status);

        builder.HasOne<Party>().WithMany().HasForeignKey(x => x.LenderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Account>().WithMany().HasForeignKey(x => x.DisbursementAccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LoanEmiInstalmentConfiguration : IEntityTypeConfiguration<LoanEmiInstalment>
{
    public void Configure(EntityTypeBuilder<LoanEmiInstalment> builder)
    {
        builder.ToTable("LoanEmiInstalment");
        builder.HasIndex(x => new { x.LoanId, x.InstalmentNo }).IsUnique();
        builder.HasIndex(x => x.DueDate);
        builder.HasIndex(x => x.Status);
        builder.HasOne<Loan>().WithMany().HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class LoanEmiPaymentConfiguration : IEntityTypeConfiguration<LoanEmiPayment>
{
    public void Configure(EntityTypeBuilder<LoanEmiPayment> builder)
    {
        builder.ToTable("LoanEmiPayment");
        builder.Property(x => x.ReferenceNo).HasMaxLength(80);
        builder.HasIndex(x => x.LoanId);
        builder.HasIndex(x => x.InstalmentId);
        builder.HasIndex(x => x.Status);
        builder.HasOne<Loan>().WithMany().HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LoanEmiInstalment>().WithMany().HasForeignKey(x => x.InstalmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Payments.PaymentMode>().WithMany().HasForeignKey(x => x.PaymentModeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Accounts.Account>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class LoanAlertConfiguration : IEntityTypeConfiguration<LoanAlert>
{
    public void Configure(EntityTypeBuilder<LoanAlert> builder)
    {
        builder.ToTable("LoanAlert");
        builder.HasIndex(x => new { x.InstalmentId, x.Kind }).IsUnique();
        builder.HasIndex(x => x.ResolvedOn);
        builder.HasOne<Loan>().WithMany().HasForeignKey(x => x.LoanId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<LoanEmiInstalment>().WithMany().HasForeignKey(x => x.InstalmentId).OnDelete(DeleteBehavior.Cascade);
    }
}
