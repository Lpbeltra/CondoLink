using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class RequestStatusHistory
{
    private RequestStatusHistory()
    {
    }

    public RequestStatusHistory(
        Guid requestId,
        RequestStatus? previousStatus,
        RequestStatus newStatus,
        Guid changedByUserId,
        string? reason,
        DateTime createdAt)
    {
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId is required.", nameof(requestId));
        }

        if (changedByUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "ChangedByUserId is required.",
                nameof(changedByUserId));
        }

        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), "NewStatus is invalid.");
        }

        if (previousStatus.HasValue && !Enum.IsDefined(previousStatus.Value))
        {
            throw new ArgumentOutOfRangeException(
                nameof(previousStatus),
                "PreviousStatus is invalid.");
        }

        Id = Guid.NewGuid();
        RequestId = requestId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedByUserId = changedByUserId;
        Reason = NormalizeOptional(reason);
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public Guid RequestId { get; private set; }
    public RequestStatus? PreviousStatus { get; private set; }
    public RequestStatus NewStatus { get; private set; }
    public Guid ChangedByUserId { get; private set; }
    public string? Reason { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
