using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class WhatsAppOutboundMessage
{
    private WhatsAppOutboundMessage() { }

    public WhatsAppOutboundMessage(
        Guid? requestId, Guid? requestMessageId, Guid userId, Guid condominiumId,
        string destinationPhone, WhatsAppNotificationType notificationType,
        WhatsAppSendMode sendMode, string idempotencyKey, string content,
        string? templateName, string? templateLanguage, DateTime now,
        WhatsAppOutboundStatus status = WhatsAppOutboundStatus.Pending,
        string? error = null, string? templateParameterContent = null,
        Guid? requestStatusHistoryId = null,
        Guid? requestClosureConfirmationId = null)
    {
        Id = Guid.NewGuid();
        RequestId = requestId;
        RequestMessageId = requestMessageId;
        UserId = userId;
        CondominiumId = condominiumId;
        DestinationPhone = destinationPhone;
        NotificationType = notificationType;
        SendMode = sendMode;
        IdempotencyKey = idempotencyKey;
        Content = content;
        TemplateParameterContent = templateParameterContent;
        RequestStatusHistoryId = requestStatusHistoryId;
        RequestClosureConfirmationId = requestClosureConfirmationId;
        TemplateName = templateName;
        TemplateLanguage = templateLanguage;
        Status = status;
        LastErrorDescription = error;
        AttemptCount = 0;
        CreatedAt = now;
        NextAttemptAt = status == WhatsAppOutboundStatus.Pending ? now : null;
        Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public Guid? RequestId { get; private set; }
    public Guid? RequestMessageId { get; private set; }
    public Guid? RequestStatusHistoryId { get; private set; }
    public Guid? RequestClosureConfirmationId { get; private set; }
    public Guid UserId { get; private set; }
    public Guid? CondominiumId { get; private set; }
    public string DestinationPhone { get; private set; } = null!;
    public WhatsAppNotificationType NotificationType { get; private set; }
    public WhatsAppSendMode SendMode { get; private set; }
    public string? TemplateName { get; private set; }
    public string? TemplateLanguage { get; private set; }
    public string Content { get; private set; } = null!;
    public string? TemplateParameterContent { get; private set; }
    public string? ExternalMessageId { get; private set; }
    public WhatsAppOutboundStatus Status { get; private set; }
    public int AttemptCount { get; private set; }
    public int ManualRetryCount { get; private set; }
    public DateTime? NextAttemptAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? SentAt { get; private set; }
    public DateTime? DeliveredAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    public DateTime? FailedAt { get; private set; }
    public string? LastErrorCode { get; private set; }
    public string? LastErrorDescription { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;
    public Guid Version { get; private set; }

    public void StartProcessing()
    {
        if (Status != WhatsAppOutboundStatus.Pending) return;
        Status = WhatsAppOutboundStatus.Processing;
        AttemptCount++;
        Version = Guid.NewGuid();
    }

    public void RecoverInterruptedProcessing(DateTime now)
    {
        if (Status != WhatsAppOutboundStatus.Processing) return;
        Status = WhatsAppOutboundStatus.Pending;
        NextAttemptAt = now;
        Version = Guid.NewGuid();
    }

    public void MarkSent(string externalId, DateTime now)
    {
        ExternalMessageId = externalId;
        Status = WhatsAppOutboundStatus.Sent;
        SentAt ??= now;
        NextAttemptAt = null;
        LastErrorCode = null;
        LastErrorDescription = null;
        Version = Guid.NewGuid();
    }

    public void MarkFailure(
        string? code, string description, bool transient, int maxAttempts,
        DateTime now, TimeSpan retryDelay)
    {
        LastErrorCode = code;
        LastErrorDescription = description.Length <= 500 ? description : description[..500];
        FailedAt = now;
        Status = transient && AttemptCount < maxAttempts
            ? WhatsAppOutboundStatus.Pending
            : WhatsAppOutboundStatus.PermanentlyFailed;
        NextAttemptAt = Status == WhatsAppOutboundStatus.Pending ? now.Add(retryDelay) : null;
        Version = Guid.NewGuid();
    }

    public void ApplyProviderStatus(
        string status, DateTime occurredAt, string? errorCode, string? errorDescription)
    {
        switch (status)
        {
            case "sent" when Status < WhatsAppOutboundStatus.Sent:
                Status = WhatsAppOutboundStatus.Sent; SentAt ??= occurredAt; break;
            case "delivered" when Status is not WhatsAppOutboundStatus.Read:
                Status = WhatsAppOutboundStatus.Delivered; SentAt ??= occurredAt;
                DeliveredAt ??= occurredAt; break;
            case "read":
                Status = WhatsAppOutboundStatus.Read; SentAt ??= occurredAt;
                DeliveredAt ??= occurredAt; ReadAt ??= occurredAt; break;
            case "failed" when Status is not WhatsAppOutboundStatus.Delivered
                    and not WhatsAppOutboundStatus.Read:
                Status = WhatsAppOutboundStatus.Failed; FailedAt ??= occurredAt;
                LastErrorCode = errorCode;
                LastErrorDescription = errorDescription is null ? null
                    : errorDescription[..Math.Min(500, errorDescription.Length)];
                break;
            default: return;
        }
        Version = Guid.NewGuid();
    }

    public bool RequestManualRetry(DateTime now)
    {
        if (Status is WhatsAppOutboundStatus.Delivered or WhatsAppOutboundStatus.Read
            or WhatsAppOutboundStatus.Sent || ManualRetryCount >= 3) return false;
        ManualRetryCount++;
        Status = WhatsAppOutboundStatus.Pending;
        NextAttemptAt = now;
        Version = Guid.NewGuid();
        return true;
    }
}
