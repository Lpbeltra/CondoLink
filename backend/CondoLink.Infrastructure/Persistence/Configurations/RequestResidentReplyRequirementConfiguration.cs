using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class RequestResidentReplyRequirementConfiguration
    : IEntityTypeConfiguration<RequestResidentReplyRequirement>
{
    public void Configure(EntityTypeBuilder<RequestResidentReplyRequirement> builder)
    {
        builder.ToTable("request_resident_reply_requirements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.RequestId).HasColumnName("request_id");
        builder.Property(x => x.RequestedByUserId).HasColumnName("requested_by_user_id");
        builder.Property(x => x.RequestStatusHistoryId).HasColumnName("request_status_history_id");
        builder.Property(x => x.Question).HasColumnName("question");
        builder.Property(x => x.RequestedAt).HasColumnName("requested_at");
        builder.Property(x => x.AnsweredAt).HasColumnName("answered_at");
        builder.Property(x => x.AnswerMessageId).HasColumnName("answer_message_id");
        builder.Property(x => x.IsActive).HasColumnName("is_active");
        builder.Property(x => x.HasUnreadAnswer).HasColumnName("has_unread_answer");
        builder.Property(x => x.ReminderCount).HasColumnName("reminder_count");
        builder.Property(x => x.LastReminderAt).HasColumnName("last_reminder_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at");
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        builder.Property(x => x.Question).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.RequestId).HasFilter("is_active = true").IsUnique();
        builder.HasIndex(x => new { x.RequestId, x.RequestedAt });
        builder.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<RequestMessage>().WithMany().HasForeignKey(x => x.AnswerMessageId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<RequestStatusHistory>().WithMany().HasForeignKey(x => x.RequestStatusHistoryId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
