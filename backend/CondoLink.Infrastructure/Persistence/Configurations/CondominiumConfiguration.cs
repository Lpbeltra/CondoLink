using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumConfiguration : IEntityTypeConfiguration<Condominium>
{
    public void Configure(EntityTypeBuilder<Condominium> builder)
    {
        builder.ToTable("condominiums");

        builder.HasKey(condominium => condominium.Id);

        builder.Property(condominium => condominium.Id)
            .HasColumnName("id");

        builder.Property(condominium => condominium.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(condominium => condominium.Email)
            .HasColumnName("email")
            .HasMaxLength(254);

        builder.Property(condominium => condominium.PhoneNumber)
            .HasColumnName("phone_number")
            .HasMaxLength(30);

        builder.Property(condominium => condominium.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(condominium => condominium.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(condominium => condominium.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();
    }
}
