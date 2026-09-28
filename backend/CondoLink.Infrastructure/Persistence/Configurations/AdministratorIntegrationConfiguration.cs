using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class AdministratorIntegrationConfiguration : IEntityTypeConfiguration<AdministratorIntegration>
{
    public void Configure(EntityTypeBuilder<AdministratorIntegration> builder)
    {
        builder.ToTable("administrator_integrations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.AdministratorId).HasColumnName("administrator_id").IsRequired();
        builder.Property(x => x.Provider).HasColumnName("provider").HasMaxLength(40).IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(40).IsRequired();
        builder.Property(x => x.EncryptedAppToken).HasColumnName("encrypted_app_token").IsRequired();
        builder.Property(x => x.EncryptedAccessToken).HasColumnName("encrypted_access_token").IsRequired();
        builder.Property(x => x.EncryptedSecret).HasColumnName("encrypted_secret").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(x => x.LastValidatedAt).HasColumnName("last_validated_at");
        builder.HasOne(x => x.Administrator).WithMany().HasForeignKey(x => x.AdministratorId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(x => new { x.AdministratorId, x.Provider }).IsUnique().HasDatabaseName("ux_administrator_integrations_administrator_provider");
    }
}
