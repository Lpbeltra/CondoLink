using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class EmployeeDocumentDeliveryConfiguration : IEntityTypeConfiguration<EmployeeDocumentDelivery>
{
    public void Configure(EntityTypeBuilder<EmployeeDocumentDelivery> b)
    {
        b.ToTable("employee_document_deliveries");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.EmployeeDocumentId).HasColumnName("employee_document_id").IsRequired();
        b.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        b.Property(x => x.CondominiumId).HasColumnName("condominium_id").IsRequired();
        b.Property(x => x.BatchId).HasColumnName("batch_id").IsRequired();
        b.Property(x => x.Channel).HasColumnName("channel").HasConversion<int>().IsRequired();
        b.Property(x => x.OutboundMessageId).HasColumnName("outbound_message_id").IsRequired();
        b.Property(x => x.QueuedByUserId).HasColumnName("queued_by_user_id").IsRequired();
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        b.Property(x => x.QueuedAt).HasColumnName("queued_at").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();

        b.HasOne<EmployeeDocument>().WithMany().HasForeignKey(x => x.EmployeeDocumentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<EmployeeDocumentBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<WhatsAppOutboundMessage>().WithMany().HasForeignKey(x => x.OutboundMessageId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.QueuedByUserId).OnDelete(DeleteBehavior.Restrict);

        // The idempotency guard: at most one delivery row per document per channel.
        b.HasIndex(x => new { x.EmployeeDocumentId, x.Channel })
            .HasDatabaseName("ux_employee_document_deliveries_document_id_channel").IsUnique();
        b.HasIndex(x => x.BatchId).HasDatabaseName("ix_employee_document_deliveries_batch_id");
    }
}
