using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class UnitMembershipConfiguration
    : IEntityTypeConfiguration<UnitMembership>
{
    public const string UniqueUserUnitRelationshipIndex =
        "ux_unit_memberships_user_unit_relationship";

    public void Configure(EntityTypeBuilder<UnitMembership> builder)
    {
        builder.ToTable("unit_memberships");

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Id)
            .HasColumnName("id");

        builder.Property(membership => membership.UserId)
            .HasColumnName("user_id")
            .IsRequired();

        builder.Property(membership => membership.UnitId)
            .HasColumnName("unit_id")
            .IsRequired();

        builder.Property(membership => membership.RelationshipType)
            .HasColumnName("relationship_type")
            .HasConversion<int>()
            .IsRequired();

        builder.Property(membership => membership.IsResident)
            .HasColumnName("is_resident")
            .IsRequired();

        builder.Property(membership => membership.IsPrimaryResidence)
            .HasColumnName("is_primary_residence")
            .IsRequired();

        builder.Property(membership => membership.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.Property(membership => membership.StartedAt)
            .HasColumnName("started_at")
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

        builder.HasOne<Unit>()
            .WithMany()
            .HasForeignKey(membership => membership.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(membership => new
            {
                membership.UserId,
                membership.UnitId,
                membership.RelationshipType
            })
            .HasDatabaseName(UniqueUserUnitRelationshipIndex)
            .IsUnique();
    }
}
