using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppOutboundMessageConfiguration
    : IEntityTypeConfiguration<WhatsAppOutboundMessage>
{
    public void Configure(EntityTypeBuilder<WhatsAppOutboundMessage> b)
    {
        b.ToTable("whatsapp_outbound_messages");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.RequestId).HasColumnName("request_id");
        b.Property(x => x.RequestMessageId).HasColumnName("request_message_id");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.CondominiumId).HasColumnName("condominium_id");
        b.Property(x => x.DestinationPhone).HasColumnName("destination_phone").HasMaxLength(20).IsRequired();
        b.Property(x => x.NotificationType).HasColumnName("notification_type").HasConversion<int>().IsRequired();
        b.Property(x => x.SendMode).HasColumnName("send_mode").HasConversion<int>().IsRequired();
        b.Property(x => x.TemplateName).HasColumnName("template_name").HasMaxLength(200);
        b.Property(x => x.TemplateLanguage).HasColumnName("template_language").HasMaxLength(20);
        b.Property(x => x.Content).HasColumnName("content").HasMaxLength(1500).IsRequired();
        b.Property(x => x.ExternalMessageId).HasColumnName("external_message_id").HasMaxLength(200);
        b.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        b.Property(x => x.ManualRetryCount).HasColumnName("manual_retry_count").IsRequired();
        b.Property(x => x.NextAttemptAt).HasColumnName("next_attempt_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.SentAt).HasColumnName("sent_at");
        b.Property(x => x.DeliveredAt).HasColumnName("delivered_at");
        b.Property(x => x.ReadAt).HasColumnName("read_at");
        b.Property(x => x.FailedAt).HasColumnName("failed_at");
        b.Property(x => x.LastErrorCode).HasColumnName("last_error_code").HasMaxLength(100);
        b.Property(x => x.LastErrorDescription).HasColumnName("last_error_description").HasMaxLength(500);
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(250).IsRequired();
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken().IsRequired();
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<RequestMessage>().WithMany().HasForeignKey(x => x.RequestMessageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.IdempotencyKey).HasDatabaseName("ux_whatsapp_outbound_idempotency_key").IsUnique();
        b.HasIndex(x => x.ExternalMessageId).HasDatabaseName("ux_whatsapp_outbound_external_message_id")
            .IsUnique().HasFilter("\"external_message_id\" IS NOT NULL");
        b.HasIndex(x => new { x.Status, x.NextAttemptAt });
        b.HasIndex(x => new { x.CondominiumId, x.CreatedAt });
    }
}
