using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumManagementCompanyLinkConfiguration : IEntityTypeConfiguration<CondominiumManagementCompanyLink>
{
    public void Configure(EntityTypeBuilder<CondominiumManagementCompanyLink> builder)
    {
        builder.ToTable("condominium_management_company_links");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CondominiumId).HasColumnName("condominium_id");
        builder.Property(x => x.ManagementCompanyId).HasColumnName("management_company_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.LinkedAt).HasColumnName("linked_at");
        builder.Property(x => x.UnlinkedAt).HasColumnName("unlinked_at");
        builder.HasOne(x => x.Condominium).WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ManagementCompany).WithMany().HasForeignKey(x => x.ManagementCompanyId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => x.CondominiumId).IsUnique().HasFilter("\"is_active\" = TRUE")
            .HasDatabaseName("ux_condominium_management_company_links_active_condominium");
        builder.HasIndex(x => new { x.ManagementCompanyId, x.CondominiumId });
    }
}
