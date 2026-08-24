using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class AgendaReminderOccurrence
{
    private AgendaReminderOccurrence() { }
    public AgendaReminderOccurrence(Guid reminderId, DateTime scheduledForUtc,
        bool email, bool whatsApp, DateTime now)
    { Id = Guid.NewGuid(); ReminderId = reminderId; ScheduledForUtc = scheduledForUtc;
      EmailStatus = email ? AgendaDeliveryStatus.Pending : AgendaDeliveryStatus.NotRequested;
      WhatsAppStatus = whatsApp ? AgendaDeliveryStatus.Pending : AgendaDeliveryStatus.NotRequested;
      CreatedAt = now; UpdatedAt = now; }
    public Guid Id { get; private set; }
    public Guid ReminderId { get; private set; }
    public DateTime ScheduledForUtc { get; private set; }
    public AgendaDeliveryStatus EmailStatus { get; private set; }
    public int EmailAttempts { get; private set; }
    public string? EmailDiagnostic { get; private set; }
    public AgendaDeliveryStatus WhatsAppStatus { get; private set; }
    public int WhatsAppAttempts { get; private set; }
    public string? WhatsAppDiagnostic { get; private set; }
    public Guid? WhatsAppOutboundMessageId { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public void EmailResult(bool sent, string? diagnostic, DateTime now)
    { EmailAttempts++; EmailStatus = sent ? AgendaDeliveryStatus.Sent : AgendaDeliveryStatus.Failed;
      EmailDiagnostic = diagnostic; Touch(now); }
    public void WhatsAppResult(AgendaDeliveryStatus status, string? diagnostic,
        Guid? outboundId, DateTime now)
    { WhatsAppAttempts++; WhatsAppStatus = status; WhatsAppDiagnostic = diagnostic;
      WhatsAppOutboundMessageId = outboundId; Touch(now); }
    private void Touch(DateTime now) { UpdatedAt = now;
      if (EmailStatus is not AgendaDeliveryStatus.Pending and not AgendaDeliveryStatus.Processing
          && WhatsAppStatus is not AgendaDeliveryStatus.Pending and not AgendaDeliveryStatus.Processing)
          ProcessedAt = now; }
}
