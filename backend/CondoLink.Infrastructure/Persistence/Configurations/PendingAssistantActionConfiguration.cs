using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class PendingAssistantActionConfiguration : IEntityTypeConfiguration<PendingAssistantAction>
{
    public void Configure(EntityTypeBuilder<PendingAssistantAction> b)
    {
        b.ToTable("pending_assistant_actions"); b.HasKey(x => x.Id);
        b.Property(x => x.ActionType).HasConversion<int>().HasColumnName("action_type");
        b.Property(x => x.Status).HasConversion<int>().HasColumnName("status");
        b.Property(x => x.ActorUserId).HasColumnName("actor_user_id");
        b.Property(x => x.CondominiumId).HasColumnName("condominium_id");
        b.Property(x => x.Channel).HasConversion<int>().HasColumnName("channel");
        b.Property(x => x.ExternalContextId).HasColumnName("external_context_id").HasMaxLength(128);
        b.Property(x => x.PayloadJson).HasColumnName("payload_json").HasColumnType("jsonb").IsRequired();
        b.Property(x => x.IdempotencyKey).HasColumnName("idempotency_key").HasMaxLength(200).IsRequired();
        b.Property(x => x.CreatedAt).HasColumnName("created_at"); b.Property(x => x.ExpiresAt).HasColumnName("expires_at");
        b.Property(x => x.ExecutedAt).HasColumnName("executed_at"); b.Property(x => x.ResultJson).HasColumnName("result_json").HasColumnType("jsonb");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => x.IdempotencyKey).IsUnique().HasDatabaseName("ux_pending_assistant_actions_idempotency_key");
        b.HasIndex(x => new { x.ActorUserId, x.CondominiumId, x.Channel, x.ExternalContextId, x.ActionType, x.Status }).HasDatabaseName("ix_pending_assistant_actions_active_context");
        b.HasIndex(x => new { x.Status, x.ExpiresAt }).HasDatabaseName("ix_pending_assistant_actions_expiration");
        b.HasOne<CondoLink.Infrastructure.Identity.ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Restrict);
    }
}
