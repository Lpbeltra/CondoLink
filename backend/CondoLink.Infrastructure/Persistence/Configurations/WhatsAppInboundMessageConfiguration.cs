using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppInboundMessageConfiguration
    : IEntityTypeConfiguration<WhatsAppInboundMessage>
{
    public const string UniqueExternalMessageIdIndex =
        "ux_whatsapp_inbound_messages_external_id";

    public void Configure(EntityTypeBuilder<WhatsAppInboundMessage> builder)
    {
        builder.ToTable("whatsapp_inbound_messages");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.ExternalMessageId).HasColumnName("external_message_id").HasMaxLength(200).IsRequired();
        builder.Property(item => item.PhoneNumber).HasColumnName("phone_number").HasMaxLength(20).IsRequired();
        builder.Property(item => item.MessageType).HasColumnName("message_type").HasMaxLength(40).IsRequired();
        builder.Property(item => item.Text).HasColumnName("text").HasMaxLength(4000);
        builder.Property(item => item.ProviderTimestamp).HasColumnName("provider_timestamp").IsRequired();
        builder.Property(item => item.ReceivedAt).HasColumnName("received_at").IsRequired();
        builder.Property(item => item.ProcessedAt).HasColumnName("processed_at");
        builder.Property(item => item.IdentifiedUserId).HasColumnName("identified_user_id");
        builder.Property(item => item.ProcessingResult).HasColumnName("processing_result").HasMaxLength(100);
        builder.HasIndex(item => item.ExternalMessageId)
            .HasDatabaseName(UniqueExternalMessageIdIndex).IsUnique();
        builder.HasOne<ApplicationUser>().WithMany()
            .HasForeignKey(item => item.IdentifiedUserId).OnDelete(DeleteBehavior.SetNull);
    }
}
