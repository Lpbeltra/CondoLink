using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class UnitMembershipConfiguration
    : IEntityTypeConfiguration<UnitMembership>
{
    public void Configure(EntityTypeBuilder<UnitMembership> builder)
    {
        builder.ToTable(
            "UnitMemberships",
            table => table.HasCheckConstraint(
                "CK_UnitMemberships_PrimaryResidenceRequiresResident",
                "NOT \"IsPrimaryResidence\" OR \"IsResident\""));

        builder.HasKey(membership => membership.Id);

        builder.Property(membership => membership.Id)
            .ValueGeneratedNever();

        builder.Property(membership => membership.UserId)
            .IsRequired();

        builder.Property(membership => membership.UnitId)
            .IsRequired();

        builder.Property(membership => membership.RelationshipType)
            .HasConversion<string>()
            .HasMaxLength(30)
            .IsRequired();

        builder.Property(membership => membership.IsResident)
            .IsRequired();

        builder.Property(membership => membership.IsPrimaryResidence)
            .IsRequired();

        builder.Property(membership => membership.IsActive)
            .IsRequired();

        builder.Property(membership => membership.StartedAt)
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

        builder.HasIndex(membership => membership.UserId);
        builder.HasIndex(membership => membership.UnitId);

        builder.HasOne<User>()
            .WithMany()
            .HasForeignKey(membership => membership.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Unit>()
            .WithMany()
            .HasForeignKey(membership => membership.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
