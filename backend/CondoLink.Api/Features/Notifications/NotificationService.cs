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
    ILogger<NotificationService>? logger = null,
    IRequestDraftAiService? ai = null)
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
        if (!ShouldNotifyResident(previousStatus, request.Status))
        {
            logger?.LogInformation(
                "Status notification skipped as internal or not meaningful to resident. RequestId: {RequestId}; PreviousStatus: {PreviousStatus}; NewStatus: {NewStatus}.",
                request.Id, previousStatus, request.Status);
            return;
        }
        if (statusHistoryId.HasValue && await dbContext.WhatsAppOutboundMessages
            .AsNoTracking().AnyAsync(message => message.IdempotencyKey
                == $"request-status:{statusHistoryId}", cancellationToken))
        {
            logger?.LogInformation(
                "Status notification skipped as duplicate. RequestId: {RequestId}; NewStatus: {NewStatus}.",
                request.Id, request.Status);
            return;
        }
        var content = request.Status == RequestStatus.WaitingForResident
            ? ResidentReplyRequestedContent(request.Title, reason!)
            : await StatusContentAsync(request, previousStatus, reason,
                cancellationToken);

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
            logger?.LogInformation(
                "WhatsApp notification enqueue completed. RequestId: {RequestId}; NewStatus: {NewStatus}; NotificationType: {NotificationType}.",
                request.Id, request.Status, type);
        }
    }

    private async Task<string> StatusContentAsync(DomainRequest request,
        RequestStatus previousStatus, string? reason,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return StatusChangedContent(request.Title, previousStatus,
                request.Status, reason);
        try
        {
            var result = ai is null
                ? new ResidentStatusSynthesisResult(false, null, "unavailable")
                : await ai.SynthesizeResidentStatusAsync(request.Title,
                    Describe(request.Status), reason.Trim(), cancellationToken);
            if (result.Succeeded && !string.IsNullOrWhiteSpace(result.Message))
            {
                logger?.LogInformation(
                    "Resident status synthesis succeeded. RequestId: {RequestId}; NewStatus: {NewStatus}; Model: {Model}; Delivery: {Delivery}.",
                    request.Id, request.Status, result.Model, "AI");
                return result.Message;
            }
            logger?.LogWarning(
                "Resident status synthesis used fallback. RequestId: {RequestId}; NewStatus: {NewStatus}; Outcome: {Outcome}; Delivery: {Delivery}.",
                request.Id, request.Status, result.Outcome, "Fallback");
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger?.LogWarning(
                "Resident status synthesis used fallback. RequestId: {RequestId}; NewStatus: {NewStatus}; FailureType: {FailureType}; Delivery: {Delivery}.",
                request.Id, request.Status, exception.GetType().Name, "Fallback");
        }
        return StatusChangedContent(request.Title, previousStatus,
            request.Status, reason);
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

        var isSpontaneousResidentUpdate =
            channel == MessageChannel.WhatsAppResidentUpdate;

        AddRange(recipients
            .Where(recipientId => recipientId != messageAuthorUserId)
            .Select(recipientId => new Notification(
                recipientId,
                condominiumId,
                isSpontaneousResidentUpdate
                    ? NotificationType.ResidentRequestUpdated
                    : NotificationType.RequestMessageReceived,
                isSpontaneousResidentUpdate
                    ? "Morador atualizou a solicitação"
                    : "Nova mensagem",
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
        RequestStatus.WaitingForManager => "Dar andamento",
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

    internal static bool ShouldNotifyResident(RequestStatus previousStatus,
        RequestStatus newStatus) => newStatus switch
    {
        RequestStatus.WaitingForResident => true,
        RequestStatus.WaitingForThirdParty => true,
        RequestStatus.Resolved => true,
        RequestStatus.Cancelled => true,
        RequestStatus.Open => previousStatus is RequestStatus.Resolved
            or RequestStatus.Cancelled,
        RequestStatus.InProgress => previousStatus is
            RequestStatus.WaitingForResident or RequestStatus.WaitingForThirdParty,
        _ => false
    };

    internal static string StatusChangedContent(string title,
        RequestStatus previousStatus, RequestStatus newStatus, string? reason)
    {
        var comment = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        var context = comment is null ? string.Empty : $" Contexto: {Shorten(comment, 300)}";
        return newStatus switch
        {
            RequestStatus.WaitingForThirdParty =>
                "Estamos aguardando uma etapa externa para continuar seu atendimento."
                + context,
            RequestStatus.InProgress =>
                "A administração retomou o andamento do seu atendimento." + context,
            RequestStatus.Resolved =>
                "Seu atendimento foi encerrado pela administração." + context,
            RequestStatus.Cancelled =>
                "Seu atendimento foi cancelado pela administração." + context,
            RequestStatus.Open when previousStatus is RequestStatus.Resolved
                or RequestStatus.Cancelled =>
                "Seu atendimento foi reaberto e voltará a ser analisado pela administração."
                + context,
            _ => $"Há uma atualização no atendimento *\"{Shorten(title, 80)}\"*."
                + context
        };
    }

    internal static string ResidentReplyRequestedContent(string title, string question) =>
        "A administração precisa de uma informação sua sobre a solicitação:\n\n" +
        $"*\"{Shorten(title, 120)}\"*\n\n{question.Trim()}\n\n" +
        "1 - Responder agora\n2 - Responder depois";
}
