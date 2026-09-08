using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class ServiceProviderConfiguration : IEntityTypeConfiguration<ServiceProvider>
{
    public void Configure(EntityTypeBuilder<ServiceProvider> b)
    {
        b.ToTable("service_providers"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.Name).HasColumnName("name").HasMaxLength(160).IsRequired();
        b.Property(x => x.CompanyName).HasColumnName("company_name").HasMaxLength(160);
        b.Property(x => x.Specialty).HasColumnName("specialty").HasMaxLength(120).IsRequired();
        b.Property(x => x.ContactName).HasColumnName("contact_name").HasMaxLength(160);
        b.Property(x => x.Phone).HasColumnName("phone").HasMaxLength(40).IsRequired();
        b.Property(x => x.Email).HasColumnName("email").HasMaxLength(254);
        b.Property(x => x.PixKey).HasColumnName("pix_key").HasMaxLength(200);
        b.Property(x => x.PixKeyType).HasColumnName("pix_key_type");
        b.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);
        b.Property(x => x.IsActive).HasColumnName("is_active").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.HasIndex(x => x.Name);
    }
}

public sealed class ServiceProviderUserLinkConfiguration : IEntityTypeConfiguration<ServiceProviderUserLink>
{
    public void Configure(EntityTypeBuilder<ServiceProviderUserLink> b)
    {
        b.ToTable("service_provider_user_links"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.ServiceProviderId).HasColumnName("service_provider_id"); b.Property(x => x.UserId).HasColumnName("user_id");
        b.HasIndex(x => new { x.ServiceProviderId, x.UserId }).IsUnique(); b.HasIndex(x => x.UserId);
        b.HasOne<ServiceProvider>().WithMany().HasForeignKey(x => x.ServiceProviderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ServiceProviderCondominiumLinkConfiguration : IEntityTypeConfiguration<ServiceProviderCondominiumLink>
{
    public void Configure(EntityTypeBuilder<ServiceProviderCondominiumLink> b)
    {
        b.ToTable("service_provider_condominium_links"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.ServiceProviderId).HasColumnName("service_provider_id"); b.Property(x => x.CondominiumId).HasColumnName("condominium_id");
        b.HasIndex(x => new { x.ServiceProviderId, x.CondominiumId }).IsUnique(); b.HasIndex(x => x.CondominiumId);
        b.HasOne<ServiceProvider>().WithMany().HasForeignKey(x => x.ServiceProviderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Cascade);
    }
}
