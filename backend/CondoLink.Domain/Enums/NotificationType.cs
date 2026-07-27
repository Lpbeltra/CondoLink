namespace CondoLink.Domain.Enums;

/// <summary>
/// What produced a notification. Stored so the UI can pick an icon and wording
/// without parsing the message text.
/// </summary>
public enum NotificationType
{
    RequestCreated = 1,
    RequestStatusChanged = 2,
    RequestPriorityChanged = 3,
    RequestMessageReceived = 4
}
