using CondoLink.Domain.Enums;

namespace CondoLink.Domain.Entities;

/// <summary>
/// An in-app notification addressed to a single user.
///
/// Backlog item: "Notificações — central de notificações".
/// Written as a fan-out row per recipient rather than a shared event, so read
/// state is per-user and the list query stays a simple indexed lookup.
/// </summary>
public sealed class Notification
{
    private Notification()
    {
    }

    public Notification(
        Guid recipientUserId,
        Guid condominiumId,
        NotificationType type,
        string title,
        string body,
        Guid? requestId = null,
        Guid? managementCompanyRequestId = null,
        string? idempotencyKey = null)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new ArgumentException(
                "RecipientUserId is required.", nameof(recipientUserId));
        }

        if (condominiumId == Guid.Empty)
        {
            throw new ArgumentException(
                "CondominiumId is required.", nameof(condominiumId));
        }

        if (!Enum.IsDefined(type))
        {
            throw new ArgumentOutOfRangeException(nameof(type), "Type is invalid.");
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            throw new ArgumentException("Body is required.", nameof(body));
        }

        if (requestId == Guid.Empty)
        {
            throw new ArgumentException("RequestId is invalid.", nameof(requestId));
        }

        if (managementCompanyRequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "ManagementCompanyRequestId is invalid.", nameof(managementCompanyRequestId));
        }

        Id = Guid.NewGuid();
        RecipientUserId = recipientUserId;
        CondominiumId = condominiumId;
        Type = type;
        Title = title.Trim();
        Body = body.Trim();
        RequestId = requestId;
        ManagementCompanyRequestId = managementCompanyRequestId;
        IdempotencyKey = string.IsNullOrWhiteSpace(idempotencyKey) ? null : idempotencyKey.Trim();
        CreatedAt = DateTime.UtcNow;
        ReadAt = null;
    }

    public Guid Id { get; private set; }
    public Guid RecipientUserId { get; private set; }

    /// <summary>Kept so the notification list can be scoped to the active condominium.</summary>
    public Guid CondominiumId { get; private set; }

    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = null!;
    public string Body { get; private set; } = null!;

    /// <summary>Deep-link target, when the notification refers to a request.</summary>
    public Guid? RequestId { get; private set; }

    /// <summary>Deep-link target, when the notification refers to a management company request.</summary>
    public Guid? ManagementCompanyRequestId { get; private set; }

    /// <summary>
    /// Deterministic dedup key (event + recipient + source entity). Null for
    /// notifications that predate this field or that do not need dedup.
    /// </summary>
    public string? IdempotencyKey { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }

    public bool IsRead => ReadAt.HasValue;

    /// <summary>Marks as read. Idempotent: re-reading must not move the timestamp.</summary>
    public void MarkAsRead(DateTime readAt)
    {
        ReadAt ??= readAt;
    }

    /// <summary>Returns to unread, so a user can revisit something later.</summary>
    public void MarkAsUnread()
    {
        ReadAt = null;
    }
}
