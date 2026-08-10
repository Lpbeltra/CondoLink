using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class WhatsAppNotificationDispatcher(
    AppDbContext db,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppNotificationDispatcher> logger)
{
    internal const int DiagnosticsVersion = 2;

    public async Task EnqueueAsync(
        Guid requestId,
        WhatsAppNotificationType type,
        string idempotencyKey,
        string content,
        Guid? requestMessageId,
        CancellationToken ct)
        => await EnqueueCoreAsync(requestId, null, type, idempotencyKey,
            content, requestMessageId, ct);

    public async Task EnqueueForUserAsync(
        Guid requestId, Guid userId, WhatsAppNotificationType type,
        string idempotencyKey, string content, Guid? requestMessageId,
        CancellationToken ct) => await EnqueueCoreAsync(requestId, userId,
            type, idempotencyKey, content, requestMessageId, ct);

    private async Task EnqueueCoreAsync(
        Guid requestId, Guid? recipientUserId, WhatsAppNotificationType type,
        string idempotencyKey, string content, Guid? requestMessageId,
        CancellationToken ct)
    {
        var stage = "loading_request";
        var condominiumId = Guid.Empty;
        var messagesCreated = 0;

        void Log(string decision, string reason, int messagesCreated) =>
            logger.LogInformation(
                "WhatsApp dispatcher diagnostic. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}; MessagesCreated: {MessagesCreated}; DiagnosticsVersion: {DiagnosticsVersion}",
                requestId, condominiumId, type, decision, reason, messagesCreated,
                DiagnosticsVersion);

        Log("Started", "DispatcherEntered", 0);
        try
        {
            var request = await db.Requests.AsNoTracking()
                .SingleOrDefaultAsync(x => x.Id == requestId, ct);
            if (request is null)
            {
                Log("Skipped", "RequestNotFound", 0);
                return;
            }
            condominiumId = request.CondominiumId;
            var userId = recipientUserId ?? request.AuthorUserId;

            stage = "checking_idempotency";
            if (await db.WhatsAppOutboundMessages.AsNoTracking()
                .AnyAsync(x => x.IdempotencyKey == idempotencyKey, ct))
            {
                Log("Skipped", "DuplicateIdempotencyKey", 0);
                return;
            }

            stage = "checking_global_configuration";
            var settings = options.Value;

            stage = "checking_condominium";
            var condominium = await db.Condominiums.AsNoTracking()
                .Where(x => x.Id == request.CondominiumId)
                .Select(x => new { x.WhatsAppUpdatesEnabled }).SingleOrDefaultAsync(ct);
            if (condominium is null)
            {
                Log("Skipped", "CondominiumNotFound", 0);
                return;
            }

            stage = "checking_user";
            var user = await db.Set<ApplicationUser>().AsNoTracking()
                .Where(x => x.Id == userId)
                .Select(x => new
                {
                    x.IsActive,
                    x.NormalizedPhoneNumber,
                    x.ReceiveWhatsAppUpdates
                }).SingleOrDefaultAsync(ct);
            stage = "checking_phone";
            var phone = user?.NormalizedPhoneNumber;
            stage = "checking_membership";
            var activeMembership = user is not null && await db.CondominiumMemberships
                .AsNoTracking().AnyAsync(x => x.UserId == userId
                    && x.CondominiumId == request.CondominiumId
                    && x.IsActive && x.EndedAt == null, ct);
            var ambiguous = phone is not null && await db.Set<ApplicationUser>()
                .AsNoTracking()
                .CountAsync(x => x.IsActive && x.NormalizedPhoneNumber == phone, ct) > 1;

            stage = "resolving_send_mode";
            var lastInboundAt = phone is null ? null : await db.WhatsAppInboundMessages
                .AsNoTracking().Where(x => x.PhoneNumber == phone)
                .MaxAsync(x => (DateTime?)x.ReceivedAt, ct);
            var sessionOpen = lastInboundAt >= DateTime.UtcNow.AddHours(-24);
            var template = TemplateFor(type, settings.Templates);
            var mode = sessionOpen ? WhatsAppSendMode.SessionText : WhatsAppSendMode.Template;
            var skipReason = !settings.Enabled ? "Integração desabilitada."
                : !condominium.WhatsAppUpdatesEnabled ? "Condomínio desabilitado."
                : user is null || !user.IsActive ? "Usuário inativo."
                : !user.ReceiveWhatsAppUpdates ? "Preferência desabilitada."
                : phone is null ? "Telefone inválido."
                : ambiguous ? "Telefone ambíguo."
                : !activeMembership ? "Vínculo inativo."
                : mode == WhatsAppSendMode.Template
                    && (string.IsNullOrWhiteSpace(template.Name)
                        || string.IsNullOrWhiteSpace(template.Language))
                    ? "Template não configurado."
                    : null;
            var technicalReason = !settings.Enabled ? "GlobalFeatureDisabled"
                : !condominium.WhatsAppUpdatesEnabled ? "CondominiumFeatureDisabled"
                : user is null ? "UserNotFound"
                : !user.IsActive ? "UserInactive"
                : !user.ReceiveWhatsAppUpdates ? "UserPreferenceDisabled"
                : phone is null ? "PhoneMissingOrInvalid"
                : ambiguous ? "PhoneAmbiguous"
                : !activeMembership ? "MembershipInvalid"
                : mode == WhatsAppSendMode.Template
                    && (string.IsNullOrWhiteSpace(template.Name)
                        || string.IsNullOrWhiteSpace(template.Language))
                    ? "TemplateNotConfigured"
                    : "Eligible";
            var status = skipReason is null
                ? WhatsAppOutboundStatus.Pending : WhatsAppOutboundStatus.Skipped;
            stage = "creating_outbound";
            db.WhatsAppOutboundMessages.Add(new WhatsAppOutboundMessage(
                request.Id, requestMessageId, userId,
                request.CondominiumId, phone ?? string.Empty, type, mode,
                idempotencyKey, content, template.Name, template.Language,
                DateTime.UtcNow, status, skipReason));
            messagesCreated = 1;
            Log("Persisting", "OutboundMessageCreated", 1);
            if (skipReason is null && type == WhatsAppNotificationType.InformationRequested)
            {
                stage = "updating_session";
                var now = DateTime.UtcNow;
                var expires = now.AddMinutes(Math.Clamp(
                    settings.SessionExpirationMinutes, 30, 30));
                var session = await db.WhatsAppSessions.SingleOrDefaultAsync(
                    x => x.PhoneNumber == phone, ct);
                if (session is null)
                {
                    session = new WhatsAppSession(phone!, now, expires);
                    session.Identify(request.AuthorUserId);
                    db.WhatsAppSessions.Add(session);
                }
                // Do not destroy an unrelated flow already in progress. The
                // outbound row remains the server-side correlation fallback for
                // the template button in that case.
                if (session.State is WhatsAppConversationState.MainMenu
                    or WhatsAppConversationState.Ended
                    or WhatsAppConversationState.AwaitingResidentReplyChoice)
                    session.OfferResidentReply(request.Id, now, expires);
            }
            stage = "saving_outbound";
            try
            {
                await db.SaveChangesAsync(ct);
            }
            catch (DbUpdateException)
            {
                if (await db.WhatsAppOutboundMessages.AsNoTracking()
                    .AnyAsync(x => x.IdempotencyKey == idempotencyKey, ct))
                {
                    Log("Skipped", "DuplicateDetectedDuringSave", 0);
                    return;
                }
                throw;
            }
            Log(skipReason is null ? "Enqueued" : "Skipped", technicalReason, 1);
            stage = "completed";
            Log("Finished", "Completed", 1);
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "WhatsApp dispatcher diagnostic. RequestId: {RequestId}; CondominiumId: {CondominiumId}; NotificationType: {NotificationType}; Decision: {Decision}; Reason: {Reason}; MessagesCreated: {MessagesCreated}; ExceptionType: {ExceptionType}; Stage: {Stage}; DiagnosticsVersion: {DiagnosticsVersion}",
                requestId, condominiumId, type, "Failed", "DispatcherException",
                messagesCreated,
                exception.GetType().Name, stage, DiagnosticsVersion);
            throw;
        }
    }

    private static WhatsAppTemplateDefinition TemplateFor(
        WhatsAppNotificationType type, WhatsAppTemplateOptions templates) =>
        type switch
        {
            WhatsAppNotificationType.AdministrationMessage =>
                templates.AdministrationMessage,
            WhatsAppNotificationType.InformationRequested =>
                templates.InformationRequested,
            WhatsAppNotificationType.ManagerNewRequest =>
                templates.ManagerNewRequest,
            _ => templates.StatusChanged
        };
}
