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
        IReadOnlyList<string> bodyParameters,
        IReadOnlyList<string> quickReplyPayloads,
        CancellationToken cancellationToken);
}

public sealed record WhatsAppSendResult(
    bool Succeeded,
    string? ExternalMessageId,
    string? Error,
    bool IsTransient = false,
    string? ErrorCode = null,
    int? HttpStatusCode = null,
    string? ErrorType = null,
    string? ErrorSubcode = null,
    string? FailureKind = null,
    string? FailureStage = null);
public sealed record WhatsAppMediaResult(
    bool Succeeded,
    byte[]? Content,
    string? ContentType,
    string? Error);
