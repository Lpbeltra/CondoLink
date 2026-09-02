using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class SubManagerModulePermissionConfiguration : IEntityTypeConfiguration<SubManagerModulePermission>
{
    public void Configure(EntityTypeBuilder<SubManagerModulePermission> builder)
    {
        builder.ToTable("sub_manager_module_permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.CondominiumMembershipId).HasColumnName("condominium_membership_id").IsRequired();
        builder.Property(x => x.Module).HasColumnName("module").IsRequired();
        builder.Property(x => x.IsAllowed).HasColumnName("is_allowed").IsRequired();
        builder.Property(x => x.GrantedByUserId).HasColumnName("granted_by_user_id").IsRequired();
        builder.Property(x => x.GrantedAt).HasColumnName("granted_at").IsRequired();
        builder.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        builder.HasIndex(x => new { x.CondominiumMembershipId, x.Module }).IsUnique();
        builder.HasOne<CondominiumMembership>().WithMany().HasForeignKey(x => x.CondominiumMembershipId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(x => x.GrantedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}
