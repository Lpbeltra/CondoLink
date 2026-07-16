using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public const string UniqueCondominiumNormalizedNameIndex =
        "ux_categories_condominium_normalized_name";

    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");

        builder.HasKey(category => category.Id);

        builder.Property(category => category.Id)
            .HasColumnName("id");

        builder.Property(category => category.CondominiumId)
            .HasColumnName("condominium_id")
            .IsRequired();

        builder.Property(category => category.Name)
            .HasColumnName("name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(category => category.NormalizedName)
            .HasColumnName("normalized_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(category => category.Description)
            .HasColumnName("description")
            .HasMaxLength(500);

        builder.Property(category => category.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(category => category.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(category => category.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        builder.HasOne<Condominium>()
            .WithMany()
            .HasForeignKey(category => category.CondominiumId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(category => new
            {
                category.CondominiumId,
                category.NormalizedName
            })
            .HasDatabaseName(UniqueCondominiumNormalizedNameIndex)
            .IsUnique();
    }
}
