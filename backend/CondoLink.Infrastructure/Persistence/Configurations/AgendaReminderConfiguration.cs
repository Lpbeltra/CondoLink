using CondoLink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CondoLink.Infrastructure.Persistence.Configurations;

public sealed class AgendaReminderConfiguration : IEntityTypeConfiguration<AgendaReminder>
{
    public void Configure(EntityTypeBuilder<AgendaReminder> b)
    {
        b.ToTable("agenda_reminders"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.CondominiumId).HasColumnName("condominium_id");
        b.Property(x => x.CreatedByUserId).HasColumnName("created_by_user_id");
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(160).IsRequired();
        b.Property(x => x.Description).HasColumnName("description").HasMaxLength(1000);
        b.Property(x => x.UnitId).HasColumnName("unit_id");
        b.Property(x => x.RelatedThirdParty).HasColumnName("related_third_party").HasMaxLength(200);
        b.Property(x => x.StartsAtUtc).HasColumnName("starts_at_utc");
        b.Property(x => x.NextOccurrenceAtUtc).HasColumnName("next_occurrence_at_utc");
        b.Property(x => x.TimeZoneId).HasColumnName("time_zone_id").HasMaxLength(100);
        b.Property(x => x.RecurrenceType).HasColumnName("recurrence_type");
        b.Property(x => x.RecurrenceDayOfMonth).HasColumnName("recurrence_day_of_month");
        b.Property(x => x.NotifyByWhatsApp).HasColumnName("notify_by_whatsapp");
        b.Property(x => x.NotifyByEmail).HasColumnName("notify_by_email");
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.CompletedAt).HasColumnName("completed_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsConcurrencyToken();
        b.HasIndex(x => new { x.CondominiumId, x.IsActive, x.NextOccurrenceAtUtc });
        b.HasOne<Condominium>().WithMany().HasForeignKey(x => x.CondominiumId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Unit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class AgendaReminderRequestConfiguration : IEntityTypeConfiguration<AgendaReminderRequest>
{
    public void Configure(EntityTypeBuilder<AgendaReminderRequest> b)
    {
        b.ToTable("agenda_reminder_requests"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ReminderId).HasColumnName("reminder_id");
        b.Property(x => x.RequestId).HasColumnName("request_id");
        b.Property(x => x.LinkedByUserId).HasColumnName("linked_by_user_id");
        b.Property(x => x.LinkedAt).HasColumnName("linked_at");
        b.HasIndex(x => x.RequestId).IsUnique();
        b.HasIndex(x => x.ReminderId);
        b.HasOne<AgendaReminder>().WithMany().HasForeignKey(x => x.ReminderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Request>().WithMany().HasForeignKey(x => x.RequestId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class AgendaReminderOccurrenceConfiguration : IEntityTypeConfiguration<AgendaReminderOccurrence>
{
    public void Configure(EntityTypeBuilder<AgendaReminderOccurrence> b)
    {
        b.ToTable("agenda_reminder_occurrences"); b.HasKey(x => x.Id);
        b.Property(x => x.Id).HasColumnName("id");
        b.Property(x => x.ReminderId).HasColumnName("reminder_id");
        b.Property(x => x.ScheduledForUtc).HasColumnName("scheduled_for_utc");
        b.Property(x => x.EmailStatus).HasColumnName("email_status");
        b.Property(x => x.EmailAttempts).HasColumnName("email_attempts");
        b.Property(x => x.EmailDiagnostic).HasColumnName("email_diagnostic").HasMaxLength(300);
        b.Property(x => x.WhatsAppStatus).HasColumnName("whatsapp_status");
        b.Property(x => x.WhatsAppAttempts).HasColumnName("whatsapp_attempts");
        b.Property(x => x.WhatsAppDiagnostic).HasColumnName("whatsapp_diagnostic").HasMaxLength(300);
        b.Property(x => x.WhatsAppOutboundMessageId).HasColumnName("whatsapp_outbound_message_id");
        b.Property(x => x.ProcessedAt).HasColumnName("processed_at");
        b.Property(x => x.CreatedAt).HasColumnName("created_at");
        b.Property(x => x.UpdatedAt).HasColumnName("updated_at");
        b.HasIndex(x => new { x.ReminderId, x.ScheduledForUtc }).IsUnique();
        b.HasOne<AgendaReminder>().WithMany().HasForeignKey(x => x.ReminderId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<WhatsAppOutboundMessage>().WithMany().HasForeignKey(x => x.WhatsAppOutboundMessageId).OnDelete(DeleteBehavior.SetNull);
    }
}
