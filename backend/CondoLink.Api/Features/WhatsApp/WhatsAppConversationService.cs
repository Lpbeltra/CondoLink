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
    NotificationService notifications,
    IOptions<WhatsAppOptions> options,
    ILogger<WhatsAppConversationService> logger)
{
    private const int PageSize = 5;
    private static readonly IReadOnlyDictionary<string, string[]> AllowedMedia =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            [".jpg"] = ["image/jpeg"], [".jpeg"] = ["image/jpeg"],
            [".png"] = ["image/png"], [".webp"] = ["image/webp"],
            [".pdf"] = ["application/pdf"]
        };

    public async Task ProcessAsync(NormalizedWhatsAppMessage message, CancellationToken ct)
    {
        var phone = PhoneNumberNormalizer.NormalizeBrazilian(message.PhoneNumber);
        if (phone is null || string.IsNullOrWhiteSpace(message.ExternalMessageId)) return;
        if (await db.WhatsAppInboundMessages.AsNoTracking()
            .AnyAsync(x => x.ExternalMessageId == message.ExternalMessageId, ct))
        {
            logger.LogInformation(
                "Duplicate WhatsApp webhook message was acknowledged idempotently.");
            return;
        }

        var inbound = new WhatsAppInboundMessage(
            message.ExternalMessageId, phone, message.MessageType, message.Text,
            message.ProviderTimestamp);
        db.WhatsAppInboundMessages.Add(inbound);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            db.Entry(inbound).State = EntityState.Detached;
            if (await db.WhatsAppInboundMessages.AsNoTracking()
                .AnyAsync(x => x.ExternalMessageId == message.ExternalMessageId, ct))
            {
                logger.LogInformation(
                    "Duplicate WhatsApp webhook message was acknowledged idempotently.");
                return;
            }
            throw;
        }

        var candidates = await db.Set<ApplicationUser>().AsNoTracking()
            .Where(x => x.IsActive && x.NormalizedPhoneNumber == phone)
            .Select(x => new { x.Id, x.FullName })
            .Take(2)
            .ToArrayAsync(ct);
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(Math.Clamp(options.Value.SessionExpirationMinutes, 5, 1440));
        var session = await db.WhatsAppSessions.SingleOrDefaultAsync(x => x.PhoneNumber == phone, ct);
        if (session is null)
        {
            session = new WhatsAppSession(phone, now, expires);
            db.WhatsAppSessions.Add(session);
        }

        string response;
        string result;
        Guid? userId = null;
        if (candidates.Length != 1)
        {
            var ambiguous = candidates.Length > 1;
            session.MoveTo(
                ambiguous ? WhatsAppConversationState.AmbiguousPhone
                    : WhatsAppConversationState.UnknownPhone, now, expires);
            response = ambiguous
                ? "Encontramos mais de um cadastro ativo para este telefone. Entre em contato com a administração."
                : "Não localizamos um cadastro ativo para este telefone. Entre em contato com a administração. Não envie senha ou CPF completo.";
            result = ambiguous ? "ambiguous_phone" : "unknown_phone";
        }
        else
        {
            userId = candidates[0].Id;
            session.Identify(userId.Value);
            var condominiums = await ActiveCondominiums(userId.Value, ct);
            if (condominiums.Length == 0)
            {
                session.MoveTo(WhatsAppConversationState.UnknownPhone, now, expires);
                response = "Seu telefone foi identificado, mas não há vínculo ativo com condomínio.";
                result = "no_active_membership";
            }
            else
            {
                (response, result) = await Respond(
                    session, candidates[0].FullName, condominiums, message, now, expires, ct);
            }
        }

        inbound.Complete(userId, result, now);
        await db.SaveChangesAsync(ct);
        var send = await client.SendTextAsync(phone, response, ct);
        logger.Log(
            send.Succeeded ? LogLevel.Information : LogLevel.Warning,
            "WhatsApp event {EventId} result {Result} phone {Phone}.",
            message.ExternalMessageId, result, PhoneNumberNormalizer.Mask(phone));
    }

    private async Task<(string, string)> Respond(
        WhatsAppSession session, string fullName, CondominiumChoice[] condominiums,
        NormalizedWhatsAppMessage message, DateTime now, DateTime expires, CancellationToken ct)
    {
        var text = message.Text?.Trim();
        if (session.ExpiresAt < now && session.HasDraft)
        {
            await DiscardDraft(session, ct);
            session.Reset(now, expires);
            var menu = SelectCondominiumOrMenu(session, fullName, condominiums, now, expires);
            return ($"Sua sessão expirou e o rascunho foi descartado com segurança.\n\n{menu.Item1}", "draft_expired");
        }
        if (session.ExpiresAt < now || session.State == WhatsAppConversationState.Ended)
        {
            session.Reset(now, expires);
            var menu = SelectCondominiumOrMenu(session, fullName, condominiums, now, expires);
            return ($"Sua sessão anterior expirou ou foi encerrada.\n\n{menu.Item1}", "session_restarted");
        }

        var command = text?.ToLowerInvariant();
        if (command == "parar atualizações" || command == "parar atualizacoes")
        {
            var user = await db.Users.SingleAsync(x => x.Id == session.UserId, ct);
            user.SetReceiveWhatsAppUpdates(false);
            return ("As atualizações pelo WhatsApp foram desativadas.\n\nVocê ainda pode usar este número para consultar ou abrir solicitações. Digite Menu para iniciar.", "updates_disabled");
        }
        if (command == "ativar atualizações" || command == "ativar atualizacoes")
        {
            var user = await db.Users.SingleAsync(x => x.Id == session.UserId, ct);
            user.SetReceiveWhatsAppUpdates(true);
            return ("As atualizações pelo WhatsApp foram ativadas. Digite Menu para iniciar um atendimento.", "updates_enabled");
        }
        if (command == "ajuda") return (Help(session), "help");
        if (command == "sair")
        {
            await DiscardDraft(session, ct);
            session.End(now);
            return ("Atendimento encerrado. Envie uma nova mensagem quando precisar.", "session_ended");
        }
        if (command == "cancelar")
        {
            await DiscardDraft(session, ct);
            session.Reset(now, expires);
            return SelectCondominiumOrMenu(session, fullName, condominiums, now, expires)
                with { Item2 = "cancelled" };
        }
        if (command is "menu" or "início" or "inicio")
        {
            if (session.HasDraft)
            {
                session.MoveTo(WhatsAppConversationState.ConfirmingResume, now, expires, session.CondominiumId);
                return ("Você possui uma solicitação não enviada.\n\n1 — Descartar e ir ao menu\n2 — Continuar preenchimento", "confirming_draft_discard");
            }
            session.Reset(now, expires);
            return SelectCondominiumOrMenu(session, fullName, condominiums, now, expires);
        }
        if (command == "voltar") return await GoBack(session, fullName, condominiums, now, expires, ct);

        if (session.State == WhatsAppConversationState.ConfirmingResume)
        {
            if (text == "1")
            {
                await DiscardDraft(session, ct);
                session.Reset(now, expires);
                return SelectCondominiumOrMenu(session, fullName, condominiums, now, expires);
            }
            if (text == "2")
            {
                session.ReturnToPrevious(now, expires);
                return (PromptForState(session), "draft_resumed");
            }
            return ("Escolha 1 para descartar o rascunho ou 2 para continuar.", "invalid_resume_choice");
        }
        if (session.State == WhatsAppConversationState.SelectingCondominium)
            return SelectCondominium(session, fullName, condominiums, text, now, expires);
        if (session.CondominiumId is null)
            return SelectCondominiumOrMenu(session, fullName, condominiums, now, expires);

        return session.State switch
        {
            WhatsAppConversationState.MainMenu =>
                await MainMenuChoice(session, fullName, text, now, expires, ct),
            WhatsAppConversationState.SelectingUnit =>
                await SelectUnit(session, text, now, expires, ct),
            WhatsAppConversationState.SelectingCategory =>
                await SelectCategory(session, text, now, expires, ct),
            WhatsAppConversationState.CollectingDescription =>
                CollectDescription(session, message, now, expires),
            WhatsAppConversationState.CollectingNewRequestAttachments =>
                await CollectDraftAttachment(session, message, text, now, expires, ct),
            WhatsAppConversationState.ReviewingNewRequest =>
                await ReviewChoice(session, text, now, expires, ct),
            WhatsAppConversationState.SelectingOpenRequest =>
                await SelectOpenRequest(session, text, now, expires, ct),
            WhatsAppConversationState.ViewingRequest =>
                await ViewRequestChoice(session, text, now, expires, ct),
            WhatsAppConversationState.ReplyingToRequest =>
                await ReplyToRequest(session, message, now, expires, ct),
            WhatsAppConversationState.CollectingExistingRequestAttachment =>
                await AttachToRequest(session, message, now, expires, ct),
            WhatsAppConversationState.ViewingRequestHistory =>
                await HistoryChoice(session, text, now, expires, ct),
            _ => (PromptForState(session), "context_recovered")
        };
    }

    private async Task<(string, string)> MainMenuChoice(
        WhatsAppSession s, string name, string? text, DateTime now, DateTime exp, CancellationToken ct)
    {
        if (text == "1")
        {
            s.ClearDraft();
            var units = await ActiveUnits(s.UserId!.Value, s.CondominiumId!.Value, ct);
            if (units.Length == 1)
            {
                s.SelectUnit(units[0].Id, now, exp);
                return (await CategoryMenu(s, ct), "selecting_category");
            }
            s.MoveTo(WhatsAppConversationState.SelectingUnit, now, exp, s.CondominiumId);
            return (UnitMenu(units), "selecting_unit");
        }
        if (text == "2")
        {
            s.MoveTo(WhatsAppConversationState.SelectingOpenRequest, now, exp, s.CondominiumId);
            s.SetPage(0, now, exp);
            return (await OpenRequestsMenu(s, ct), "selecting_open_request");
        }
        if (text == "0") { s.End(now); return ("Atendimento encerrado.", "session_ended"); }
        return (MainMenu(name, await CondominiumName(s.CondominiumId!.Value, ct)), "invalid_main_menu_choice");
    }

    private (string, string) SelectCondominium(
        WhatsAppSession s, string name, CondominiumChoice[] choices, string? text, DateTime now, DateTime exp)
    {
        if (int.TryParse(text, out var index) && index >= 1 && index <= choices.Length)
        {
            var selected = choices[index - 1];
            s.MoveTo(WhatsAppConversationState.MainMenu, now, exp, selected.Id);
            return (MainMenu(name, selected.Name), "condominium_selected");
        }
        return ("Não consegui relacionar sua resposta.\n\n" + CondominiumMenu(choices), "invalid_condominium_choice");
    }

    private async Task<(string, string)> SelectUnit(
        WhatsAppSession s, string? text, DateTime now, DateTime exp, CancellationToken ct)
    {
        if (text == "0")
        {
            await DiscardDraft(s, ct);
            s.MoveTo(WhatsAppConversationState.MainMenu, now, exp, s.CondominiumId);
            return ("Operação cancelada. Digite 1 para abrir uma solicitação, 2 para consultar solicitações ou 0 para encerrar.", "cancelled");
        }
        var units = await ActiveUnits(s.UserId!.Value, s.CondominiumId!.Value, ct);
        if (text == (units.Length + 1).ToString())
        {
            s.SelectUnit(null, now, exp);
            return (await CategoryMenu(s, ct), "selecting_category");
        }
        if (int.TryParse(text, out var index) && index >= 1 && index <= units.Length)
        {
            s.SelectUnit(units[index - 1].Id, now, exp);
            return (await CategoryMenu(s, ct), "selecting_category");
        }
        return ("Escolha uma unidade válida.\n\n" + UnitMenu(units), "invalid_unit_choice");
    }

    private async Task<(string, string)> SelectCategory(
        WhatsAppSession s, string? text, DateTime now, DateTime exp, CancellationToken ct)
    {
        if (text == "0")
        {
            await DiscardDraft(s, ct);
            s.MoveTo(WhatsAppConversationState.MainMenu, now, exp, s.CondominiumId);
            return ("Operação cancelada. Digite 1 para abrir uma solicitação, 2 para consultar solicitações ou 0 para encerrar.", "cancelled");
        }
        var categories = await Categories(s.CondominiumId!.Value, ct);
        if (int.TryParse(text, out var index) && index >= 1 && index <= categories.Length)
        {
            s.SelectCategory(categories[index - 1].Id, now, exp);
            return (PromptForState(s), "collecting_description");
        }
        return ("Escolha uma categoria válida.\n\n" + CategoryMenu(categories), "invalid_category_choice");
    }

    private static (string, string) CollectDescription(
        WhatsAppSession s, NormalizedWhatsAppMessage message, DateTime now, DateTime exp)
    {
        if (message.MessageType != "text" || string.IsNullOrWhiteSpace(message.Text))
            return (PromptForState(s), "description_required");
        var description = message.Text.Trim();
        if (description.Length > 4000)
            return ("A descrição deve ter no máximo 4000 caracteres. Envie um texto menor ou digite Cancelar.", "description_too_long");
        s.SetDescription(description, now, exp);
        return ($"Entendi. Você informou:\n\n“{Short(description, 300)}”\n\nDeseja adicionar foto ou documento?\n\n1 — Adicionar arquivo\n2 — Continuar para revisão\n0 — Cancelar", "description_collected");
    }

    private async Task<(string, string)> CollectDraftAttachment(
        WhatsAppSession s, NormalizedWhatsAppMessage message, string? text,
        DateTime now, DateTime exp, CancellationToken ct)
    {
        if (text is "2" or "continuar")
        {
            s.MoveTo(WhatsAppConversationState.ReviewingNewRequest, now, exp, s.CondominiumId);
            return (await Review(s, ct), "reviewing_new_request");
        }
        if (text == "1")
            return ("Envie JPG, PNG, WebP ou PDF, com até 15 MB. Digite Continuar para revisar.", "awaiting_draft_media");
        if (message.MessageType is not ("image" or "document") || message.MediaId is null)
            return ("Este arquivo não é aceito. Envie JPG, PNG, WebP ou PDF, com até 15 MB, ou digite Continuar.", "unsupported_draft_media");
        var count = await db.WhatsAppDraftAttachments.CountAsync(x => x.SessionId == s.Id, ct);
        if (count >= 6) return ("O limite de 6 anexos foi atingido. Digite Continuar.", "draft_attachment_limit");
        var saved = await DownloadDraft(s, message, ct);
        if (saved.Error is not null) return (saved.Error, "draft_media_rejected");
        return ($"Arquivo recebido: {saved.Name}\n\nAnexos adicionados: {count + 1} de 6.\n\n1 — Enviar outro arquivo\n2 — Continuar para revisão\n0 — Cancelar", "draft_media_saved");
    }

    private async Task<(string, string)> ReviewChoice(
        WhatsAppSession s, string? text, DateTime now, DateTime exp, CancellationToken ct)
    {
        if (text == "0")
        {
            await DiscardDraft(s, ct);
            s.MoveTo(WhatsAppConversationState.MainMenu, now, exp, s.CondominiumId);
            return ("Solicitação não enviada e rascunho descartado. Digite Menu para ver as opções.", "cancelled");
        }
        if (text == "1") return await ConfirmRequest(s, now, exp, ct);
        if (text == "2")
        {
            s.MoveTo(WhatsAppConversationState.SelectingUnit, now, exp, s.CondominiumId);
            return (UnitMenu(await ActiveUnits(s.UserId!.Value, s.CondominiumId!.Value, ct)), "changing_unit");
        }
        if (text == "3")
        {
            s.MoveTo(WhatsAppConversationState.SelectingCategory, now, exp, s.CondominiumId);
            return (await CategoryMenu(s, ct), "changing_category");
        }
        if (text == "4")
        {
            s.MoveTo(WhatsAppConversationState.CollectingDescription, now, exp, s.CondominiumId);
            return (PromptForState(s), "changing_description");
        }
        if (text == "5")
        {
            s.MoveTo(WhatsAppConversationState.CollectingNewRequestAttachments, now, exp, s.CondominiumId);
            return ("Envie outro arquivo ou digite Continuar para revisar.", "changing_attachments");
        }
        return (await Review(s, ct), "invalid_review_choice");
    }

    private async Task<(string, string)> ConfirmRequest(
        WhatsAppSession s, DateTime now, DateTime exp, CancellationToken ct)
    {
        var validUser = await db.Users.AsNoTracking().AnyAsync(x => x.Id == s.UserId && x.IsActive, ct);
        var validMembership = await db.CondominiumMemberships.AsNoTracking().AnyAsync(x =>
            x.UserId == s.UserId && x.CondominiumId == s.CondominiumId && x.IsActive && x.EndedAt == null, ct);
        var category = await db.Categories.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == s.CategoryId && x.CondominiumId == s.CondominiumId && x.IsActive, ct);
        var validUnit = !s.UnitId.HasValue || await db.UnitMemberships.AsNoTracking()
            .Join(db.Units.AsNoTracking(), m => m.UnitId, u => u.Id, (m, u) => new { m, u })
            .AnyAsync(x => x.m.UserId == s.UserId && x.m.UnitId == s.UnitId && x.m.IsActive
                && x.m.EndedAt == null && x.u.CondominiumId == s.CondominiumId && x.u.IsActive, ct);
        if (!validUser || !validMembership || category is null || !validUnit
            || string.IsNullOrWhiteSpace(s.DraftDescription))
            return ("Não foi possível confirmar porque algum vínculo ou dado deixou de ser válido. Digite Menu ou Cancelar.", "confirmation_revalidation_failed");

        var description = s.DraftDescription;
        var title = description.Length <= 200 ? description : description[..200];
        var request = new DomainRequest(
            s.CondominiumId!.Value, s.UserId!.Value, s.UnitId, category.Id,
            title, description, RequestSource.WhatsApp);
        var history = new RequestStatusHistory(
            request.Id, null, RequestStatus.Open, s.UserId.Value, null, request.CreatedAt);
        var drafts = await db.WhatsAppDraftAttachments.Where(x => x.SessionId == s.Id).ToArrayAsync(ct);
        var promoted = new List<string>();
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            db.Requests.Add(request);
            db.RequestStatusHistories.Add(history);
            foreach (var draft in drafts)
            {
                var extension = Path.GetExtension(draft.OriginalFileName).ToLowerInvariant();
                var key = storage.PromoteWhatsAppDraft(request.Id, draft.StorageKey, extension);
                promoted.Add(key);
                db.RequestAttachments.Add(new RequestAttachment(
                    request.Id, s.UserId.Value, draft.OriginalFileName, key,
                    draft.ContentType, draft.FileSize));
            }
            db.WhatsAppDraftAttachments.RemoveRange(drafts);
            s.ClearDraft();
            s.SelectRequest(request.Id, now, exp);
            await db.SaveChangesAsync(ct);
            await notifications.NotifyRequestCreatedAsync(request, category.Name, ct);
            await transaction.CommitAsync(ct);
            foreach (var draft in drafts) storage.Delete(draft.StorageKey);
        }
        catch
        {
            await transaction.RollbackAsync(ct);
            foreach (var key in promoted) storage.Delete(key);
            throw;
        }
        return ($"Solicitação #{ShortId(request.Id)} aberta com sucesso.\n\nStatus: Aberta\n\n1 — Enviar mensagem\n2 — Enviar anexo\n3 — Ver histórico\n0 — Menu", "request_created");
    }

    private async Task<(string, string)> SelectOpenRequest(
        WhatsAppSession s, string? text, DateTime now, DateTime exp, CancellationToken ct)
    {
        if (text == "0")
        {
            s.MoveTo(WhatsAppConversationState.MainMenu, now, exp, s.CondominiumId);
            return ("Você voltou ao menu.\n\n1 — Abrir uma nova solicitação\n2 — Ver minhas solicitações abertas\n0 — Encerrar", "main_menu");
        }
        if (text?.Equals("próxima", StringComparison.OrdinalIgnoreCase) == true)
        {
            s.SetPage(s.Page + 1, now, exp);
            return (await OpenRequestsMenu(s, ct), "requests_next_page");
        }
        if (text?.Equals("anterior", StringComparison.OrdinalIgnoreCase) == true)
        {
            s.SetPage(s.Page - 1, now, exp);
            return (await OpenRequestsMenu(s, ct), "requests_previous_page");
        }
        var requests = await OpenRequests(s, ct);
        if (requests.Length == 0 && s.Page == 0 && text == "1")
            return await MainMenuChoice(s, "", "1", now, exp, ct);
        if (int.TryParse(text, out var index) && index >= 1 && index <= requests.Length)
        {
            s.SelectRequest(requests[index - 1].Id, now, exp);
            return (await RequestDetail(s, ct), "viewing_request");
        }
        return (await OpenRequestsMenu(s, ct), "invalid_request_choice");
    }

    private async Task<(string, string)> ViewRequestChoice(
        WhatsAppSession s, string? text, DateTime now, DateTime exp, CancellationToken ct)
    {
        var request = await OwnedRequest(s, includeClosed: true, ct);
        if (request is null) return ("A solicitação não está mais disponível. Digite Menu.", "request_unavailable");
        if (text == "0")
        {
            s.MoveTo(WhatsAppConversationState.SelectingOpenRequest, now, exp, s.CondominiumId);
            s.SetPage(0, now, exp);
            return (await OpenRequestsMenu(s, ct), "returned_to_requests");
        }
        if (text == "1" && !IsClosed(request.Status))
        {
            s.MoveTo(WhatsAppConversationState.ReplyingToRequest, now, exp, s.CondominiumId);
            return ($"Você está respondendo à solicitação #{ShortId(request.Id)} — {Short(request.Title, 100)}.\n\nEnvie sua mensagem ou digite Cancelar.", "replying_to_request");
        }
        if (text == "2" && !IsClosed(request.Status))
        {
            s.MoveTo(WhatsAppConversationState.CollectingExistingRequestAttachment, now, exp, s.CondominiumId);
            return ($"Envie uma imagem ou PDF para a solicitação #{ShortId(request.Id)}.\n\nFormatos: JPG, PNG, WebP ou PDF. Limite: 15 MB.", "collecting_request_attachment");
        }
        if (text == "3")
        {
            s.MoveTo(WhatsAppConversationState.ViewingRequestHistory, now, exp, s.CondominiumId);
            s.SetPage(0, now, exp);
            return (await History(s, ct), "viewing_history");
        }
        return (await RequestDetail(s, ct), "invalid_request_action");
    }

    private async Task<(string, string)> ReplyToRequest(
        WhatsAppSession s, NormalizedWhatsAppMessage message, DateTime now, DateTime exp, CancellationToken ct)
    {
        var request = await OwnedRequest(s, false, ct);
        if (request is null || IsClosed(request.Status))
            return ("A solicitação está encerrada ou não está disponível para resposta.", "reply_blocked");
        if (message.MessageType != "text" || string.IsNullOrWhiteSpace(message.Text))
            return ("Envie uma mensagem de texto com até 4000 caracteres ou digite Cancelar.", "reply_text_required");
        var content = message.Text.Trim();
        if (content.Length > 4000) return ("A mensagem deve ter no máximo 4000 caracteres.", "reply_too_long");
        var requestMessage = new RequestMessage(request.Id, s.UserId!.Value, content, MessageChannel.WhatsApp);
        db.RequestMessages.Add(requestMessage);
        await db.SaveChangesAsync(ct);
        try { await notifications.NotifyMessageAsync(
            request.Id, request.CondominiumId, request.AuthorUserId,
            request.Title, s.UserId.Value, content, ct,
            requestMessage.Id, MessageChannel.WhatsApp); }
        catch (Exception ex) { logger.LogError(ex, "Failed to notify WhatsApp message {MessageId}.", requestMessage.Id); }
        s.MoveTo(WhatsAppConversationState.ViewingRequest, now, exp, s.CondominiumId);
        return ($"Mensagem adicionada à solicitação #{ShortId(request.Id)}.\n\n1 — Enviar mensagem\n2 — Enviar anexo\n3 — Ver histórico\n0 — Menu", "request_message_created");
    }

    private async Task<(string, string)> AttachToRequest(
        WhatsAppSession s, NormalizedWhatsAppMessage message, DateTime now, DateTime exp, CancellationToken ct)
    {
        var request = await OwnedRequest(s, false, ct);
        if (request is null || IsClosed(request.Status))
            return ("A solicitação está encerrada ou não está disponível para anexos.", "attachment_blocked");
        if (message.MessageType is not ("image" or "document") || message.MediaId is null)
            return ("Envie JPG, PNG, WebP ou PDF, com até 15 MB.", "request_media_required");
        var media = await DownloadAndValidate(message, ct);
        if (media.Error is not null) return (media.Error, "request_media_rejected");
        var file = new FormFile(
            new MemoryStream(media.Content!), 0, media.Content!.Length, "file", media.Name!);
        file.Headers = new HeaderDictionary();
        file.ContentType = media.ContentType!;
        var key = await storage.SaveAsync(request.Id, file, media.Extension!, ct);
        db.RequestAttachments.Add(new RequestAttachment(
            request.Id, s.UserId!.Value, media.Name!, key, media.ContentType!, media.Content!.Length));
        try { await db.SaveChangesAsync(ct); }
        catch { storage.Delete(key); throw; }
        s.MoveTo(WhatsAppConversationState.ViewingRequest, now, exp, s.CondominiumId);
        return ($"Arquivo adicionado à solicitação #{ShortId(request.Id)}.\n\n1 — Enviar mensagem\n2 — Enviar anexo\n3 — Ver histórico\n0 — Menu", "request_attachment_created");
    }

    private async Task<(string, string)> HistoryChoice(
        WhatsAppSession s, string? text, DateTime now, DateTime exp, CancellationToken ct)
    {
        if (text == "0")
        {
            s.MoveTo(WhatsAppConversationState.ViewingRequest, now, exp, s.CondominiumId);
            return (await RequestDetail(s, ct), "returned_to_request");
        }
        if (text == "1") s.SetPage(s.Page + 1, now, exp);
        return (await History(s, ct), "history_page");
    }

    private async Task<(string, string)> GoBack(
        WhatsAppSession s, string name, CondominiumChoice[] condos, DateTime now, DateTime exp, CancellationToken ct)
    {
        s.ReturnToPrevious(now, exp);
        if (s.State == WhatsAppConversationState.MainMenu)
            return (MainMenu(name, await CondominiumName(s.CondominiumId!.Value, ct)), "returned");
        if (s.State == WhatsAppConversationState.SelectingCondominium)
            return (CondominiumMenu(condos), "returned");
        return (PromptForState(s), "returned");
    }

    private (string, string) SelectCondominiumOrMenu(
        WhatsAppSession s, string name, CondominiumChoice[] condos, DateTime now, DateTime exp)
    {
        if (condos.Length > 1)
        {
            s.MoveTo(WhatsAppConversationState.SelectingCondominium, now, exp);
            return (CondominiumMenu(condos), "selecting_condominium");
        }
        s.MoveTo(WhatsAppConversationState.MainMenu, now, exp, condos[0].Id);
        return (MainMenu(name, condos[0].Name), "main_menu");
    }

    private async Task<string> Review(WhatsAppSession s, CancellationToken ct)
    {
        var condo = await CondominiumName(s.CondominiumId!.Value, ct);
        var category = await db.Categories.Where(x => x.Id == s.CategoryId).Select(x => x.Name).SingleAsync(ct);
        var unit = s.UnitId.HasValue
            ? await UnitLabel(s.UnitId.Value, ct) : "Área comum / sem unidade";
        var attachments = await db.WhatsAppDraftAttachments.CountAsync(x => x.SessionId == s.Id, ct);
        return $"Confira sua solicitação:\n\nCondomínio: {condo}\nUnidade: {unit}\nCategoria: {category}\nDescrição: {Short(s.DraftDescription!, 300)}\nAnexos: {attachments}\n\n1 — Confirmar e enviar\n2 — Alterar unidade\n3 — Alterar categoria\n4 — Alterar descrição\n5 — Alterar anexos\n0 — Cancelar";
    }

    private async Task<string> RequestDetail(WhatsAppSession s, CancellationToken ct)
    {
        var request = await OwnedRequest(s, true, ct);
        if (request is null) return "A solicitação não está disponível. Digite Menu.";
        var category = await db.Categories.Where(x => x.Id == request.CategoryId).Select(x => x.Name).SingleAsync(ct);
        var unit = request.TargetUnitId.HasValue ? await UnitLabel(request.TargetUnitId.Value, ct) : "Sem unidade";
        var lastMessage = await db.RequestMessages.AsNoTracking().Where(x => x.RequestId == request.Id)
            .OrderByDescending(x => x.CreatedAt).Select(x => x.Content).FirstOrDefaultAsync(ct);
        var closed = IsClosed(request.Status);
        return $"Solicitação #{ShortId(request.Id)}\n\nCategoria: {category}\nUnidade: {unit}\nStatus: {Status(request.Status)}\nAberta em: {request.CreatedAt:dd/MM/yyyy}\nÚltima atualização: {request.UpdatedAt:dd/MM/yyyy 'às' HH:mm}\n\nÚltima mensagem:\n“{Short(lastMessage ?? request.Description, 300)}”\n\n"
            + (closed ? "Esta solicitação está encerrada e disponível somente para consulta.\n\n3 — Ver histórico\n0 — Voltar"
                : "1 — Enviar mensagem\n2 — Enviar anexo\n3 — Ver histórico\n0 — Voltar");
    }

    private async Task<string> History(WhatsAppSession s, CancellationToken ct)
    {
        var request = await OwnedRequest(s, true, ct);
        if (request is null) return "Solicitação indisponível.";
        var messages = await db.RequestMessages.AsNoTracking().Where(x => x.RequestId == request.Id)
            .OrderByDescending(x => x.CreatedAt).Skip(s.Page * PageSize).Take(PageSize)
            .Select(x => new { x.AuthorUserId, x.Content, x.CreatedAt }).ToArrayAsync(ct);
        var lines = messages.Select(x =>
            $"{x.CreatedAt:dd/MM HH:mm} — {(x.AuthorUserId == s.UserId ? "Você" : "Administração")}:\n{Short(x.Content, 300)}");
        var body = messages.Length == 0 ? "Não há mensagens nesta página." : string.Join("\n\n", lines);
        return $"Histórico da solicitação #{ShortId(request.Id)}:\n\n{body}\n\n"
            + (messages.Length == PageSize ? "1 — Ver mensagens anteriores\n" : "") + "0 — Voltar";
    }

    private async Task<string> OpenRequestsMenu(WhatsAppSession s, CancellationToken ct)
    {
        var requests = await OpenRequests(s, ct);
        if (requests.Length == 0 && s.Page == 0)
            return "Você não possui solicitações abertas neste condomínio.\n\n1 — Abrir nova solicitação\n0 — Menu";
        var lines = requests.Select((x, i) =>
            $"{i + 1} — #{ShortId(x.Id)} · {Short(x.Title, 80)}\nStatus: {Status(x.Status)}");
        return "Estas são suas solicitações em andamento:\n\n"
            + string.Join("\n\n", lines)
            + (requests.Length == PageSize ? "\n\nPróxima — Próxima página" : "")
            + (s.Page > 0 ? "\nAnterior — Página anterior" : "")
            + "\n0 — Voltar ao menu";
    }

    private Task<RequestChoice[]> OpenRequests(WhatsAppSession s, CancellationToken ct) =>
        db.Requests.AsNoTracking().Where(x => x.AuthorUserId == s.UserId
                && x.CondominiumId == s.CondominiumId
                && x.Status != RequestStatus.Resolved && x.Status != RequestStatus.Cancelled)
            .OrderByDescending(x => x.UpdatedAt).Skip(s.Page * PageSize).Take(PageSize)
            .Select(x => new RequestChoice(x.Id, x.Title, x.Status)).ToArrayAsync(ct);

    private Task<DomainRequest?> OwnedRequest(WhatsAppSession s, bool includeClosed, CancellationToken ct) =>
        db.Requests.SingleOrDefaultAsync(x => x.Id == s.RequestId
            && x.AuthorUserId == s.UserId && x.CondominiumId == s.CondominiumId
            && (includeClosed || x.Status != RequestStatus.Resolved && x.Status != RequestStatus.Cancelled), ct);

    private async Task<(string? Error, string? Name)> DownloadDraft(
        WhatsAppSession s, NormalizedWhatsAppMessage message, CancellationToken ct)
    {
        if (await db.WhatsAppDraftAttachments.AnyAsync(x => x.ExternalMediaId == message.MediaId, ct))
            return ("Este arquivo já foi recebido.", null);
        var media = await DownloadAndValidate(message, ct);
        if (media.Error is not null) return (media.Error, null);
        var key = await storage.SaveWhatsAppDraftAsync(s.Id, media.Content!, media.Extension!, ct);
        db.WhatsAppDraftAttachments.Add(new WhatsAppDraftAttachment(
            s.Id, message.MediaId!, media.Name!, key, media.ContentType!, media.Content!.Length));
        try { await db.SaveChangesAsync(ct); }
        catch { storage.Delete(key); throw; }
        return (null, media.Name);
    }

    private async Task<MediaValidation> DownloadAndValidate(
        NormalizedWhatsAppMessage message, CancellationToken ct)
    {
        var result = await client.DownloadMediaAsync(message.MediaId!, ct);
        if (!result.Succeeded || result.Content is null)
            return new("Não foi possível baixar o arquivo. Envie novamente ou digite Cancelar.");
        var contentType = (result.ContentType ?? message.MediaContentType)?.Split(';')[0].Trim().ToLowerInvariant();
        var defaultExtension = contentType switch
        {
            "image/jpeg" => ".jpg", "image/png" => ".png",
            "image/webp" => ".webp", "application/pdf" => ".pdf", _ => null
        };
        var name = Path.GetFileName(message.FileName ?? $"arquivo{defaultExtension}");
        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 255
            || result.Content.Length == 0 || result.Content.Length > 15 * 1024 * 1024
            || contentType is null || !AllowedMedia.TryGetValue(extension, out var types)
            || !types.Contains(contentType, StringComparer.OrdinalIgnoreCase))
            return new("Este arquivo não é aceito. Envie JPG, PNG, WebP ou PDF, com até 15 MB.");
        return new(null, result.Content, contentType, extension, name);
    }

    private async Task DiscardDraft(WhatsAppSession s, CancellationToken ct)
    {
        var drafts = await db.WhatsAppDraftAttachments.Where(x => x.SessionId == s.Id).ToArrayAsync(ct);
        db.WhatsAppDraftAttachments.RemoveRange(drafts);
        foreach (var draft in drafts) storage.Delete(draft.StorageKey);
        s.ClearDraft();
    }

    private Task<CondominiumChoice[]> ActiveCondominiums(Guid userId, CancellationToken ct) =>
        (from m in db.CondominiumMemberships.AsNoTracking()
         join c in db.Condominiums.AsNoTracking() on m.CondominiumId equals c.Id
         where m.UserId == userId && m.IsActive && m.EndedAt == null && c.IsActive
         orderby c.Name select new CondominiumChoice(c.Id, c.Name))
        .Distinct().Take(10).ToArrayAsync(ct);

    private Task<UnitChoice[]> ActiveUnits(Guid userId, Guid condominiumId, CancellationToken ct) =>
        (from m in db.UnitMemberships.AsNoTracking()
         join u in db.Units.AsNoTracking() on m.UnitId equals u.Id
         join b in db.CondominiumBlocks.AsNoTracking() on u.BlockId equals b.Id into blocks
         from b in blocks.DefaultIfEmpty()
         where m.UserId == userId && m.IsActive && m.EndedAt == null
             && u.CondominiumId == condominiumId && u.IsActive
         orderby b == null ? "" : b.Identifier, u.Identifier
         select new UnitChoice(u.Id, b == null ? null : b.Identifier, u.Identifier))
        .Distinct().Take(10).ToArrayAsync(ct);

    private Task<CategoryChoice[]> Categories(Guid condominiumId, CancellationToken ct) =>
        db.Categories.AsNoTracking().Where(x => x.CondominiumId == condominiumId && x.IsActive)
            .OrderBy(x => x.Name).Take(10).Select(x => new CategoryChoice(x.Id, x.Name)).ToArrayAsync(ct);

    private async Task<string> CategoryMenu(WhatsAppSession s, CancellationToken ct) =>
        CategoryMenu(await Categories(s.CondominiumId!.Value, ct));
    private static string CategoryMenu(CategoryChoice[] categories) =>
        "Sobre qual assunto deseja falar?\n\n"
        + string.Join("\n", categories.Select((x, i) => $"{i + 1} — {x.Name}"))
        + "\n0 — Cancelar";
    private static string UnitMenu(UnitChoice[] units) =>
        "Para qual unidade deseja abrir a solicitação?\n\n"
        + string.Join("\n", units.Select((x, i) => $"{i + 1} — {x.Label}"))
        + $"\n{units.Length + 1} — Área comum / sem unidade\n0 — Cancelar";
    private static string CondominiumMenu(CondominiumChoice[] choices) =>
        "Escolha o condomínio:\n\n"
        + string.Join("\n", choices.Select((x, i) => $"{i + 1} — {x.Name}"))
        + "\n0 — Cancelar";
    private static string MainMenu(string name, string condo) =>
        $"Olá, {name.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0]}! Condomínio em contexto: {condo}.\n\nComo podemos ajudar?\n\n1 — Abrir uma nova solicitação\n2 — Ver minhas solicitações abertas\n0 — Encerrar";
    private static string Help(WhatsAppSession s) =>
        $"Etapa atual: {s.State}.\n\n{PromptForState(s)}\n\nDigite Menu para o início, Voltar para a etapa anterior ou Cancelar para sair.";
    private static string PromptForState(WhatsAppSession s) => s.State switch
    {
        WhatsAppConversationState.CollectingDescription =>
            "Conte com suas palavras o que aconteceu. Envie texto com até 4000 caracteres.",
        WhatsAppConversationState.CollectingNewRequestAttachments =>
            "Envie JPG, PNG, WebP ou PDF, com até 15 MB, ou digite Continuar.",
        WhatsAppConversationState.ReplyingToRequest =>
            "Envie sua mensagem com até 4000 caracteres.",
        WhatsAppConversationState.CollectingExistingRequestAttachment =>
            "Envie JPG, PNG, WebP ou PDF, com até 15 MB.",
        _ => "Responda com uma das opções exibidas."
    };
    private Task<string> CondominiumName(Guid id, CancellationToken ct) =>
        db.Condominiums.Where(x => x.Id == id).Select(x => x.Name).SingleAsync(ct);
    private async Task<string> UnitLabel(Guid id, CancellationToken ct)
    {
        var unit = await (from u in db.Units.AsNoTracking()
            join b in db.CondominiumBlocks.AsNoTracking() on u.BlockId equals b.Id into blocks
            from b in blocks.DefaultIfEmpty() where u.Id == id
            select new { u.Identifier, Block = b == null ? null : b.Identifier }).SingleAsync(ct);
        return unit.Block is null ? $"Unidade {unit.Identifier}" : $"Bloco {unit.Block} — {unit.Identifier}";
    }
    private static bool IsClosed(RequestStatus status) =>
        status is RequestStatus.Resolved or RequestStatus.Cancelled;
    private static string Status(RequestStatus status) => status switch
    {
        RequestStatus.Open => "Aberta", RequestStatus.InProgress => "Em andamento",
        RequestStatus.WaitingForResident => "Aguardando morador",
        RequestStatus.WaitingForThirdParty => "Aguardando terceiro",
        RequestStatus.Resolved => "Resolvida", RequestStatus.Cancelled => "Cancelada",
        _ => status.ToString()
    };
    private static string Short(string value, int max) =>
        value.Length <= max ? value : value[..(max - 1)] + "…";
    private static string ShortId(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    private sealed record CondominiumChoice(Guid Id, string Name);
    private sealed record UnitChoice(Guid Id, string? Block, string Identifier)
    {
        public string Label => Block is null ? $"Unidade {Identifier}" : $"Bloco {Block}, unidade {Identifier}";
    }
    private sealed record CategoryChoice(Guid Id, string Name);
    private sealed record RequestChoice(Guid Id, string Title, RequestStatus Status);
    private sealed record MediaValidation(
        string? Error, byte[]? Content = null, string? ContentType = null,
        string? Extension = null, string? Name = null);
}
