using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

/// <summary>
/// Correlates one <see cref="EmployeeDocument"/> with the current
/// <see cref="WhatsAppOutboundMessage"/> distributing it. Live delivery status
/// (sent/delivered/read/failed) is never duplicated here — it is always read
/// from the linked outbound message, the single source of truth the existing
/// WhatsApp webhook already keeps up to date. A unique (EmployeeDocumentId,
/// Channel) row is the idempotency guard: a document can have at most one
/// active delivery per channel, and a resend mutates this same row instead of
/// creating a second one.
/// </summary>
public sealed class EmployeeDocumentDelivery
{
    private EmployeeDocumentDelivery() { }

    public EmployeeDocumentDelivery(Guid employeeDocumentId, Guid employeeId, Guid condominiumId, Guid batchId,
        EmployeeDocumentDeliveryChannel channel, Guid outboundMessageId, Guid queuedByUserId, DateTime now)
    {
        if (employeeDocumentId == Guid.Empty) throw new ArgumentException("Employee document id is required.", nameof(employeeDocumentId));
        if (employeeId == Guid.Empty) throw new ArgumentException("Employee id is required.", nameof(employeeId));
        if (condominiumId == Guid.Empty) throw new ArgumentException("Condominium id is required.", nameof(condominiumId));
        if (outboundMessageId == Guid.Empty) throw new ArgumentException("Outbound message id is required.", nameof(outboundMessageId));

        Id = Guid.NewGuid();
        EmployeeDocumentId = employeeDocumentId;
        EmployeeId = employeeId;
        CondominiumId = condominiumId;
        BatchId = batchId;
        Channel = channel;
        OutboundMessageId = outboundMessageId;
        QueuedByUserId = queuedByUserId;
        AttemptCount = 1;
        QueuedAt = now;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }
    public Guid EmployeeDocumentId { get; private set; }
    public Guid EmployeeId { get; private set; }
    public Guid CondominiumId { get; private set; }
    public Guid BatchId { get; private set; }
    public EmployeeDocumentDeliveryChannel Channel { get; private set; }
    public Guid OutboundMessageId { get; private set; }
    public Guid QueuedByUserId { get; private set; }
    public int AttemptCount { get; private set; }
    public DateTime QueuedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void Retry(Guid newOutboundMessageId, Guid actorUserId, DateTime now)
    {
        if (newOutboundMessageId == Guid.Empty) throw new ArgumentException("Outbound message id is required.", nameof(newOutboundMessageId));
        OutboundMessageId = newOutboundMessageId;
        QueuedByUserId = actorUserId;
        AttemptCount++;
        QueuedAt = now;
    }
}
