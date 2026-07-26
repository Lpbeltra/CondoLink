using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ManagementCompanyRequestCategoryConfiguration
    : IEntityTypeConfiguration<ManagementCompanyRequestCategory>
{
    public const string UniqueCompanyNormalizedNameIndex =
        "ux_management_company_request_categories_company_normalized_name";

    public void Configure(
        EntityTypeBuilder<ManagementCompanyRequestCategory> builder)
    {
        builder.ToTable("management_company_request_categories");
        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id).HasColumnName("id");
        builder.Property(category => category.ManagementCompanyId)
            .HasColumnName("management_company_id").IsRequired();
        builder.Property(category => category.Name)
            .HasColumnName("name").HasMaxLength(150).IsRequired();
        builder.Property(category => category.NormalizedName)
            .HasColumnName("normalized_name").HasMaxLength(150).IsRequired();
        builder.Property(category => category.Description)
            .HasColumnName("description").HasMaxLength(500);
        builder.Property(category => category.FormType)
            .HasColumnName("form_type").HasConversion<string>()
            .HasMaxLength(50).IsRequired();
        builder.Property(category => category.IsActive)
            .HasColumnName("is_active").IsRequired();
        builder.Property(category => category.CreatedAt)
            .HasColumnName("created_at").IsRequired();
        builder.Property(category => category.UpdatedAt)
            .HasColumnName("updated_at").IsRequired();

        builder.HasOne(category => category.ManagementCompany)
            .WithMany(company => company.RequestCategories)
            .HasForeignKey(category => category.ManagementCompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(category => new
            {
                category.ManagementCompanyId,
                category.NormalizedName
            })
            .HasDatabaseName(UniqueCompanyNormalizedNameIndex)
            .IsUnique();
    }
}
