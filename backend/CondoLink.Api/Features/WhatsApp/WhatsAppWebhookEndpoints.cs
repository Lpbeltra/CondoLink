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
        endpoints.MapGet("/integrations/whatsapp/webhook", VerifyAsync)
            .AllowAnonymous();
        endpoints.MapPost("/integrations/whatsapp/webhook", ReceiveAsync)
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
            return Results.BadRequest(new { error = "Could not read webhook payload." });
        }

        if (!request.Headers.TryGetValue("X-Hub-Signature-256", out var signature)
            || !ValidateSignature(body, signature.ToString(), settings.AppSecret))
            return Results.Json(
                new { error = "Invalid webhook signature." },
                statusCode: StatusCodes.Status401Unauthorized);

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
            return Results.BadRequest(new { error = "Invalid JSON payload." });
        }

        using (document)
        {
            var messages = WhatsAppWebhookParser.Parse(document.RootElement);
            foreach (var message in messages)
            {
                try
                {
                    await conversations.ProcessAsync(message, cancellationToken);
                }
                catch (Exception exception)
                {
                    loggerFactory.CreateLogger(typeof(WhatsAppWebhookEndpoints))
                        .LogError(
                            exception,
                            "WhatsApp webhook processing failed for event {ExternalMessageId}.",
                            message.ExternalMessageId);
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
        return Results.Ok();
    }

    internal static bool ValidateSignature(
        ReadOnlySpan<byte> body,
        string signature,
        string appSecret)
    {
        const string prefix = "sha256=";
        if (!signature.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
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
