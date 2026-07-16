using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestAttachmentConfiguration : IEntityTypeConfiguration<RequestAttachment>
{
    public void Configure(EntityTypeBuilder<RequestAttachment> builder)
    {
        builder.ToTable("request_attachments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RequestId).HasColumnName("request_id").IsRequired();
        builder.Property(x => x.RequestMessageId).HasColumnName("request_message_id");
        builder.Property(x => x.UploadedByUserId).HasColumnName("uploaded_by_user_id").IsRequired();
        builder.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255).IsRequired();
        builder.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(500).IsRequired();
        builder.Property(x => x.ContentType).HasColumnName("content_type").HasMaxLength(100).IsRequired();
        builder.Property(x => x.FileSize).HasColumnName("file_size").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        builder.HasOne<DomainRequest>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RequestMessage>().WithMany().HasForeignKey(x => x.RequestMessageId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.RequestId, x.CreatedAt });
        builder.HasIndex(x => x.RequestMessageId);
        builder.HasIndex(x => x.UploadedByUserId);
    }
}
