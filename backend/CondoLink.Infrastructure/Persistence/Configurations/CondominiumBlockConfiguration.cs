using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumBlockConfiguration : IEntityTypeConfiguration<CondominiumBlock>
{
    public const string UniqueIndex = "ux_condominium_blocks_condominium_identifier";
    public void Configure(EntityTypeBuilder<CondominiumBlock> builder)
    {
        builder.ToTable("condominium_blocks"); builder.HasKey(block => block.Id);
        builder.Property(block => block.Id).HasColumnName("id");
        builder.Property(block => block.CondominiumId).HasColumnName("condominium_id").IsRequired();
        builder.Property(block => block.Identifier).HasColumnName("identifier").HasMaxLength(50).IsRequired();
        builder.Property(block => block.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(block => block.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasOne<Condominium>().WithMany().HasForeignKey(block => block.CondominiumId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(block => new { block.CondominiumId, block.Identifier }).HasDatabaseName(UniqueIndex).IsUnique();
    }
}
