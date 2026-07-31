using System.Globalization;
using System.Text;
using System.Text.Json;
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
    NotificationService notifications,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppConversationService> logger)
{
    private const string IdentificationFailure =
        "Não consegui identificar seu cadastro. Entre em contato com a administração do condomínio para verificar seu número.";

    public async Task ProcessAsync(NormalizedWhatsAppMessage message, CancellationToken ct)
    {
        var phone = PhoneNumberNormalizer.NormalizeWhatsAppIdentifier(message.PhoneNumber);
        if (phone is null || string.IsNullOrWhiteSpace(message.ExternalMessageId)) return;

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
        var identity = await ResolveIdentity(phone, ct);
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
        if (identity is null)
        {
            session.InvalidateIdentity(now, expires);
            logger.LogWarning("WhatsApp session state assigned after context validation: {State}.", session.State);
            response = IdentificationFailure;
            result = "identity_not_resolved";
        }
        else
        {
            try
            {
                if (!isNewSession && session.State == WhatsAppConversationState.UnknownPhone)
                {
                    session.RecoverContext(
                        identity.UserId, identity.CondominiumId, identity.UnitId,
                        now, expires);
                    logger.LogInformation(
                        "WhatsApp session residential context recovered from UnknownPhone.");
                    response = MainMenu(identity.FullName);
                    result = "main_menu";
                }
                else
                {
                    session.ResolveContext(identity.UserId, identity.CondominiumId, identity.UnitId);
                    (response, result) = await Respond(
                        session, identity, message, now, expires, isNewSession, ct);
                }
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

        inbound.Complete(identity?.UserId, result, now);
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

    private async Task<ResolvedIdentity?> ResolveIdentity(string phone, CancellationToken ct)
    {
        var candidates = PhoneNumberNormalizer.IdentificationCandidates(phone);
        var users = await db.Set<ApplicationUser>().AsNoTracking()
            .Where(x => x.NormalizedPhoneNumber != null
                && candidates.Contains(x.NormalizedPhoneNumber))
            .Select(x => new { x.Id, x.FullName, x.IsActive, x.NormalizedPhoneNumber })
            .Take(3).ToArrayAsync(ct);
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
        logger.LogInformation(user.NormalizedPhoneNumber == phone
            ? "Exact WhatsApp phone match found."
            : "Brazilian WhatsApp phone variant match found.");
        logger.LogInformation("Unique WhatsApp user found by canonical phone.");

        var unitLinks = await db.UnitMemberships.AsNoTracking()
            .Where(x => x.UserId == user.Id && x.IsActive && x.EndedAt == null)
            .Select(x => new { x.UnitId, x.IsResident, x.IsPrimaryResidence })
            .ToArrayAsync(ct);
        if (unitLinks.Length == 0)
        {
            logger.LogWarning("No active unit membership found for WhatsApp user.");
            return null;
        }

        var residentialLinks = unitLinks.Where(x => x.IsResident).ToArray();
        if (residentialLinks.Length == 0)
        {
            logger.LogWarning("No eligible residential membership found for WhatsApp user.");
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
            logger.LogWarning("No eligible residential membership found for WhatsApp user.");
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

        logger.LogInformation("WhatsApp residential context resolved successfully.");
        return new ResolvedIdentity(user.Id, user.FullName,
            resolved.CondominiumId, resolved.UnitId);
    }

    private async Task<(string Response, string Result)> Respond(
        WhatsAppSession session, ResolvedIdentity identity,
        NormalizedWhatsAppMessage message, DateTime now, DateTime expires,
        bool isNewSession, CancellationToken ct)
    {
        if (isNewSession)
        {
            session.Restart(now, expires);
            return (MainMenu(identity.FullName), "main_menu");
        }
        if (session.ExpiresAt <= now || session.State == WhatsAppConversationState.Ended)
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

        var text = message.Text?.Trim();
        var command = NormalizeCommand(text);
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
        if (command == "cancelar")
        {
            if (session.State == WhatsAppConversationState.MainMenu)
            {
                session.Touch(now, expires);
                return ("Não há operação em andamento. Digite 1 para abrir uma solicitação.", "nothing_to_cancel");
            }
            await DiscardDraftAttachments(session, ct);
            session.Restart(now, expires);
            logger.LogInformation("WhatsApp draft flow cancelled for phone {Phone}.", PhoneNumberNormalizer.Mask(session.PhoneNumber));
            return ($"A abertura foi cancelada.\n\n{MainMenu(identity.FullName)}", "cancelled");
        }

        return session.State switch
        {
            WhatsAppConversationState.MainMenu => MainMenuChoice(session, text, now, expires),
            WhatsAppConversationState.CollectingDescription =>
                CollectDescription(session, message, now, expires),
            WhatsAppConversationState.CollectingAttachments =>
                await CollectAttachments(session, message, identity.FullName, now, expires, ct),
            WhatsAppConversationState.ReviewingNewRequest =>
                await ReviewChoice(session, text, identity.FullName, now, expires, ct),
            WhatsAppConversationState.SelectingCategory =>
                await SelectCategory(session, text, identity.FullName, now, expires, ct),
            _ => Recover(session, identity.FullName, now, expires)
        };
    }

    private (string, string) MainMenuChoice(
        WhatsAppSession session, string? text, DateTime now, DateTime expires)
    {
        if (text == "1")
        {
            session.BeginDescription(now, expires);
            return (DescriptionPrompt(), "collecting_description");
        }
        if (text is "2" or "3" or "4")
        {
            session.Touch(now, expires);
            return ("Essa opção estará disponível em breve. Digite ‘menu’ para voltar.", "option_unavailable");
        }
        session.Touch(now, expires);
        return ("Para abrir uma solicitação, digite 1.", "invalid_main_menu_choice");
    }

    private static (string, string) CollectDescription(
        WhatsAppSession session, NormalizedWhatsAppMessage message,
        DateTime now, DateTime expires)
    {
        if (message.MessageType != "text" || string.IsNullOrWhiteSpace(message.Text))
            return (DescriptionPrompt(), "description_required");
        var description = message.Text.Trim();
        if (description.Length > 4000)
            return ("A descrição deve ter no máximo 4000 caracteres. Envie um texto menor.", "description_too_long");
        session.SetDescriptionForReview(description, now, expires);
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
            return ("Digite 1 para confirmar, 2 para corrigir a descrição ou 3 para cancelar.", "invalid_confirmation_choice");
        }

        var proposal = Proposal(session);
        if (proposal is null)
            return await GenerateAiProposal(session, now, expires, ct);
        var categories = await ActiveCategories(session.CondominiumId!.Value, ct);
        var suggested = categories.SingleOrDefault(x => string.Equals(
            x.Name, proposal.SuggestedCategory, StringComparison.OrdinalIgnoreCase));
        if (suggested is not null)
        {
            session.ChooseCategory(suggested.Id, now, expires);
            return await CreateRequest(session, suggested.Name, now, expires, ct);
        }
        if (categories.Length == 1)
        {
            session.ChooseCategory(categories[0].Id, now, expires);
            return await CreateRequest(session, categories[0].Name, now, expires, ct);
        }
        session.BeginCategorySelection(now, expires);
        return categories.Length == 0
            ? ("Não há uma categoria ativa disponível para concluir a solicitação. Digite ‘menu’ para voltar.", "category_unavailable")
            : (CategoryMenu(categories), "selecting_category");
    }

    private async Task<(string, string)> SelectCategory(
        WhatsAppSession session, string? text, string fullName,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        var categories = await ActiveCategories(session.CondominiumId!.Value, ct);
        if (int.TryParse(text, out var choice) && choice >= 1 && choice <= categories.Length)
        {
            var category = categories[choice - 1];
            session.ChooseCategory(category.Id, now, expires);
            return await CreateRequest(session, category.Name, now, expires, ct);
        }
        session.Touch(now, expires);
        return categories.Length == 0
            ? ($"Não há uma categoria ativa disponível. Digite ‘menu’ para voltar.\n\n{MainMenu(fullName)}", "category_unavailable")
            : ("Escolha uma categoria válida.\n\n" + CategoryMenu(categories), "invalid_category_choice");
    }

    private async Task<(string, string)> CreateRequest(
        WhatsAppSession session, string categoryName,
        DateTime now, DateTime expires, CancellationToken ct)
    {
        var identityStillValid = await ResolveIdentity(session.PhoneNumber, ct);
        var categoryValid = await db.Categories.AsNoTracking().AnyAsync(x =>
            x.Id == session.CategoryId && x.CondominiumId == session.CondominiumId && x.IsActive, ct);
        var proposal = Proposal(session);
        if (identityStillValid is null
            || identityStillValid.UserId != session.UserId
            || identityStillValid.CondominiumId != session.CondominiumId
            || identityStillValid.UnitId != session.UnitId
            || !categoryValid || string.IsNullOrWhiteSpace(session.DraftDescription)
            || proposal is null)
        {
            session.InvalidateIdentity(now, expires);
            return (IdentificationFailure, "confirmation_revalidation_failed");
        }

        var originalReport = session.DraftDescription;
        var description = proposal.Description;
        var title = proposal.Title;
        var request = new DomainRequest(
            session.CondominiumId!.Value, session.UserId!.Value, session.UnitId,
            session.CategoryId!.Value, title, description, RequestSource.WhatsApp);
        db.Requests.Add(request);
        db.RequestStatusHistories.Add(new RequestStatusHistory(
            request.Id, null, RequestStatus.Open, session.UserId.Value, null, request.CreatedAt));
        db.RequestMessages.Add(new RequestMessage(
            request.Id, session.UserId.Value, originalReport, MessageChannel.WhatsApp));
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
                    key, draft.ContentType, draft.FileSize));
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
            throw;
        }
        logger.LogInformation("WhatsApp request {RequestId} created.", request.Id);

        try { await notifications.NotifyRequestCreatedAsync(request, categoryName, ct); }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to notify creation of WhatsApp request {RequestId}.", request.Id);
        }
        return ($"Solicitação criada com sucesso.\n\nProtocolo: {ShortId(request.Id)}\n\nPara iniciar outro atendimento, basta chamar novamente!", "request_created");
    }

    private static (string, string) Recover(
        WhatsAppSession session, string fullName, DateTime now, DateTime expires)
    {
        session.Restart(now, expires);
        return (MainMenu(fullName), "context_recovered");
    }

    private Task<CategoryChoice[]> ActiveCategories(Guid condominiumId, CancellationToken ct) =>
        db.Categories.AsNoTracking().Where(x => x.CondominiumId == condominiumId && x.IsActive)
            .OrderBy(x => x.Name).Select(x => new CategoryChoice(x.Id, x.Name)).Take(20).ToArrayAsync(ct);

    private static string MainMenu(string fullName) =>
        $"Olá, {FirstName(fullName)}! Como posso ajudar?\n\n" +
        "1 - Abrir uma solicitação\n" +
        "2 - Acompanhar minhas solicitações\n" +
        "3 - Falar sobre uma solicitação existente\n" +
        "4 - Falar com a administração\n\n" +
        "Digite o número da opção.\n\n" +
        "A qualquer momento, envie ‘menu’ para recomeçar ou ‘sair’ para encerrar.";

    private static string DescriptionPrompt() =>
        "Descreva o que aconteceu em uma só mensagem, com o máximo de detalhes que puder.\n\n" +
        "Pode escrever o quanto precisar. Usaremos essas informações para abrir sua solicitação.\n\n" +
        "Depois da descrição, você poderá adicionar fotos e vídeos.";

    private static string AttachmentPrompt() =>
        "Deseja adicionar fotos, vídeos ou documentos?\n\n" +
        "Se sim, envie os arquivos agora. Quando terminar, responda com uma das opções:\n\n" +
        "1 - Terminei de enviar os arquivos\n" +
        "2 - Não quero enviar arquivos\n" +
        "3 - Cancelar e voltar ao início";

    private static string ReviewPrompt(RequestDraftAiProposal proposal)
    {
        var text = "Entendi sua solicitação desta forma:\n\n" +
            $"Título\n\n{proposal.Title}\n\n" +
            $"Descrição\n\n{proposal.Description}\n\n" +
            $"Categoria\n\n{proposal.SuggestedCategory ?? "Não identificada"}";
        if (proposal.MissingInformation.Length > 0)
            text += "\n\nA IA identificou que talvez faltem estas informações:\n\n" +
                string.Join("\n", proposal.MissingInformation);
        return text + "\n\n1 - Confirmar solicitação\n" +
            "2 - Reescrever descrição\n" +
            "3 - Cancelar e voltar ao início";
    }

    private static string CategoryMenu(CategoryChoice[] categories) =>
        "Escolha a categoria da solicitação:\n\n" +
        string.Join('\n', categories.Select((x, i) => $"{i + 1} - {x.Name}"));

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

    private static string FirstName(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Olá";
    private static string ShortId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private async Task<(string, string)> GenerateAiProposal(
        WhatsAppSession session, DateTime now, DateTime expires, CancellationToken ct)
    {
        var categories = await ActiveCategories(session.CondominiumId!.Value, ct);
        var result = await requestDraftAi.ProposeAsync(
            session.DraftDescription!, categories.Select(x => x.Name).ToArray(), ct);
        var proposal = result.Succeeded && result.Proposal is not null
            ? result.Proposal
            : FallbackProposal(session.DraftDescription!);
        session.SetAiProposal(JsonSerializer.Serialize(proposal), now, expires);
        logger.LogInformation(result.Succeeded
            ? "Request draft AI proposal generated."
            : "Request draft AI unavailable; safe fallback proposal generated.");
        return (ReviewPrompt(proposal), result.Succeeded
            ? "reviewing_ai_proposal" : "reviewing_fallback_proposal");
    }

    private static RequestDraftAiProposal? Proposal(WhatsAppSession session)
    {
        if (string.IsNullOrWhiteSpace(session.DraftAiProposalJson)) return null;
        try
        {
            return JsonSerializer.Deserialize<RequestDraftAiProposal>(
                session.DraftAiProposalJson);
        }
        catch (JsonException) { return null; }
    }

    private static RequestDraftAiProposal FallbackProposal(string originalReport) =>
        new(originalReport.Length <= 200 ? originalReport : originalReport[..200],
            originalReport, null, [], null);

    private async Task DiscardDraftAttachments(WhatsAppSession session, CancellationToken ct)
    {
        var drafts = await db.WhatsAppDraftAttachments
            .Where(x => x.SessionId == session.Id).ToArrayAsync(ct);
        if (drafts.Length == 0) return;
        db.WhatsAppDraftAttachments.RemoveRange(drafts);
        await db.SaveChangesAsync(ct);
        foreach (var draft in drafts) storage.Delete(draft.StorageKey);
    }

    private sealed record ResolvedIdentity(Guid UserId, string FullName, Guid CondominiumId, Guid UnitId);
    private sealed record ResidentialContext(Guid CondominiumId, Guid UnitId, bool IsPrimaryResidence);
    private sealed record CategoryChoice(Guid Id, string Name);
}
