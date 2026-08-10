using System.Globalization;
using System.Text;
using System.Text.Json;
using CondoLink.Api.Features.Categories;
using CondoLink.Api.Features.Notifications;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Identity;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using DomainRequest = CondoLink.Domain.Entities.Request;

namespace CondoLink.Api.Features.WhatsApp;

public sealed class WhatsAppConversationService(
    AppDbContext db,
    IWhatsAppClient client,
    LocalFileStorage storage,
    IRequestDraftAiService requestDraftAi,
    IResidentReplyAiService residentReplyAi,
    IWhatsAppAudioTranscriptionService audioTranscription,
    CondoLink.Api.Features.Requests.ResidentReplyService residentReplies,
    NotificationService notifications,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppConversationService> logger,
    AdministrativeResidentRegistrationService administrativeResidents,
    AdministrativeResidentLookupService administrativeResidentLookup,
    AdministrativeResidentMembershipMutationService administrativeResidentMutation,
    RequestCategoryResolver requestCategories,
    CondoLink.Api.Features.Requests.RequestAiAnalysisRefresher? analysisRefresher = null)
{
    private const string FallbackRequestTitle = "Solicitação recebida pelo WhatsApp";
    private const string AiReviewSource = "ai";
    private const string FallbackReviewSource = "fallback";
    private const int OwnRequestsPageSize = 5;
    private const string IdentificationFailure =
        "Não consegui identificar seu cadastro. Entre em contato com a administração do condomínio para verificar seu número.";
    private const string ResidentialContextFailure =
        "Seu cadastro foi identificado, mas não há uma unidade residencial ativa disponível para este atendimento.";

    public async Task ProcessAsync(NormalizedWhatsAppMessage message, CancellationToken ct)
    {
        var phone = PhoneNumberNormalizer.NormalizeWhatsAppIdentifier(message.PhoneNumber);
        if (phone is null || string.IsNullOrWhiteSpace(message.ExternalMessageId)) return;
        var canonicalPhone = PhoneNumberNormalizer.NormalizeBrazilian(phone);
        if (canonicalPhone is null) return;

        var inbound = await db.WhatsAppInboundMessages
            .SingleOrDefaultAsync(x => x.ExternalMessageId == message.ExternalMessageId, ct);
        if (inbound?.ProcessedAt is not null)
        {
            logger.LogInformation("Duplicate WhatsApp message acknowledged idempotently.");
            return;
        }

        if (inbound is null)
        {
            inbound = new WhatsAppInboundMessage(
                message.ExternalMessageId, phone, message.MessageType, message.Text,
                message.ProviderTimestamp);
            db.WhatsAppInboundMessages.Add(inbound);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException)
            {
                db.Entry(inbound).State = EntityState.Detached;
                inbound = await db.WhatsAppInboundMessages
                    .SingleOrDefaultAsync(x => x.ExternalMessageId == message.ExternalMessageId, ct);
                if (inbound?.ProcessedAt is not null) return;
                if (inbound is null) throw;
            }
        }

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(Math.Clamp(options.Value.SessionExpirationMinutes, 30, 30));
        var identifiedUser = await ResolveUser(canonicalPhone, ct);
        var session = await db.WhatsAppSessions.SingleOrDefaultAsync(x => x.PhoneNumber == phone, ct);
        var isNewSession = session is null;
        if (session is null)
        {
            session = new WhatsAppSession(phone, now, expires);
            db.WhatsAppSessions.Add(session);
            logger.LogInformation("WhatsApp session created for phone {Phone}.", PhoneNumberNormalizer.Mask(phone));
            logger.LogInformation("WhatsApp session initial state assigned: {State}.", session.State);
        }

        string response;
        string result;
        var initialSessionState = session.State;
        var initialRequestIdPresent = session.RequestId.HasValue;
        var hasAdministrativeAccess = identifiedUser is not null
            && await HasAdministrativeAccess(identifiedUser.Id, ct);
        var administrativeText = message.Text;
        AdministrativeWhatsAppResponse? administrativeAudioFailure = null;
        if (identifiedUser is not null && hasAdministrativeAccess
            && message.MessageType == "audio")
        {
            var transcription = await TranscribeAdministrativeAudio(message, ct);
            if (transcription.Text is not null)
                administrativeText = transcription.Text;
            else
                administrativeAudioFailure = new(transcription.Error!,
                    "admin_audio_transcription_failed");
        }
        var administrativeResponse = identifiedUser is null ? null
            : administrativeAudioFailure ?? await administrativeResidents.TryHandleAsync(
                identifiedUser, session, administrativeText, now, expires, ct);
        administrativeResponse ??= identifiedUser is null ? null
            : await administrativeResidentMutation.TryHandleAsync(
                identifiedUser, session, administrativeText, now, expires, ct);
        administrativeResponse ??= identifiedUser is null ? null
            : await administrativeResidentLookup.TryHandleAsync(
                identifiedUser, session, administrativeText, now, expires, ct);
        Guid? identifiedUserId = identifiedUser?.Id;
        if (administrativeResponse is not null)
        {
            identifiedUserId = identifiedUser!.Id;
            response = administrativeResponse.Text;
            result = administrativeResponse.Result;
            if (result == "admin_command_forbidden")
                logger.LogWarning("AdministrativeAuthorizationRejected for WhatsApp user.");
            else
                logger.LogInformation("AdministrativeContextResolved for WhatsApp user.");
        }
        else if (identifiedUser is null)
        {
            session.InvalidateIdentity(now, expires);
            logger.LogWarning("UserResolved: false. WhatsApp session state: {State}.", session.State);
            response = IdentificationFailure;
            result = "identity_not_resolved";
        }
        else
        {
            var identity = await ResolveResidentialContext(identifiedUser, ct);
            if (identity is null)
            {
                session.Restart(now, expires);
                logger.LogInformation("ResidentialContextUnavailable for identified WhatsApp user.");
                response = hasAdministrativeAccess
                    ? AdministrativeFallback()
                    : ResidentialContextFailure;
                result = hasAdministrativeAccess
                    ? "administrative_action_not_recognized"
                    : "residential_context_unavailable";
            }
            else
            {
                try
                {
                    session.ResolveContext(identity.UserId, identity.CondominiumId, identity.UnitId);
                    (response, result) = await Respond(
                        session, identity, message, now, expires, isNewSession, ct);
                }
                catch (DbUpdateConcurrencyException)
                {
                    throw;
                }
                catch
                {
                    await DiscardDraftAttachments(session, ct);
                    session.End(now);
                    await db.SaveChangesAsync(ct);
                    throw;
                }
            }
        }

        inbound.Complete(identifiedUserId, result, now);
        var quickReplyRecognized = ResidentReplyButtonChoice(message) is not null;
        var requirementFound = quickReplyRecognized
            && result is "collecting_resident_reply" or "resident_reply_deferred";
        var routingDecision = result switch
        {
            "collecting_resident_reply" when quickReplyRecognized =>
                "CollectingResidentReply",
            "resident_reply_deferred" when quickReplyRecognized =>
                "DeferResidentReply",
            "resident_reply_correlation_failed" => "CorrelationFailed",
            _ => result
        };
        logger.LogInformation(
            "WhatsApp inbound routing diagnostic. ParsedMessageType: {ParsedMessageType}; QuickReplyRecognized: {QuickReplyRecognized}; QuickReplyId: {QuickReplyId}; SessionState: {SessionState}; RequestIdPresent: {RequestIdPresent}; RequirementFound: {RequirementFound}; RoutingDecision: {RoutingDecision}.",
            message.ParsedMessageType,
            quickReplyRecognized,
            KnownResidentReplyId(message),
            initialSessionState,
            initialRequestIdPresent,
            requirementFound,
            routingDecision);
        logger.LogInformation("WhatsApp inbound final processing result: {ProcessingResult}.", result);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("WhatsApp session concurrency conflict for phone {Phone}.", PhoneNumberNormalizer.Mask(phone));
            throw;
        }

        var send = await client.SendTextAsync(phone, response, ct);
        logger.Log(send.Succeeded ? LogLevel.Information : LogLevel.Warning,
            "WhatsApp message processed with result {Result} for phone {Phone}.",
            result, PhoneNumberNormalizer.Mask(phone));
    }

    private async Task<bool> HasAdministrativeAccess(Guid userId, CancellationToken ct)
    {
        var platformAdmin = await db.UserRoles.AsNoTracking()
            .Join(db.Roles.AsNoTracking(), link => link.RoleId, role => role.Id,
                (link, role) => new { link.UserId, role.NormalizedName })
            .AnyAsync(x => x.UserId == userId
                && x.NormalizedName == CondoLink.Infrastructure.DependencyInjection
                    .PlatformAdminRole.ToUpper(), ct);
        if (platformAdmin) return true;
        return await (from membership in db.CondominiumMemberships.AsNoTracking()
            join role in db.CondominiumMembershipRoles.AsNoTracking()
                on membership.Id equals role.CondominiumMembershipId
            where membership.UserId == userId && membership.IsActive
                && membership.EndedAt == null && role.Role == CondominiumRole.Manager
                && role.IsActive && role.RevokedAt == null
            select membership.Id).AnyAsync(ct);
    }

    private async Task<(string? Text, string? Error)> TranscribeAdministrativeAudio(
        NormalizedWhatsAppMessage message, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.MediaId))
            return (null, "Não consegui acessar esse áudio. Envie novamente ou escreva a consulta.");
        var media = await client.DownloadMediaAsync(message.MediaId, ct);
        if (!media.Succeeded || media.Content is null)
            return (null, "Não consegui acessar esse áudio. Envie novamente ou escreva a consulta.");
        var contentType = media.ContentType ?? message.MediaContentType
            ?? "application/octet-stream";
        var extension = AttachmentPolicy.PreferredExtension(contentType);
        var fileName = string.IsNullOrWhiteSpace(message.FileName)
            ? $"audio{extension}" : message.FileName;
        var validation = AttachmentPolicy.Validate(
            fileName, media.Content.LongLength, contentType);
        if (validation.Error is not null) return (null, validation.Error);
        var transcription = await audioTranscription.TranscribeAsync(
            media.Content, validation.Name!, validation.ContentType!, ct);
        return transcription.Succeeded && !string.IsNullOrWhiteSpace(transcription.Text)
            ? (transcription.Text.Trim(), null)
            : (null, "Não consegui transcrever esse áudio. Envie novamente ou escreva a consulta.");
    }

    private static string AdministrativeFallback() =>
        "Não consegui identificar o que você deseja fazer.\n\n"
        + "Você pode pedir, por exemplo:\n"
        + "• \"Cadastrar morador\"\n"
        + "• \"Me passe os moradores do bloco 1 apto 1201\"\n"
        + "• \"Qual o telefone da Tatiana do 1201/1?\"";

    private async Task<ApplicationUser?> ResolveUser(
        string canonicalPhone, CancellationToken ct)
    {
        var users = await db.Set<ApplicationUser>()
            .Where(x => x.NormalizedPhoneNumber != null
                && x.NormalizedPhoneNumber == canonicalPhone)
            .Take(2).ToArrayAsync(ct);
        var activeUsers = users.Where(x => x.IsActive).DistinctBy(x => x.Id).Take(2).ToArray();
        if (activeUsers.Length == 0)
        {
            if (users.Length == 1)
                logger.LogWarning("WhatsApp user is inactive.");
            else
                logger.LogInformation("No WhatsApp phone match found.");
            return null;
        }
        if (activeUsers.Length != 1)
        {
            logger.LogWarning("Ambiguous WhatsApp phone match found.");
            return null;
        }
        var user = activeUsers[0];
        logger.LogInformation("UserResolved by exact canonical WhatsApp phone.");
        return user;
    }

    private async Task<ResolvedIdentity?> ResolveResidentialContext(
        ApplicationUser user, CancellationToken ct)
    {
        var unitLinks = await db.UnitMemberships.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.IsActive && x.EndedAt == null)
            .Select(x => new { x.UnitId, x.IsResident, x.IsPrimaryResidence })
            .ToArrayAsync(ct);
        if (unitLinks.Length == 0)
        {
            logger.LogInformation("NoResidentialMembership for identified WhatsApp user.");
            return null;
        }

        var residentialLinks = unitLinks.Where(x => x.IsResident).ToArray();
        if (residentialLinks.Length == 0)
        {
            logger.LogInformation("ResidentialContextUnavailable: no resident unit membership.");
            return null;
        }

        var unitIds = residentialLinks.Select(x => x.UnitId).Distinct().ToArray();
        var units = await db.Units.AsNoTracking()
            .Where(x => unitIds.Contains(x.Id))
            .Select(x => new { x.Id, x.CondominiumId, x.IsActive })
            .ToArrayAsync(ct);
        if (units.Any(x => !x.IsActive))
            logger.LogWarning("Active unit membership points to an inactive unit.");

        var condominiumIds = units.Select(x => x.CondominiumId).Distinct().ToArray();
        var condominiums = await db.Condominiums.AsNoTracking()
            .Where(x => condominiumIds.Contains(x.Id))
            .Select(x => new { x.Id, x.IsActive })
            .ToArrayAsync(ct);
        if (condominiums.Any(x => !x.IsActive))
            logger.LogWarning("Residential context points to an inactive condominium.");

        var activeCondominiumMemberships = await db.CondominiumMemberships.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.IsActive && x.EndedAt == null
                && condominiumIds.Contains(x.CondominiumId))
            .Select(x => x.CondominiumId).Distinct().ToArrayAsync(ct);

        var contexts = (
            from link in residentialLinks
            join unit in units on link.UnitId equals unit.Id
            join condominium in condominiums on unit.CondominiumId equals condominium.Id
            where unit.IsActive && condominium.IsActive
                && activeCondominiumMemberships.Contains(condominium.Id)
            select new ResidentialContext(
                condominium.Id, unit.Id, link.IsPrimaryResidence))
            .DistinctBy(x => new { x.CondominiumId, x.UnitId }).ToArray();
        if (contexts.Length == 0)
        {
            logger.LogInformation("ResidentialContextUnavailable: no active residential context.");
            return null;
        }

        var resolved = contexts.Length == 1
            ? contexts[0]
            : contexts.Where(x => x.IsPrimaryResidence).Take(2).ToArray() switch
            {
                [var primary] => primary,
                _ => null
            };
        if (resolved is null)
        {
            logger.LogWarning("More than one eligible residential context found for WhatsApp user.");
            return null;
        }

        logger.LogInformation("ResidentialContextResolved for WhatsApp user.");
        return new ResolvedIdentity(user.Id, user.FullName,
            resolved.CondominiumId, resolved.UnitId);
    }

    private async Task<(string Response, string Result)> Respond(
        WhatsAppSession session, ResolvedIdentity identity,
        NormalizedWhatsAppMessage message, DateTime now, DateTime expires,
        bool isNewSession, CancellationToken ct)
    {
        var residentReplyButton = ResidentReplyButtonChoice(message);
        var text = message.Text?.Trim();
        var command = NormalizeCommand(text);
        logger.LogInformation(
            "WhatsApp message routing. SessionState: {SessionState}; MessageType: {MessageType}; HasMediaId: {HasMediaId}; HasMimeType: {HasMimeType}; HasFileName: {HasFileName}; ProcessingBranch: {ProcessingBranch}.",
            session.State,
            message.MessageType,
            !string.IsNullOrWhiteSpace(message.MediaId),
            !string.IsNullOrWhiteSpace(message.MediaContentType),
            !string.IsNullOrWhiteSpace(message.FileName),
            ProcessingBranch(session.State, message));
        if (command is "menu" or "inicio" or "reiniciar")
        {
            await DiscardDraftAttachments(session, ct);
            session.Restart(now, expires);
            return (MainMenu(identity.FullName), "main_menu");
        }
        if (command == "sair")
        {
            await DiscardDraftAttachments(session, ct);
            session.End(now);
            return ("Atendimento encerrado. Envie uma nova mensagem quando precisar.", "session_ended");
        }

        // Template quick replies use button.payload/button.text, while regular
        // interactive messages use interactive.button_reply.id/title. Both are
        // normalized and routed before session-state and menu handling.
        if (residentReplyButton is not null)
        {
            var activeRequirement = await ActiveResidentReplyRequirement(
                session.RequestId, identity, ct);
            var correlationReason = activeRequirement is not null
                ? "SessionRequestIdMatched"
                : "NotAttempted";
            var outboundMatched = false;
            if (activeRequirement is null)
            {
                var correlated = await CorrelatedResidentReplyRequirement(
                    message.ReplyToExternalMessageId, identity, ct);
                activeRequirement = correlated.Requirement;
                outboundMatched = correlated.OutboundMatched;
                correlationReason = correlated.Reason;
            }
            if (activeRequirement is null)
            {
                activeRequirement = await UniqueActiveResidentReplyRequirement(
                    identity, ct);
                correlationReason = activeRequirement is null
                    ? correlationReason + ";UniqueRequirementFallbackUnavailable"
                    : correlationReason + ";UniqueRequirementFallbackMatched";
            }
            logger.LogInformation(
                "WhatsApp quick reply correlation diagnostic. ReplyToExternalMessageIdPresent: {ReplyToExternalMessageIdPresent}; OutboundMatched: {OutboundMatched}; CorrelationReason: {CorrelationReason}.",
                !string.IsNullOrWhiteSpace(message.ReplyToExternalMessageId),
                outboundMatched,
                correlationReason);
            if (activeRequirement is null)
                return ResidentReplyCorrelationFailed(
                    session, identity.FullName, now, expires);
            if (residentReplyButton == "1")
            {
                session.OfferResidentReply(activeRequirement.RequestId, now, expires);
                session.BeginResidentReply(now, expires, true);
                return (ResidentReplyInputPrompt(activeRequirement.Question),
                    "collecting_resident_reply");
            }
            session.OfferResidentReply(activeRequirement.RequestId, now, expires);
            return await DeferResidentReply(session, now, expires, ct, true);
        }
        if (isNewSession)
        {
            session.Restart(now, expires);
            return (MainMenu(identity.FullName), "main_menu");
        }
        if (session.ExpiresAt <= now
            || session.State == WhatsAppConversationState.Ended)
        {
            var expired = session.State != WhatsAppConversationState.Ended;
            if (expired) await DiscardDraftAttachments(session, ct);
            session.Restart(now, expires);
            logger.LogInformation(expired
                ? "Expired WhatsApp session restarted for phone {Phone}."
                : "Ended WhatsApp session restarted for phone {Phone}.",
                PhoneNumberNormalizer.Mask(session.PhoneNumber));
            return (MainMenu(identity.FullName), expired ? "session_expired" : "session_restarted");
        }
        if (command == "cancelar")
        {
            if (session.State == WhatsAppConversationState.MainMenu)
            {
                session.Touch(now, expires);
                return ("Não há operação em andamento. Digite 1 para abrir uma solicitação.", "nothing_to_cancel");
            }
            if (IsResidentReplyFlow(session.State))
                return await DeferResidentReply(session, now, expires, ct);
            if (session.State == WhatsAppConversationState.ReplyingToRequest)
            {
                session.Restart(now, expires);
                return ("Atualização encerrada.\n\n" + MainMenu(identity.FullName),
                    "request_update_cancelled");
            }
            var ownRequestFlow = IsOwnRequestFlow(session.State);
            await DiscardDraftAttachments(session, ct);
            session.Restart(now, expires);
            if (ownRequestFlow)
                return (MainMenu(identity.FullName), "main_menu");
            logger.LogInformation("WhatsApp draft flow cancelled for phone {Phone}.", PhoneNumberNormalizer.Mask(session.PhoneNumber));
            return ($"A abertura foi cancelada.\n\n{MainMenu(identity.FullName)}", "cancelled");
        }

        return session.State switch
        {
            WhatsAppConversationState.MainMenu =>
                await MainMenuChoice(session, identity, text, now, expires, ct),
            WhatsAppConversationState.ListingOwnRequests =>
                await OwnRequestsChoice(session, identity, text, now, expires, ct),
            WhatsAppConversationState.SelectingOpenRequest =>
                await ExistingRequestChoice(session, identity, text, now, expires, ct),
            WhatsAppConversationState.ViewingOwnRequest =>
                await OwnRequestDetailsChoice(session, identity, text, now, expires, ct),
            WhatsAppConversationState.ViewingOwnRequestUpdates =>
                await OwnRequestUpdatesChoice(session, identity, text, now, expires, ct),
            WhatsAppConversationState.AwaitingResidentReplyChoice =>
                await ResidentReplyChoice(session, identity,
                    text, now, expires, ct),
            WhatsAppConversationState.CollectingResidentReply =>
                await CollectResidentReply(session, identity, message, now, expires, ct),
            WhatsAppConversationState.ReviewingResidentReply =>
                await ResidentReplyReviewChoice(session, identity, text, now, expires, ct),
            WhatsAppConversationState.CollectingResidentReplyAttachments =>
                await CollectResidentReplyAttachments(session, identity, message, now, expires, ct),
            WhatsAppConversationState.ReplyingToRequest =>
                await CollectRequestUpdate(session, identity, message, now, expires, ct),
            WhatsAppConversationState.CollectingDescription =>
                await CollectDescription(session, message, identity.FullName, now, expires, ct),
            WhatsAppConversationState.CollectingAttachments =>
                await CollectAttachments(session, message, identity.FullName, now, expires, ct),
            WhatsAppConversationState.ReviewingNewRequest =>
                await ReviewChoice(session, text, identity.FullName, now, expires, ct),
            WhatsAppConversationState.SelectingCategory =>
                await ResumeLegacyCategorySession(session, now, expires, ct),
            _ => Recover(session, identity.FullName, now, expires)
        };
    }

    private async Task<(string, string)> MainMenuChoice(
        WhatsAppSession session, ResolvedIdentity identity, string? text,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        if (text == "1")
        {
            session.BeginDescription(now, expires);
            return (DescriptionPrompt(), "collecting_description");
        }
        if (text == "2")
        {
            session.BeginOwnRequestListing(now, expires);
            return (await OwnRequestsPage(session, identity, now, expires, ct),
                "listing_own_requests");
        }
        if (text == "3")
        {
            session.BeginExistingRequestSelection(now, expires);
            return (await EligibleRequestsPage(session, identity, now, expires, ct),
                "selecting_existing_request");
        }
        session.Touch(now, expires);
        return ("Não reconheci essa opção. Escolha 1, 2 ou 3, ou envie ‘menu’ para recomeçar.", "invalid_main_menu_choice");
    }

    private async Task<(string, string)> OwnRequestsChoice(
        WhatsAppSession session, ResolvedIdentity identity, string? text,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        if (text == "0")
        {
            session.Restart(now, expires);
            return (MainMenu(identity.FullName), "main_menu");
        }
        session.Touch(now, expires);
        return ("Escolha uma opção válida.\n\n"
                + await OwnRequestsPage(session, identity, now, expires, ct),
            "invalid_own_request_choice");
    }

    private async Task<(string, string)> ExistingRequestChoice(
        WhatsAppSession session, ResolvedIdentity identity, string? text,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        if (text == "0")
        {
            session.Restart(now, expires);
            return (MainMenu(identity.FullName), "main_menu");
        }
        if (text == "6")
        {
            session.SetPage(session.Page + 1, now, expires);
            return (await EligibleRequestsPage(session, identity, now, expires, ct),
                "selecting_existing_request_next_page");
        }
        if (text == "7" && session.Page > 0)
        {
            session.SetPage(session.Page - 1, now, expires);
            return (await EligibleRequestsPage(session, identity, now, expires, ct),
                "selecting_existing_request_previous_page");
        }

        var page = await CurrentOwnRequestsPage(
            session, identity, now, expires, ct, true);
        if (int.TryParse(text, out var choice)
            && choice >= 1 && choice <= page.Items.Length)
        {
            var selected = page.Items[choice - 1];
            session.BeginRequestUpdate(selected.Id, now, expires);
            return (RequestUpdatePrompt(), "collecting_request_update");
        }

        session.Touch(now, expires);
        return ("Escolha uma opção válida.\n\n" + EligibleRequestsPageText(page),
            "invalid_existing_request_choice");
    }

    private async Task<(string, string)> OwnRequestDetailsChoice(
        WhatsAppSession session, ResolvedIdentity identity, string? text,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        if (text == "0")
        {
            session.Restart(now, expires);
            return (MainMenu(identity.FullName), "main_menu");
        }
        if (text == "2")
        {
            session.ReturnToOwnRequestListing(now, expires);
            return (await OwnRequestsPage(session, identity, now, expires, ct),
                "listing_own_requests");
        }
        var request = await AccessibleOwnRequest(session.RequestId, identity, ct);
        if (request is null)
        {
            session.ReturnToOwnRequestListing(now, expires);
            return ("Não foi possível acessar essa solicitação.\n\n"
                + await OwnRequestsPage(session, identity, now, expires, ct),
                "own_request_no_longer_accessible");
        }
        if (text == "1")
        {
            session.ShowOwnRequestUpdates(now, expires);
            return (await OwnRequestUpdates(request, identity.UserId, ct),
                "viewing_own_request_updates");
        }
        session.Touch(now, expires);
        return ("Escolha uma opção válida.\n\n" + OwnRequestDetails(request),
            "invalid_own_request_details_choice");
    }

    private async Task<(string, string)> OwnRequestUpdatesChoice(
        WhatsAppSession session, ResolvedIdentity identity, string? text,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        if (text == "0")
        {
            session.Restart(now, expires);
            return (MainMenu(identity.FullName), "main_menu");
        }
        var request = await AccessibleOwnRequest(session.RequestId, identity, ct);
        if (request is null)
        {
            session.ReturnToOwnRequestListing(now, expires);
            return ("Não foi possível acessar essa solicitação.\n\n"
                + await OwnRequestsPage(session, identity, now, expires, ct),
                "own_request_no_longer_accessible");
        }
        if (text == "1")
        {
            session.ReturnToOwnRequestDetails(now, expires);
            return (OwnRequestDetails(request), "viewing_own_request");
        }
        session.Touch(now, expires);
        return ("Escolha uma opção válida.\n\n"
            + await OwnRequestUpdates(request, identity.UserId, ct),
            "invalid_own_request_updates_choice");
    }

    private async Task<string> OwnRequestsPage(
        WhatsAppSession session, ResolvedIdentity identity,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        session.Touch(now, expires);
        var items = await AllowedOwnRequests(identity)
            .Where(request => request.Status != RequestStatus.Cancelled)
            .OrderByDescending(request => request.UpdatedAt)
            .ThenBy(request => request.Id)
            .Select(request => new OwnRequestItem(request.Id, request.Title,
                request.Description, request.Status, request.UpdatedAt))
            .ToArrayAsync(ct);
        return OwnRequestsStatusText(items);
    }

    private async Task<string> EligibleRequestsPage(
        WhatsAppSession session, ResolvedIdentity identity,
        DateTime now, DateTime expires, CancellationToken ct) =>
        EligibleRequestsPageText(await CurrentOwnRequestsPage(
            session, identity, now, expires, ct, true));

    private async Task<OwnRequestPage> CurrentOwnRequestsPage(
        WhatsAppSession session, ResolvedIdentity identity,
        DateTime now, DateTime expires, CancellationToken ct,
        bool eligibleOnly = false)
    {
        var allowed = AllowedOwnRequests(identity);
        if (eligibleOnly)
            allowed = allowed.Where(x => x.Status == RequestStatus.Open
                || x.Status == RequestStatus.InProgress
                || x.Status == RequestStatus.WaitingForResident);
        var total = await allowed.CountAsync(ct);
        var lastPage = total == 0 ? 0 : (total - 1) / OwnRequestsPageSize;
        var page = Math.Clamp(session.Page, 0, lastPage);
        if (page != session.Page) session.SetPage(page, now, expires);
        else session.Touch(now, expires);
        var items = await allowed
            .OrderBy(x => x.Status == RequestStatus.Resolved
                || x.Status == RequestStatus.Cancelled)
            .ThenByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.Id)
            .Skip(page * OwnRequestsPageSize)
            .Take(OwnRequestsPageSize)
            .Select(x => new OwnRequestItem(
                x.Id, x.Title, x.Description, x.Status, x.UpdatedAt))
            .ToArrayAsync(ct);
        return new OwnRequestPage(items, page, total,
            (page + 1) * OwnRequestsPageSize < total);
    }

    private IQueryable<DomainRequest> AllowedOwnRequests(ResolvedIdentity identity) =>
        db.Requests.AsNoTracking().Where(x =>
            x.AuthorUserId == identity.UserId
            && x.CondominiumId == identity.CondominiumId
            && (x.TargetUnitId == null || x.TargetUnitId == identity.UnitId));

    private async Task<OwnRequestItem?> AccessibleOwnRequest(
        Guid? requestId, ResolvedIdentity identity, CancellationToken ct)
    {
        if (requestId is null) return null;
        return await AllowedOwnRequests(identity)
            .Where(x => x.Id == requestId.Value)
            .Select(x => new OwnRequestItem(
                x.Id, x.Title, x.Description, x.Status, x.UpdatedAt))
            .SingleOrDefaultAsync(ct);
    }

    private async Task<OwnRequestItem?> AccessibleEligibleOwnRequest(
        Guid? requestId, ResolvedIdentity identity, CancellationToken ct)
    {
        var request = await AccessibleOwnRequest(requestId, identity, ct);
        return request?.Status is RequestStatus.Open
            or RequestStatus.InProgress
            or RequestStatus.WaitingForResident
                ? request
                : null;
    }

    private static string OwnRequestsStatusText(IReadOnlyList<OwnRequestItem> items)
    {
        if (items.Count == 0)
            return "Você ainda não possui solicitações para consultar.\n\n0 - Voltar ao menu";
        var content = string.Join("\n\n", items.Select(item =>
            $"• {item.Title}\n"
            + $"Status: {FriendlyStatus(item.Status)}\n"
            + $"Atualizada em: {LocalDateTime(item.UpdatedAt)}"));
        return "Status de suas solicitações:\n\n"
            + content + "\n\n0 - Voltar ao menu";
    }

    private static string EligibleRequestsPageText(OwnRequestPage page)
    {
        if (page.Total == 0)
            return "Não há solicitações disponíveis para receber uma atualização.\n\n"
                + "0 - Voltar ao menu";
        var items = string.Join("\n\n", page.Items.Select((item, index) =>
            $"{index + 1} - {item.Title}\n"
            + $"Status: {FriendlyStatus(item.Status)}\n"
            + $"Atualizada em: {LocalDateTime(item.UpdatedAt)}"));
        var navigation = new List<string>();
        if (page.HasNext) navigation.Add("6 - Ver mais");
        if (page.Page > 0) navigation.Add("7 - Página anterior");
        navigation.Add("0 - Voltar ao menu");
        return "Sobre qual solicitação você deseja falar?\n\n"
            + items + "\n\nDigite o número da solicitação.\n\n"
            + string.Join('\n', navigation);
    }

    private static string OwnRequestDetails(OwnRequestItem request) =>
        $"*Título:*\n{request.Title}\n\n"
        + $"*Status:*\n{FriendlyStatus(request.Status)}\n\n"
        + $"*Descrição:*\n{request.Description}\n\n"
        + $"*Última atualização:*\n{LocalDateTime(request.UpdatedAt)}\n\n"
        + "1 - Ver atualizações\n"
        + "2 - Voltar para minhas solicitações\n"
        + "0 - Voltar ao menu";

    private async Task<string> OwnRequestUpdates(
        OwnRequestItem request, Guid userId, CancellationToken ct)
    {
        var originalMessageId = await db.RequestMessages.AsNoTracking()
            .Where(x => x.RequestId == request.Id
                && x.AuthorUserId == userId
                && x.Channel == MessageChannel.WhatsApp)
            .OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
            .Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        var latest = await db.RequestMessages.AsNoTracking()
            .Where(x => x.RequestId == request.Id
                && (originalMessageId == null || x.Id != originalMessageId.Value))
            .OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id)
            .Take(5)
            .Select(x => new OwnRequestUpdate(
                x.Id, x.AuthorUserId, x.Content, x.CreatedAt))
            .ToArrayAsync(ct);
        var updates = latest.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToArray();
        var content = updates.Length == 0
            ? "Ainda não há novas atualizações nesta solicitação."
            : string.Join("\n\n", updates.Select(x =>
                $"{LocalDateTime(x.CreatedAt)} — "
                + (x.AuthorUserId == userId ? "Você" : "Administração")
                + $"\n{x.Content}"));
        return "Atualizações da solicitação:\n\n" + content
            + "\n\n1 - Voltar aos detalhes\n0 - Voltar ao menu";
    }

    internal static string FriendlyStatus(RequestStatus status) => status switch
    {
        RequestStatus.Open => "Aberta",
        RequestStatus.InProgress => "Em andamento",
        RequestStatus.WaitingForResident => "Aguardando morador",
        RequestStatus.WaitingForManager => "Dar andamento",
        RequestStatus.WaitingForThirdParty => "Aguardando terceiro",
        RequestStatus.Resolved => "Resolvida",
        RequestStatus.Cancelled => "Cancelada",
        _ => "Status indisponível"
    };

    private static string LocalDateTime(DateTime value)
    {
        var utc = value.Kind == DateTimeKind.Utc
            ? value : DateTime.SpecifyKind(value, DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeFromUtc(utc, SaoPauloTimeZone)
            .ToString("dd/MM/yyyy 'às' HH:mm", CultureInfo.GetCultureInfo("pt-BR"));
    }

    private static readonly TimeZoneInfo SaoPauloTimeZone = FindSaoPauloTimeZone();
    private static TimeZoneInfo FindSaoPauloTimeZone()
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo"); }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        }
    }

    private static bool IsOwnRequestFlow(WhatsAppConversationState state) => state is
        WhatsAppConversationState.ListingOwnRequests
        or WhatsAppConversationState.ViewingOwnRequest
        or WhatsAppConversationState.ViewingOwnRequestUpdates;

    private static bool IsResidentReplyFlow(WhatsAppConversationState state) => state is
        WhatsAppConversationState.AwaitingResidentReplyChoice
        or WhatsAppConversationState.CollectingResidentReply
        or WhatsAppConversationState.ReviewingResidentReply
        or WhatsAppConversationState.CollectingResidentReplyAttachments;

    private async Task<(string, string)> CollectRequestUpdate(
        WhatsAppSession session, ResolvedIdentity identity,
        NormalizedWhatsAppMessage message, DateTime now, DateTime expires,
        CancellationToken ct)
    {
        var request = await AccessibleEligibleOwnRequest(
            session.RequestId, identity, ct);
        if (request is null)
        {
            session.Restart(now, expires);
            return ("Esta solicitação não está mais disponível para atualização.\n\n"
                + MainMenu(identity.FullName), "request_update_no_longer_available");
        }

        var text = message.Text?.Trim();
        var command = NormalizeCommand(text);
        if (command == "finalizar" || text == "1")
        {
            session.End(now);
            return ("Atualização finalizada. Envie uma nova mensagem quando precisar.",
                "request_update_finished");
        }
        if (message.MessageType == "text")
        {
            if (string.IsNullOrWhiteSpace(text))
                return (RequestUpdatePrompt(), "request_update_content_required");
            if (text.Length > 4000)
            {
                session.Touch(now, expires);
                return ("A mensagem deve ter no máximo 4000 caracteres.",
                    "request_update_too_long");
            }
            var requestMessage = new RequestMessage(
                request.Id, identity.UserId, text,
                MessageChannel.WhatsAppResidentUpdate);
            db.RequestMessages.Add(requestMessage);
            await db.SaveChangesAsync(ct);
            await NotifyRequestUpdate(request, identity, requestMessage, ct);
            if (analysisRefresher is not null)
                await analysisRefresher.RefreshAsync(request.Id,
                    "whatsapp_resident_update", ct);
            session.Touch(now, expires);
            return (RequestUpdateReceivedPrompt("Mensagem recebida."),
                "request_update_message_received");
        }

        if (message.MessageType is not ("image" or "video" or "document" or "audio")
            || string.IsNullOrWhiteSpace(message.MediaId))
        {
            session.Touch(now, expires);
            return ("No momento este tipo de conteúdo ainda não é suportado.\n\n"
                + RequestUpdatePrompt(), "unsupported_request_update_content");
        }

        var media = await client.DownloadMediaAsync(message.MediaId, ct);
        if (!media.Succeeded || media.Content is null)
        {
            session.Touch(now, expires);
            return ("Não foi possível baixar o arquivo. Tente enviá-lo novamente.",
                "request_update_attachment_download_failed");
        }
        var contentType = media.ContentType ?? message.MediaContentType;
        var extension = Path.GetExtension(message.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = AttachmentPolicy.PreferredExtension(contentType);
        var fileName = string.IsNullOrWhiteSpace(message.FileName)
            ? $"{message.MessageType}-{Guid.NewGuid():N}{extension}"
            : message.FileName;
        var validation = AttachmentPolicy.Validate(
            fileName, media.Content.LongLength, contentType);
        if (validation.Error is not null)
        {
            session.Touch(now, expires);
            return (validation.Error, "request_update_attachment_rejected");
        }

        string? storageKey = null;
        try
        {
            await using var stream = new MemoryStream(media.Content, writable: false);
            storageKey = await storage.SaveAsync(
                request.Id, stream, validation.Extension!, ct);
            var description = message.MessageType == "audio"
                ? "Áudio enviado pelo morador."
                : "Anexo enviado pelo morador.";
            if (message.MessageType == "audio")
            {
                try
                {
                    var transcription = await audioTranscription.TranscribeAsync(
                        media.Content, validation.Name!, validation.ContentType!, ct);
                    if (transcription.Succeeded
                        && !string.IsNullOrWhiteSpace(transcription.Text)
                        && transcription.Text.Length <= 4000)
                    {
                        description = transcription.Text;
                    }
                    else
                    {
                        logger.LogWarning(
                            "Resident update audio transcription failed. Code: {Code}.",
                            transcription.Code);
                    }
                }
                catch (Exception exception)
                {
                    logger.LogWarning(exception,
                        "Resident update audio transcription failed unexpectedly; audio will remain available.");
                }
            }
            var requestMessage = new RequestMessage(
                request.Id, identity.UserId, description,
                MessageChannel.WhatsAppResidentUpdate);
            db.RequestMessages.Add(requestMessage);
            db.RequestAttachments.Add(new RequestAttachment(
                request.Id, identity.UserId, validation.Name!, storageKey,
                validation.ContentType!, media.Content.LongLength,
                requestMessage.Id));
            await db.SaveChangesAsync(ct);
            await NotifyRequestUpdate(request, identity, requestMessage, ct);
            if (analysisRefresher is not null)
                await analysisRefresher.RefreshAsync(request.Id,
                    "whatsapp_resident_update", ct);
        }
        catch
        {
            if (storageKey is not null) storage.Delete(storageKey);
            throw;
        }
        session.Touch(now, expires);
        return (RequestUpdateReceivedPrompt(message.MessageType == "audio"
                ? "Áudio recebido." : "Arquivo recebido."),
            "request_update_attachment_received");
    }

    private async Task NotifyRequestUpdate(
        OwnRequestItem request, ResolvedIdentity identity,
        RequestMessage message, CancellationToken ct)
    {
        try
        {
            await notifications.NotifyMessageAsync(
                request.Id, identity.CondominiumId, identity.UserId,
                request.Title, identity.UserId, message.Content, ct,
                message.Id, message.Channel);
        }
        catch (Exception exception)
        {
            logger.LogError(exception,
                "Failed to notify spontaneous resident request update.");
        }
    }

    private async Task<(string, string)> ResidentReplyChoice(
        WhatsAppSession session, ResolvedIdentity identity, string? text,
        DateTime now, DateTime expires, CancellationToken ct,
        bool templateButton = false)
    {
        var requirement = await ActiveResidentReplyRequirement(session.RequestId, identity, ct);
        if (requirement is null)
            return ResidentReplyNoLongerAvailable(session, identity.FullName, now, expires);
        if (text == "1")
        {
            session.BeginResidentReply(now, expires, true);
            return (ResidentReplyInputPrompt(requirement.Question), "collecting_resident_reply");
        }
        if (text == "2" && session.PreviousState == WhatsAppConversationState.ViewingOwnRequest)
        {
            var request = await AccessibleOwnRequest(session.RequestId, identity, ct);
            if (request is null)
                return ResidentReplyNoLongerAvailable(session, identity.FullName, now, expires);
            session.ShowOwnRequest(request.Id, now, expires);
            return (OwnRequestDetails(request), "viewing_own_request");
        }
        if (text == "2")
            return await DeferResidentReply(session, now, expires, ct,
                templateButton);
        if (text == "0" && session.PreviousState == WhatsAppConversationState.ViewingOwnRequest)
        {
            session.Restart(now, expires);
            return (MainMenu(identity.FullName), "main_menu");
        }
        session.Touch(now, expires);
        return (ResidentReplyOfferPrompt(requirement.Question,
            session.PreviousState == WhatsAppConversationState.ViewingOwnRequest),
            "invalid_resident_reply_choice");
    }

    private async Task<(string, string)> CollectResidentReply(
        WhatsAppSession session, ResolvedIdentity identity,
        NormalizedWhatsAppMessage message, DateTime now, DateTime expires,
        CancellationToken ct)
    {
        var requirement = await ActiveResidentReplyRequirement(session.RequestId, identity, ct);
        if (requirement is null)
            return ResidentReplyNoLongerAvailable(session, identity.FullName, now, expires);
        if (IsAudioTranscriptionFailure(session))
        {
            if (message.MessageType == "text" && message.Text?.Trim() is "1" or "2")
            {
                session.ClearPendingAudioState(now, expires);
                return (ResidentReplyInputPrompt(), "collecting_resident_reply_retry");
            }
            if (message.MessageType == "text" && message.Text?.Trim() == "3")
                return await DeferResidentReply(session, now, expires, ct);
        }
        if (message.MessageType == "audio")
        {
            if (string.IsNullOrWhiteSpace(message.MediaId))
            {
                MarkAudioFailure(session, now, expires);
                return (AudioFailurePrompt(), "resident_reply_audio_missing");
            }
            var media = await client.DownloadMediaAsync(message.MediaId, ct);
            if (!media.Succeeded || media.Content is null)
            {
                MarkAudioFailure(session, now, expires);
                return (AudioFailurePrompt(), "resident_reply_audio_download_failed");
            }
            var contentType = media.ContentType ?? message.MediaContentType;
            var extension = AttachmentPolicy.PreferredExtension(contentType);
            var validation = AttachmentPolicy.Validate($"audio-{Guid.NewGuid():N}{extension}",
                media.Content.LongLength, contentType);
            if (validation.Error is not null) return (validation.Error, "resident_reply_audio_rejected");
            var key = await storage.SaveWhatsAppDraftAsync(session.Id, media.Content,
                validation.Extension!, ct);
            var draft = new WhatsAppDraftAttachment(session.Id, message.MediaId,
                validation.Name!, key, validation.ContentType!, media.Content.LongLength);
            db.WhatsAppDraftAttachments.Add(draft);
            try { await db.SaveChangesAsync(ct); }
            catch { storage.Delete(key); throw; }
            var transcription = await audioTranscription.TranscribeAsync(media.Content,
                validation.Name!, validation.ContentType!, ct);
            if (!transcription.Succeeded || string.IsNullOrWhiteSpace(transcription.Text)
                || transcription.Text.Length > 4000)
            {
                db.WhatsAppDraftAttachments.Remove(draft);
                await db.SaveChangesAsync(ct);
                storage.Delete(key);
                MarkAudioFailure(session, now, expires);
                return (AudioFailurePrompt(), "resident_reply_transcription_failed");
            }
            return await OrganizeResidentReply(session, requirement.Question,
                transcription.Text, draft.Id, now, expires, ct);
        }
        if (message.MessageType != "text" || string.IsNullOrWhiteSpace(message.Text))
            return (ResidentReplyInputPrompt(), "resident_reply_required");
        var original = message.Text.Trim();
        if (original.Length > 4000)
            return ("A resposta deve ter no máximo 4000 caracteres.", "resident_reply_too_long");
        return await OrganizeResidentReply(session, requirement.Question, original,
            null, now, expires, ct);
    }

    private async Task<(string, string)> OrganizeResidentReply(WhatsAppSession session,
        string question, string original, Guid? audioDraftId, DateTime now,
        DateTime expires, CancellationToken ct)
    {
        var result = await residentReplyAi.OrganizeAsync(question, original, ct);
        var review = new ResidentReplyReview(result.Succeeded ? AiReviewSource : FallbackReviewSource,
            result.Succeeded ? result.Answer! : original, audioDraftId);
        session.SetResidentReplyForReview(original, JsonSerializer.Serialize(review), now, expires);
        return (ResidentReplyReviewPrompt(review), result.Succeeded
            ? "reviewing_resident_reply_ai" : "reviewing_resident_reply_fallback");
    }

    private async Task<(string, string)> ResidentReplyReviewChoice(
        WhatsAppSession session, ResolvedIdentity identity, string? text,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        if (await ActiveResidentReplyRequirement(session.RequestId, identity, ct) is null)
            return ResidentReplyNoLongerAvailable(session, identity.FullName, now, expires);
        if (text == "2")
        {
            await DiscardOriginalAudioDraft(session, ct);
            session.BeginResidentReply(now, expires, true);
            return (ResidentReplyInputPrompt(), "resident_reply_correction");
        }
        if (text == "3") return await DeferResidentReply(session, now, expires, ct);
        var review = ResidentReview(session);
        if (text != "1" || review is null)
        {
            session.Touch(now, expires);
            return (review is null ? ResidentReplyInputPrompt() : ResidentReplyReviewPrompt(review),
                "invalid_resident_reply_confirmation");
        }
        session.BeginResidentReplyAttachments(now, expires);
        return (ResidentReplyAttachmentPrompt(), "collecting_resident_reply_attachments");
    }

    private async Task<(string, string)> CollectResidentReplyAttachments(
        WhatsAppSession session, ResolvedIdentity identity,
        NormalizedWhatsAppMessage message, DateTime now, DateTime expires,
        CancellationToken ct)
    {
        if (await ActiveResidentReplyRequirement(session.RequestId, identity, ct) is null)
            return ResidentReplyNoLongerAvailable(session, identity.FullName, now, expires);
        var text = message.Text?.Trim();
        if (text == "3") return await DeferResidentReply(session, now, expires, ct);
        if (text is "1" or "2")
            return await ConfirmResidentReply(session, identity, now, expires, ct);
        if (message.MessageType is not ("image" or "video" or "document")
            || string.IsNullOrWhiteSpace(message.MediaId))
            return (ResidentReplyAttachmentPrompt(), "unsupported_resident_reply_attachment");
        var count = await db.WhatsAppDraftAttachments.CountAsync(x => x.SessionId == session.Id, ct);
        if (count >= AttachmentPolicy.MaximumFileCount)
            return ($"É permitido enviar no máximo {AttachmentPolicy.MaximumFileCount} arquivos.",
                "resident_reply_attachment_limit");
        var media = await client.DownloadMediaAsync(message.MediaId, ct);
        if (!media.Succeeded || media.Content is null)
            return ("Não foi possível baixar o arquivo. Tente novamente.",
                "resident_reply_attachment_download_failed");
        var extension = Path.GetExtension(message.FileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = AttachmentPolicy.PreferredExtension(media.ContentType);
        var fileName = string.IsNullOrWhiteSpace(message.FileName)
            ? $"arquivo-{Guid.NewGuid():N}{extension}" : message.FileName;
        var validation = AttachmentPolicy.Validate(fileName, media.Content.LongLength, media.ContentType);
        if (validation.Error is not null) return (validation.Error, "resident_reply_attachment_rejected");
        var key = await storage.SaveWhatsAppDraftAsync(session.Id, media.Content,
            validation.Extension!, ct);
        try
        {
            db.WhatsAppDraftAttachments.Add(new WhatsAppDraftAttachment(session.Id,
                message.MediaId, validation.Name!, key, validation.ContentType!, media.Content.LongLength));
            session.Touch(now, expires);
            await db.SaveChangesAsync(ct);
        }
        catch { storage.Delete(key); throw; }
        return ("Arquivo recebido. Envie outro ou digite 1 quando terminar.",
            "resident_reply_attachment_received");
    }

    private async Task<(string, string)> ConfirmResidentReply(WhatsAppSession session,
        ResolvedIdentity identity, DateTime now, DateTime expires, CancellationToken ct)
    {
        var review = ResidentReview(session);
        if (review is null || !session.RequestId.HasValue)
            return ResidentReplyNoLongerAvailable(session, identity.FullName, now, expires);
        var drafts = await db.WhatsAppDraftAttachments.AsNoTracking()
            .Where(x => x.SessionId == session.Id).ToArrayAsync(ct);
        var files = drafts.Select(draft => new CondoLink.Api.Features.Requests.ResidentReplyService.ReplyFile(
            draft.OriginalFileName, draft.ContentType, draft.FileSize,
            _ => Task.FromResult<Stream>(storage.OpenRead(draft.StorageKey)
                ?? throw new FileNotFoundException("Temporary WhatsApp attachment was not found."))))
            .ToArray();
        var result = await residentReplies.ReplyAsync(session.RequestId.Value,
            identity.UserId, review.Answer, files, MessageChannel.WhatsApp, ct);
        if (result.Code != CondoLink.Api.Features.Requests.ResidentReplyService.ResultCode.Succeeded)
        {
            await DiscardDraftAttachments(session, ct);
            session.Restart(now, expires);
            return ("Não foi possível enviar a resposta porque a pendência foi alterada.\n\n"
                + MainMenu(identity.FullName), "resident_reply_conflict");
        }
        await DiscardDraftAttachments(session, ct);
        session.Restart(now, expires);
        return ("Resposta enviada com sucesso.\n\n" + MainMenu(identity.FullName),
            "resident_reply_sent");
    }

    private async Task<(string, string)> DeferResidentReply(WhatsAppSession session,
        DateTime now, DateTime expires, CancellationToken ct,
        bool endSession = false)
    {
        await DiscardDraftAttachments(session, ct);
        if (endSession) session.End(now);
        else session.Restart(now, expires);
        return ("Tudo bem. A solicitação continuará aguardando sua resposta.\n\n"
            + "Você pode responder depois pelo portal ou consultar a solicitação pelo WhatsApp.",
            "resident_reply_deferred");
    }

    private static string? ResidentReplyButtonChoice(
        NormalizedWhatsAppMessage message)
    {
        if (!string.IsNullOrWhiteSpace(message.QuickReplyId))
            return message.QuickReplyId switch
            {
                "resident_reply_now" => "1",
                "resident_reply_later" => "2",
                _ => null
            };
        return message.QuickReplyTitle?.Trim() switch
        {
            "Responder agora" => "1",
            "Lembrar-me em 3 horas" => "2",
            _ => null
        };
    }

    private static string? KnownResidentReplyId(
        NormalizedWhatsAppMessage message) => message.QuickReplyId switch
        {
            "resident_reply_now" => "resident_reply_now",
            "resident_reply_later" => "resident_reply_later",
            _ => null
        };

    private (string, string) ResidentReplyCorrelationFailed(
        WhatsAppSession session, string fullName, DateTime now, DateTime expires)
    {
        session.Restart(now, expires);
        return ("Não consegui localizar a solicitação que precisa da sua resposta.\n\n"
            + "Você pode consultar suas solicitações para continuar.\n\n"
            + MainMenu(fullName), "resident_reply_correlation_failed");
    }

    private (string, string) ResidentReplyNoLongerAvailable(WhatsAppSession session,
        string fullName, DateTime now, DateTime expires)
    {
        session.Restart(now, expires);
        return ("Não foi possível continuar este atendimento.\n\n" + MainMenu(fullName),
            "resident_reply_no_longer_available");
    }

    private async Task<ResidentReplyRequirement?> ActiveResidentReplyRequirement(
        Guid? requestId, ResolvedIdentity identity, CancellationToken ct)
    {
        if (!requestId.HasValue) return null;
        var matches = await db.Requests.AsNoTracking()
            .Where(x => x.Id == requestId && x.AuthorUserId == identity.UserId
                && x.CondominiumId == identity.CondominiumId
                && (x.TargetUnitId == null || x.TargetUnitId == identity.UnitId)
                && x.Status == RequestStatus.WaitingForResident)
            .Join(db.RequestResidentReplyRequirements.AsNoTracking()
                    .Where(x => x.IsActive && x.AnswerMessageId == null),
                request => request.Id, requirement => requirement.RequestId,
                (request, requirement) => new ResidentReplyRequirement(request.Id,
                    requirement.Id,
                    requirement.Question))
            .Take(2)
            .ToArrayAsync(ct);
        return matches.Length == 1 ? matches[0] : null;
    }

    private async Task<OutboundCorrelationResult> CorrelatedResidentReplyRequirement(
        string? replyToExternalMessageId,
        ResolvedIdentity identity,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(replyToExternalMessageId))
            return new(null, false, "ReplyContextMissing");
        var outbound = await db.WhatsAppOutboundMessages.AsNoTracking()
            .Where(x => x.ExternalMessageId == replyToExternalMessageId)
            .Select(x => new
            {
                x.RequestId,
                x.UserId,
                x.CondominiumId,
                x.NotificationType,
                x.SendMode,
                x.Status
            })
            .SingleOrDefaultAsync(ct);
        if (outbound is null)
            return new(null, false, "OutboundExternalMessageIdNotFound");
        if (outbound.UserId != identity.UserId)
            return new(null, true, "OutboundUserMismatch");
        if (outbound.CondominiumId != identity.CondominiumId)
            return new(null, true, "OutboundCondominiumMismatch");
        if (outbound.NotificationType != WhatsAppNotificationType.InformationRequested)
            return new(null, true, "OutboundNotificationTypeMismatch");
        if (outbound.SendMode != WhatsAppSendMode.Template)
            return new(null, true, "OutboundSendModeMismatch");
        if (outbound.Status is not WhatsAppOutboundStatus.Sent
            and not WhatsAppOutboundStatus.Delivered
            and not WhatsAppOutboundStatus.Read)
            return new(null, true, "OutboundStatusNotCorrelatable");
        if (!outbound.RequestId.HasValue)
            return new(null, true, "OutboundRequestIdMissing");
        var requirement = await ActiveResidentReplyRequirement(
            outbound.RequestId, identity, ct);
        return requirement is null
            ? new(null, true, "RequirementNotActiveOrUnauthorized")
            : new(requirement, true, "OutboundMatched");
    }

    private async Task<ResidentReplyRequirement?> UniqueActiveResidentReplyRequirement(
        ResolvedIdentity identity, CancellationToken ct)
    {
        var matches = await db.Requests.AsNoTracking()
            .Where(x => x.AuthorUserId == identity.UserId
                && x.CondominiumId == identity.CondominiumId
                && (x.TargetUnitId == null || x.TargetUnitId == identity.UnitId)
                && x.Status == RequestStatus.WaitingForResident)
            .Join(db.RequestResidentReplyRequirements.AsNoTracking()
                    .Where(x => x.IsActive && x.AnswerMessageId == null),
                request => request.Id, requirement => requirement.RequestId,
                (request, requirement) => new ResidentReplyRequirement(request.Id,
                    requirement.Id, requirement.Question))
            .Take(2)
            .ToArrayAsync(ct);
        return matches.Length == 1 ? matches[0] : null;
    }

    private async Task<(string, string)> CollectDescription(
        WhatsAppSession session, NormalizedWhatsAppMessage message,
        string fullName, DateTime now, DateTime expires, CancellationToken ct)
    {
        if (IsAudioTranscriptionFailure(session))
        {
            if (message.MessageType == "text" && message.Text?.Trim() == "1")
            {
                session.ClearPendingAudioState(now, expires);
                return ("Envie o novo áudio quando estiver pronto.", "collecting_audio_retry");
            }
            if (message.MessageType == "text" && message.Text?.Trim() == "2")
            {
                session.ClearPendingAudioState(now, expires);
                return (DescriptionPrompt(), "collecting_text_retry");
            }
            if (message.MessageType == "text" && message.Text?.Trim() == "3")
            {
                await DiscardDraftAttachments(session, ct);
                session.Restart(now, expires);
                return ($"A abertura foi cancelada.\n\n{MainMenu(fullName)}", "cancelled");
            }
        }

        if (message.MessageType == "audio")
            return await CollectAudioDescription(session, message, now, expires, ct);
        if (message.MessageType != "text" || string.IsNullOrWhiteSpace(message.Text))
            return (DescriptionPrompt(), "description_required");
        var description = message.Text.Trim();
        if (description.Length > 4000)
            return ("A descrição deve ter no máximo 4000 caracteres. Envie um texto menor.", "description_too_long");
        session.SetDescriptionForReview(description, now, expires);
        return (AttachmentPrompt(), "collecting_attachments");
    }

    private async Task<(string, string)> CollectAudioDescription(
        WhatsAppSession session, NormalizedWhatsAppMessage message,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.MediaId))
        {
            logger.LogWarning(
                "WhatsApp audio branch cannot download media because MediaId is absent.");
            MarkAudioFailure(session, now, expires);
            return (AudioFailurePrompt(), "audio_missing");
        }
        var media = await client.DownloadMediaAsync(message.MediaId, ct);
        if (!media.Succeeded || media.Content is null)
        {
            MarkAudioFailure(session, now, expires);
            return (AudioFailurePrompt(), "audio_download_failed");
        }
        var contentType = media.ContentType ?? message.MediaContentType;
        var extension = AttachmentPolicy.PreferredExtension(contentType);
        var fileName = $"audio-{Guid.NewGuid():N}{extension}";
        var validation = AttachmentPolicy.Validate(fileName, media.Content.LongLength, contentType);
        if (validation.Error is not null)
        {
            session.Touch(now, expires);
            return (validation.Error, "audio_rejected");
        }
        var count = await db.WhatsAppDraftAttachments.CountAsync(
            x => x.SessionId == session.Id, ct);
        if (count >= AttachmentPolicy.MaximumFileCount)
            return ($"É permitido enviar no máximo {AttachmentPolicy.MaximumFileCount} arquivos.",
                "attachment_limit");

        var storageKey = await storage.SaveWhatsAppDraftAsync(
            session.Id, media.Content, validation.Extension!, ct);
        var draft = new WhatsAppDraftAttachment(
            session.Id, message.MediaId, validation.Name!, storageKey,
            validation.ContentType!, media.Content.LongLength);
        try
        {
            db.WhatsAppDraftAttachments.Add(draft);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(storageKey);
            throw;
        }

        var transcription = await audioTranscription.TranscribeAsync(
            media.Content, validation.Name!, validation.ContentType!, ct);
        if (!transcription.Succeeded || string.IsNullOrWhiteSpace(transcription.Text)
            || transcription.Text.Length > 4000)
        {
            db.WhatsAppDraftAttachments.Remove(draft);
            await db.SaveChangesAsync(ct);
            storage.Delete(storageKey);
            MarkAudioFailure(session, now, expires);
            return (AudioFailurePrompt(), transcription.Code == "timeout"
                ? "audio_transcription_timeout" : "audio_transcription_failed");
        }

        session.SetAudioDescriptionForReview(
            transcription.Text,
            JsonSerializer.Serialize(new RequestDraftReview(
                PendingAudioSource, null, null, draft.Id)),
            now, expires);
        return (AttachmentPrompt(), "collecting_attachments");
    }

    private async Task<(string, string)> CollectAttachments(
        WhatsAppSession session, NormalizedWhatsAppMessage message, string fullName,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        var text = message.Text?.Trim();
        if (text is "1" or "2")
        {
            return await GenerateAiProposal(session, now, expires, ct);
        }
        if (text == "3")
        {
            await DiscardDraftAttachments(session, ct);
            session.Restart(now, expires);
            return ($"A abertura foi cancelada.\n\n{MainMenu(fullName)}", "cancelled");
        }
        if (message.MessageType is not ("image" or "video" or "document")
            || string.IsNullOrWhiteSpace(message.MediaId))
        {
            session.Touch(now, expires);
            return ("No momento este tipo de arquivo ainda não é suportado.", "unsupported_attachment");
        }

        var count = await db.WhatsAppDraftAttachments.CountAsync(
            x => x.SessionId == session.Id, ct);
        if (count >= AttachmentPolicy.MaximumFileCount)
        {
            session.Touch(now, expires);
            return ($"É permitido enviar no máximo {AttachmentPolicy.MaximumFileCount} arquivos. Digite 1 para continuar.", "attachment_limit");
        }

        var media = await client.DownloadMediaAsync(message.MediaId, ct);
        if (!media.Succeeded || media.Content is null)
        {
            session.Touch(now, expires);
            return ("Não foi possível baixar o arquivo. Tente enviá-lo novamente.", "attachment_download_failed");
        }
        var extension = Path.GetExtension(message.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = AttachmentPolicy.PreferredExtension(media.ContentType);
        var fileName = string.IsNullOrWhiteSpace(message.FileName)
            ? $"arquivo-{Guid.NewGuid():N}{extension}"
            : message.FileName;
        var validation = AttachmentPolicy.Validate(
            fileName, media.Content.LongLength, media.ContentType);
        if (validation.Error is not null)
        {
            session.Touch(now, expires);
            return (validation.Error, "attachment_rejected");
        }

        var storageKey = await storage.SaveWhatsAppDraftAsync(
            session.Id, media.Content, validation.Extension!, ct);
        try
        {
            db.WhatsAppDraftAttachments.Add(new WhatsAppDraftAttachment(
                session.Id, message.MediaId, validation.Name!, storageKey,
                validation.ContentType!, media.Content.LongLength));
            session.Touch(now, expires);
            await db.SaveChangesAsync(ct);
        }
        catch
        {
            storage.Delete(storageKey);
            throw;
        }
        return ("Arquivo recebido.\n\nVocê pode enviar outro arquivo ou digitar 1 quando terminar.", "attachment_received");
    }

    private async Task<(string, string)> ReviewChoice(
        WhatsAppSession session, string? text, string fullName,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        if (text == "2")
        {
            await DiscardOriginalAudioDraft(session, ct);
            session.RewriteDescription(now, expires);
            return (DescriptionPrompt(), "description_correction");
        }
        if (text == "3")
        {
            await DiscardDraftAttachments(session, ct);
            session.Restart(now, expires);
            return ($"A abertura foi cancelada.\n\n{MainMenu(fullName)}", "cancelled");
        }
        if (text != "1")
        {
            session.Touch(now, expires);
            return ("Digite 1 para confirmar, 2 para corrigir o relato ou 3 para cancelar.", "invalid_confirmation_choice");
        }

        var review = Review(session);
        if (review is null)
            return await GenerateAiProposal(session, now, expires, ct);
        var category = await requestCategories.ResolveForClassificationAsync(
            session.CondominiumId!.Value, review.Proposal?.SuggestedCategory, ct);
        session.ChooseCategory(category.Id, now, expires);
        return await CreateRequest(session, category.Name, now, expires, ct);
    }

    private async Task<(string, string)> ResumeLegacyCategorySession(
        WhatsAppSession session, DateTime now, DateTime expires, CancellationToken ct)
    {
        var review = Review(session);
        if (review is not null)
        {
            session.SetAiProposal(session.DraftAiProposalJson!, now, expires);
            return (review.Source == AiReviewSource
                ? ReviewPrompt(review.Proposal!)
                : FallbackReviewPrompt(session.DraftDescription!), "reviewing_request");
        }
        if (!string.IsNullOrWhiteSpace(session.DraftDescription))
            return await GenerateAiProposal(session, now, expires, ct);
        session.Restart(now, expires);
        return ("O atendimento anterior expirou. Digite 1 para abrir uma solicitação.",
            "legacy_category_session_recovered");
    }

    private async Task<(string, string)> CreateRequest(
        WhatsAppSession session, string categoryName,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        var canonicalPhone = PhoneNumberNormalizer.NormalizeBrazilian(session.PhoneNumber);
        var userStillValid = canonicalPhone is null
            ? null
            : await ResolveUser(canonicalPhone, ct);
        var identityStillValid = userStillValid is null
            ? null
            : await ResolveResidentialContext(userStillValid, ct);
        var categoryValid = await db.Categories.AsNoTracking().AnyAsync(x =>
            x.Id == session.CategoryId && x.CondominiumId == session.CondominiumId && x.IsActive, ct);
        var review = Review(session);
        if (identityStillValid is null
            || identityStillValid.UserId != session.UserId
            || identityStillValid.CondominiumId != session.CondominiumId
            || identityStillValid.UnitId != session.UnitId
            || !categoryValid || string.IsNullOrWhiteSpace(session.DraftDescription)
            || review is null)
        {
            session.InvalidateIdentity(now, expires);
            return (IdentificationFailure, "confirmation_revalidation_failed");
        }

        var originalReport = session.DraftDescription;
        var description = review.Source == FallbackReviewSource
            ? originalReport
            : review.Proposal!.Description;
        var title = review.Source == FallbackReviewSource
            ? FallbackRequestTitle
            : review.Proposal!.Title;
        var shouldIntroducePortal = false;
        try
        {
            shouldIntroducePortal = !await db.Requests.AsNoTracking()
                .AnyAsync(x => x.AuthorUserId == session.UserId.Value, ct);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception,
                "Could not determine WhatsApp first-request onboarding eligibility; request creation will continue.");
        }

        var request = new DomainRequest(
            session.CondominiumId!.Value, session.UserId!.Value, session.UnitId,
            session.CategoryId!.Value, title, description, RequestSource.WhatsApp);
        db.Requests.Add(request);
        db.RequestStatusHistories.Add(new RequestStatusHistory(
            request.Id, null, RequestStatus.InProgress, session.UserId.Value, null, request.CreatedAt));
        var originalMessage = new RequestMessage(
            request.Id, session.UserId.Value, originalReport, MessageChannel.WhatsApp);
        db.RequestMessages.Add(originalMessage);
        if (review.Source == AiReviewSource)
        {
            db.RequestAiAnalyses.Add(new RequestAiAnalysis(
                request.Id,
                review.Proposal!.Title,
                review.Proposal.Description,
                review.Proposal.SuggestedCategory,
                review.Proposal.Confidence,
                JsonSerializer.Serialize(review.Proposal.MissingInformation),
                review.Model));
        }
        var drafts = await db.WhatsAppDraftAttachments
            .Where(x => x.SessionId == session.Id).ToArrayAsync(ct);
        var promotedKeys = new List<string>();

        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var draft in drafts)
            {
                var extension = Path.GetExtension(draft.OriginalFileName);
                var key = storage.PromoteWhatsAppDraft(request.Id, draft.StorageKey, extension);
                promotedKeys.Add(key);
                db.RequestAttachments.Add(new RequestAttachment(
                    request.Id, session.UserId.Value, draft.OriginalFileName,
                    key, draft.ContentType, draft.FileSize,
                    draft.Id == review.OriginalAudioDraftId ? originalMessage.Id : null));
            }
            db.WhatsAppDraftAttachments.RemoveRange(drafts);
            session.CompleteRequest(request.Id, now, expires);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            foreach (var draft in drafts) storage.Delete(draft.StorageKey);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            foreach (var key in promotedKeys) storage.Delete(key);
            foreach (var entry in db.ChangeTracker.Entries().Where(x =>
                x.State == EntityState.Added
                && x.Entity is DomainRequest or RequestStatusHistory
                    or RequestMessage or RequestAttachment or RequestAiAnalysis))
                entry.State = EntityState.Detached;
            foreach (var draft in drafts)
                if (db.Entry(draft).State == EntityState.Deleted)
                    db.Entry(draft).State = EntityState.Unchanged;
            throw;
        }
        logger.LogInformation("WhatsApp request {RequestId} created.", request.Id);

        try { await notifications.NotifyRequestCreatedAsync(request, categoryName, ct); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to notify creation of WhatsApp request {RequestId}.", request.Id);
        }
        var response = $"Solicitação criada com sucesso.\n\nProtocolo: {ShortId(request.Id)}";
        if (shouldIntroducePortal)
        {
            var portalUrl = options.Value.PortalUrl?.Trim().TrimEnd('/');
            if (!string.IsNullOrWhiteSpace(portalUrl))
                response += $"\n\nVocê pode acompanhar as atualizações por aqui. Se preferir, consulte também o histórico completo no Comvy:\n{portalUrl}";
        }
        response += "\n\nPara iniciar outro atendimento, basta chamar novamente!";
        return (response, "request_created");
    }

    private static (string, string) Recover(
        WhatsAppSession session, string fullName, DateTime now, DateTime expires)
    {
        session.Restart(now, expires);
        return (MainMenu(fullName), "context_recovered");
    }

    private Task<CategoryChoice[]> ActiveCategories(Guid condominiumId, CancellationToken ct) =>
        db.Categories.AsNoTracking().Where(x => x.CondominiumId == condominiumId && x.IsActive)
            .OrderBy(x => x.Name).ThenBy(x => x.Id)
            .Select(x => new CategoryChoice(x.Id, x.Name)).ToArrayAsync(ct);

    private static string MainMenu(string fullName) =>
        $"Olá, {FirstName(fullName)}! Como posso ajudar?\n\n" +
        "1 - Abrir uma solicitação\n" +
        "2 - Ver os status de minhas solicitações\n" +
        "3 - Falar sobre uma solicitação existente\n\n" +
        "Digite uma opção. Você também pode enviar ‘menu’ para recomeçar ou ‘sair’ para encerrar.";

    private static string DescriptionPrompt() =>
        "Conte o que aconteceu em uma mensagem. Você também pode enviar um áudio de até 2 minutos.\n\n" +
        "Depois, você poderá adicionar fotos, vídeos ou documentos.";

    private static string AttachmentPrompt() =>
        "Deseja adicionar fotos, vídeos ou documentos?\n\n" +
        "Se sim, envie os arquivos agora. Quando terminar, responda com uma das opções:\n\n" +
        "1 - Terminei de enviar os arquivos\n" +
        "2 - Não quero enviar arquivos\n" +
        "3 - Cancelar e voltar ao início";

    private static string ResidentReplyOfferPrompt(string question, bool fromDetails) =>
        "A administração está aguardando uma resposta sua:\n\n" + question.Trim() + "\n\n" +
        (fromDetails
            ? "1 - Responder agora\n2 - Ver detalhes\n0 - Voltar ao menu"
            : "1 - Responder agora\n2 - Responder depois");

    private static string RequestUpdatePrompt() =>
        "Envie sua mensagem.\nVocê também pode enviar fotos, documentos, vídeos ou áudio.\n\n"
        + "Quando terminar, envie ‘Finalizar’.\nPara encerrar este atendimento, envie ‘Cancelar’.";

    private static string RequestUpdateReceivedPrompt(string confirmation) =>
        confirmation + "\n\nVocê pode enviar outra mensagem ou arquivo.\n\n"
        + "1 - Finalizar\nCancelar - Encerrar este atendimento";

    private static string ResidentReplyInputPrompt(string? question = null) =>
        (string.IsNullOrWhiteSpace(question)
            ? string.Empty
            : "A administração precisa da seguinte informação:\n\n"
                + question.Trim() + "\n\n") +
        "Envie sua resposta por texto ou áudio.";

    private static string ResidentReplyReviewPrompt(ResidentReplyReview review) =>
        (review.Source == AiReviewSource
            ? "Revise sua resposta antes de enviá-la.\n\n*Resposta:*\n"
            : "Você respondeu:\n\n") + review.Answer + "\n\n" +
        "1 - Confirmar resposta\n2 - Corrigir relato\n" +
        "3 - Cancelar e responder depois";

    private static string ResidentReplyAttachmentPrompt() =>
        "Deseja adicionar fotos, vídeos ou documentos?\n\n" +
        "Se sim, envie os arquivos agora.\n\nQuando terminar:\n\n" +
        "1 - Terminei de enviar os arquivos\n" +
        "2 - Não quero enviar arquivos\n" +
        "3 - Cancelar e responder depois";

    private static string ReviewPrompt(RequestDraftAiProposal proposal)
    {
        return "Revise sua solicitação antes de enviá-la.\n\n" +
            $"*Título:*\n{proposal.Title}\n\n" +
            $"*Descrição:*\n{proposal.Description}\n\n" +
            "1 - Confirmar solicitação\n" +
            "2 - Corrigir relato\n" +
            "3 - Cancelar e voltar ao início";
    }

    private static string FallbackReviewPrompt(string originalReport) =>
        $"Você descreveu:\n\n{originalReport}\n\n" +
        "1 - Confirmar e continuar\n" +
        "2 - Corrigir relato\n" +
        "3 - Cancelar e voltar ao início";

    private static string NormalizeCommand(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var normalized = value.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
            if (CharUnicodeInfo.GetUnicodeCategory(character) != UnicodeCategory.NonSpacingMark)
                builder.Append(character);
        return string.Join(' ', builder.ToString().Normalize(NormalizationForm.FormC)
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string ProcessingBranch(
        WhatsAppConversationState state, NormalizedWhatsAppMessage message) =>
        state switch
        {
            WhatsAppConversationState.CollectingDescription
                when message.MessageType == "audio" => "audio",
            WhatsAppConversationState.CollectingDescription
                when message.MessageType == "text" => "text",
            WhatsAppConversationState.CollectingAttachments
                when message.MessageType is "image" or "video" or "document" => "attachment",
            _ when message.ParsedMessageType == "quick_reply" => "quick_reply",
            _ when message.MessageType is "text" or "interactive" => "text",
            _ => "unsupported"
        };

    private static string FirstName(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Olá";
    private static string ShortId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private async Task<(string, string)> GenerateAiProposal(
        WhatsAppSession session, DateTime now, DateTime expires, CancellationToken ct)
    {
        await requestCategories.GetOrCreateOtherAsync(
            session.CondominiumId!.Value, ct);
        var categories = await ActiveCategories(session.CondominiumId!.Value, ct);
        var condominiumName = await db.Condominiums.AsNoTracking()
            .Where(x => x.Id == session.CondominiumId.Value)
            .Select(x => x.Name).SingleAsync(ct);
        var result = await requestDraftAi.ProposeAsync(
            session.DraftDescription!, categories.Select(x => x.Name).ToArray(),
            condominiumName, ct);
        var review = result.Succeeded && result.Proposal is not null
            ? new RequestDraftReview(AiReviewSource, result.Proposal, result.Model,
                OriginalAudioDraftId(session))
            : new RequestDraftReview(FallbackReviewSource, null, null,
                OriginalAudioDraftId(session));
        session.SetAiProposal(JsonSerializer.Serialize(review), now, expires);
        logger.LogInformation(result.Succeeded
            ? "Request draft AI proposal generated."
            : "Request draft AI unavailable; safe fallback proposal generated.");
        return (review.Source == AiReviewSource
                ? ReviewPrompt(review.Proposal!)
                : FallbackReviewPrompt(session.DraftDescription!), result.Succeeded
            ? "reviewing_ai_proposal" : "reviewing_fallback_proposal");
    }

    private static RequestDraftReview? Review(WhatsAppSession session)
    {
        if (string.IsNullOrWhiteSpace(session.DraftAiProposalJson)) return null;
        try
        {
            using var document = JsonDocument.Parse(session.DraftAiProposalJson);
            if (document.RootElement.TryGetProperty(nameof(RequestDraftReview.Source), out _))
            {
                var review = JsonSerializer.Deserialize<RequestDraftReview>(
                    session.DraftAiProposalJson);
                if (review?.Source == FallbackReviewSource && review.Proposal is null)
                    return review;
                if (review?.Source == AiReviewSource && review.Proposal is not null)
                    return review;
                return null;
            }

            // Propostas persistidas antes da inclusão da origem eram sempre revisões de IA.
            var legacyProposal = JsonSerializer.Deserialize<RequestDraftAiProposal>(
                session.DraftAiProposalJson);
            return legacyProposal is null
                || string.IsNullOrWhiteSpace(legacyProposal.Title)
                || string.IsNullOrWhiteSpace(legacyProposal.Description)
                || legacyProposal.MissingInformation is null
                ? null
                : new RequestDraftReview(AiReviewSource, legacyProposal, null, null);
        }
        catch (JsonException) { return null; }
    }

    private static ResidentReplyReview? ResidentReview(WhatsAppSession session)
    {
        if (string.IsNullOrWhiteSpace(session.DraftAiProposalJson)) return null;
        try
        {
            var review = JsonSerializer.Deserialize<ResidentReplyReview>(
                session.DraftAiProposalJson);
            return review is not null
                && review.Source is AiReviewSource or FallbackReviewSource
                && !string.IsNullOrWhiteSpace(review.Answer)
                ? review : null;
        }
        catch (JsonException) { return null; }
    }

    private async Task DiscardDraftAttachments(WhatsAppSession session, CancellationToken ct)
    {
        var drafts = await db.WhatsAppDraftAttachments
            .Where(x => x.SessionId == session.Id).ToArrayAsync(ct);
        if (drafts.Length == 0) return;
        db.WhatsAppDraftAttachments.RemoveRange(drafts);
        await db.SaveChangesAsync(ct);
        foreach (var draft in drafts) storage.Delete(draft.StorageKey);
    }

    private async Task DiscardOriginalAudioDraft(
        WhatsAppSession session, CancellationToken ct)
    {
        var audioDraftId = OriginalAudioDraftId(session);
        if (audioDraftId is null) return;
        var draft = await db.WhatsAppDraftAttachments.SingleOrDefaultAsync(
            x => x.Id == audioDraftId && x.SessionId == session.Id, ct);
        if (draft is null) return;
        db.WhatsAppDraftAttachments.Remove(draft);
        await db.SaveChangesAsync(ct);
        storage.Delete(draft.StorageKey);
    }

    private static Guid? OriginalAudioDraftId(WhatsAppSession session)
    {
        if (string.IsNullOrWhiteSpace(session.DraftAiProposalJson)) return null;
        try
        {
            using var document = JsonDocument.Parse(session.DraftAiProposalJson);
            return document.RootElement.TryGetProperty(
                    nameof(RequestDraftReview.OriginalAudioDraftId), out var id)
                && id.ValueKind == JsonValueKind.String
                && Guid.TryParse(id.GetString(), out var parsed)
                    ? parsed
                    : null;
        }
        catch (JsonException) { return null; }
    }

    private static bool IsAudioTranscriptionFailure(WhatsAppSession session)
    {
        if (string.IsNullOrWhiteSpace(session.DraftAiProposalJson)) return false;
        try
        {
            using var document = JsonDocument.Parse(session.DraftAiProposalJson);
            return document.RootElement.TryGetProperty(nameof(RequestDraftReview.Source), out var source)
                && source.GetString() == AudioFailureSource;
        }
        catch (JsonException) { return false; }
    }

    private static string AudioFailurePrompt() =>
        "Não consegui compreender o áudio.\n\n" +
        "Você pode:\n\n" +
        "1 - Enviar outro áudio\n" +
        "2 - Escrever a descrição\n" +
        "3 - Cancelar";

    private static void MarkAudioFailure(
        WhatsAppSession session, DateTime now, DateTime expires) =>
        session.MarkAudioTranscriptionFailure(
            JsonSerializer.Serialize(new RequestDraftReview(
                AudioFailureSource, null, null, null)), now, expires);

    private sealed record ResolvedIdentity(Guid UserId, string FullName, Guid CondominiumId, Guid UnitId);
    private sealed record ResidentialContext(Guid CondominiumId, Guid UnitId, bool IsPrimaryResidence);
    private sealed record CategoryChoice(Guid Id, string Name);
    private sealed record OwnRequestItem(Guid Id, string Title, string Description,
        RequestStatus Status, DateTime UpdatedAt);
    private sealed record OwnRequestPage(OwnRequestItem[] Items, int Page, int Total,
        bool HasNext);
    private sealed record OwnRequestUpdate(Guid Id, Guid AuthorUserId, string Content,
        DateTime CreatedAt);
    private sealed record ResidentReplyRequirement(
        Guid RequestId, Guid Id, string Question);
    private sealed record OutboundCorrelationResult(
        ResidentReplyRequirement? Requirement, bool OutboundMatched,
        string Reason);
    private sealed record ResidentReplyReview(string Source, string Answer,
        Guid? OriginalAudioDraftId);
    private const string PendingAudioSource = "pending_audio";
    private const string AudioFailureSource = "audio_failure";
    private sealed record RequestDraftReview(
        string Source, RequestDraftAiProposal? Proposal, string? Model,
        Guid? OriginalAudioDraftId);
}
