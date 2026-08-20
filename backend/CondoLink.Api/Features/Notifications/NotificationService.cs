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
        if (managerIds.Length != 1)
        {
            logger?.LogInformation(
                "Manager new request WhatsApp notification skipped. RequestId: {RequestId}; CondominiumId: {CondominiumId}; ActiveManagerCount: {ActiveManagerCount}; Reason: {Reason}.",
                request.Id, request.CondominiumId, managerIds.Length,
                managerIds.Length == 0 ? "ManagerNotFound" : "ManagerAmbiguous");
            return;
        }
        if (whatsApp is null)
        {
            logger?.LogInformation(
                "Manager new request WhatsApp notification skipped. RequestId: {RequestId}; CondominiumId: {CondominiumId}; ActiveManagerCount: {ActiveManagerCount}; Reason: {Reason}.",
                request.Id, request.CondominiumId, managerIds.Length,
                "WhatsAppDispatcherUnavailable");
            return;
        }
        var residentName = await dbContext
            .Set<CondoLink.Infrastructure.Identity.ApplicationUser>()
            .AsNoTracking().Where(x => x.Id == request.AuthorUserId)
            .Select(x => x.FullName).SingleAsync(cancellationToken);
        var location = request.TargetUnitId.HasValue
            ? await (from unit in dbContext.Units.AsNoTracking()
                where unit.Id == request.TargetUnitId.Value
                join block in dbContext.CondominiumBlocks.AsNoTracking()
                    on unit.BlockId equals block.Id into blocks
                from block in blocks.DefaultIfEmpty()
                select new
                {
                    Unit = unit.Identifier,
                    Block = block == null ? null : block.Identifier
                }).SingleOrDefaultAsync(cancellationToken)
            : null;
        var condominiumName = await dbContext.Condominiums.AsNoTracking()
            .Where(x => x.Id == request.CondominiumId)
            .Select(x => x.Name).SingleAsync(cancellationToken);
        var content = ManagerNewRequestContent(condominiumName, residentName,
            location?.Unit, location?.Block, request.Title);
        var managerId = managerIds[0];
        await whatsApp.EnqueueForUserAsync(request.Id, managerId,
            WhatsAppNotificationType.ManagerNewRequest,
            $"manager-new-request:{request.Id}:{managerId}", content, null,
            cancellationToken);
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
        // Closure is still awaiting the resident's decision. Keep both choices
        // deterministic and let RequestClosureService remain their authority.
        if (request.Status == RequestStatus.WaitingForResidentClosure)
            return StatusChangedContent(request.Title, previousStatus,
                request.Status, reason);

        // Cancellation is an administrative fact. Preserve the manager's
        // comment verbatim (within the existing safe length limit) instead of
        // making delivery depend on an optional AI synthesis.
        if (request.Status == RequestStatus.Cancelled)
            return StatusChangedContent(request.Title, previousStatus,
                request.Status, reason);
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
                var content = TerminalContent(request.Status, result.Message);
                logger?.LogInformation(
                    "Resident status synthesis succeeded. RequestId: {RequestId}; NewStatus: {NewStatus}; Model: {Model}; Delivery: {Delivery}.",
                    request.Id, request.Status, result.Model, "AI");
                return content;
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
        var fallbackReason = request.Status == RequestStatus.Resolved
            ? null : reason;
        return StatusChangedContent(request.Title, previousStatus,
            request.Status, fallbackReason);
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
            .Join(
                dbContext.Set<CondoLink.Infrastructure.Identity.ApplicationUser>()
                    .AsNoTracking().Where(user => user.IsActive),
                userId => userId,
                user => user.Id,
                (userId, _) => userId)
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
        RequestStatus.WaitingForResidentClosure => "ConcluÃ­do pela administraÃ§Ã£o â€” aguardando sua confirmaÃ§Ã£o",
        RequestStatus.Resolved => "Resolvida",
        RequestStatus.Cancelled => "Cancelada",
        _ => status.ToString()
    };

    internal static WhatsAppNotificationType StatusNotificationType(
        RequestStatus previousStatus, RequestStatus currentStatus) => currentStatus switch
    {
        RequestStatus.WaitingForResident => WhatsAppNotificationType.InformationRequested,
        RequestStatus.WaitingForResidentClosure => WhatsAppNotificationType.StatusChanged,
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
        RequestStatus.WaitingForResidentClosure => true,
        RequestStatus.Resolved => true,
        RequestStatus.Cancelled => true,
        RequestStatus.Open => previousStatus is RequestStatus.Resolved
            or RequestStatus.Cancelled,
        RequestStatus.InProgress => false,
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
            RequestStatus.WaitingForResidentClosure =>
                "*A administraÃ§Ã£o informou que sua solicitaÃ§Ã£o foi concluÃ­da:*\n\n"
                + (comment ?? "A atuaÃ§Ã£o da administraÃ§Ã£o foi concluÃ­da.")
                + "\n\nEstÃ¡ tudo certo?\n\n1 - Sim, finalizar atendimento\n2 - Ainda tenho uma dÃºvida",
            RequestStatus.InProgress =>
                "A administração retomou o andamento do seu atendimento." + context,
            RequestStatus.Resolved =>
                "*Seu atendimento foi finalizado.*\n\n"
                + (comment is null
                    ? "A administração concluiu esta solicitação."
                    : Shorten(comment, 300)),
            RequestStatus.Cancelled =>
                "*Seu atendimento foi cancelado.*\n\n"
                + (comment is null
                    ? "A administração encerrou esta solicitação."
                    : Shorten(comment, 300)),
            RequestStatus.Open when previousStatus is RequestStatus.Resolved
                or RequestStatus.Cancelled =>
                "Seu atendimento foi reaberto e voltará a ser analisado pela administração."
                + context,
            _ => $"Há uma atualização no atendimento *\"{Shorten(title, 80)}\"*."
                + context
        };
    }

    private static string TerminalContent(RequestStatus status, string message)
    {
        if (status is not (RequestStatus.Resolved or RequestStatus.Cancelled))
            return message.Trim();

        var heading = status == RequestStatus.Resolved
            ? "*Seu atendimento foi finalizado.*"
            : "*Seu atendimento foi cancelado.*";
        var trimmed = message.Trim();
        if (trimmed.StartsWith(heading, StringComparison.OrdinalIgnoreCase))
            trimmed = trimmed[heading.Length..].TrimStart('\r', '\n', ' ');
        return string.IsNullOrWhiteSpace(trimmed)
            ? StatusChangedContent(string.Empty, RequestStatus.InProgress, status, null)
            : $"{heading}\n\n{trimmed}";
    }

    internal static string ResidentReplyRequestedContent(string title, string question) =>
        "A administração precisa de uma informação sua sobre a solicitação:\n\n" +
        $"*\"{Shorten(title, 120)}\"*\n\n{question.Trim()}\n\n" +
        "1 - Responder agora\n2 - Responder depois";

    internal static string ManagerNewRequestContent(string condominiumName,
        string residentName, string? unit, string? block, string? title)
    {
        var location = string.IsNullOrWhiteSpace(unit)
            ? "Unidade não informada"
            : $"Apto {unit.Trim()}";
        if (!string.IsNullOrWhiteSpace(block))
        {
            var blockLabel = block.Trim().StartsWith("Bloco ",
                StringComparison.OrdinalIgnoreCase)
                ? block.Trim()[6..].Trim() : block.Trim();
            location += $" · Bloco {blockLabel}";
        }
        var subject = string.IsNullOrWhiteSpace(title)
            ? "Solicitação sem assunto" : Shorten(title, 160);
        return $"*Nova solicitação recebida*\n\n{Shorten(condominiumName, 160)}\n"
            + $"{Shorten(residentName, 160)} · {location}\nAssunto: {subject}";
    }
}
