using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Identity;

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> b)
    {
        b.ToTable("refresh_sessions"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id"); b.Property(x => x.UserId).HasColumnName("user_id");
        b.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64);
        b.Property(x => x.SecurityStamp).HasColumnName("security_stamp").HasMaxLength(256);
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.LastUsedAt).HasColumnName("last_used_at"); b.Property(x => x.RevokedAt).HasColumnName("revoked_at");
        b.Property(x => x.ReplacedBySessionId).HasColumnName("replaced_by_session_id");
        b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => new { x.UserId, x.ExpiresAt });
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
