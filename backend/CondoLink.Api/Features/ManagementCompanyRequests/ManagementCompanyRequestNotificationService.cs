using System.Net;
using CondoLink.Api.Features.Auth;
using CondoLink.Api.Features.Notifications;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.ManagementCompanyRequests;

/// <summary>
/// Resolves recipients and delivers the Lote 5 notifications (in-app + email) for
/// ManagementCompanyRequest events. The in-app <see cref="Notification"/> row is
/// always persisted first; email is best-effort on top of it and never rolls back
/// or blocks the caller — callers are expected to invoke this after the request
/// mutation has already committed, wrapped in their own try/catch.
/// </summary>
public sealed class ManagementCompanyRequestNotificationService(
    AppDbContext db,
    IEmailSender emailSender,
    IOptions<FirstAccessOptions> frontendOptions,
    ILogger<ManagementCompanyRequestNotificationService>? logger = null)
{
    private const string KeyPrefix = "management-company-request";

    /// <summary>Evento A: gestão criou a solicitação — avisa a administradora responsável pela categoria.</summary>
    public async Task NotifyCreatedAsync(ManagementCompanyRequest request, CancellationToken ct)
    {
        var recipients = await AdministradoraRecipientsAsync(request.ManagementCompanyId, request.CategoryId, ct);
        var condominiumName = await CondominiumNameAsync(request.CondominiumId, ct);
        var subject = await RequestSubjectAsync(request, ct);
        var typeLabel = TypeLabel(request.Type);
        var link = BuildLink($"/administrator/requests/{request.Id}");

        var body = NotificationService.Shorten(
            $"{condominiumName} enviou uma nova solicitação de {typeLabel}: {request.FriendlyIdentifier} — {subject}");
        var html = BuildEmailHtml(
            "A gestão enviou uma nova solicitação para sua administradora.",
            condominiumName, request.FriendlyIdentifier, typeLabel, subject, null, link, "Abrir no Comvy");

        await DispatchAsync(recipients, request.CondominiumId, request.Id, request.FriendlyIdentifier,
            NotificationType.ManagementCompanyRequestCreated, "Nova solicitação", body,
            $"{KeyPrefix}:{request.Id}:created",
            $"Nova solicitação no Comvy — {request.FriendlyIdentifier}", html, "Created", ct);
    }

    /// <summary>Evento B: administradora solicitou informação — avisa Manager + SubManager do condomínio.</summary>
    public async Task NotifyEditedAsync(ManagementCompanyRequest request, CancellationToken ct)
    {
        var recipients = await AdministradoraRecipientsAsync(request.ManagementCompanyId, request.CategoryId, ct);
        var condominiumName = await CondominiumNameAsync(request.CondominiumId, ct);
        var subject = await RequestSubjectAsync(request, ct);
        var typeLabel = TypeLabel(request.Type);
        var link = BuildLink($"/administrator/requests/{request.Id}");
        var body = NotificationService.Shorten($"{request.FriendlyIdentifier} foi editada pela gestÃ£o.");
        var html = BuildEmailHtml("A gestÃ£o editou uma solicitaÃ§Ã£o.", condominiumName,
            request.FriendlyIdentifier, typeLabel, subject, null, link, "Abrir no Comvy");
        await DispatchAsync(recipients, request.CondominiumId, request.Id, request.FriendlyIdentifier,
            NotificationType.ManagementCompanyRequestEdited, "SolicitaÃ§Ã£o editada", body,
            $"{KeyPrefix}:{request.Id}:edited:{request.UpdatedAt.Ticks}",
            $"SolicitaÃ§Ã£o editada â€” {request.FriendlyIdentifier}", html, "Edited", ct);
    }

    public async Task NotifyInformationRequestedAsync(
        ManagementCompanyRequest request, ManagementCompanyRequestHistory history, CancellationToken ct)
    {
        var recipients = await GestaoRecipientsAsync(request.CondominiumId, ct);
        var condominiumName = await CondominiumNameAsync(request.CondominiumId, ct);
        var subject = await RequestSubjectAsync(request, ct);
        var typeLabel = TypeLabel(request.Type);
        var link = BuildLink($"/management/administrator/{request.Id}");
        var summary = string.IsNullOrWhiteSpace(history.Reason) ? null : NotificationService.Shorten(history.Reason, 200);

        var body = NotificationService.Shorten(summary is null
            ? $"{request.FriendlyIdentifier} — {condominiumName}"
            : $"{request.FriendlyIdentifier} — {condominiumName}: {summary}");
        var html = BuildEmailHtml(
            "A administradora precisa de uma informação para continuar sua solicitação.",
            condominiumName, request.FriendlyIdentifier, typeLabel, subject, summary, link, "Responder no Comvy");

        await DispatchAsync(recipients, request.CondominiumId, request.Id, request.FriendlyIdentifier,
            NotificationType.ManagementCompanyRequestInfoRequested, "A administradora solicitou uma informação", body,
            $"{KeyPrefix}:{request.Id}:waiting-manager:{history.Id}",
            $"A administradora precisa de uma informação — {request.FriendlyIdentifier}", html,
            "InfoRequested", ct);
    }

    /// <summary>Evento C: gestão respondeu — avisa a administradora responsável pela categoria histórica.</summary>
    public async Task NotifyManagerRepliedAsync(
        ManagementCompanyRequest request, ManagementCompanyRequestMessage message, CancellationToken ct)
    {
        var recipients = await AdministradoraRecipientsAsync(request.ManagementCompanyId, request.CategoryId, ct);
        var condominiumName = await CondominiumNameAsync(request.CondominiumId, ct);
        var subject = await RequestSubjectAsync(request, ct);
        var typeLabel = TypeLabel(request.Type);
        var link = BuildLink($"/administrator/requests/{request.Id}");
        var reply = NotificationService.Shorten(message.Content, 200);

        var body = NotificationService.Shorten($"A gestão respondeu à solicitação {request.FriendlyIdentifier}: {reply}");
        var html = BuildEmailHtml(
            "A gestão respondeu à sua solicitação.",
            condominiumName, request.FriendlyIdentifier, typeLabel, subject, reply, link, "Abrir no Comvy");

        await DispatchAsync(recipients, request.CondominiumId, request.Id, request.FriendlyIdentifier,
            NotificationType.ManagementCompanyRequestManagerReplied, "Nova resposta da gestão", body,
            $"{KeyPrefix}:{request.Id}:management-reply:{message.Id}",
            $"Nova resposta no Comvy — {request.FriendlyIdentifier}", html, "ManagerReplied", ct);
    }

    /// <summary>Evento D: administradora concluiu — avisa Manager + SubManager do condomínio histórico.</summary>
    public async Task NotifyMessageAsync(ManagementCompanyRequest request, ManagementCompanyRequestMessage message,
        ManagementCompanyRequestActorKind senderKind, Guid senderUserId, CancellationToken ct)
    {
        var toManagement = senderKind == ManagementCompanyRequestActorKind.ManagementCompany;
        var recipients = (toManagement
                ? await GestaoRecipientsAsync(request.CondominiumId, ct)
                : await AdministradoraRecipientsAsync(request.ManagementCompanyId, request.CategoryId, ct))
            .Where(x => x.UserId != senderUserId).ToArray();
        var condominiumName = await CondominiumNameAsync(request.CondominiumId, ct);
        var subject = await RequestSubjectAsync(request, ct);
        var typeLabel = TypeLabel(request.Type);
        var link = BuildLink(toManagement ? $"/management/administrator/{request.Id}" : $"/administrator/requests/{request.Id}");
        var summary = NotificationService.Shorten(message.Content, 200);
        var title = toManagement ? "Nova mensagem da administradora" : "Nova mensagem da gestão";
        var html = BuildEmailHtml(title + ".", condominiumName, request.FriendlyIdentifier, typeLabel,
            subject, summary, link, "Abrir conversa no Comvy");
        await DispatchAsync(recipients, request.CondominiumId, request.Id, request.FriendlyIdentifier,
            toManagement ? NotificationType.ManagementCompanyRequestInfoRequested : NotificationType.ManagementCompanyRequestManagerReplied,
            title, NotificationService.Shorten($"{request.FriendlyIdentifier}: {summary}"),
            $"{KeyPrefix}:{request.Id}:message:{message.Id}", $"{title} - {request.FriendlyIdentifier}", html, "Message", ct);
    }

    public async Task NotifyCompletedAsync(ManagementCompanyRequest request, CancellationToken ct)
    {
        var recipients = await GestaoRecipientsAsync(request.CondominiumId, ct);
        var condominiumName = await CondominiumNameAsync(request.CondominiumId, ct);
        var subject = await RequestSubjectAsync(request, ct);
        var typeLabel = TypeLabel(request.Type);
        var link = BuildLink($"/management/administrator/{request.Id}");
        var label = CompletedLabel(request.Type);

        var body = NotificationService.Shorten($"{request.FriendlyIdentifier} foi concluída pela administradora.");
        var html = BuildEmailHtml($"{label}.", condominiumName, request.FriendlyIdentifier, typeLabel, subject, null, link, "Abrir no Comvy");

        await DispatchAsync(recipients, request.CondominiumId, request.Id, request.FriendlyIdentifier,
            NotificationType.ManagementCompanyRequestCompleted, label, body,
            $"{KeyPrefix}:{request.Id}:completed",
            $"{label} — {request.FriendlyIdentifier}", html, "Completed", ct);
    }

    /// <summary>Cancelamento avisa a ponta oposta ao autor.</summary>
    public async Task NotifyCancelledAsync(ManagementCompanyRequest request, string? reason, CancellationToken ct,
        ManagementCompanyRequestActorKind actorKind = ManagementCompanyRequestActorKind.Management)
    {
        var byCompany = actorKind == ManagementCompanyRequestActorKind.ManagementCompany;
        var recipients = byCompany ? await GestaoRecipientsAsync(request.CondominiumId, ct) : await AdministradoraRecipientsAsync(request.ManagementCompanyId, request.CategoryId, ct);
        var condominiumName = await CondominiumNameAsync(request.CondominiumId, ct);
        var subject = await RequestSubjectAsync(request, ct);
        var typeLabel = TypeLabel(request.Type);
        var link = BuildLink(byCompany ? $"/management/administrator/{request.Id}" : $"/administrator/requests/{request.Id}");
        var summary = string.IsNullOrWhiteSpace(reason) ? null : NotificationService.Shorten(reason, 200);

        var actorLabel = byCompany ? "administradora" : "gestão";
        var body = NotificationService.Shorten(summary is null
            ? $"{request.FriendlyIdentifier} foi cancelada pela {actorLabel}."
            : $"{request.FriendlyIdentifier} foi cancelada pela {actorLabel}: {summary}");
        var html = BuildEmailHtml(
            $"A {actorLabel} cancelou esta solicitação.",
            condominiumName, request.FriendlyIdentifier, typeLabel, subject, summary, link, "Abrir no Comvy");

        await DispatchAsync(recipients, request.CondominiumId, request.Id, request.FriendlyIdentifier,
            NotificationType.ManagementCompanyRequestCancelled, "Solicitação cancelada", body,
            $"{KeyPrefix}:{request.Id}:cancelled",
            $"Solicitação cancelada — {request.FriendlyIdentifier}", html, "Cancelled", ct);
    }

    /// <summary>
    /// Fans a single event out to every recipient: skips ones already notified for
    /// this exact key, persists the in-app notification, then best-effort emails.
    /// </summary>
    private async Task DispatchAsync(
        IReadOnlyCollection<Recipient> recipients,
        Guid condominiumId,
        Guid managementCompanyRequestId,
        string friendlyId,
        NotificationType type,
        string title,
        string body,
        string idempotencyKeyBase,
        string emailSubject,
        string emailHtml,
        string eventLabel,
        CancellationToken ct)
    {
        if (recipients.Count == 0)
        {
            logger?.LogInformation(
                "ManagementCompanyRequest notification skipped: no eligible recipient. ManagementCompanyRequestId: {RequestId}; FriendlyId: {FriendlyId}; Event: {Event}.",
                managementCompanyRequestId, friendlyId, eventLabel);
            return;
        }

        var sent = 0;
        foreach (var recipient in recipients)
        {
            var key = $"{idempotencyKeyBase}:{recipient.UserId}";
            var alreadyNotified = await db.Notifications.AsNoTracking()
                .AnyAsync(n => n.IdempotencyKey == key, ct);
            if (alreadyNotified)
            {
                logger?.LogInformation(
                    "ManagementCompanyRequest notification skipped: duplicate. ManagementCompanyRequestId: {RequestId}; FriendlyId: {FriendlyId}; Event: {Event}; RecipientUserId: {RecipientUserId}.",
                    managementCompanyRequestId, friendlyId, eventLabel, recipient.UserId);
                continue;
            }

            var notification = new Notification(
                recipient.UserId, condominiumId, type, title, body,
                requestId: null, managementCompanyRequestId: managementCompanyRequestId, idempotencyKey: key);
            db.Notifications.Add(notification);
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                // Unique index race: another writer already inserted this exact key.
                db.Entry(notification).State = EntityState.Detached;
                logger?.LogInformation(
                    "ManagementCompanyRequest notification race handled as duplicate. ManagementCompanyRequestId: {RequestId}; FriendlyId: {FriendlyId}; Event: {Event}; RecipientUserId: {RecipientUserId}.",
                    managementCompanyRequestId, friendlyId, eventLabel, recipient.UserId);
                continue;
            }

            sent++;
            if (!recipient.EmailDeliveryEnabled || string.IsNullOrWhiteSpace(recipient.Email))
                continue;

            try
            {
                await emailSender.SendAsync(recipient.Email, emailSubject, emailHtml, ct);
            }
            catch (Exception exception)
            {
                logger?.LogWarning(exception,
                    "ManagementCompanyRequest notification email failed. ManagementCompanyRequestId: {RequestId}; FriendlyId: {FriendlyId}; Event: {Event}; RecipientUserId: {RecipientUserId}.",
                    managementCompanyRequestId, friendlyId, eventLabel, recipient.UserId);
            }
        }

        logger?.LogInformation(
            "ManagementCompanyRequest notification dispatched. ManagementCompanyRequestId: {RequestId}; FriendlyId: {FriendlyId}; Event: {Event}; RecipientCount: {RecipientCount}; Sent: {Sent}.",
            managementCompanyRequestId, friendlyId, eventLabel, recipients.Count, sent);
    }

    /// <summary>
    /// Acessos ativos da administradora responsáveis pela categoria — sempre a partir
    /// dos valores históricos gravados na Request, nunca do vínculo atual do condomínio.
    /// </summary>
    private Task<Recipient[]> AdministradoraRecipientsAsync(
        Guid managementCompanyId, Guid categoryId, CancellationToken ct) =>
        (from responsible in db.ManagementCompanyRequestCategoryResponsibles.AsNoTracking()
         join employee in db.ManagementCompanyEmployees.AsNoTracking()
             on responsible.ManagementCompanyEmployeeId equals employee.Id
         join user in db.Set<ApplicationUser>().AsNoTracking() on employee.UserId equals user.Id
         where responsible.ManagementCompanyRequestCategoryId == categoryId
            && employee.ManagementCompanyId == managementCompanyId
            && employee.IsActive
            && user.IsActive
         select new Recipient(user.Id, user.Email, user.EmailDeliveryEnabled))
        .Distinct()
        .ToArrayAsync(ct);

    /// <summary>Manager + SubManager ativos do condomínio, deduplicados por usuário.</summary>
    private Task<Recipient[]> GestaoRecipientsAsync(Guid condominiumId, CancellationToken ct) =>
        db.CondominiumMemberships.AsNoTracking()
            .Where(membership => membership.CondominiumId == condominiumId
                && membership.IsActive && membership.EndedAt == null)
            .Join(
                db.CondominiumMembershipRoles.AsNoTracking().Where(role =>
                    (role.Role == CondominiumRole.Manager || role.Role == CondominiumRole.SubManager)
                    && role.IsActive && role.RevokedAt == null),
                membership => membership.Id, role => role.CondominiumMembershipId,
                (membership, _) => membership.UserId)
            .Distinct()
            .Join(
                db.Set<ApplicationUser>().AsNoTracking().Where(user => user.IsActive),
                userId => userId, user => user.Id,
                (_, user) => new Recipient(user.Id, user.Email, user.EmailDeliveryEnabled))
            .ToArrayAsync(ct);

    private Task<string> CondominiumNameAsync(Guid condominiumId, CancellationToken ct) =>
        db.Condominiums.AsNoTracking()
            .Where(condominium => condominium.Id == condominiumId)
            .Select(condominium => condominium.Name)
            .SingleAsync(ct);

    private Task<string> RequestSubjectAsync(ManagementCompanyRequest request, CancellationToken ct) => request.Type switch
    {
        ManagementCompanyRequestType.Fine => db.ManagementCompanyFineRequests.AsNoTracking()
            .Where(x => x.RequestId == request.Id).Select(x => x.Nature).SingleAsync(ct),
        ManagementCompanyRequestType.Payment => db.ManagementCompanyPaymentRequests.AsNoTracking()
            .Where(x => x.RequestId == request.Id).Select(x => x.Nature).SingleAsync(ct),
        _ => db.ManagementCompanyGeneralQuestionRequests.AsNoTracking()
            .Where(x => x.RequestId == request.Id).Select(x => x.Theme).SingleAsync(ct)
    };

    private string BuildLink(string relativePath) =>
        $"{frontendOptions.Value.FrontendBaseUrl.TrimEnd('/')}{relativePath}";

    private static string TypeLabel(ManagementCompanyRequestType type) => type switch
    {
        ManagementCompanyRequestType.Fine => "Multa",
        ManagementCompanyRequestType.Payment => "Pagamento",
        ManagementCompanyRequestType.GeneralQuestion => "Dúvida",
        _ => type.ToString()
    };

    private static string CompletedLabel(ManagementCompanyRequestType type) => type switch
    {
        ManagementCompanyRequestType.Fine => "Multa processada",
        ManagementCompanyRequestType.Payment => "Pagamento efetuado",
        ManagementCompanyRequestType.GeneralQuestion => "Dúvida respondida",
        _ => "Solicitação concluída"
    };

    private static string BuildEmailHtml(
        string heading, string condominiumName, string friendlyId, string typeLabel,
        string subject, string? message, string link, string ctaLabel)
    {
        var html = $"<p>{WebUtility.HtmlEncode(heading)}</p>"
            + $"<p><strong>Condomínio:</strong> {WebUtility.HtmlEncode(condominiumName)}<br/>"
            + $"<strong>Identificador:</strong> {WebUtility.HtmlEncode(friendlyId)}<br/>"
            + $"<strong>Tipo:</strong> {WebUtility.HtmlEncode(typeLabel)}<br/>"
            + $"<strong>Assunto:</strong> {WebUtility.HtmlEncode(subject)}</p>";
        if (!string.IsNullOrWhiteSpace(message))
            html += $"<p>{WebUtility.HtmlEncode(message)}</p>";
        html += $"<p><a href=\"{WebUtility.HtmlEncode(link)}\">{WebUtility.HtmlEncode(ctaLabel)}</a></p>";
        return html;
    }

    private sealed record Recipient(Guid UserId, string? Email, bool EmailDeliveryEnabled);
}
