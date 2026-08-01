using System.Text.Json;

namespace CondoLink.Api.Features.WhatsApp;

public sealed record NormalizedWhatsAppMessage(
    string ExternalMessageId,
    string PhoneNumber,
    string MessageType,
    string? Text,
    DateTime ProviderTimestamp,
    string? MediaId = null,
    string? FileName = null,
    string? MediaContentType = null);

public sealed record NormalizedWhatsAppStatus(
    string ExternalMessageId,
    string Status,
    DateTime OccurredAt,
    string? ErrorCode,
    string? ErrorDescription);

public static class WhatsAppWebhookParser
{
    public static IReadOnlyList<NormalizedWhatsAppMessage> Parse(JsonElement root)
    {
        var result = new List<NormalizedWhatsAppMessage>();
        if (!root.TryGetProperty("entry", out var entries)
            || entries.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes)
                || changes.ValueKind != JsonValueKind.Array) continue;
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value)
                    || !value.TryGetProperty("messages", out var messages)
                    || messages.ValueKind != JsonValueKind.Array) continue;
                foreach (var message in messages.EnumerateArray())
                {
                    if (!message.TryGetProperty("id", out var id)
                        || !message.TryGetProperty("from", out var from)
                        || !message.TryGetProperty("type", out var type)) continue;
                    var messageType = type.GetString() ?? "unknown";
                    string? mediaId = null;
                    string? fileName = null;
                    string? mediaContentType = null;
                    string? text = messageType switch
                    {
                        "text" when message.TryGetProperty("text", out var body)
                            && body.TryGetProperty("body", out var textBody) =>
                            textBody.GetString(),
                        "interactive" when message.TryGetProperty("interactive", out var interactive) =>
                            InteractiveText(interactive),
                        _ => null
                    };
                    if (messageType is "image" or "video" or "document" or "audio"
                        && message.TryGetProperty(messageType, out var media))
                    {
                        mediaId = media.TryGetProperty("id", out var mediaIdElement)
                            ? mediaIdElement.GetString() : null;
                        fileName = media.TryGetProperty("filename", out var fileNameElement)
                            ? fileNameElement.GetString() : null;
                        mediaContentType = media.TryGetProperty("mime_type", out var mimeElement)
                            ? mimeElement.GetString() : null;
                    }
                    var timestamp = message.TryGetProperty("timestamp", out var timestampElement)
                        && long.TryParse(timestampElement.GetString(), out var seconds)
                            ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                            : DateTime.UtcNow;
                    result.Add(new(
                        id.GetString() ?? string.Empty,
                        from.GetString() ?? string.Empty,
                        messageType,
                        text,
                        timestamp,
                        mediaId,
                        fileName,
                        mediaContentType));
                }
            }
        }
        return result;
    }

    public static IReadOnlyList<NormalizedWhatsAppStatus> ParseStatuses(
        JsonElement root)
    {
        var result = new List<NormalizedWhatsAppStatus>();
        if (!root.TryGetProperty("entry", out var entries)
            || entries.ValueKind != JsonValueKind.Array) return result;
        foreach (var entry in entries.EnumerateArray())
        {
            if (!entry.TryGetProperty("changes", out var changes)) continue;
            foreach (var change in changes.EnumerateArray())
            {
                if (!change.TryGetProperty("value", out var value)
                    || !value.TryGetProperty("statuses", out var statuses)
                    || statuses.ValueKind != JsonValueKind.Array) continue;
                foreach (var status in statuses.EnumerateArray())
                {
                    if (!status.TryGetProperty("id", out var id)
                        || !status.TryGetProperty("status", out var state)) continue;
                    var occurred = status.TryGetProperty("timestamp", out var timestamp)
                        && long.TryParse(timestamp.GetString(), out var seconds)
                        ? DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime
                        : DateTime.UtcNow;
                    string? code = null;
                    string? description = null;
                    if (status.TryGetProperty("errors", out var errors)
                        && errors.ValueKind == JsonValueKind.Array
                        && errors.GetArrayLength() > 0)
                    {
                        var error = errors[0];
                        code = error.TryGetProperty("code", out var codeElement)
                            ? codeElement.ToString() : null;
                        description = error.TryGetProperty("title", out var title)
                            ? title.GetString() : null;
                    }
                    result.Add(new(id.GetString()!, state.GetString()!, occurred,
                        code, description));
                }
            }
        }
        return result;
    }

    private static string? InteractiveText(JsonElement interactive)
    {
        foreach (var property in new[] { "button_reply", "list_reply" })
        {
            if (interactive.TryGetProperty(property, out var reply))
            {
                if (reply.TryGetProperty("id", out var id)) return id.GetString();
                if (reply.TryGetProperty("title", out var title)) return title.GetString();
            }
        }
        return null;
    }
}
