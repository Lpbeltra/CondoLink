using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppDraftAttachmentConfiguration
    : IEntityTypeConfiguration<WhatsAppDraftAttachment>
{
    public void Configure(EntityTypeBuilder<WhatsAppDraftAttachment> builder)
    {
        builder.ToTable("whatsapp_draft_attachments");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.SessionId).HasColumnName("session_id").IsRequired();
        builder.Property(item => item.ExternalMediaId).HasColumnName("external_media_id").HasMaxLength(200).IsRequired();
        builder.Property(item => item.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(item => item.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        builder.Property(item => item.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(item => item.FileSize).HasColumnName("file_size").IsRequired();
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasOne<WhatsAppSession>().WithMany().HasForeignKey(item => item.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(item => item.ExternalMediaId).HasDatabaseName("ux_whatsapp_draft_attachments_external_media_id").IsUnique();
        builder.HasIndex(item => new { item.SessionId, item.CreatedAt });
    }
}
