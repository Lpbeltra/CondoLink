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
        CancellationToken cancellationToken,
        string? bodyParameterName = null);
    Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        string language,
        IReadOnlyList<string> bodyParameters,
        IReadOnlyList<string> quickReplyPayloads,
        CancellationToken cancellationToken,
        string? bodyParameterName,
        IReadOnlyList<string> urlButtonParameters) =>
        SendTemplateAsync(phoneNumber, templateName, language, bodyParameters,
            quickReplyPayloads, cancellationToken, bodyParameterName);

    // Adds a DOCUMENT header referencing previously uploaded media (see
    // UploadDocumentAsync). Defaults to the header-less overload above when no
    // header is supplied, and to a hard failure when a header IS supplied but
    // the concrete client has no override — so a client that hasn't been
    // taught how to attach a document never silently sends a bodyless message.
    Task<WhatsAppSendResult> SendTemplateAsync(
        string phoneNumber,
        string templateName,
        string language,
        IReadOnlyList<string> bodyParameters,
        IReadOnlyList<string> quickReplyPayloads,
        CancellationToken cancellationToken,
        string? bodyParameterName,
        IReadOnlyList<string> urlButtonParameters,
        WhatsAppDocumentHeader? documentHeader) =>
        documentHeader is null
            ? SendTemplateAsync(phoneNumber, templateName, language, bodyParameters,
                quickReplyPayloads, cancellationToken, bodyParameterName, urlButtonParameters)
            : throw new NotSupportedException("This WhatsApp client does not support document headers.");

    // Uploads binary content (e.g. a PDF) to the provider's media store ahead of
    // sending, returning a media id usable as a template DOCUMENT header.
    Task<WhatsAppMediaUploadResult> UploadDocumentAsync(
        byte[] content,
        string fileName,
        string mimeType,
        CancellationToken cancellationToken) =>
        Task.FromResult(new WhatsAppMediaUploadResult(false, null,
            "This WhatsApp client does not support media upload.", FailureKind: "Configuration"));
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
public sealed record WhatsAppDocumentHeader(string MediaId, string FileName);
public sealed record WhatsAppMediaUploadResult(
    bool Succeeded,
    string? MediaId,
    string? Error,
    bool IsTransient = false,
    string? ErrorCode = null,
    string? FailureKind = null);
