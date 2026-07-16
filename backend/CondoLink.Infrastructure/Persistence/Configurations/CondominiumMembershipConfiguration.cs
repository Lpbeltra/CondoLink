using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class CondominiumMembershipConfiguration
    : IEntityTypeConfiguration<CondominiumMembership>
{
    public const string UniqueUserCondominiumIndex =
        "ux_condominium_memberships_user_condominium";

    public void Configure(EntityTypeBuilder<CondominiumMembership> builder)
    {
        builder.ToTable("condominium_memberships");

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Id)
            .HasColumnName("id");

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(membership => membership.CondominiumId)
            .HasColumnName("condominium_id")
            .IsRequired();

        builder.Property(membership => membership.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(membership => membership.JoinedAt)
            .HasColumnName("joined_at")
            .IsRequired();

        builder.Property(membership => membership.EndedAt)
            .HasColumnName("ended_at");

        builder.Property(membership => membership.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Condominium>()
            .WithMany()
            .HasForeignKey(membership => membership.CondominiumId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new
            {
                membership.UserId,
                membership.CondominiumId
            })
            .HasDatabaseName(UniqueUserCondominiumIndex)
            .IsUnique();
    }
}
