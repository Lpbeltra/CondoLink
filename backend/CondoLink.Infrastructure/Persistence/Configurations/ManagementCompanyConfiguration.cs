using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ManagementCompanyConfiguration : IEntityTypeConfiguration<ManagementCompany>
{
    public const string UniqueCnpjIndex = "ux_management_companies_cnpj";
    public const string UniqueEmailIndex = "ux_management_companies_email";

    public void Configure(EntityTypeBuilder<ManagementCompany> builder)
    {
        builder.ToTable("management_companies");
        builder.HasKey(company => company.Id);
        builder.Property(company => company.Id).HasColumnName("id");
        builder.Property(company => company.Name).HasColumnName("name").HasMaxLength(150).IsRequired();
        // Historical document values are preserved in this column. New writes
        // are still validated as exactly 14 digits by the API.
        builder.Property(company => company.Cnpj).HasColumnName("cnpj").HasMaxLength(20);
        builder.Property(company => company.Address).HasColumnName("address").HasMaxLength(200);
        builder.Property(company => company.City).HasColumnName("city").HasMaxLength(100);
        builder.Property(company => company.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(company => company.Email).HasColumnName("email").HasMaxLength(254);
        builder.Property(company => company.PhoneNumber).HasColumnName("phone_number").HasMaxLength(30);
        builder.Property(company => company.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(company => company.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(company => company.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(company => company.Cnpj).HasDatabaseName(UniqueCnpjIndex)
            .IsUnique().HasFilter("\"cnpj\" IS NOT NULL");
        builder.HasIndex(company => company.Email).HasDatabaseName(UniqueEmailIndex)
            .IsUnique().HasFilter("\"email\" IS NOT NULL");
    }
}
