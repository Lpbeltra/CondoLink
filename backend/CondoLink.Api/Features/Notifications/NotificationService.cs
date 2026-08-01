using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using DomainRequest = CondoLink.Domain.Entities.Request;
using CondoLink.Api.Features.WhatsApp;

namespace CondoLink.Api.Features.Notifications;

/// <summary>
/// Creates in-app notifications when something happens to a request.
///
/// Fan-out rules live here rather than in the endpoints so the "who should be
/// told" decision is in one place and directly testable.
/// </summary>
public sealed class NotificationService(
    AppDbContext dbContext,
    WhatsAppNotificationDispatcher? whatsApp = null,
    ILogger<NotificationService>? logger = null)
{
    /// <summary>
    /// Notifies the managers of a condominium that a new request was opened.
    /// The author is never notified about their own action.
    /// </summary>
    public async Task NotifyRequestCreatedAsync(
        DomainRequest request,
        string categoryName,
        CancellationToken cancellationToken)
    {
        var managerIds = await ManagerIdsAsync(
            request.CondominiumId, request.AuthorUserId, cancellationToken);

        AddRange(managerIds.Select(managerId => new Notification(
            managerId,
            request.CondominiumId,
            NotificationType.RequestCreated,
            "Nova solicitação",
            $"{categoryName}: {Shorten(request.Title)}",
            request.Id)));

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Notifies the author when a manager changes the status of their request.
    /// If the author made the change themselves, nobody is notified.
    /// </summary>
    public async Task NotifyStatusChangedAsync(
        DomainRequest request,
        RequestStatus previousStatus,
        Guid changedByUserId,
        CancellationToken cancellationToken,
        Guid? statusHistoryId = null,
        string? reason = null)
    {
        var content = request.Status == RequestStatus.WaitingForResident
            ? ResidentReplyRequestedContent(request.Title, reason!)
            : StatusChangedContent(request.Title, previousStatus, request.Status, reason);

        dbContext.Notifications.Add(new Notification(
            request.AuthorUserId,
            request.CondominiumId,
            NotificationType.RequestStatusChanged,
            "Status atualizado",
            Shorten(content, 500),
            request.Id));

        await dbContext.SaveChangesAsync(cancellationToken);
        var type = StatusNotificationType(previousStatus, request.Status);
        logger?.LogInformation(
            "WhatsApp notification flow. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}",
            request.Id, request.CondominiumId, type, "EnteredNotificationService",
            "InAppNotificationSaved");
        if (whatsApp is null)
        {
            logger?.LogInformation(
                "WhatsApp notification flow. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}",
                request.Id, request.CondominiumId, type, "Stopped", "WhatsAppDispatcherUnavailable");
        }
        else if (!statusHistoryId.HasValue)
        {
            logger?.LogInformation(
                "WhatsApp notification flow. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}",
                request.Id, request.CondominiumId, type, "Stopped", "StatusHistoryIdMissing");
        }
        else
        {
            logger?.LogInformation(
                "WhatsApp notification flow. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}",
                request.Id, request.CondominiumId, type, "CallingWhatsAppDispatcher",
                "EligibleForEnqueueEvaluation");
            await whatsApp.EnqueueAsync(
                request.Id, type, $"request-status:{statusHistoryId}",
                content,
                null, cancellationToken);
        }
    }

    /// <summary>
    /// Notifies the counterpart of a new message: the author hears about manager
    /// replies, and managers hear about the author's replies.
    /// </summary>
    public async Task NotifyMessageAsync(
        Guid requestId,
        Guid condominiumId,
        Guid requestAuthorUserId,
        string requestTitle,
        Guid messageAuthorUserId,
        string content,
        CancellationToken cancellationToken,
        Guid? requestMessageId = null,
        MessageChannel channel = MessageChannel.Portal)
    {
        Guid[] recipients = messageAuthorUserId == requestAuthorUserId
            ? await ManagerIdsAsync(condominiumId, messageAuthorUserId, cancellationToken)
            : [requestAuthorUserId];

        AddRange(recipients
            .Where(recipientId => recipientId != messageAuthorUserId)
            .Select(recipientId => new Notification(
                recipientId,
                condominiumId,
                NotificationType.RequestMessageReceived,
                "Nova mensagem",
                $"{Shorten(requestTitle, 60)}: {Shorten(content, 90)}",
                requestId)));

        await dbContext.SaveChangesAsync(cancellationToken);
        if (whatsApp is not null
            && requestMessageId.HasValue
            && messageAuthorUserId != requestAuthorUserId
            && channel != MessageChannel.WhatsApp)
        {
            await whatsApp.EnqueueAsync(
                requestId,
                WhatsAppNotificationType.AdministrationMessage,
                $"request-message:{requestMessageId}",
                $"A administração enviou uma mensagem na solicitação "
                + $"#{requestId.ToString("N")[..8].ToUpperInvariant()}: "
                + Shorten(content, 300),
                requestMessageId,
                cancellationToken);
        }
    }

    /// <summary>Active managers of a condominium, excluding one user.</summary>
    private Task<Guid[]> ManagerIdsAsync(
        Guid condominiumId,
        Guid excludeUserId,
        CancellationToken cancellationToken)
        => dbContext.CondominiumMemberships
            .AsNoTracking()
            .Where(membership =>
                membership.CondominiumId == condominiumId
                && membership.IsActive
                && membership.EndedAt == null
                && membership.UserId != excludeUserId)
            .Join(
                dbContext.CondominiumMembershipRoles
                    .AsNoTracking()
                    .Where(role =>
                        role.Role == CondominiumRole.Manager
                        && role.IsActive
                        && role.RevokedAt == null),
                membership => membership.Id,
                role => role.CondominiumMembershipId,
                (membership, _) => membership.UserId)
            .Distinct()
            .ToArrayAsync(cancellationToken);

    private void AddRange(IEnumerable<Notification> notifications)
    {
        foreach (var notification in notifications)
        {
            dbContext.Notifications.Add(notification);
        }
    }

    /// <summary>Keeps bodies within the column limit without throwing.</summary>
    internal static string Shorten(string value, int maximumLength = 160)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= maximumLength
            ? trimmed
            : string.Concat(trimmed.AsSpan(0, maximumLength - 1).TrimEnd(), "…");
    }

    internal static string Describe(RequestStatus status) => status switch
    {
        RequestStatus.Open => "Aberta",
        RequestStatus.InProgress => "Em andamento",
        RequestStatus.WaitingForResident => "Aguardando morador",
        RequestStatus.WaitingForThirdParty => "Aguardando terceiro",
        RequestStatus.Resolved => "Resolvida",
        RequestStatus.Cancelled => "Cancelada",
        _ => status.ToString()
    };

    internal static WhatsAppNotificationType StatusNotificationType(
        RequestStatus previousStatus, RequestStatus currentStatus) => currentStatus switch
    {
        RequestStatus.WaitingForResident => WhatsAppNotificationType.InformationRequested,
        RequestStatus.Resolved => WhatsAppNotificationType.RequestResolved,
        RequestStatus.Cancelled => WhatsAppNotificationType.RequestCancelled,
        RequestStatus.Open when previousStatus is RequestStatus.Resolved
            or RequestStatus.Cancelled => WhatsAppNotificationType.RequestReopened,
        _ => WhatsAppNotificationType.StatusChanged
    };

    internal static string StatusChangedContent(string title,
        RequestStatus previousStatus, RequestStatus newStatus, string? reason)
    {
        var content = $"A solicitação *\"{Shorten(title, 80)}\"* foi alterada de "
            + $"*{Describe(previousStatus)}* para *{Describe(newStatus)}*.";
        var comment = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        return comment is null
            ? content
            : content + $"\n\nComentário da administração:\n\n{comment}";
    }

    internal static string ResidentReplyRequestedContent(string title, string question) =>
        "A administração precisa de uma informação sua sobre a solicitação:\n\n" +
        $"*\"{Shorten(title, 120)}\"*\n\n{question.Trim()}\n\n" +
        "1 - Responder agora\n2 - Responder depois";
}
