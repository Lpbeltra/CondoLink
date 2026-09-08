using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestInternalNoteConfiguration : IEntityTypeConfiguration<RequestInternalNote>
{
    public void Configure(EntityTypeBuilder<RequestInternalNote> builder)
    {
        builder.ToTable("request_internal_notes");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RequestId).HasColumnName("request_id");
        builder.Property(x => x.AuthorUserId).HasColumnName("author_user_id");
        builder.Property(x => x.Content).HasColumnName("content")
            .HasMaxLength(RequestInternalNote.MaximumContentLength).IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.RequestId, x.CreatedAt });
    }
}
