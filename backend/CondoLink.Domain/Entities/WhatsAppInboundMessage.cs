namespace CondoLink.Domain.Entities;

public sealed class WhatsAppInboundMessage
{
    private WhatsAppInboundMessage() { }

    public WhatsAppInboundMessage(
        string externalMessageId,
        string phoneNumber,
        string messageType,
        string? text,
        DateTime providerTimestamp)
    {
        if (string.IsNullOrWhiteSpace(externalMessageId))
            throw new ArgumentException("External message id is required.", nameof(externalMessageId));
        if (string.IsNullOrWhiteSpace(phoneNumber))
            throw new ArgumentException("Phone number is required.", nameof(phoneNumber));
        if (string.IsNullOrWhiteSpace(messageType))
            throw new ArgumentException("Message type is required.", nameof(messageType));

        Id = Guid.NewGuid();
        ExternalMessageId = externalMessageId.Trim();
        PhoneNumber = phoneNumber.Trim();
        MessageType = messageType.Trim();
        Text = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        ProviderTimestamp = providerTimestamp;
        ReceivedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public string ExternalMessageId { get; private set; } = null!;
    public string PhoneNumber { get; private set; } = null!;
    public string MessageType { get; private set; } = null!;
    public string? Text { get; private set; }
    public DateTime ProviderTimestamp { get; private set; }
    public DateTime ReceivedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public Guid? IdentifiedUserId { get; private set; }
    public string? ProcessingResult { get; private set; }

    public void Complete(Guid? identifiedUserId, string result, DateTime processedAt)
    {
        IdentifiedUserId = identifiedUserId;
        ProcessingResult = result;
        ProcessedAt = processedAt;
    }
}
