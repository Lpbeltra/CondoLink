using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class EmployeeDocumentBatchConfiguration : IEntityTypeConfiguration<EmployeeDocumentBatch>
{
    public void Configure(EntityTypeBuilder<EmployeeDocumentBatch> b)
    {
        b.ToTable("employee_document_batches");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CondominiumId).HasColumnName("condominium_id").IsRequired();
        b.Property(x => x.DocumentType).HasColumnName("document_type").HasConversion<int>().IsRequired();
        b.Property(x => x.CompetenceMonth).HasColumnName("competence_month").IsRequired();
        b.Property(x => x.CompetenceYear).HasColumnName("competence_year").IsRequired();
        b.Property(x => x.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        b.Property(x => x.ConfirmedByUserId).HasColumnName("confirmed_by_user_id");
        b.Property(x => x.FailureReason).HasColumnName("failure_reason").HasMaxLength(500);
        b.Property(x => x.PendingUploadsJson).HasColumnName("pending_uploads_json");

        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);

        b.HasIndex(x => new { x.CondominiumId, x.CreatedAt }).HasDatabaseName("ix_employee_document_batches_condominium_id_created_at");
        b.HasIndex(x => new { x.CondominiumId, x.DocumentType, x.CompetenceYear, x.CompetenceMonth })
            .HasDatabaseName("ix_employee_document_batches_condominium_id_type_competence");
    }
}
