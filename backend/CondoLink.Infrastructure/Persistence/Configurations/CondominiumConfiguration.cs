using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumConfiguration : IEntityTypeConfiguration<Condominium>
{
    public void Configure(EntityTypeBuilder<Condominium> builder)
    {
        builder.ToTable("condominiums");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Id).HasColumnName("id");
        builder.Property(item => item.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(item => item.Email).HasColumnName("email").HasMaxLength(254);
        builder.Property(item => item.Cnpj).HasColumnName("cnpj").HasMaxLength(14);
        builder.Property(item => item.Address).HasColumnName("address").HasMaxLength(200);
        builder.Property(item => item.City).HasColumnName("city").HasMaxLength(100);
        builder.Property(item => item.State).HasColumnName("state").HasMaxLength(2);
        builder.Property(item => item.HasDoorman).HasColumnName("has_doorman").IsRequired();
        builder.Property(item => item.IsRemoteDoorman).HasColumnName("is_remote_doorman").IsRequired();
        builder.Property(item => item.DoormanContact).HasColumnName("doorman_contact").HasMaxLength(100);
        builder.Property(item => item.ManagementCompanyId).HasColumnName("management_company_id");
        builder.HasIndex(item => item.ManagementCompanyId)
            .HasDatabaseName("ix_condominiums_management_company_id");
        builder.HasIndex(item => item.Cnpj).HasDatabaseName("ux_condominiums_cnpj")
            .IsUnique().HasFilter("\"cnpj\" IS NOT NULL");
        builder.HasOne(item => item.ManagementCompany).WithMany(company => company.Condominiums)
            .HasForeignKey(item => item.ManagementCompanyId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(item => item.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(item => item.WhatsAppUpdatesEnabled)
            .HasColumnName("whatsapp_updates_enabled").HasDefaultValue(false).IsRequired();
        builder.Property(item => item.WhatsAppDisplayName)
            .HasColumnName("whatsapp_display_name").HasMaxLength(200);
        builder.Property(item => item.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(item => item.UpdatedAt).HasColumnName("updated_at").IsRequired();
    }
}
