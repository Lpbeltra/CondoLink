using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class RequestClosureConfirmation
{
    private RequestClosureConfirmation() { }

    public RequestClosureConfirmation(Guid requestId, Guid requestStatusHistoryId,
        string conclusion, DateTime requestedAt)
    {
        if (requestId == Guid.Empty) throw new ArgumentException("RequestId is required.", nameof(requestId));
        if (requestStatusHistoryId == Guid.Empty) throw new ArgumentException("RequestStatusHistoryId is required.", nameof(requestStatusHistoryId));
        if (string.IsNullOrWhiteSpace(conclusion)) throw new ArgumentException("Conclusion is required.", nameof(conclusion));
        if (conclusion.Trim().Length > 500) throw new ArgumentException("Conclusion must not exceed 500 characters.", nameof(conclusion));
        Id = Guid.NewGuid();
        RequestId = requestId;
        RequestStatusHistoryId = requestStatusHistoryId;
        Conclusion = conclusion.Trim();
        RequestedAt = requestedAt;
        ExpiresAt = requestedAt.AddHours(1);
        Status = RequestClosureConfirmationStatus.Pending;
        CreatedAt = UpdatedAt = requestedAt;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid RequestStatusHistoryId { get; private set; }
    public string Conclusion { get; private set; } = null!;
    public DateTime RequestedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public RequestClosureConfirmationStatus Status { get; private set; }
    public DateTime? DecidedAt { get; private set; }
    public Guid? ResponseMessageId { get; private set; }
    public bool FinalizedAutomatically { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
}
