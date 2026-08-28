using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ManagementCompanyRequestCategoryResponsibleConfiguration : IEntityTypeConfiguration<ManagementCompanyRequestCategoryResponsible>
{
    public void Configure(EntityTypeBuilder<ManagementCompanyRequestCategoryResponsible> builder)
    {
        builder.ToTable("management_company_request_category_responsibles");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ManagementCompanyRequestCategoryId).HasColumnName("category_id");
        builder.Property(x => x.ManagementCompanyEmployeeId).HasColumnName("access_id");
        builder.Property(x => x.AssignedAt).HasColumnName("assigned_at");
        builder.HasOne(x => x.Category).WithMany(x => x.Responsibles)
            .HasForeignKey(x => x.ManagementCompanyRequestCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Access).WithMany(x => x.CategoryResponsibilities)
            .HasForeignKey(x => x.ManagementCompanyEmployeeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.ManagementCompanyRequestCategoryId, x.ManagementCompanyEmployeeId })
            .IsUnique().HasDatabaseName("ux_mc_category_responsibles_category_access");
    }
}
