using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class EmployeeDocumentBatchEmployeeConfiguration : IEntityTypeConfiguration<EmployeeDocumentBatchEmployee>
{
    public void Configure(EntityTypeBuilder<EmployeeDocumentBatchEmployee> builder)
    {
        builder.ToTable("employee_document_batch_employees"); builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id"); builder.Property(x => x.BatchId).HasColumnName("batch_id").IsRequired();
        builder.Property(x => x.EmployeeId).HasColumnName("employee_id").IsRequired();
        builder.HasOne<EmployeeDocumentBatch>().WithMany().HasForeignKey(x => x.BatchId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Employee>().WithMany().HasForeignKey(x => x.EmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.BatchId, x.EmployeeId }).IsUnique().HasDatabaseName("ux_employee_document_batch_employees");
    }
}
