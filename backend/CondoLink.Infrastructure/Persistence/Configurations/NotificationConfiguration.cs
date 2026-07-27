using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration
    : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");
        builder.HasKey(notification => notification.Id);

        builder.Property(notification => notification.Id)
            .HasColumnName("id");

        builder.Property(notification => notification.RecipientUserId)
            .HasColumnName("recipient_user_id")
            .IsRequired();

        builder.Property(notification => notification.CondominiumId)
            .HasColumnName("condominium_id")
            .IsRequired();

        builder.Property(notification => notification.Type)
            .HasColumnName("type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(notification => notification.Title)
            .HasColumnName("title")
            .HasMaxLength(160)
            .IsRequired();

        builder.Property(notification => notification.Body)
            .HasColumnName("body")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(notification => notification.RequestId)
            .HasColumnName("request_id");

        builder.Property(notification => notification.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(notification => notification.ReadAt)
            .HasColumnName("read_at");

        builder.Ignore(notification => notification.IsRead);

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(notification => notification.RecipientUserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Condominium>()
            .WithMany()
            .HasForeignKey(notification => notification.CondominiumId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DomainRequest>()
            .WithMany()
            .HasForeignKey(notification => notification.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        // Drives the inbox query: newest first for one recipient in one condominium.
        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.CondominiumId,
            notification.CreatedAt
        });

        // Drives the unread badge count.
        builder.HasIndex(notification => new
        {
            notification.RecipientUserId,
            notification.ReadAt
        });
    }
}
