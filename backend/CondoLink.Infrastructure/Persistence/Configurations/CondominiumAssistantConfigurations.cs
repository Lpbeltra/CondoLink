using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumDocumentConfiguration : IEntityTypeConfiguration<CondominiumDocument>
{
    public void Configure(EntityTypeBuilder<CondominiumDocument> b)
    {
        b.ToTable("condominium_documents"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CondominiumId).HasColumnName("condominium_id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(200); b.Property(x => x.DocumentType).HasColumnName("document_type").HasConversion<int>();
        b.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(255); b.Property(x => x.StorageKey).HasColumnName("storage_key").HasMaxLength(500);
        b.Property(x => x.MimeType).HasColumnName("mime_type").HasMaxLength(100); b.Property(x => x.Version).HasColumnName("version");
        b.Property(x => x.DocumentDate).HasColumnName("document_date"); b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.Property(x => x.UploadedByUserId).HasColumnName("uploaded_by_user_id"); b.Property(x => x.ProcessingStatus).HasColumnName("processing_status").HasConversion<int>();
        b.Property(x => x.ProcessingError).HasColumnName("processing_error").HasMaxLength(500);
        b.HasIndex(x => new { x.CondominiumId, x.IsActive, x.ProcessingStatus });
        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<CondoLink.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class CondominiumDocumentChunkConfiguration : IEntityTypeConfiguration<CondominiumDocumentChunk>
{
    public void Configure(EntityTypeBuilder<CondominiumDocumentChunk> b)
    {
        b.ToTable("condominium_document_chunks"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CondominiumDocumentId).HasColumnName("condominium_document_id");
        b.Property(x => x.CondominiumId).HasColumnName("condominium_id"); b.Property(x => x.ChunkIndex).HasColumnName("chunk_index");
        b.Property(x => x.Content).HasColumnName("content"); b.Property(x => x.Embedding).HasColumnName("embedding");
        b.Property(x => x.PageNumber).HasColumnName("page_number"); b.Property(x => x.SectionTitle).HasColumnName("section_title").HasMaxLength(300);
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.HasIndex(x => new { x.CondominiumId, x.CondominiumDocumentId });
        b.HasIndex(x => new { x.CondominiumDocumentId, x.ChunkIndex }).IsUnique();
        b.HasOne<CondominiumDocument>().WithMany().HasForeignKey(x => x.CondominiumDocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Cascade);
    }
}
public sealed class CondominiumAssistantConversationConfiguration : IEntityTypeConfiguration<CondominiumAssistantConversation>
{
    public void Configure(EntityTypeBuilder<CondominiumAssistantConversation> b)
    {
        b.ToTable("condominium_assistant_conversations"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.CondominiumId).HasColumnName("condominium_id");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id"); b.Property(x => x.RequestId).HasColumnName("request_id");
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200); b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.CondominiumId, x.CreatedByUserId, x.UpdatedAt });
        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<CondoLink.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<CondoLink.Domain.Entities.Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.SetNull);
    }
}
public sealed class CondominiumAssistantMessageConfiguration : IEntityTypeConfiguration<CondominiumAssistantMessage>
{
    public void Configure(EntityTypeBuilder<CondominiumAssistantMessage> b)
    {
        b.ToTable("condominium_assistant_messages"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.ConversationId).HasColumnName("conversation_id");
        b.Property(x => x.Role).HasColumnName("role").HasConversion<int>(); b.Property(x => x.Content).HasColumnName("content");
        b.Property(x => x.SourcesJson).HasColumnName("sources_json"); b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.HasIndex(x => new { x.ConversationId, x.CreatedAt });
        b.HasOne<CondominiumAssistantConversation>().WithMany().HasForeignKey(x => x.ConversationId).OnDelete(DeleteBehavior.Cascade);
    }
}
