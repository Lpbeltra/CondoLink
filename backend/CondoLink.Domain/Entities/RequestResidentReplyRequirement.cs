namespace CondoLink.Domain.Entities;

public sealed class RequestResidentReplyRequirement
{
    private RequestResidentReplyRequirement() { }

    public RequestResidentReplyRequirement(Guid requestId, Guid requestedByUserId,
        Guid requestStatusHistoryId, string question, DateTime now)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("RequestId is required.", nameof(requestId));
        if (requestedByUserId == Guid.Empty) throw new ArgumentException("RequestedByUserId is required.", nameof(requestedByUserId));
        if (requestStatusHistoryId == Guid.Empty) throw new ArgumentException("RequestStatusHistoryId is required.", nameof(requestStatusHistoryId));
        if (string.IsNullOrWhiteSpace(question)) throw new ArgumentException("Question is required.", nameof(question));
        Id = Guid.NewGuid();
        RequestId = requestId;
        RequestedByUserId = requestedByUserId;
        RequestStatusHistoryId = requestStatusHistoryId;
        Question = question.Trim();
        RequestedAt = now;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public Guid RequestStatusHistoryId { get; private set; }
    public string Question { get; private set; } = null!;
    public DateTime RequestedAt { get; private set; }
    public DateTime? AnsweredAt { get; private set; }
    public Guid? AnswerMessageId { get; private set; }
    public bool IsActive { get; private set; }
    public bool HasUnreadAnswer { get; private set; }
    public int ReminderCount { get; private set; }
    public DateTime? LastReminderAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    public void Answer(Guid messageId, DateTime now)
    {
        if (!IsActive) throw new InvalidOperationException("The requirement is not active.");
        if (messageId == Guid.Empty) throw new ArgumentException("MessageId is required.", nameof(messageId));
        IsActive = false;
        AnsweredAt = now;
        AnswerMessageId = messageId;
        HasUnreadAnswer = true;
        UpdatedAt = now;
    }

    public void CloseWithoutAnswer(DateTime now)
    {
        IsActive = false;
        HasUnreadAnswer = false;
        UpdatedAt = now;
    }

    public void MarkAnswerRead(DateTime now)
    {
        HasUnreadAnswer = false;
        UpdatedAt = now;
    }
}
