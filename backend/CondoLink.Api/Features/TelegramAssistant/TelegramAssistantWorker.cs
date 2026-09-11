using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Api.Features.RequestAttachments;
using CondoLink.Api.Features.WhatsApp;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PhoneNumberNormalizer = CondoLink.Domain.PhoneNumberNormalizer;

namespace CondoLink.Api.Features.TelegramAssistant;

public sealed class TelegramAssistantWorker(IServiceScopeFactory scopes,
    IOptions<TelegramAssistantOptions> options, TimeProvider time,
    ILogger<TelegramAssistantWorker> logger) : BackgroundService
{
    private const string AudioFailure = "Não consegui entender esse áudio. Tente novamente ou envie sua pergunta em texto.";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            if (options.Value.IsConfigured)
            {
                try { await ProcessOneAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception exception) { logger.LogError(exception, "Telegram assistant worker batch failed."); }
            }
            await Task.Delay(TimeSpan.FromSeconds(Math.Clamp(options.Value.PollingSeconds, 1, 30)), stoppingToken);
        }
    }

    internal async Task<bool> ProcessOneAsync(CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = time.GetUtcNow().UtcDateTime;
        await db.TelegramInboundUpdates.Where(x => x.ProcessedAt < now.AddDays(-30)
                && (x.Status == TelegramInboundStatus.Completed || x.Status == TelegramInboundStatus.Ignored))
            .ExecuteDeleteAsync(ct);
        await db.TelegramInboundUpdates.Where(x => x.Status == TelegramInboundStatus.Processing
                && x.ProcessingStartedAt < now.AddMinutes(-5))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, TelegramInboundStatus.Pending)
                .SetProperty(x => x.ProcessingToken, (Guid?)null)
                .SetProperty(x => x.NextAttemptAt, now), ct);
        var id = await db.TelegramInboundUpdates.AsNoTracking()
            .Where(x => x.Status == TelegramInboundStatus.Pending && x.NextAttemptAt <= now)
            .OrderBy(x => x.ReceivedAt).Select(x => (Guid?)x.Id).FirstOrDefaultAsync(ct);
        if (id is null) return false;
        var token = Guid.NewGuid();
        var claimed = await db.TelegramInboundUpdates.Where(x => x.Id == id
                && x.Status == TelegramInboundStatus.Pending)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, TelegramInboundStatus.Processing)
                .SetProperty(x => x.ProcessingToken, token)
                .SetProperty(x => x.ProcessingStartedAt, now)
                .SetProperty(x => x.Attempts, x => x.Attempts + 1), ct);
        if (claimed == 0) return false;
        var update = await db.TelegramInboundUpdates.SingleAsync(x => x.ProcessingToken == token, ct);
        update.RecordQueue((long)(now - update.ReceivedAt).TotalMilliseconds);
        var stage = "claimed";
        logger.LogInformation("Telegram update claimed. UpdateId: {UpdateId}; ChatId: {ChatId}; Attempt: {Attempt}.",
            update.UpdateId, update.ChatId, update.Attempts);
        try
        {
            if (update.ResponseText is null)
            {
                stage = "processing";
                var response = await HandleAsync(update, db, scope.ServiceProvider, ct);
                if (response is null) { update.Ignore(now); await db.SaveChangesAsync(ct); return true; }
                if (update.ResponseText is null) update.PrepareResponse(response);
                await db.SaveChangesAsync(ct);
            }
            stage = "telegram_delivery";
            var client = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            var parts = SplitMessage(update.ResponseText!);
            var delivery = Stopwatch.StartNew();
            for (var index = update.SentPartCount; index < parts.Count; index++)
            {
                await client.SendMessageAsync(update.ChatId, parts[index],
                    index == 0 ? update.ReplyMarkup : TelegramReplyMarkup.None, ct);
                update.PartSent(); await db.SaveChangesAsync(ct);
            }
            delivery.Stop(); update.RecordDelivery(delivery.ElapsedMilliseconds);
            var completedAt = time.GetUtcNow().UtcDateTime;
            update.RecordTotal((long)(completedAt - update.ReceivedAt).TotalMilliseconds);
            update.Complete(completedAt); await db.SaveChangesAsync(ct);
            LogLatency(update, "delivered");
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            var error = SafeError(exception);
            update.Retry(time.GetUtcNow().UtcDateTime,
                TimeSpan.FromSeconds(5 * Math.Pow(2, Math.Min(update.Attempts - 1, 5))),
                error, Math.Clamp(options.Value.MaximumAttempts, 1, 8));
            update.RecordTotal((long)(time.GetUtcNow().UtcDateTime - update.ReceivedAt).TotalMilliseconds);
            await db.SaveChangesAsync(CancellationToken.None);
            LogFailure(update, stage, error, exception);
            if (update.Status == TelegramInboundStatus.Failed && update.ResponseText is null)
                await TrySendTerminalFailureAsync(update, db, scope.ServiceProvider, ct);
        }
        return true;
    }

    private async Task<string?> HandleAsync(TelegramInboundUpdate update, AppDbContext db,
        IServiceProvider services, CancellationToken ct)
    {
        var authorization = Stopwatch.StartNew();
        var command = ParseCommand(update.Text);
        logger.LogInformation("Telegram command inspected. UpdateId: {UpdateId}; Command: {Command}; HasStartParameter: {HasStartParameter}.",
            update.UpdateId, command.Name ?? "message",
            command.Name == "/start" && !string.IsNullOrWhiteSpace(command.Argument));
        var link = await db.TelegramUserLinks.SingleOrDefaultAsync(x => x.TelegramUserId == update.TelegramUserId
            && x.TelegramChatId == update.ChatId && x.IsActive, ct);
        if (command.Name == "/start" && !string.IsNullOrWhiteSpace(command.Argument))
            return await BeginLinkAsync(update, command.Argument, db, ct);
        if (update.Kind == TelegramInboundKind.Contact)
            return await ConfirmPhoneAsync(update, db, ct);
        if (link is null) return "Para usar o Assistente do Comvy, primeiro faça a vinculação pela sua conta no Comvy.";
        if (command.Name is "/sair" or "/desvincular")
        { link.Deactivate(time.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(ct); return "Telegram desvinculado do Comvy."; }

        var isQuestion = command.Name is null
            && update.Kind is TelegramInboundKind.Text or TelegramInboundKind.Voice or TelegramInboundKind.Audio;
        if (!isQuestion) return await HandleAuthorizedAsync(update, link, command, db, services, authorization, ct);
        var client = services.GetRequiredService<ITelegramBotClient>();
        using var typing = new CancellationTokenSource();
        var typingTask = TypingAsync(client, update.ChatId, typing.Token);
        try { return await HandleAuthorizedAsync(update, link, command, db, services, authorization, ct); }
        finally { typing.Cancel(); try { await typingTask; } catch (OperationCanceledException) { } }
    }

    private async Task<string> HandleAuthorizedAsync(TelegramInboundUpdate update, TelegramUserLink link,
        (string? Name, string? Argument) command, AppDbContext db, IServiceProvider services,
        Stopwatch authorization, CancellationToken ct)
    {
        var condominiums = await TelegramAssistantAccess.CondominiumsAsync(db, link.UserId, ct);
        if (condominiums.Length == 0)
        { RecordAuthorization(update, authorization); return "Seu acesso ao Assistente não está disponível. Verifique suas permissões no Comvy."; }
        if (command.Name == "/ajuda")
        { RecordAuthorization(update, authorization); return "Envie perguntas sobre os documentos do condomínio. Use /condominio para consultar ou trocar o contexto e /sair para desvincular. O bot não executa alterações."; }
        if (command.Name == "/start") { RecordAuthorization(update, authorization); return Status(link, condominiums); }
        if (command.Name == "/condominio")
        {
            if (int.TryParse(command.Argument, out var selected) && selected >= 1 && selected <= condominiums.Length)
            { var item = condominiums[selected - 1]; link.SelectCondominium(item.Id, time.GetUtcNow().UtcDateTime);
              await db.SaveChangesAsync(ct); RecordAuthorization(update, authorization); return $"Contexto atual: {item.Name}."; }
            RecordAuthorization(update, authorization); return CondominiumChoices(condominiums);
        }
        var active = condominiums.SingleOrDefault(x => x.Id == link.ActiveCondominiumId);
        if (active == default && condominiums.Length == 1)
        { active = condominiums[0]; link.SelectCondominium(active.Id, time.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(ct); }
        if (active == default)
        { link.ClearCondominium(time.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(ct);
          RecordAuthorization(update, authorization); return CondominiumChoices(condominiums); }
        var recent = await db.TelegramInboundUpdates.CountAsync(x => x.ChatId == update.ChatId
            && x.ReceivedAt > time.GetUtcNow().UtcDateTime.AddMinutes(-1), ct);
        if (recent > 10) { RecordAuthorization(update, authorization); return "Você enviou várias mensagens em sequência. Aguarde um instante."; }

        var conversation = await db.CondominiumAssistantConversations
            .Where(x => x.CreatedByUserId == link.UserId && x.CondominiumId == active.Id
                && x.Channel == CondominiumAssistantChannel.Telegram)
            .OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(ct);
        if (conversation is null)
        { conversation = new(active.Id, link.UserId, null, "Telegram", CondominiumAssistantChannel.Telegram);
          db.CondominiumAssistantConversations.Add(conversation); }
        RecordAuthorization(update, authorization);

        var executionId = update.AssistantExecutionId ?? Guid.NewGuid();
        update.AttachAssistantExecution(executionId);
        if (update.Kind is TelegramInboundKind.Voice or TelegramInboundKind.Audio
            && string.IsNullOrWhiteSpace(update.Text))
        {
            var audioFailure = await TranscribeAudioAsync(update, services, executionId, ct);
            if (audioFailure is not null) return audioFailure;
        }
        if (update.ConversationId is null)
        { var userMessage = new CondominiumAssistantMessage(conversation.Id, CondominiumAssistantRole.User, update.Text);
          db.CondominiumAssistantMessages.Add(userMessage); conversation.Touch();
          update.AttachConversation(conversation.Id, userMessage.Id); await db.SaveChangesAsync(ct); }
        else conversation = await db.CondominiumAssistantConversations.SingleAsync(x => x.Id == update.ConversationId, ct);

        if (IsSourceQuestion(update.Text))
        {
            var previous = await db.CondominiumAssistantMessages.AsNoTracking()
                .Where(x => x.ConversationId == conversation.Id && x.Role == CondominiumAssistantRole.Assistant)
                .OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(ct);
            var sources = ParseSources(previous?.SourcesJson);
            var response = FormatSources(sources);
            db.CondominiumAssistantMessages.Add(new(conversation.Id, CondominiumAssistantRole.Assistant,
                response, sources.Count == 0 ? null : previous!.SourcesJson));
            conversation.Touch(); update.PrepareResponse(response); await db.SaveChangesAsync(ct);
            return response;
        }

        var assistant = services.GetRequiredService<CondominiumAssistantService>();
        logger.LogInformation("Telegram assistant started. UpdateId: {UpdateId}; CondominiumId: {CondominiumId}; AssistantExecutionId: {AssistantExecutionId}.",
            update.UpdateId, active.Id, executionId);
        var assistantTimer = Stopwatch.StartNew();
        var answer = await assistant.AskAsync(conversation, update.Text, ct,
            executionId, CondominiumAssistantChannel.Telegram);
        assistantTimer.Stop(); update.RecordAssistant(assistantTimer.ElapsedMilliseconds);
        var responseText = FormatAnswer(answer);
        db.CondominiumAssistantMessages.Add(new(conversation.Id, CondominiumAssistantRole.Assistant,
            answer.Answer, JsonSerializer.Serialize(answer.Sources, CondominiumAssistantEndpoints.AssistantJsonOptions)));
        conversation.Touch(); update.PrepareResponse(responseText); await db.SaveChangesAsync(ct);
        logger.LogInformation("Telegram assistant completed. UpdateId: {UpdateId}; AssistantExecutionId: {AssistantExecutionId}; SourceCount: {SourceCount}.",
            update.UpdateId, executionId, answer.Sources.Count);
        return responseText;
    }

    private async Task<string> BeginLinkAsync(TelegramInboundUpdate update, string code,
        AppDbContext db, CancellationToken ct)
    {
        if (code.Length != 8 || code.Any(x => !char.IsAsciiLetterOrDigit(x)))
            return "Código de vinculação inválido ou expirado.";
        var now = time.GetUtcNow().UtcDateTime;
        var hash = TelegramAssistantEndpoints.HashCode(code);
        var linkCode = await db.TelegramLinkCodes.SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
        if (linkCode is null || !linkCode.IsUsable(now)) return "Código de vinculação inválido ou expirado.";
        if ((await TelegramAssistantAccess.CondominiumsAsync(db, linkCode.UserId, ct)).Length == 0)
            return "Sua conta não possui acesso autorizado ao Assistente.";
        var normalizedPhone = await db.Users.AsNoTracking().Where(x => x.Id == linkCode.UserId)
            .Select(x => x.NormalizedPhoneNumber).SingleOrDefaultAsync(ct);
        if (normalizedPhone is null || PhoneNumberNormalizer.Normalize(normalizedPhone) != normalizedPhone)
            return "Antes de vincular o Telegram, cadastre/atualize seu telefone no Comvy.";
        if (await db.TelegramUserLinks.AnyAsync(x => x.TelegramUserId == update.TelegramUserId
            && x.UserId != linkCode.UserId, ct)) return "Este Telegram já está vinculado a outra conta Comvy.";
        if (!linkCode.BeginPhoneVerification(update.TelegramUserId, update.ChatId, now))
            return "Código de vinculação inválido ou expirado.";
        await db.SaveChangesAsync(ct);
        const string response = "Para confirmar sua identidade, preciso validar o telefone cadastrado no Comvy.\n\nToque em Confirmar meu telefone abaixo.";
        update.PrepareResponse(response, TelegramReplyMarkup.RequestContact);
        return response;
    }

    private async Task<string> ConfirmPhoneAsync(TelegramInboundUpdate update, AppDbContext db,
        CancellationToken ct)
    {
        var now = time.GetUtcNow().UtcDateTime;
        var linkCode = await db.TelegramLinkCodes
            .Where(x => x.PendingTelegramUserId == update.TelegramUserId
                && x.PendingTelegramChatId == update.ChatId && x.UsedAt == null
                && x.InvalidatedAt == null && x.ExpiresAt > now)
            .OrderByDescending(x => x.VerificationStartedAt).FirstOrDefaultAsync(ct);
        if (linkCode is null) return "Inicie a vinculação novamente pela sua conta no Comvy.";
        if (update.ContactUserId != update.TelegramUserId)
        {
            const string response = "Esse contato não pertence à sua conta do Telegram. Toque em Confirmar meu telefone para compartilhar seu próprio contato.";
            update.PrepareResponse(response, TelegramReplyMarkup.RequestContact); return response;
        }
        var user = await db.Users.SingleOrDefaultAsync(x => x.Id == linkCode.UserId && x.IsActive, ct);
        if (user?.NormalizedPhoneNumber is null
            || PhoneNumberNormalizer.Normalize(user.NormalizedPhoneNumber) != user.NormalizedPhoneNumber)
            return "Antes de vincular o Telegram, cadastre/atualize seu telefone no Comvy.";
        if (!string.Equals(PhoneNumberNormalizer.Normalize(update.ContactPhoneNumber),
            user.NormalizedPhoneNumber, StringComparison.Ordinal))
        {
            const string response = "Este telefone não corresponde ao telefone cadastrado na sua conta do Comvy.\n\nConfira seus dados no portal e tente novamente.";
            update.PrepareResponse(response, TelegramReplyMarkup.RemoveKeyboard); return response;
        }
        var allowed = await TelegramAssistantAccess.CondominiumsAsync(db, linkCode.UserId, ct);
        if (allowed.Length == 0) return "Sua conta não possui acesso autorizado ao Assistente.";
        if (await db.TelegramUserLinks.AnyAsync(x => x.TelegramUserId == update.TelegramUserId
            && x.UserId != linkCode.UserId, ct)) return "Este Telegram já está vinculado a outra conta Comvy.";

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(ct) : null;
        var consumed = await db.TelegramLinkCodes.Where(x => x.Id == linkCode.Id
                && x.UsedAt == null && x.InvalidatedAt == null && x.ExpiresAt > now
                && x.PendingTelegramUserId == update.TelegramUserId
                && x.PendingTelegramChatId == update.ChatId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(x => x.UsedAt, now), ct);
        if (consumed == 0) return "Código de vinculação inválido ou expirado.";
        var userLink = await db.TelegramUserLinks.SingleOrDefaultAsync(x => x.UserId == linkCode.UserId, ct);
        if (userLink is null) { userLink = new(linkCode.UserId, update.TelegramUserId, update.ChatId, now); db.TelegramUserLinks.Add(userLink); }
        else userLink.Reactivate(update.TelegramUserId, update.ChatId, now);
        if (allowed.Length == 1) userLink.SelectCondominium(allowed[0].Id, now); else userLink.ClearCondominium(now);
        await db.SaveChangesAsync(ct);
        if (transaction is not null) await transaction.CommitAsync(ct);
        var responseText = allowed.Length == 1
            ? $"Telegram vinculado ao Comvy. Contexto atual: {allowed[0].Name}.\n\nPode perguntar sobre as informações disponíveis desse condomínio."
            : "Telegram vinculado ao Comvy.\n\n" + CondominiumChoices(allowed);
        update.PrepareResponse(responseText, TelegramReplyMarkup.RemoveKeyboard);
        return responseText;
    }

    private async Task<string?> TranscribeAudioAsync(TelegramInboundUpdate update,
        IServiceProvider services, Guid executionId, CancellationToken ct)
    {
        var settings = options.Value;
        var maximumBytes = Math.Clamp(settings.MaximumAudioBytes, 1, AttachmentPolicy.MaximumFileSize);
        var maximumDuration = Math.Clamp(settings.MaximumAudioDurationSeconds, 1, 1200);
        if (string.IsNullOrWhiteSpace(update.FileId)
            || update.FileSize is <= 0 || update.FileSize > maximumBytes
            || update.DurationSeconds is <= 0 || update.DurationSeconds > maximumDuration) return AudioFailure;
        var contentType = update.Kind == TelegramInboundKind.Voice ? "audio/ogg" : update.MimeType;
        if (AttachmentPolicy.ResolveAudioMultipartFormat(contentType) is null) return AudioFailure;
        byte[]? audio = null;
        try
        {
            var download = Stopwatch.StartNew();
            audio = await services.GetRequiredService<ITelegramBotClient>()
                .DownloadFileAsync(update.FileId, maximumBytes, ct);
            download.Stop(); update.RecordAudioDownload(download.ElapsedMilliseconds);
            var transcription = Stopwatch.StartNew();
            AudioTranscriptionResult result;
            using (AssistantExecutionContext.Begin(executionId))
                result = await services.GetRequiredService<IWhatsAppAudioTranscriptionService>()
                    .TranscribeAsync(audio, "telegram-audio", contentType!, ct);
            transcription.Stop(); update.RecordTranscription(transcription.ElapsedMilliseconds);
            if (!result.Succeeded || string.IsNullOrWhiteSpace(result.Text) || result.Text.Length > 4096)
                return AudioFailure;
            update.SetTranscribedText(result.Text);
            return null;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            logger.LogWarning("Telegram audio processing failed. UpdateId: {UpdateId}; FailureType: {FailureType}.",
                update.UpdateId, exception.GetType().Name);
            return AudioFailure;
        }
        finally { if (audio is not null) CryptographicOperations.ZeroMemory(audio); }
    }

    private static IReadOnlyList<AssistantSource> ParseSources(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<List<AssistantSource>>(json,
            CondominiumAssistantEndpoints.AssistantJsonOptions) ?? []; }
        catch (JsonException) { return []; }
    }

    private static void RecordAuthorization(TelegramInboundUpdate update, Stopwatch stopwatch)
    { stopwatch.Stop(); update.RecordAuthorization(stopwatch.ElapsedMilliseconds); }

    internal static bool IsSourceQuestion(string text)
    {
        var normalized = string.Concat(text.Normalize(NormalizationForm.FormD)
            .Where(x => CharUnicodeInfo.GetUnicodeCategory(x) != UnicodeCategory.NonSpacingMark))
            .ToLowerInvariant();
        return normalized.Contains("qual a fonte") || normalized.Contains("quais as fontes")
            || normalized.Contains("de onde voce tirou") || normalized.Contains("em qual documento")
            || normalized.Contains("qual documento voce consultou") || normalized.Contains("mostre a fonte")
            || normalized.Contains("onde isso esta escrito");
    }

    internal static string FormatSources(IReadOnlyList<AssistantSource> sources)
    {
        var groups = sources.GroupBy(x => x.DocumentName)
            .Select(group => new { Name = group.Key, Pages = Pages(group.Select(x => x.PageNumber)) }).ToArray();
        if (groups.Length == 0) return "Essa resposta não possui uma fonte documental associada.";
        if (groups.Length == 1) return $"Essa informação veio de {groups[0].Name}{groups[0].Pages}.";
        return "Consultei:\n\n" + string.Join('\n', groups.Select(x => $"• {x.Name}{x.Pages}"));
    }

    private static string Status(TelegramUserLink link, (Guid Id, string Name)[] condominiums)
    { var active = condominiums.SingleOrDefault(x => x.Id == link.ActiveCondominiumId);
      return active == default ? CondominiumChoices(condominiums)
          : $"Telegram conectado. Contexto atual: {active.Name}. Envie uma pergunta ou use /condominio para trocar."; }
    private static string CondominiumChoices((Guid Id, string Name)[] items) =>
        "Escolha primeiro o condomínio que deseja consultar:\n"
        + string.Join('\n', items.Select((x, index) => $"{index + 1}. {x.Name}"))
        + "\n\nEnvie /condominio NÚMERO.";
    internal static string FormatAnswer(AssistantAnswer answer) =>
        Regex.Replace(answer.Answer, @"\s*\[S\d+\]", string.Empty).Trim();
    private static string Pages(IEnumerable<int?> pages)
    { var values = pages.Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order().ToArray();
      return values.Length == 0 ? string.Empty : values.Length == 1
          ? $", página {values[0]}" : ", págs. " + string.Join(", ", values); }
    internal static IReadOnlyList<string> SplitMessage(string text, int limit = 3800)
    { var result = new List<string>(); var remaining = text;
      while (remaining.Length > limit) { var cut = remaining.LastIndexOf("\n\n", limit, StringComparison.Ordinal);
        if (cut < limit / 2) cut = remaining.LastIndexOf('\n', limit); if (cut < limit / 2) cut = limit;
        result.Add(remaining[..cut].Trim()); remaining = remaining[cut..].TrimStart(); }
      if (remaining.Length > 0) result.Add(remaining); return result; }
    internal static async Task TypingAsync(ITelegramBotClient client, long chatId, CancellationToken ct,
        TimeSpan? renewalInterval = null)
    { var interval = renewalInterval ?? TimeSpan.FromSeconds(4);
      while (!ct.IsCancellationRequested) { try { await client.SendTypingAsync(chatId, ct); await Task.Delay(interval, ct); }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; } catch { await Task.Delay(interval, ct); } } }
    private static string SafeError(Exception exception) => exception switch
    { TelegramBotApiException telegram when telegram.Category == "http_error" && telegram.HttpStatus.HasValue => $"telegram_http_{(int)telegram.HttpStatus.Value}",
      TelegramBotApiException telegram => $"telegram_{telegram.Category}",
      HttpRequestException http when http.StatusCode.HasValue => $"telegram_http_{(int)http.StatusCode.Value}",
      HttpRequestException => "telegram_api", TimeoutException => "timeout", _ => "processing_failed" };

    private void LogFailure(TelegramInboundUpdate update, string stage, string error, Exception exception)
    {
        var terminal = update.Status == TelegramInboundStatus.Failed;
        if (stage == "telegram_delivery" && exception is TelegramBotApiException telegram)
        {
            logger.LogWarning("Telegram delivery failed. Stage: telegram_delivery; Operation: {Operation}; UpdateId: {UpdateId}; ChatId: {ChatId}; Attempt: {Attempt}; HTTPStatus: {HTTPStatus}; TelegramErrorCode: {TelegramErrorCode}; TelegramDescription: {TelegramDescription}; ExceptionCategory: {ExceptionCategory}; ExceptionType: {ExceptionType}; Error: {Error}; Terminal: {Terminal}.",
                telegram.Operation, update.UpdateId, update.ChatId, update.Attempts,
                telegram.HttpStatus.HasValue ? (int)telegram.HttpStatus.Value : null,
                telegram.TelegramErrorCode, telegram.TelegramDescription,
                telegram.Category, telegram.ExceptionType, error, terminal); return;
        }
        logger.LogWarning("Telegram update failed. UpdateId: {UpdateId}; ChatId: {ChatId}; Stage: {Stage}; Attempt: {Attempt}; ExceptionCategory: {ExceptionCategory}; Error: {Error}; Terminal: {Terminal}.",
            update.UpdateId, update.ChatId, stage, update.Attempts,
            exception is HttpRequestException ? "network" : exception.GetType().Name, error, terminal);
    }

    private void LogLatency(TelegramInboundUpdate update, string result) =>
        logger.LogInformation("Telegram latency. UpdateId: {UpdateId}; AssistantExecutionId: {AssistantExecutionId}; ReceivedAt: {ReceivedAt}; WorkerStartedAt: {WorkerStartedAt}; QueueMs: {QueueMs}; AuthorizationMs: {AuthorizationMs}; AudioDownloadMs: {AudioDownloadMs}; TranscriptionMs: {TranscriptionMs}; AssistantMs: {AssistantMs}; DeliveryMs: {DeliveryMs}; TotalMs: {TotalMs}; Result: {Result}.",
            update.UpdateId, update.AssistantExecutionId, update.ReceivedAt, update.ProcessingStartedAt,
            update.QueueDurationMs, update.AuthorizationDurationMs, update.AudioDownloadDurationMs,
            update.TranscriptionDurationMs, update.AssistantDurationMs, update.DeliveryDurationMs,
            update.TotalDurationMs, result);

    internal static (string? Name, string? Argument) ParseCommand(string text)
    {
        var parts = text.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0 || !parts[0].StartsWith('/')) return (null, null);
        var name = parts[0].Split('@', 2)[0].ToLowerInvariant();
        return (name, parts.Length == 2 ? parts[1].Trim() : null);
    }

    private async Task TrySendTerminalFailureAsync(TelegramInboundUpdate update, AppDbContext db,
        IServiceProvider services, CancellationToken ct)
    {
        const string message = "Não consegui concluir essa consulta agora. Tente novamente em instantes.";
        update.PrepareResponse(message); await db.SaveChangesAsync(CancellationToken.None);
        try
        {
            var delivery = Stopwatch.StartNew();
            await services.GetRequiredService<ITelegramBotClient>().SendMessageAsync(update.ChatId, message, ct);
            delivery.Stop(); update.RecordDelivery(delivery.ElapsedMilliseconds);
            var completedAt = time.GetUtcNow().UtcDateTime;
            update.RecordTotal((long)(completedAt - update.ReceivedAt).TotalMilliseconds);
            update.PartSent(); update.Complete(completedAt); await db.SaveChangesAsync(CancellationToken.None);
            LogLatency(update, "terminal_failure_notified");
        }
        catch (Exception exception)
        { logger.LogError("Telegram terminal failure could not be delivered. UpdateId: {UpdateId}; ChatId: {ChatId}; Error: {Error}.",
            update.UpdateId, update.ChatId, SafeError(exception)); }
    }
}
