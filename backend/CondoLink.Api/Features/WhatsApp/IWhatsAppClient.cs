namespace CondoLink.Api.Features.WhatsApp;

public interface IWhatsAppClient
{
    Task<WhatsAppSendResult> SendTextAsync(
        string phoneNumber,
        string text,
        CancellationToken cancellationToken);
    Task<WhatsAppMediaResult> DownloadMediaAsync(
        string mediaId,
        CancellationToken cancellationToken);
    Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        string language,
        CancellationToken cancellationToken);
}

public sealed record WhatsAppSendResult(
    bool Succeeded,
    string? ExternalMessageId,
    string? Error,
    bool IsTransient = false,
    string? ErrorCode = null);
public sealed record WhatsAppMediaResult(
    bool Succeeded,
    byte[]? Content,
    string? ContentType,
    string? Error);
