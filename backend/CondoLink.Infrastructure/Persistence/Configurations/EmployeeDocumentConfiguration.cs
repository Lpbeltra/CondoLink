using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class EmployeeDocumentConfiguration : IEntityTypeConfiguration<EmployeeDocument>
{
    public void Configure(EntityTypeBuilder<EmployeeDocument> b)
    {
        b.ToTable("employee_documents");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CondominiumId).HasColumnName("condominium_id").IsRequired();
        b.Property(x => x.BatchId).HasColumnName("batch_id").IsRequired();
        b.Property(x => x.EmployeeId).HasColumnName("employee_id");
        b.Property(x => x.DocumentType).HasColumnName("document_type").HasConversion<int>().IsRequired();
        b.Property(x => x.CompetenceMonth).HasColumnName("competence_month").IsRequired();
        b.Property(x => x.CompetenceYear).HasColumnName("competence_year").IsRequired();
        b.Property(x => x.FileKey).HasColumnName("file_key").HasMaxLength(400).IsRequired();
        b.Property(x => x.OriginalFileName).HasColumnName("original_file_name").HasMaxLength(260).IsRequired();
        b.Property(x => x.PageStart).HasColumnName("page_start").IsRequired();
        b.Property(x => x.PageEnd).HasColumnName("page_end").IsRequired();
        b.Property(x => x.ContentHash).HasColumnName("content_hash").HasMaxLength(64).IsRequired();
        b.Property(x => x.IdentificationStatus).HasColumnName("identification_status").HasConversion<int>().IsRequired();
        b.Property(x => x.IdentificationConfidence).HasColumnName("identification_confidence").HasConversion<int>().IsRequired();
        b.Property(x => x.IdentificationMethod).HasColumnName("identification_method").HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");

        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<EmployeeDocumentBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => x.BatchId).HasDatabaseName("ix_employee_documents_batch_id");
        b.HasIndex(x => new { x.CondominiumId, x.EmployeeId }).HasDatabaseName("ix_employee_documents_condominium_id_employee_id");
        // Duplicate-import detection: same rendered document content for the same
        // employee/competence/type. Not unique — re-importing after a correction is
        // allowed; callers surface this as a warning, never a silent overwrite.
        b.HasIndex(x => new { x.CondominiumId, x.EmployeeId, x.DocumentType, x.CompetenceYear, x.CompetenceMonth, x.ContentHash })
            .HasDatabaseName("ix_employee_documents_duplicate_lookup");
    }
}
