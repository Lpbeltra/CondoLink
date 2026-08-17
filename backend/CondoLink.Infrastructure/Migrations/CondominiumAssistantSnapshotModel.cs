using System;
using CondoLink.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Infrastructure.Migrations;

internal static class CondominiumAssistantSnapshotModel
{
    internal static void Build(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity("CondoLink.Domain.Entities.CondominiumDocument", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("CondominiumId").HasColumnName("condominium_id").HasColumnType("uuid");
            b.Property<string>("Name").IsRequired().HasColumnName("name").HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<CondominiumDocumentType>("DocumentType").HasConversion<int>().HasColumnName("document_type").HasColumnType("integer");
            b.Property<string>("OriginalFileName").IsRequired().HasColumnName("original_file_name").HasMaxLength(255).HasColumnType("character varying(255)");
            b.Property<string>("StorageKey").IsRequired().HasColumnName("storage_key").HasMaxLength(500).HasColumnType("character varying(500)");
            b.Property<string>("MimeType").IsRequired().HasColumnName("mime_type").HasMaxLength(100).HasColumnType("character varying(100)");
            b.Property<int>("Version").HasColumnName("version").HasColumnType("integer");
            b.Property<DateOnly?>("DocumentDate").HasColumnName("document_date").HasColumnType("date");
            b.Property<bool>("IsActive").HasColumnName("is_active").HasColumnType("boolean");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("timestamp with time zone");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("timestamp with time zone");
            b.Property<Guid>("UploadedByUserId").HasColumnName("uploaded_by_user_id").HasColumnType("uuid");
            b.Property<CondominiumDocumentProcessingStatus>("ProcessingStatus").HasConversion<int>().HasColumnName("processing_status").HasColumnType("integer");
            b.Property<string>("ProcessingError").HasColumnName("processing_error").HasMaxLength(500).HasColumnType("character varying(500)");
            b.HasKey("Id");
            b.HasIndex("CondominiumId", "IsActive", "ProcessingStatus");
            b.HasIndex("UploadedByUserId");
            b.ToTable("condominium_documents", (string)null);
        });

        modelBuilder.Entity("CondoLink.Domain.Entities.CondominiumDocumentChunk", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("CondominiumDocumentId").HasColumnName("condominium_document_id").HasColumnType("uuid");
            b.Property<Guid>("CondominiumId").HasColumnName("condominium_id").HasColumnType("uuid");
            b.Property<int>("ChunkIndex").HasColumnName("chunk_index").HasColumnType("integer");
            b.Property<string>("Content").IsRequired().HasColumnName("content").HasColumnType("text");
            b.Property<string>("Embedding").IsRequired().HasColumnName("embedding").HasColumnType("text");
            b.Property<int?>("PageNumber").HasColumnName("page_number").HasColumnType("integer");
            b.Property<string>("SectionTitle").HasColumnName("section_title").HasMaxLength(300).HasColumnType("character varying(300)");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("CondominiumId", "CondominiumDocumentId");
            b.HasIndex("CondominiumDocumentId", "ChunkIndex").IsUnique();
            b.ToTable("condominium_document_chunks", (string)null);
        });

        modelBuilder.Entity("CondoLink.Domain.Entities.CondominiumAssistantConversation", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("CondominiumId").HasColumnName("condominium_id").HasColumnType("uuid");
            b.Property<Guid>("CreatedByUserId").HasColumnName("created_by_user_id").HasColumnType("uuid");
            b.Property<Guid?>("RequestId").HasColumnName("request_id").HasColumnType("uuid");
            b.Property<string>("Title").IsRequired().HasColumnName("title").HasMaxLength(200).HasColumnType("character varying(200)");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("timestamp with time zone");
            b.Property<DateTime>("UpdatedAt").HasColumnName("updated_at").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("CondominiumId", "CreatedByUserId", "UpdatedAt");
            b.HasIndex("RequestId");
            b.ToTable("condominium_assistant_conversations", (string)null);
        });

        modelBuilder.Entity("CondoLink.Domain.Entities.CondominiumAssistantMessage", b =>
        {
            b.Property<Guid>("Id").HasColumnName("id").HasColumnType("uuid");
            b.Property<Guid>("ConversationId").HasColumnName("conversation_id").HasColumnType("uuid");
            b.Property<CondominiumAssistantRole>("Role").HasConversion<int>().HasColumnName("role").HasColumnType("integer");
            b.Property<string>("Content").IsRequired().HasColumnName("content").HasColumnType("text");
            b.Property<string>("SourcesJson").HasColumnName("sources_json").HasColumnType("text");
            b.Property<DateTime>("CreatedAt").HasColumnName("created_at").HasColumnType("timestamp with time zone");
            b.HasKey("Id");
            b.HasIndex("ConversationId", "CreatedAt");
            b.ToTable("condominium_assistant_messages", (string)null);
        });

        modelBuilder.Entity("CondoLink.Domain.Entities.CondominiumDocument", b =>
        {
            b.HasOne("CondoLink.Domain.Entities.Condominium", null)
                .WithMany()
                .HasForeignKey("CondominiumId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("CondoLink.Infrastructure.Identity.ApplicationUser", null)
                .WithMany()
                .HasForeignKey("UploadedByUserId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();
        });

        modelBuilder.Entity("CondoLink.Domain.Entities.CondominiumDocumentChunk", b =>
        {
            b.HasOne("CondoLink.Domain.Entities.CondominiumDocument", null)
                .WithMany()
                .HasForeignKey("CondominiumDocumentId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("CondoLink.Domain.Entities.Condominium", null)
                .WithMany()
                .HasForeignKey("CondominiumId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });

        modelBuilder.Entity("CondoLink.Domain.Entities.CondominiumAssistantConversation", b =>
        {
            b.HasOne("CondoLink.Domain.Entities.Condominium", null)
                .WithMany()
                .HasForeignKey("CondominiumId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();

            b.HasOne("CondoLink.Infrastructure.Identity.ApplicationUser", null)
                .WithMany()
                .HasForeignKey("CreatedByUserId")
                .OnDelete(DeleteBehavior.Restrict)
                .IsRequired();

            b.HasOne("CondoLink.Domain.Entities.Request", null)
                .WithMany()
                .HasForeignKey("RequestId")
                .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity("CondoLink.Domain.Entities.CondominiumAssistantMessage", b =>
        {
            b.HasOne("CondoLink.Domain.Entities.CondominiumAssistantConversation", null)
                .WithMany()
                .HasForeignKey("ConversationId")
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired();
        });
    }
}
