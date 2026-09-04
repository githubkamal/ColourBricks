using ColourBricks.Domain.Notifications;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace ColourBricks.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notification");
        builder.Property(x => x.Trigger).HasMaxLength(60).IsRequired();
        builder.Property(x => x.DedupeKey).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Severity).HasMaxLength(20).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(60);
        builder.Property(x => x.EmailError).HasMaxLength(500);
        builder.HasIndex(x => x.DedupeKey).IsUnique();
        builder.HasIndex(x => x.Trigger);
    }
}

internal sealed class NotificationReadConfiguration : IEntityTypeConfiguration<NotificationRead>
{
    public void Configure(EntityTypeBuilder<NotificationRead> builder)
    {
        builder.ToTable("NotificationRead");
        builder.HasIndex(x => new { x.NotificationId, x.UserId }).IsUnique();
        builder.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class NotificationMuteConfiguration : IEntityTypeConfiguration<NotificationMute>
{
    public void Configure(EntityTypeBuilder<NotificationMute> builder)
    {
        builder.ToTable("NotificationMute");
        builder.Property(x => x.Trigger).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.Trigger }).IsUnique();
    }
}

internal sealed class NotificationChannelConfigConfiguration : IEntityTypeConfiguration<NotificationChannelConfig>
{
    public void Configure(EntityTypeBuilder<NotificationChannelConfig> builder)
    {
        builder.ToTable("NotificationChannelConfig");
        builder.Property(x => x.Trigger).HasMaxLength(60).IsRequired();
        builder.HasIndex(x => new { x.RoleId, x.Trigger }).IsUnique();
    }
}
