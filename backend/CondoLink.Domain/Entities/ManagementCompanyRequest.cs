using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class ManagementCompanyRequest
{
    private ManagementCompanyRequest() { }
    public ManagementCompanyRequest(Guid condominiumId, Guid managementCompanyId,
        Guid categoryId, Guid createdByUserId, ManagementCompanyRequestType type)
    {
        if (condominiumId == Guid.Empty || managementCompanyId == Guid.Empty
            || categoryId == Guid.Empty || createdByUserId == Guid.Empty)
            throw new ArgumentException("Request ownership fields are required.");
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        var now = DateTime.UtcNow;
        Id = Guid.NewGuid();
        FriendlyIdentifier = $"ADM-{Id:N}"[..16].ToUpperInvariant();
        CondominiumId = condominiumId; ManagementCompanyId = managementCompanyId;
        CategoryId = categoryId; CreatedByUserId = createdByUserId; Type = type;
        Status = ManagementCompanyRequestStatus.Submitted;
        ConcurrencyStamp = Guid.NewGuid(); CreatedAt = now; UpdatedAt = now;
    }
    public Guid Id { get; private set; }
    public string FriendlyIdentifier { get; private set; } = null!;
    public Guid CondominiumId { get; private set; }
    public Guid ManagementCompanyId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public ManagementCompanyRequestType Type { get; private set; }
    public ManagementCompanyRequestStatus Status { get; private set; }
    public Guid ConcurrencyStamp { get; private set; }
    public Guid? AcknowledgedByUserId { get; private set; }
    public DateTime? AcknowledgedAt { get; private set; }
    public Guid? CompletedByUserId { get; private set; }
    public DateTime? CompletedAt { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancellationReason { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsTerminal => Status is ManagementCompanyRequestStatus.Completed or ManagementCompanyRequestStatus.Cancelled;

    public ManagementCompanyRequestStatus Acknowledge(Guid userId, DateTime at)
    {
        if (Status != ManagementCompanyRequestStatus.Submitted) return Status;
        AcknowledgedByUserId = userId; AcknowledgedAt = at;
        ApplyTransition(ManagementCompanyRequestStatus.Acknowledged, at);
        return Status;
    }
    public ManagementCompanyRequestStatus TransitionTo(ManagementCompanyRequestStatus next, DateTime at)
    {
        if (!CanTransition(Status, next)) throw new InvalidOperationException("Management company request status transition is not allowed.");
        ApplyTransition(next, at); return Status;
    }
    public void Complete(Guid userId, DateTime at)
    {
        TransitionTo(ManagementCompanyRequestStatus.Completed, at);
        CompletedByUserId = userId; CompletedAt = at;
    }
    public void Cancel(Guid userId, string reason, DateTime at)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            throw new ArgumentException("Cancellation reason is required and must not exceed 500 characters.", nameof(reason));
        if (IsTerminal) throw new InvalidOperationException("Terminal requests cannot be cancelled.");
        ApplyTransition(ManagementCompanyRequestStatus.Cancelled, at);
        CancelledByUserId = userId; CancelledAt = at; CancellationReason = reason.Trim();
    }
    public static bool CanTransition(ManagementCompanyRequestStatus current, ManagementCompanyRequestStatus next) => current switch
    {
        ManagementCompanyRequestStatus.Submitted => next is ManagementCompanyRequestStatus.Acknowledged or ManagementCompanyRequestStatus.Cancelled,
        ManagementCompanyRequestStatus.Acknowledged => next is ManagementCompanyRequestStatus.InProgress or ManagementCompanyRequestStatus.WaitingManager or ManagementCompanyRequestStatus.Cancelled,
        ManagementCompanyRequestStatus.InProgress => next is ManagementCompanyRequestStatus.WaitingManager or ManagementCompanyRequestStatus.Completed or ManagementCompanyRequestStatus.Cancelled,
        ManagementCompanyRequestStatus.WaitingManager => next is ManagementCompanyRequestStatus.InProgress or ManagementCompanyRequestStatus.Cancelled,
        _ => false
    };
    private void ApplyTransition(ManagementCompanyRequestStatus next, DateTime at)
    { Status = next; UpdatedAt = at; ConcurrencyStamp = Guid.NewGuid(); }
}
