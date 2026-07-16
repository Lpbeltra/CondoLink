using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units");

        builder.HasKey(unit => unit.Id);

        builder.Property(unit => unit.Id)
            .ValueGeneratedNever();

        builder.Property(unit => unit.CondominiumId)
            .IsRequired();

        builder.Property(unit => unit.Identifier)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(unit => unit.Block)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(unit => unit.Floor)
            .HasMaxLength(30)
            .IsRequired(false);

        builder.Property(unit => unit.Description)
            .HasMaxLength(500)
            .IsRequired(false);

        builder.Property(unit => unit.IsActive)
            .IsRequired();

        builder.Property(unit => unit.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(unit => unit.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.HasIndex(unit => new
            {
                unit.CondominiumId,
                unit.Block,
                unit.Identifier
            })
            .IsUnique()
            .AreNullsDistinct(false);
    }
}
