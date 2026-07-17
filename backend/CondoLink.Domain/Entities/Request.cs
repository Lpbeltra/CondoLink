using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class Request
{
    private Request()
    {
    }

    public Request(
        Guid condominiumId,
        Guid authorUserId,
        Guid? targetUnitId,
        Guid categoryId,
        string title,
        string description)
    {
        if (condominiumId == Guid.Empty)
        {
            throw new ArgumentException("CondominiumId is required.", nameof(condominiumId));
        }

        if (authorUserId == Guid.Empty)
        {
            throw new ArgumentException("AuthorUserId is required.", nameof(authorUserId));
        }

        if (categoryId == Guid.Empty)
        {
            throw new ArgumentException("CategoryId is required.", nameof(categoryId));
        }

        if (targetUnitId == Guid.Empty)
        {
            throw new ArgumentException("TargetUnitId is invalid.", nameof(targetUnitId));
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            throw new ArgumentException("Description is required.", nameof(description));
        }

        var now = DateTime.UtcNow;

        Id = Guid.NewGuid();
        CondominiumId = condominiumId;
        AuthorUserId = authorUserId;
        TargetUnitId = targetUnitId;
        CategoryId = categoryId;
        Title = title.Trim();
        Description = description.Trim();
        Status = RequestStatus.Open;
        Priority = RequestPriority.Normal;
        CreatedAt = now;
        UpdatedAt = now;
        ResolvedAt = null;
    }

    public Guid Id { get; private set; }
    public Guid CondominiumId { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public Guid? TargetUnitId { get; private set; }
    public Guid CategoryId { get; private set; }
    public string Title { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public RequestStatus Status { get; private set; }
    public RequestPriority Priority { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }

    public void ChangeStatus(RequestStatus newStatus, DateTime changedAt)
    {
        if (!Enum.IsDefined(newStatus))
        {
            throw new ArgumentOutOfRangeException(nameof(newStatus), "Status is invalid.");
        }

        if (newStatus == Status)
        {
            throw new InvalidOperationException("Request already has this status.");
        }

        if (!IsTransitionAllowed(Status, newStatus))
        {
            throw new InvalidOperationException("Request status transition is not allowed.");
        }

        Status = newStatus;
        UpdatedAt = changedAt;
        ResolvedAt = newStatus == RequestStatus.Resolved ? changedAt : null;
    }

    public void ChangePriority(RequestPriority newPriority, DateTime changedAt)
    {
        if (!Enum.IsDefined(newPriority))
        {
            throw new ArgumentOutOfRangeException(nameof(newPriority), "Priority is invalid.");
        }

        if (Status == RequestStatus.Cancelled)
        {
            throw new InvalidOperationException(
                "Cancelled requests cannot have their priority changed.");
        }

        if (newPriority == Priority)
        {
            throw new InvalidOperationException("Request already has this priority.");
        }

        Priority = newPriority;
        UpdatedAt = changedAt;
    }

    private static bool IsTransitionAllowed(
        RequestStatus currentStatus,
        RequestStatus newStatus)
    {
        return currentStatus switch
        {
            RequestStatus.Open =>
                newStatus is RequestStatus.InProgress
                    or RequestStatus.Resolved
                    or RequestStatus.Cancelled,
            RequestStatus.InProgress =>
                newStatus is RequestStatus.WaitingForResident
                    or RequestStatus.WaitingForThirdParty
                    or RequestStatus.Resolved
                    or RequestStatus.Cancelled,
            RequestStatus.WaitingForResident =>
                newStatus is RequestStatus.InProgress
                    or RequestStatus.Resolved
                    or RequestStatus.Cancelled,
            RequestStatus.WaitingForThirdParty =>
                newStatus is RequestStatus.InProgress
                    or RequestStatus.Resolved
                    or RequestStatus.Cancelled,
            RequestStatus.Resolved => newStatus == RequestStatus.Open,
            RequestStatus.Cancelled => newStatus == RequestStatus.Open,
            _ => false
        };
    }
}
