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
        var deepLink = string.IsNullOrWhiteSpace(options.Value.BotUsername) ? null
            : $"https://t.me/{options.Value.BotUsername.TrimStart('@')}?start={code}";
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
        if (!options.Value.IsConfigured) return Results.NotFound();
        if (!request.Headers.TryGetValue(SecretHeader, out var secret)
            || !FixedTimeEquals(secret.ToString(), options.Value.WebhookSecret!)) return Results.Unauthorized();
        if (request.ContentLength > MaximumBodyBytes) return Results.StatusCode(413);
        JsonDocument document;
        try { document = await JsonDocument.ParseAsync(request.Body, cancellationToken: ct); }
        catch (JsonException) { return Results.BadRequest(); }
        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("update_id", out var updateNode) || !updateNode.TryGetInt64(out var updateId)) return Results.Ok();
            if (!TryPrivateText(root, out var telegramUserId, out var chatId, out var text)) return Results.Ok();
            if (text.Length is 0 or > 4096) return Results.Ok();
            var now = time.GetUtcNow().UtcDateTime;
            var inbound = new TelegramInboundUpdate(updateId, telegramUserId, chatId, text, now);
            var pending = await db.TelegramInboundUpdates.CountAsync(x => x.ChatId == chatId
                && (x.Status == CondoLink.Domain.Enums.TelegramInboundStatus.Pending
                    || x.Status == CondoLink.Domain.Enums.TelegramInboundStatus.Processing), ct);
            if (pending >= 20) inbound.Ignore(now);
            db.TelegramInboundUpdates.Add(inbound);
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateException) { return Results.Ok(); }
        }
        return Results.Ok();
    }

    internal static bool TryPrivateText(JsonElement root, out long userId, out long chatId, out string text)
    {
        userId = chatId = 0; text = string.Empty;
        if (!root.TryGetProperty("message", out var message)
            || !message.TryGetProperty("chat", out var chat)
            || !chat.TryGetProperty("type", out var type) || type.GetString() != "private"
            || !chat.TryGetProperty("id", out var chatNode) || !chatNode.TryGetInt64(out chatId)
            || !message.TryGetProperty("from", out var from)
            || !from.TryGetProperty("id", out var userNode) || !userNode.TryGetInt64(out userId)
            || !message.TryGetProperty("text", out var textNode)) return false;
        text = textNode.GetString()?.Trim() ?? string.Empty;
        return userId > 0 && chatId > 0;
    }
    internal static string HashCode(string code) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(code.Trim().ToUpperInvariant())));
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
