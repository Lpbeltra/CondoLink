using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumConfiguration
    : IEntityTypeConfiguration<Condominium>
{
    public void Configure(EntityTypeBuilder<Condominium> builder)
    {
        builder.ToTable("Condominiums");

        builder.HasKey(condominium => condominium.Id);

        builder.Property(condominium => condominium.Id)
            .ValueGeneratedNever();

        builder.Property(condominium => condominium.Name)
            .HasMaxLength(150)
            .IsRequired();

        builder.Property(condominium => condominium.Email)
            .HasMaxLength(254)
            .IsRequired();

        builder.Property(condominium => condominium.PhoneNumber)
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(condominium => condominium.IsActive)
            .IsRequired();

        builder.Property(condominium => condominium.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(condominium => condominium.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.HasMany(condominium => condominium.Units)
            .WithOne(unit => unit.Condominium)
            .HasForeignKey(unit => unit.CondominiumId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(condominium => condominium.Units)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
