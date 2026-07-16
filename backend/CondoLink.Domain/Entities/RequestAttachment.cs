namespace CondoLink.Domain.Entities;

public sealed class RequestAttachment
{
    private RequestAttachment() { }

    public RequestAttachment(Guid requestId, Guid uploadedByUserId, string originalFileName,
        string storageKey, string contentType, long fileSize)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("RequestId is required.", nameof(requestId));
        if (uploadedByUserId == Guid.Empty) throw new ArgumentException("UploadedByUserId is required.", nameof(uploadedByUserId));
        if (string.IsNullOrWhiteSpace(originalFileName)) throw new ArgumentException("OriginalFileName is required.", nameof(originalFileName));
        if (string.IsNullOrWhiteSpace(storageKey)) throw new ArgumentException("StorageKey is required.", nameof(storageKey));
        if (string.IsNullOrWhiteSpace(contentType)) throw new ArgumentException("ContentType is required.", nameof(contentType));
        if (fileSize <= 0) throw new ArgumentOutOfRangeException(nameof(fileSize), "FileSize must be greater than zero.");

        Id = Guid.NewGuid();
        RequestId = requestId;
        RequestMessageId = null;
        UploadedByUserId = uploadedByUserId;
        OriginalFileName = originalFileName.Trim();
        StorageKey = storageKey.Trim();
        ContentType = contentType.Trim();
        FileSize = fileSize;
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid? RequestMessageId { get; private set; }
    public Guid UploadedByUserId { get; private set; }
    public string OriginalFileName { get; private set; } = null!;
    public string StorageKey { get; private set; } = null!;
    public string ContentType { get; private set; } = null!;
    public long FileSize { get; private set; }
    public DateTime CreatedAt { get; private set; }
}
