using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public const string UniqueWithoutBlockIndex = "ux_units_condominium_identifier_without_block";
    public const string UniqueWithBlockIndex = "ux_units_condominium_block_identifier";

    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("units");

        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.Id)
            .HasColumnName("id");

        builder.Property(unit => unit.CondominiumId)
            .HasColumnName("condominium_id")
            .IsRequired();

        builder.Property(unit => unit.Identifier)
            .HasColumnName("identifier")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(unit => unit.Block)
            .HasColumnName("block")
            .HasMaxLength(50);

        builder.Property(unit => unit.Floor)
            .HasColumnName("floor")
            .HasMaxLength(20);

        builder.Property(unit => unit.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(unit => unit.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(unit => unit.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(unit => unit.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne<Condominium>()
            .WithMany()
            .HasForeignKey(unit => unit.CondominiumId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(unit => new { unit.CondominiumId, unit.Identifier })
            .HasDatabaseName(UniqueWithoutBlockIndex)
            .HasFilter("block IS NULL")
            .IsUnique();

        builder.HasIndex(unit => new { unit.CondominiumId, unit.Block, unit.Identifier })
            .HasDatabaseName(UniqueWithBlockIndex)
            .HasFilter("block IS NOT NULL")
            .IsUnique();
    }
}
