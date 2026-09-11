using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ManagementCompanyEmployeeModulePermissionConfiguration
    : IEntityTypeConfiguration<ManagementCompanyEmployeeModulePermission>
{
    public const string UniqueEmployeeModuleIndex = "ux_mc_employee_module_permissions_employee_module";

    public void Configure(EntityTypeBuilder<ManagementCompanyEmployeeModulePermission> builder)
    {
        builder.ToTable("management_company_employee_module_permissions");
        builder.HasKey(permission => permission.Id);

        builder.Property(permission => permission.Id).HasColumnName("id");
        builder.Property(permission => permission.ManagementCompanyEmployeeId).HasColumnName("management_company_employee_id").IsRequired();
        builder.Property(permission => permission.Module).HasColumnName("module").HasConversion<int>().IsRequired();
        builder.Property(permission => permission.IsAllowed).HasColumnName("is_allowed").IsRequired();
        builder.Property(permission => permission.GrantedByUserId).HasColumnName("granted_by_user_id").IsRequired();
        builder.Property(permission => permission.GrantedAt).HasColumnName("granted_at").IsRequired();
        builder.Property(permission => permission.RevokedAt).HasColumnName("revoked_at");

        builder.HasOne<ManagementCompanyEmployee>().WithMany()
            .HasForeignKey(permission => permission.ManagementCompanyEmployeeId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(permission => new { permission.ManagementCompanyEmployeeId, permission.Module })
            .HasDatabaseName(UniqueEmployeeModuleIndex)
            .IsUnique();
    }
}
