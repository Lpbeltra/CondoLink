using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ExternalCondominiumMappingConfiguration : IEntityTypeConfiguration<ExternalCondominiumMapping>
{
    public void Configure(EntityTypeBuilder<ExternalCondominiumMapping> builder)
    {
        builder.ToTable("external_condominium_mappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AdministratorIntegrationId).HasColumnName("administrator_integration_id").IsRequired();
        builder.Property(x => x.CondominiumId).HasColumnName("condominium_id").IsRequired();
        builder.Property(x => x.ExternalCondominiumId).HasColumnName("external_condominium_id").HasMaxLength(100).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasOne(x => x.AdministratorIntegration).WithMany().HasForeignKey(x => x.AdministratorIntegrationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Condominium).WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.AdministratorIntegrationId, x.ExternalCondominiumId }).IsUnique().HasDatabaseName("ux_external_condominium_mappings_integration_external_id");
        builder.HasIndex(x => new { x.AdministratorIntegrationId, x.CondominiumId }).IsUnique().HasDatabaseName("ux_external_condominium_mappings_integration_condominium");
    }
}
