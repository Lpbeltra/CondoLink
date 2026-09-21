using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

public sealed class PendingAssistantAction
{
    private PendingAssistantAction() { }

    public PendingAssistantAction(PendingAssistantActionType actionType, Guid actorUserId,
        Guid condominiumId, CondominiumAssistantChannel channel, string? externalContextId,
        string payloadJson, string idempotencyKey, DateTime now, DateTime expiresAt)
    {
        Id = Guid.NewGuid(); ActionType = actionType; ActorUserId = actorUserId;
        CondominiumId = condominiumId; Channel = channel; ExternalContextId = externalContextId;
        PayloadJson = payloadJson; IdempotencyKey = idempotencyKey; CreatedAt = now;
        ExpiresAt = expiresAt; Status = PendingAssistantActionStatus.Pending; Version = Guid.NewGuid();
    }

    public Guid Id { get; private set; }
    public PendingAssistantActionType ActionType { get; private set; }
    public PendingAssistantActionStatus Status { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid CondominiumId { get; private set; }
    public CondominiumAssistantChannel Channel { get; private set; }
    public string? ExternalContextId { get; private set; }
    public string PayloadJson { get; private set; } = null!;
    public string IdempotencyKey { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime ExpiresAt { get; private set; }
    public DateTime? ExecutedAt { get; private set; }
    public string? ResultJson { get; private set; }
    public Guid Version { get; private set; }

    public bool IsExpired(DateTime now) => Status == PendingAssistantActionStatus.Pending && ExpiresAt <= now;
    public bool TryExpire(DateTime now) { if (!IsExpired(now)) return false; Status = PendingAssistantActionStatus.Expired; Touch(); return true; }
    public bool TryCancel() { if (Status != PendingAssistantActionStatus.Pending) return false; Status = PendingAssistantActionStatus.Cancelled; Touch(); return true; }
    public bool TryBeginExecution(DateTime now) { if (TryExpire(now) || Status != PendingAssistantActionStatus.Pending) return false; Status = PendingAssistantActionStatus.Executing; Touch(); return true; }
    public void Complete(DateTime now, string resultJson) { if (Status != PendingAssistantActionStatus.Executing) throw new InvalidOperationException("Only executing actions can complete."); Status = PendingAssistantActionStatus.Executed; ExecutedAt = now; ResultJson = resultJson; Touch(); }
    public void Fail(DateTime now, string resultJson) { if (Status != PendingAssistantActionStatus.Executing) throw new InvalidOperationException("Only executing actions can fail."); Status = PendingAssistantActionStatus.Failed; ExecutedAt = now; ResultJson = resultJson; Touch(); }
    private void Touch() => Version = Guid.NewGuid();
}
