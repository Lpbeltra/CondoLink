using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ManagementCompanyModuleConfiguration : IEntityTypeConfiguration<ManagementCompanyModule>
{
    public void Configure(EntityTypeBuilder<ManagementCompanyModule> builder)
    {
        builder.ToTable("management_company_modules");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ManagementCompanyId).HasColumnName("management_company_id").IsRequired();
        builder.Property(x => x.Module).HasColumnName("module").HasConversion<int>().IsRequired();
        builder.Property(x => x.IsEnabled).HasColumnName("is_enabled").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasOne<ManagementCompany>().WithMany().HasForeignKey(x => x.ManagementCompanyId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ManagementCompanyId, x.Module }).HasDatabaseName("ux_management_company_modules_company_module").IsUnique();
    }
}
