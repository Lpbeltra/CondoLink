using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumMembershipRoleConfiguration
    : IEntityTypeConfiguration<CondominiumMembershipRole>
{
    public const string UniqueMembershipRoleIndex =
        "ux_condominium_membership_roles_membership_role";

    public void Configure(EntityTypeBuilder<CondominiumMembershipRole> builder)
    {
        builder.ToTable("condominium_membership_roles");

        builder.HasKey(membershipRole => membershipRole.Id);

        builder.Property(membershipRole => membershipRole.Id)
            .HasColumnName("id");

        builder.Property(membershipRole => membershipRole.CondominiumMembershipId)
            .HasColumnName("condominium_membership_id")
            .IsRequired();

        builder.Property(membershipRole => membershipRole.Role)
            .HasColumnName("role")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(membershipRole => membershipRole.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(membershipRole => membershipRole.GrantedAt)
            .HasColumnName("granted_at")
            .IsRequired();

        builder.Property(membershipRole => membershipRole.RevokedAt)
            .HasColumnName("revoked_at");

        builder.HasOne<CondominiumMembership>()
            .WithMany()
            .HasForeignKey(membershipRole => membershipRole.CondominiumMembershipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membershipRole => new
            {
                membershipRole.CondominiumMembershipId,
                membershipRole.Role
            })
            .HasDatabaseName(UniqueMembershipRoleIndex)
            .IsUnique();
    }
}
