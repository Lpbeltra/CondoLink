using CondoLink.Domain.Entities;
using CondoLink.Domain.Enums;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CondoLink.Api.Features.TelegramAssistant;

internal enum TelegramActionDecision { Confirm, Cancel }
internal sealed record TelegramActionCallback(Guid ActionId, TelegramActionDecision Decision);
internal sealed record TelegramActionResolution(bool Accepted, string Message, TelegramActionCallback? Callback = null);

internal static class TelegramActionCallbacks
{
    private const string Prefix = "ca:v1:";
    internal static string Format(Guid actionId, TelegramActionDecision decision) => $"{Prefix}{actionId:N}:{(decision == TelegramActionDecision.Confirm ? "c" : "x")}";
    internal static TelegramInlineKeyboard Keyboard(Guid actionId) => new([
        new("Confirmar", Format(actionId, TelegramActionDecision.Confirm)),
        new("Cancelar", Format(actionId, TelegramActionDecision.Cancel))]);
    internal static bool TryParse(string? value, out TelegramActionCallback callback)
    {
        callback = default!;
        if (string.IsNullOrWhiteSpace(value) || System.Text.Encoding.UTF8.GetByteCount(value) > 64 || !value.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        var parts = value.Split(':');
        if (parts.Length != 4 || !Guid.TryParseExact(parts[2], "N", out var id) || id == Guid.Empty) return false;
        callback = parts[3] switch { "c" => new(id, TelegramActionDecision.Confirm), "x" => new(id, TelegramActionDecision.Cancel), _ => default! };
        return callback is not null;
    }
    internal static async Task<TelegramActionResolution> ResolveAsync(AppDbContext db, TelegramActionCallback callback,
        Guid actorUserId, long chatId, Guid? activeCondominiumId, CancellationToken ct)
    {
        var action = await db.PendingAssistantActions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == callback.ActionId
            && x.ActorUserId == actorUserId && x.Channel == CondominiumAssistantChannel.Telegram
            && x.ExternalContextId == ChatContext(chatId), ct);
        if (action is null || activeCondominiumId != action.CondominiumId) return new(false, "Essa ação não está mais disponível.");
        if (action.IsExpired(DateTime.UtcNow) || action.Status == PendingAssistantActionStatus.Expired)
            return new(false, "Essa confirmação expirou. Envie o pedido novamente para preparar um novo cadastro.");
        if (action.Status == PendingAssistantActionStatus.Executed)
            return new(false, callback.Decision == TelegramActionDecision.Confirm ? "Esse cadastro já foi realizado." : "Esse cadastro já foi realizado e não pode mais ser cancelado por aqui.");
        if (action.Status == PendingAssistantActionStatus.Cancelled)
            return new(false, callback.Decision == TelegramActionDecision.Cancel ? "Esse cadastro já foi cancelado." : "Esse cadastro foi cancelado e não pode mais ser confirmado.");
        if (action.Status != PendingAssistantActionStatus.Pending) return new(false, "Essa confirmação não está mais disponível.");
        return new(true, callback.Decision == TelegramActionDecision.Confirm ? "Confirmação recebida." : "Cancelamento recebido.", callback);
    }
    internal static string ChatContext(long chatId) => $"telegram-chat:{chatId}";
    internal static string InboundText(string data) => "\u001fcallback:" + data;
    internal static bool TryInbound(string text, out TelegramActionCallback callback)
    {
        callback = default!;
        return text.StartsWith("\u001fcallback:", StringComparison.Ordinal) && TryParse(text[10..], out callback);
    }
}

/// <summary>Persisted worker response. The opaque action id is stripped before Telegram delivery.</summary>
internal sealed record TelegramAssistantResponse(string Text, Guid? PendingActionId = null)
{
    private const string Prefix = "\u001faction:";
    internal string Persist() => PendingActionId is Guid id ? $"{Prefix}{id:N}\n{Text}" : Text;
    internal static TelegramAssistantResponse Read(string persisted)
    {
        if (persisted.StartsWith(Prefix, StringComparison.Ordinal))
        {
            var newline = persisted.IndexOf('\n');
            if (newline > Prefix.Length && Guid.TryParseExact(persisted[Prefix.Length..newline], "N", out var id))
                return new(persisted[(newline + 1)..], id);
        }
        return new(persisted);
    }
}
