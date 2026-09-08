namespace CondoLink.Domain.Entities;

public sealed class RequestInternalNote
{
    public const int MaximumContentLength = 3000;
    private RequestInternalNote() { }

    public RequestInternalNote(Guid requestId, Guid authorUserId, string content)
    {
        if (requestId == Guid.Empty || authorUserId == Guid.Empty)
            throw new ArgumentException("Request and author are required.");
        Id = Guid.NewGuid();
        RequestId = requestId;
        AuthorUserId = authorUserId;
        Content = ValidateContent(content);
        CreatedAt = DateTime.UtcNow;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Content { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? UpdatedAt { get; private set; }

    public void Edit(string content)
    {
        Content = ValidateContent(content);
        UpdatedAt = DateTime.UtcNow;
    }

    private static string ValidateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Trim().Length > MaximumContentLength)
            throw new ArgumentException("A note must contain between 1 and 3000 characters.");
        return content.Trim();
    }
}
