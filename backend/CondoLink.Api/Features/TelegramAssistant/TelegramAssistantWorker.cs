using System.Text.Json;
using CondoLink.Api.Features.CondominiumAssistant;
using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.TelegramAssistant;

public sealed class TelegramAssistantWorker(IServiceScopeFactory scopes,
    IOptions<TelegramAssistantOptions> options, TimeProvider time,
    ILogger<TelegramAssistantWorker> logger) : BackgroundService
{
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
        try
        {
            if (update.ResponseText is null)
            {
                var response = await HandleAsync(update, db, scope.ServiceProvider, ct);
                if (response is null) { update.Ignore(now); await db.SaveChangesAsync(ct); return true; }
                update.PrepareResponse(response); await db.SaveChangesAsync(ct);
            }
            var client = scope.ServiceProvider.GetRequiredService<ITelegramBotClient>();
            var parts = SplitMessage(update.ResponseText!);
            for (var index = update.SentPartCount; index < parts.Count; index++)
            { await client.SendMessageAsync(update.ChatId, parts[index], ct); update.PartSent(); await db.SaveChangesAsync(ct); }
            update.Complete(time.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(ct);
            logger.LogInformation("Telegram update processed. UpdateId: {UpdateId}; ChatId: {ChatId}.", update.UpdateId, update.ChatId);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            update.Retry(time.GetUtcNow().UtcDateTime,
                TimeSpan.FromSeconds(5 * Math.Pow(2, Math.Min(update.Attempts - 1, 5))),
                SafeError(exception), Math.Clamp(options.Value.MaximumAttempts, 1, 8));
            await db.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning("Telegram update failed. UpdateId: {UpdateId}; Attempt: {Attempt}; Error: {Error}.",
                update.UpdateId, update.Attempts, SafeError(exception));
        }
        return true;
    }

    private static async Task<string?> HandleAsync(TelegramInboundUpdate update, AppDbContext db,
        IServiceProvider services, CancellationToken ct)
    {
        var command = update.Text.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var link = await db.TelegramUserLinks.SingleOrDefaultAsync(x => x.TelegramUserId == update.TelegramUserId
            && x.TelegramChatId == update.ChatId && x.IsActive, ct);
        if (command.Length == 2 && command[0].Equals("/start", StringComparison.OrdinalIgnoreCase))
            return await LinkAsync(update, command[1], link, db, ct);
        if (link is null) return "Telegram não vinculado. Gere um código na sua conta Comvy e envie /start CÓDIGO.";
        if (command[0].Equals("/sair", StringComparison.OrdinalIgnoreCase)
            || command[0].Equals("/desvincular", StringComparison.OrdinalIgnoreCase))
        { link.Deactivate(DateTime.UtcNow); await db.SaveChangesAsync(ct); return "Telegram desvinculado do Comvy."; }

        var condominiums = await TelegramAssistantAccess.CondominiumsAsync(db, link.UserId, ct);
        if (condominiums.Length == 0) return "Seu acesso ao Assistente não está disponível. Verifique suas permissões no Comvy.";
        if (command[0].Equals("/ajuda", StringComparison.OrdinalIgnoreCase))
            return "Envie perguntas sobre os documentos do condomínio. Use /condominio para consultar ou trocar o contexto e /sair para desvincular. O bot não executa alterações.";
        if (command[0].Equals("/start", StringComparison.OrdinalIgnoreCase))
            return Status(link, condominiums);
        if (command[0].Equals("/condominio", StringComparison.OrdinalIgnoreCase))
        {
            if (command.Length == 2 && int.TryParse(command[1], out var selected)
                && selected >= 1 && selected <= condominiums.Length)
            { var item = condominiums[selected - 1]; link.SelectCondominium(item.Id, DateTime.UtcNow);
              await db.SaveChangesAsync(ct); return $"Contexto atual: {item.Name}."; }
            return CondominiumChoices(condominiums);
        }
        var active = condominiums.SingleOrDefault(x => x.Id == link.ActiveCondominiumId);
        if (active == default && condominiums.Length == 1)
        { active = condominiums[0]; link.SelectCondominium(active.Id, DateTime.UtcNow); await db.SaveChangesAsync(ct); }
        if (active == default)
        { link.ClearCondominium(DateTime.UtcNow); await db.SaveChangesAsync(ct); return CondominiumChoices(condominiums); }
        var recent = await db.TelegramInboundUpdates.CountAsync(x => x.ChatId == update.ChatId
            && x.ReceivedAt > DateTime.UtcNow.AddMinutes(-1), ct);
        if (recent > 10) return "Você enviou várias mensagens em sequência. Aguarde um instante.";

        var conversation = await db.CondominiumAssistantConversations
            .Where(x => x.CreatedByUserId == link.UserId && x.CondominiumId == active.Id
                && x.Channel == CondominiumAssistantChannel.Telegram)
            .OrderByDescending(x => x.UpdatedAt).FirstOrDefaultAsync(ct);
        if (conversation is null)
        { conversation = new(active.Id, link.UserId, null, "Telegram", CondominiumAssistantChannel.Telegram);
          db.CondominiumAssistantConversations.Add(conversation); }
        if (update.ConversationId is null)
        { var userMessage = new CondominiumAssistantMessage(conversation.Id, CondominiumAssistantRole.User, update.Text);
          db.CondominiumAssistantMessages.Add(userMessage); conversation.Touch();
          update.AttachConversation(conversation.Id, userMessage.Id); await db.SaveChangesAsync(ct); }
        else conversation = await db.CondominiumAssistantConversations.SingleAsync(x => x.Id == update.ConversationId, ct);

        var client = services.GetRequiredService<ITelegramBotClient>();
        using var typing = new CancellationTokenSource();
        var typingTask = TypingAsync(client, update.ChatId, typing.Token);
        try
        {
            var assistant = services.GetRequiredService<CondominiumAssistantService>();
            var answer = await assistant.AskAsync(conversation, update.Text, ct, channel: CondominiumAssistantChannel.Telegram);
            var response = FormatAnswer(answer);
            db.CondominiumAssistantMessages.Add(new(conversation.Id, CondominiumAssistantRole.Assistant,
                answer.Answer, JsonSerializer.Serialize(answer.Sources, CondominiumAssistantEndpoints.AssistantJsonOptions)));
            conversation.Touch(); update.PrepareResponse(response); await db.SaveChangesAsync(ct);
            return response;
        }
        finally { typing.Cancel(); try { await typingTask; } catch (OperationCanceledException) { } }
    }

    private static async Task<string> LinkAsync(TelegramInboundUpdate update, string code,
        TelegramUserLink? existing, AppDbContext db, CancellationToken ct)
    {
        if (code.Length != 8 || code.Any(x => !char.IsAsciiLetterOrDigit(x))) return "Código de vinculação inválido ou expirado.";
        var now = DateTime.UtcNow; var hash = TelegramAssistantEndpoints.HashCode(code);
        var linkCode = await db.TelegramLinkCodes.SingleOrDefaultAsync(x => x.CodeHash == hash, ct);
        if (linkCode is null || !linkCode.IsUsable(now)) return "Código de vinculação inválido ou expirado.";
        var allowed = await TelegramAssistantAccess.CondominiumsAsync(db, linkCode.UserId, ct);
        if (allowed.Length == 0) return "Sua conta não possui acesso autorizado ao Assistente.";
        var occupied = await db.TelegramUserLinks.AnyAsync(x => x.TelegramUserId == update.TelegramUserId
            && x.UserId != linkCode.UserId, ct);
        if (occupied) return "Este Telegram já está vinculado a outra conta Comvy.";
        var userLink = existing ?? await db.TelegramUserLinks.SingleOrDefaultAsync(x => x.UserId == linkCode.UserId, ct);
        if (userLink is null) { userLink = new(linkCode.UserId, update.TelegramUserId, update.ChatId, now); db.TelegramUserLinks.Add(userLink); }
        else userLink.Reactivate(update.TelegramUserId, update.ChatId, now);
        linkCode.Use(now);
        if (allowed.Length == 1) userLink.SelectCondominium(allowed[0].Id, now); else userLink.ClearCondominium(now);
        await db.SaveChangesAsync(ct);
        return allowed.Length == 1
            ? $"Telegram vinculado ao Comvy. Contexto atual: {allowed[0].Name}.\n\nPode perguntar sobre as informações disponíveis desse condomínio."
            : "Telegram vinculado ao Comvy.\n\n" + CondominiumChoices(allowed);
    }

    private static string Status(TelegramUserLink link, (Guid Id, string Name)[] condominiums)
    { var active = condominiums.SingleOrDefault(x => x.Id == link.ActiveCondominiumId);
      return active == default ? CondominiumChoices(condominiums)
          : $"Telegram conectado. Contexto atual: {active.Name}. Envie uma pergunta ou use /condominio para trocar."; }
    private static string CondominiumChoices((Guid Id, string Name)[] items) =>
        "Escolha primeiro o condomínio que deseja consultar:\n"
        + string.Join('\n', items.Select((x, index) => $"{index + 1}. {x.Name}"))
        + "\n\nEnvie /condominio NÚMERO.";
    internal static string FormatAnswer(AssistantAnswer answer)
    { if (answer.Sources.Count == 0) return answer.Answer;
      var sources = answer.Sources.GroupBy(x => new { x.DocumentId, x.DocumentName })
        .Select(group => $"• {group.Key.DocumentName}" + Pages(group.Select(x => x.PageNumber)));
      return answer.Answer + "\n\nFontes:\n" + string.Join('\n', sources); }
    private static string Pages(IEnumerable<int?> pages)
    { var values = pages.Where(x => x.HasValue).Select(x => x!.Value).Distinct().Order().ToArray();
      return values.Length == 0 ? string.Empty : " — pág. " + string.Join(", ", values); }
    internal static IReadOnlyList<string> SplitMessage(string text, int limit = 3800)
    { var result = new List<string>(); var remaining = text;
      while (remaining.Length > limit) { var cut = remaining.LastIndexOf("\n\n", limit, StringComparison.Ordinal);
        if (cut < limit / 2) cut = remaining.LastIndexOf('\n', limit); if (cut < limit / 2) cut = limit;
        result.Add(remaining[..cut].Trim()); remaining = remaining[cut..].TrimStart(); }
      if (remaining.Length > 0) result.Add(remaining); return result; }
    private static async Task TypingAsync(ITelegramBotClient client, long chatId, CancellationToken ct)
    { while (!ct.IsCancellationRequested) { try { await client.SendTypingAsync(chatId, ct); await Task.Delay(4000, ct); }
      catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; } catch { await Task.Delay(4000, ct); } } }
    private static string SafeError(Exception exception) => exception switch
    { HttpRequestException => "telegram_api", TimeoutException => "timeout", _ => "processing_failed" };
}
