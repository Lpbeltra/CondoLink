using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestConfiguration : IEntityTypeConfiguration<DomainRequest>
{
    public void Configure(EntityTypeBuilder<DomainRequest> builder)
    {
        builder.ToTable("requests");
        builder.HasKey(request => request.Id);

        builder.Property(request => request.Id).HasColumnName("id");
        builder.Property(request => request.CondominiumId).HasColumnName("condominium_id").IsRequired();
        builder.Property(request => request.AuthorUserId).HasColumnName("author_user_id").IsRequired();
        builder.Property(request => request.TargetUnitId).HasColumnName("target_unit_id");
        builder.Property(request => request.CategoryId).HasColumnName("category_id").IsRequired();
        builder.Property(request => request.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        builder.Property(request => request.Description).HasColumnName("description").HasMaxLength(4000).IsRequired();
        builder.Property(request => request.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(request => request.Priority).HasColumnName("priority").HasConversion<int>().IsRequired();
        builder.Property(request => request.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(request => request.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.Property(request => request.ResolvedAt).HasColumnName("resolved_at");

        builder.HasOne<Condominium>().WithMany().HasForeignKey(request => request.CondominiumId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(request => request.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Unit>().WithMany().HasForeignKey(request => request.TargetUnitId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Category>().WithMany().HasForeignKey(request => request.CategoryId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(request => request.CondominiumId);
        builder.HasIndex(request => request.AuthorUserId);
        builder.HasIndex(request => request.CategoryId);
        builder.HasIndex(request => request.TargetUnitId);
        builder.HasIndex(request => request.Status);
        builder.HasIndex(request => request.CreatedAt);
    }
}
