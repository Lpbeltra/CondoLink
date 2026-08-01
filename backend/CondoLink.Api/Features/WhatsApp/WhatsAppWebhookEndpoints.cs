using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CondoLink.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CondoLink.Api.Features.WhatsApp;

public static class WhatsAppWebhookEndpoints
{
    private const int MaximumPayloadBytes = 256 * 1024;

    public static IEndpointRouteBuilder MapWhatsAppWebhook(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/webhooks/whatsapp", VerifyAsync)
            .AllowAnonymous();
        endpoints.MapPost("/webhooks/whatsapp", ReceiveAsync)
            .AllowAnonymous();
        return endpoints;
    }

    private static IResult VerifyAsync(
        HttpRequest request,
        IOptions<WhatsAppOptions> options)
    {
        var settings = options.Value;
        if (!settings.Enabled) return Results.NotFound();
        var mode = request.Query["hub.mode"].ToString();
        var token = request.Query["hub.verify_token"].ToString();
        var challenge = request.Query["hub.challenge"].ToString();
        if (mode != "subscribe"
            || string.IsNullOrEmpty(challenge)
            || string.IsNullOrEmpty(settings.VerifyToken)
            || !FixedTimeEquals(token, settings.VerifyToken))
            return Results.Json(
                new { error = "Webhook verification failed." },
                statusCode: StatusCodes.Status403Forbidden);
        return Results.Text(challenge, "text/plain", Encoding.UTF8);
    }

    private static async Task<IResult> ReceiveAsync(
        HttpRequest request,
        IOptions<WhatsAppOptions> options,
        WhatsAppConversationService conversations,
        AppDbContext dbContext,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var logger = loggerFactory.CreateLogger(
            typeof(WhatsAppWebhookEndpoints));
        logger.LogInformation(
            "WhatsApp webhook POST received. ContentLength: {ContentLength}; SignatureHeaderPresent: {SignatureHeaderPresent}.",
            request.ContentLength,
            request.Headers.ContainsKey("X-Hub-Signature-256"));

        var settings = options.Value;
        if (!settings.Enabled) return Results.NotFound();
        if (string.IsNullOrWhiteSpace(settings.AppSecret))
            return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
        if (request.ContentLength > MaximumPayloadBytes)
            return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);

        byte[] body;
        try
        {
            await using var buffer = new MemoryStream();
            var chunk = new byte[8192];
            int read;
            while ((read = await request.Body.ReadAsync(chunk, cancellationToken)) > 0)
            {
                if (buffer.Length + read > MaximumPayloadBytes)
                    return Results.StatusCode(StatusCodes.Status413PayloadTooLarge);
                await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken);
            }
            body = buffer.ToArray();
        }
        catch (IOException)
        {
            logger.LogWarning("WhatsApp webhook payload could not be read.");
            return Results.BadRequest(new { error = "Could not read webhook payload." });
        }

        if (body.Length == 0)
            logger.LogWarning("WhatsApp webhook payload is empty.");

        if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var signature))
        {
            logger.LogWarning("WhatsApp webhook signature is missing.");
            return Results.Json(
                new { error = "Invalid webhook signature." },
                statusCode: StatusCodes.Status401Unauthorized);
        }
        if (!ValidateSignature(body, signature.ToString(), settings.AppSecret))
        {
            logger.LogWarning("WhatsApp webhook signature is invalid.");
            return Results.Json(
                new { error = "Invalid webhook signature." },
                statusCode: StatusCodes.Status401Unauthorized);
        }
        logger.LogInformation("WhatsApp webhook signature validated successfully.");

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(body, new JsonDocumentOptions
            {
                MaxDepth = 32
            });
        }
        catch (JsonException)
        {
            logger.LogWarning("WhatsApp webhook payload contains invalid JSON.");
            return Results.BadRequest(new { error = "Invalid JSON payload." });
        }

        using (document)
        {
            var messages = WhatsAppWebhookParser.Parse(document.RootElement);
            logger.LogInformation(
                "WhatsApp webhook parsing completed. ProcessableMessageCount: {ProcessableMessageCount}.",
                messages.Count);
            if (messages.Count == 0)
                logger.LogInformation(
                    "WhatsApp webhook event ignored because it contains no processable message.");
            foreach (var message in messages)
            {
                logger.LogInformation(
                    "WhatsApp parsed message metadata. MessageType: {MessageType}; HasMediaId: {HasMediaId}; HasMimeType: {HasMimeType}; HasFileName: {HasFileName}.",
                    message.MessageType,
                    !string.IsNullOrWhiteSpace(message.MediaId),
                    !string.IsNullOrWhiteSpace(message.MediaContentType),
                    !string.IsNullOrWhiteSpace(message.FileName));
                try
                {
                    await conversations.ProcessAsync(message, cancellationToken);
                }
                catch (Exception exception)
                {
                    logger.LogError(
                        exception,
                        "WhatsApp webhook processing failed.");
                    return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
                }
            }
            foreach (var status in WhatsAppWebhookParser.ParseStatuses(
                document.RootElement))
            {
                var outbound = await dbContext.WhatsAppOutboundMessages
                    .SingleOrDefaultAsync(
                        x => x.ExternalMessageId == status.ExternalMessageId,
                        cancellationToken);
                if (outbound is null) continue;
                outbound.ApplyProviderStatus(
                    status.Status, status.OccurredAt,
                    status.ErrorCode, status.ErrorDescription);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }
        logger.LogInformation("WhatsApp webhook processing completed.");
        return Results.Ok();
    }

    internal static bool ValidateSignature(
        ReadOnlySpan<byte> body,
        string signature,
        string appSecret)
    {
        const string prefix = "sha256=";
        if (signature.Length != prefix.Length + 64
            || !signature.StartsWith(prefix, StringComparison.Ordinal)
            || signature.AsSpan(prefix.Length).ContainsAnyExcept(
                "0123456789abcdef"))
            return false;
        byte[] provided;
        try
        {
            provided = Convert.FromHexString(signature[prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }
        if (provided.Length != 32) return false;
        var expected = HMACSHA256.HashData(Encoding.UTF8.GetBytes(appSecret), body);
        return CryptographicOperations.FixedTimeEquals(expected, provided);
    }

    private static bool FixedTimeEquals(string provided, string expected)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));
        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}
