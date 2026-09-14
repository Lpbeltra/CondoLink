using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ManagementCompanyEmployeeModuleGrantConfiguration
    : IEntityTypeConfiguration<ManagementCompanyEmployeeModuleGrant>
{
    public void Configure(EntityTypeBuilder<ManagementCompanyEmployeeModuleGrant> builder)
    {
        builder.ToTable("management_company_employee_module_grants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.ManagementCompanyEmployeeId).HasColumnName("management_company_employee_id").IsRequired();
        builder.Property(x => x.Module).HasColumnName("module").HasConversion<int>().IsRequired();
        builder.Property(x => x.IsAllowed).HasColumnName("is_allowed").IsRequired();
        builder.Property(x => x.GrantedByUserId).HasColumnName("granted_by_user_id").IsRequired();
        builder.Property(x => x.GrantedAt).HasColumnName("granted_at").IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.HasOne<ManagementCompanyEmployee>().WithMany().HasForeignKey(x => x.ManagementCompanyEmployeeId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.ManagementCompanyEmployeeId, x.Module })
            .HasDatabaseName("ux_mc_employee_module_grants_employee_module").IsUnique();
    }
}
