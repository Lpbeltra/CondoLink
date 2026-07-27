using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ManagementCompanyEmployeeConfiguration
    : IEntityTypeConfiguration<ManagementCompanyEmployee>
{
    public const string UniqueUserIndex =
        "ux_management_company_employees_user_id";

    public void Configure(
        EntityTypeBuilder<ManagementCompanyEmployee> builder)
    {
        builder.ToTable("management_company_employees");
        builder.HasKey(employee => employee.Id);

        builder.Property(employee => employee.Id).HasColumnName("id");
        builder.Property(employee => employee.ManagementCompanyId)
            .HasColumnName("management_company_id").IsRequired();
        builder.Property(employee => employee.UserId)
            .HasColumnName("user_id").IsRequired();
        builder.Property(employee => employee.JobTitle)
            .HasColumnName("job_title").HasMaxLength(100).IsRequired();
        builder.Property(employee => employee.IsActive)
            .HasColumnName("is_active").IsRequired();
        builder.Property(employee => employee.CreatedAt)
            .HasColumnName("created_at").IsRequired();
        builder.Property(employee => employee.UpdatedAt)
            .HasColumnName("updated_at").IsRequired();

        builder.HasOne(employee => employee.ManagementCompany)
            .WithMany(company => company.Employees)
            .HasForeignKey(employee => employee.ManagementCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithOne()
            .HasForeignKey<ManagementCompanyEmployee>(
                employee => employee.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(employee => employee.ManagementCompanyId)
            .HasDatabaseName(
                "ix_management_company_employees_management_company_id");
        builder.HasIndex(employee => employee.UserId)
            .HasDatabaseName(UniqueUserIndex)
            .IsUnique();
    }
}
