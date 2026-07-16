using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumMembershipRoleConfiguration
    : IEntityTypeConfiguration<CondominiumMembershipRole>
{
    public void Configure(
        EntityTypeBuilder<CondominiumMembershipRole> builder)
    {
        builder.ToTable("CondominiumMembershipRoles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Id)
            .ValueGeneratedNever();

        builder.Property(role => role.CondominiumMembershipId)
            .IsRequired();

        builder.Property(role => role.Role)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(role => role.IsActive)
            .IsRequired();

        builder.Property(role => role.GrantedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(role => role.RevokedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.HasIndex(role => new
            {
                role.CondominiumMembershipId,
                role.Role
            })
            .IsUnique();
    }
}
