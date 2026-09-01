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
    RequestMessageReceived = 4,
    ResidentRequestUpdated = 5,

    /// <summary>Gestão criou uma nova solicitação — destinatário é sempre a administradora.</summary>
    ManagementCompanyRequestCreated = 6,

    /// <summary>Administradora pediu uma informação — destinatário é sempre a gestão (Manager/SubManager).</summary>
    ManagementCompanyRequestInfoRequested = 7,

    /// <summary>Gestão respondeu à administradora — destinatário é sempre a administradora.</summary>
    ManagementCompanyRequestManagerReplied = 8,

    /// <summary>Administradora concluiu a solicitação — destinatário é sempre a gestão.</summary>
    ManagementCompanyRequestCompleted = 9,

    /// <summary>Gestão cancelou a solicitação — destinatário é sempre a administradora.</summary>
    ManagementCompanyRequestCancelled = 10,
    ManagementCompanyRequestEdited = 11
}
