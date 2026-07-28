namespace CondoLink.Domain.Entities;

public sealed class WhatsAppDraftAttachment
{
    private WhatsAppDraftAttachment() { }

    public WhatsAppDraftAttachment(
        Guid sessionId,
        string externalMediaId,
        string originalFileName,
        string storageKey,
        string contentType,
        long fileSize)
    {
        if (sessionId == Guid.Empty) throw new ArgumentException("Session id is required.", nameof(sessionId));
        if (string.IsNullOrWhiteSpace(externalMediaId)) throw new ArgumentException("Media id is required.", nameof(externalMediaId));
        Id = Guid.NewGuid();
        SessionId = sessionId;
        ExternalMediaId = externalMediaId.Trim();
        OriginalFileName = Path.GetFileName(originalFileName);
        StorageKey = storageKey;
        ContentType = contentType;
        FileSize = fileSize;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid SessionId { get; private set; }
    public string ExternalMediaId { get; private set; } = null!;
    public string OriginalFileName { get; private set; } = null!;
    public string StorageKey { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSize { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
