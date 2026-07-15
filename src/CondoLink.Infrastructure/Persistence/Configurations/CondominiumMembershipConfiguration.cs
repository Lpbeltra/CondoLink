using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumMembershipConfiguration
    : IEntityTypeConfiguration<CondominiumMembership>
{
    public void Configure(EntityTypeBuilder<CondominiumMembership> builder)
    {
        builder.ToTable("CondominiumMemberships");

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Id)
            .ValueGeneratedNever();

        builder.Property(membership => membership.UserId)
            .IsRequired();

        builder.Property(membership => membership.CondominiumId)
            .IsRequired();

        builder.Property(membership => membership.IsActive)
            .IsRequired();

        builder.Property(membership => membership.JoinedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(membership => membership.EndedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(membership => membership.CreatedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(membership => membership.UpdatedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.HasIndex(membership => new
            {
                membership.UserId,
                membership.CondominiumId
            })
            .IsUnique();

        builder.HasMany(membership => membership.Roles)
            .WithOne()
            .HasForeignKey(role => role.CondominiumMembershipId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Navigation(membership => membership.Roles)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
