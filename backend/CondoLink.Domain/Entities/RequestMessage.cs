namespace CondoLink.Domain.Entities;

public sealed class RequestMessage
{
    private RequestMessage()
    {
    }

    public RequestMessage(Guid requestId, Guid authorUserId, string content)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId is required.", nameof(requestId));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException("AuthorUserId is required.", nameof(authorUserId));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content is required.", nameof(content));
        }

        Id = Guid.NewGuid();
        RequestId = requestId;
        AuthorUserId = authorUserId;
        Content = content.Trim();
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
}
