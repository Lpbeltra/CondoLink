using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CondoLink.Domain.Entities;
using CondoLink.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.TelegramAssistant;

public static class TelegramAssistantEndpoints
{
    private const int MaximumBodyBytes = 64 * 1024;
    private const string SecretHeader = "X-Telegram-Bot-Api-Secret-Token";
    public static IEndpointRouteBuilder MapTelegramAssistant(this IEndpointRouteBuilder app)
    {
        var me = app.MapGroup("/users/me/telegram").RequireAuthorization().WithTags("Telegram Assistant");
        me.MapGet("", StatusAsync); me.MapPost("/link-code", CreateCodeAsync); me.MapDelete("", UnlinkAsync);
        app.MapPost("/integrations/telegram/webhook", WebhookAsync)
            .WithMetadata(new RequestSizeLimitAttribute(MaximumBodyBytes));
        return app;
    }

    private static async Task<IResult> StatusAsync(ClaimsPrincipal principal, AppDbContext db,
        IOptions<TelegramAssistantOptions> options, CancellationToken ct)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        var link = await db.TelegramUserLinks.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == userId && x.IsActive, ct);
        string? condominium = null;
        if (link?.ActiveCondominiumId is Guid id)
            condominium = await db.Condominiums.Where(x => x.Id == id).Select(x => x.Name).SingleOrDefaultAsync(ct);
        return Results.Ok(new { enabled = options.Value.IsConfigured, options.Value.BotUsername,
            linked = link is not null, linkedAt = link?.VerifiedAt, activeCondominiumName = condominium });
    }

    private static async Task<IResult> CreateCodeAsync(ClaimsPrincipal principal, AppDbContext db,
        IOptions<TelegramAssistantOptions> options, TimeProvider time, CancellationToken ct)
    {
        if (!options.Value.IsConfigured) return Results.Problem("Assistente Telegram está desativado.", statusCode: 503);
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        if ((await TelegramAssistantAccess.CondominiumsAsync(db, userId, ct)).Length == 0) return Results.Forbid();
        var now = time.GetUtcNow().UtcDateTime;
        var recent = await db.TelegramLinkCodes.CountAsync(x => x.UserId == userId && x.CreatedAt > now.AddMinutes(-5), ct);
        if (recent >= 3) return Results.Json(new { message = "Aguarde alguns minutos antes de gerar outro código." }, statusCode: 429);
        await db.TelegramLinkCodes.Where(x => x.UserId == userId && x.UsedAt == null && x.InvalidatedAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.InvalidatedAt, now), ct);
        var code = CreateCode();
        var expiresAt = now.AddMinutes(10);
        db.TelegramLinkCodes.Add(new(userId, HashCode(code), now, expiresAt));
        await db.SaveChangesAsync(ct);
        var deepLink = BuildDeepLink(options.Value.BotUsername, code);
        return Results.Ok(new { code, deepLink, expiresAt });
    }

    private static async Task<IResult> UnlinkAsync(ClaimsPrincipal principal, AppDbContext db,
        TimeProvider time, CancellationToken ct)
    {
        if (!TryUserId(principal, out var userId)) return Results.Unauthorized();
        var link = await db.TelegramUserLinks.SingleOrDefaultAsync(x => x.UserId == userId && x.IsActive, ct);
        if (link is not null) { link.Deactivate(time.GetUtcNow().UtcDateTime); await db.SaveChangesAsync(ct); }
        return Results.NoContent();
    }

    private static async Task<IResult> WebhookAsync(HttpRequest request, AppDbContext db,
        IOptions<TelegramAssistantOptions> options, TimeProvider time, CancellationToken ct)
    {
        var logger = request.HttpContext.RequestServices.GetRequiredService<ILoggerFactory>()
            .CreateLogger("TelegramWebhook");
        if (!options.Value.IsConfigured) { logger.LogWarning("Telegram webhook rejected: integration_disabled."); return Results.NotFound(); }
        if (!request.Headers.TryGetValue(SecretHeader, out var secret)
            || !FixedTimeEquals(secret.ToString(), options.Value.WebhookSecret!))
        { logger.LogWarning("Telegram webhook rejected: invalid_secret."); return Results.Unauthorized(); }
        if (request.ContentLength > MaximumBodyBytes) return Results.StatusCode(413);
        JsonDocument document;
        try { document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct); }
        catch (JsonException)
        { logger.LogInformation("Telegram webhook rejected: invalid_json."); return Results.BadRequest(); }
        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("update_id", out var updateNode) || !updateNode.TryGetInt64(out var updateId))
            { logger.LogInformation("Telegram webhook ignored: missing_update_id."); return Results.Ok(); }
            if (!TryPrivateMessage(root, out var inboundMessage))
            { logger.LogInformation("Telegram update ignored. UpdateId: {UpdateId}; Reason: unsupported_or_non_private.", updateId); return Results.Ok(); }
            if (inboundMessage.Kind == CondoLink.Domain.Enums.TelegramInboundKind.Text
                && inboundMessage.Text.Length is 0 or > 4096)
            { logger.LogInformation("Telegram update ignored. UpdateId: {UpdateId}; Reason: invalid_text_length.", updateId); return Results.Ok(); }
            var now = time.GetUtcNow().UtcDateTime;
            var inbound = inboundMessage.Kind == CondoLink.Domain.Enums.TelegramInboundKind.Text
                ? new TelegramInboundUpdate(updateId, inboundMessage.UserId, inboundMessage.ChatId, inboundMessage.Text, now)
                : new TelegramInboundUpdate(updateId, inboundMessage.UserId, inboundMessage.ChatId,
                    inboundMessage.Kind, now, inboundMessage.ContactPhoneNumber,
                    inboundMessage.ContactUserId, inboundMessage.FileId, inboundMessage.FileSize,
                    inboundMessage.DurationSeconds, inboundMessage.FileName, inboundMessage.MimeType);
            var pending = await db.TelegramInboundUpdates.CountAsync(x => x.ChatId == inboundMessage.ChatId
                && (x.Status == CondoLink.Domain.Enums.TelegramInboundStatus.Pending
                    || x.Status == CondoLink.Domain.Enums.TelegramInboundStatus.Processing), ct);
            if (pending >= 20) inbound.Ignore(now);
            db.TelegramInboundUpdates.Add(inbound);
            try
            { await db.SaveChangesAsync(ct); request.HttpContext.RequestServices.GetService<TelegramInboundSignal>()?.Wake(); logger.LogInformation("Telegram update persisted. UpdateId: {UpdateId}; ChatId: {ChatId}; Kind: {Kind}; QueueLimited: {QueueLimited}.", updateId, inboundMessage.ChatId, inboundMessage.Kind, pending >= 20); }
            catch (DbUpdateException)
            { logger.LogInformation("Telegram update deduplicated. UpdateId: {UpdateId}.", updateId); return Results.Ok(); }
        }
        return Results.Ok();
    }

    internal static bool TryPrivateText(JsonElement root, out long userId, out long chatId, out string text)
    {
        if (TryPrivateMessage(root, out var message)
            && message.Kind == CondoLink.Domain.Enums.TelegramInboundKind.Text)
        { userId = message.UserId; chatId = message.ChatId; text = message.Text; return true; }
        userId = chatId = 0; text = string.Empty; return false;
    }

    internal static bool TryPrivateMessage(JsonElement root, out TelegramInboundMessage result)
    {
        result = default;
        if (!root.TryGetProperty("message", out var message)
            || !message.TryGetProperty("chat", out var chat)
            || !chat.TryGetProperty("type", out var type) || type.GetString() != "private"
            || !chat.TryGetProperty("id", out var chatNode) || !chatNode.TryGetInt64(out var chatId)
            || !message.TryGetProperty("from", out var from)
            || !from.TryGetProperty("id", out var userNode) || !userNode.TryGetInt64(out var userId)
            || userId <= 0 || chatId <= 0) return false;
        if (message.TryGetProperty("text", out var textNode))
        { result = new(userId, chatId, CondoLink.Domain.Enums.TelegramInboundKind.Text,
            textNode.GetString()?.Trim() ?? string.Empty); return true; }
        if (message.TryGetProperty("contact", out var contact)
            && contact.TryGetProperty("phone_number", out var phone))
        {
            var phoneNumber = phone.GetString();
            if (string.IsNullOrWhiteSpace(phoneNumber) || phoneNumber.Length > 32) return false;
            long? contactUserId = contact.TryGetProperty("user_id", out var contactUser)
                && contactUser.TryGetInt64(out var value) ? value : null;
            result = new(userId, chatId, CondoLink.Domain.Enums.TelegramInboundKind.Contact,
                string.Empty, phoneNumber, contactUserId); return true;
        }
        var kind = message.TryGetProperty("voice", out var media)
            ? CondoLink.Domain.Enums.TelegramInboundKind.Voice
            : message.TryGetProperty("audio", out media)
                ? CondoLink.Domain.Enums.TelegramInboundKind.Audio : (CondoLink.Domain.Enums.TelegramInboundKind?)null;
        if (kind is null || !media.TryGetProperty("file_id", out var fileId)) return false;
        var identifier = fileId.GetString();
        var fileName = OptionalString(media, "file_name");
        var mimeType = OptionalString(media, "mime_type");
        if (string.IsNullOrWhiteSpace(identifier) || identifier.Length > 256
            || fileName is { Length: > 255 } || mimeType is { Length: > 100 }) return false;
        result = new(userId, chatId, kind.Value, string.Empty, null, null,
            identifier, OptionalInt64(media, "file_size"), OptionalInt32(media, "duration"),
            fileName, mimeType);
        return true;
    }
    private static string? OptionalString(JsonElement value, string name) =>
        value.TryGetProperty(name, out var node) && node.ValueKind == JsonValueKind.String ? node.GetString() : null;
    private static long? OptionalInt64(JsonElement value, string name) =>
        value.TryGetProperty(name, out var node) && node.TryGetInt64(out var number) ? number : null;
    private static int? OptionalInt32(JsonElement value, string name) =>
        value.TryGetProperty(name, out var node) && node.TryGetInt32(out var number) ? number : null;
    internal static string HashCode(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));
    internal static string? BuildDeepLink(string? username, string code)
    {
        var normalized = username?.Trim().TrimStart('@');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > 32
            || normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_')) return null;
        return $"https://t.me/{normalized}?start={Uri.EscapeDataString(code)}";
    }
    private static string CreateCode()
    { const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789"; Span<byte> bytes = stackalloc byte[8];
      RandomNumberGenerator.Fill(bytes); return string.Create(8, bytes.ToArray(), (chars, state) =>
      { for (var i = 0; i < chars.Length; i++) chars[i] = alphabet[state[i] % alphabet.Length]; }); }
    private static bool FixedTimeEquals(string left, string right)
    { var a = SHA256.HashData(Encoding.UTF8.GetBytes(left)); var b = SHA256.HashData(Encoding.UTF8.GetBytes(right));
      return CryptographicOperations.FixedTimeEquals(a, b); }
    private static bool TryUserId(ClaimsPrincipal principal, out Guid userId)
    { var value = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
      return Guid.TryParse(value, out userId); }
}

internal readonly record struct TelegramInboundMessage(long UserId, long ChatId,
    CondoLink.Domain.Enums.TelegramInboundKind Kind, string Text,
    string? ContactPhoneNumber = null, long? ContactUserId = null,
    string? FileId = null, long? FileSize = null, int? DurationSeconds = null,
    string? FileName = null, string? MimeType = null);
