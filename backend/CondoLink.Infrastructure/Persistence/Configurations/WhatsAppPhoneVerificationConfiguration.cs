using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class WhatsAppPhoneVerificationConfiguration
    : IEntityTypeConfiguration<WhatsAppPhoneVerification>
{
    public void Configure(EntityTypeBuilder<WhatsAppPhoneVerification> b)
    {
        b.ToTable("whatsapp_phone_verifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        b.Property(x => x.NormalizedPhoneNumber)
            .HasColumnName("normalized_phone_number").HasMaxLength(14).IsRequired();
        b.Property(x => x.CodeHash).HasColumnName("code_hash").IsRequired();
        b.Property(x => x.CodeSalt).HasColumnName("code_salt").IsRequired();
        b.Property(x => x.AttemptCount).HasColumnName("attempt_count").IsRequired();
        b.Property(x => x.MaximumAttempts).HasColumnName("maximum_attempts").IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        b.Property(x => x.ExpiresAt).HasColumnName("expires_at").IsRequired();
        b.Property(x => x.ConfirmedAt).HasColumnName("confirmed_at");
        b.Property(x => x.InvalidatedAt).HasColumnName("invalidated_at");
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });
        b.HasIndex(x => new
            {
                x.NormalizedPhoneNumber,
                x.ConfirmedAt,
                x.InvalidatedAt,
                x.ExpiresAt
            })
            .HasDatabaseName(
                "ix_whatsapp_phone_verifications_phone_state_expiration");
        b.HasIndex(x => x.UserId).IsUnique()
            .HasDatabaseName("ux_whatsapp_phone_verifications_active_user")
            .HasFilter("\"confirmed_at\" IS NULL AND \"invalidated_at\" IS NULL");
    }
}
